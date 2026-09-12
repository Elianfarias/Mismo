using System;
using System.Linq;
using Mismo.Gameplay.Enemies;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using UnityEngine.Playables;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Equipment;

namespace Mismo.Gameplay.Player.Editor
{
    public static class EnemyRigChecks
    {
        static int count;
        public static void GroundSupportChecks()
        {
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(10,1,10);
                foreach(string path in new[]{"Assets/Prefabs/Enemies/Goblin.prefab","Assets/Prefabs/Enemies/FirstBoss.prefab"})
                {
                    var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    var animator=root.GetComponentInChildren<Animator>();animator.enabled=false;
                    var idle=animator.runtimeAnimatorController.animationClips.First(c=>c.name.EndsWith("Idle"));
                    var visual=root.transform.Find("Visual");
                    var contacts=EnemyAnimationRetargeter.FootContacts(animator.gameObject);
                    var support=new EnemyGroundSupport();
                    foreach(float height in new[]{.0f,.15f,.35f})
                    foreach(float phase in new[]{0f,.25f,.5f,.75f})
                    {
                        root.transform.position=Vector3.up*height;visual.localPosition=Vector3.zero;
                        idle.SampleAnimation(animator.gameObject,idle.length*phase);Physics.SyncTransforms();
                        Check(support.Apply(root.transform,visual,animator.transform.lossyScale.y),"Physical floor found below enemy");
                        float gap=EnemyAnimationRetargeter.ContactHeight(contacts);
                        Check(Mathf.Abs(gap)<.015f,"Idle sole touches physical floor despite agent height: "+path+" gap="+gap);
                        Check(Mathf.Abs(root.transform.position.y-height)<.0001f,"Navigation root remains unchanged");
                    }
                    root.transform.position=new Vector3(20,2,0);visual.localPosition=Vector3.zero;Physics.SyncTransforms();
                    Check(!support.Apply(root.transform,visual,animator.transform.lossyScale.y)&&visual.localPosition==Vector3.zero,"No ground leaves visual unchanged");
                    UnityEngine.Object.DestroyImmediate(root);
                }
                UnityEngine.Object.DestroyImmediate(floor);
                Debug.Log("GROUND_SUPPORT_PASS "+count);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void TerrainDiagnostic()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/VoxelRegion_7319.unity");
            Physics.SyncTransforms();
            foreach(var enemy in UnityEngine.Object.FindObjectsByType<GoblinController>().Take(12))
            {
                if(!UnityEngine.AI.NavMesh.SamplePosition(enemy.transform.position,out var nav,3,UnityEngine.AI.NavMesh.AllAreas))continue;
                var hits=Physics.RaycastAll(nav.position+Vector3.up,Vector3.down,3,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>!h.collider.transform.IsChildOf(enemy.transform)&&h.collider.GetComponentInParent<GoblinController>()==null).OrderBy(h=>h.distance).ToArray();
                if(hits.Length>0)Debug.Log("TERRAIN_GAP "+enemy.name+" nav="+nav.position+" floor="+hits[0].point+" gap="+(nav.position.y-hits[0].point.y));
            }
            EditorApplication.Exit(0);
        }
        public static void GroundDiagnostic()
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Goblin.prefab"));
            root.transform.position=Vector3.zero;
            var animator=root.GetComponentInChildren<Animator>();animator.enabled=false;
            var contacts=EnemyAnimationRetargeter.FootContacts(animator.gameObject);
            var clips=animator.runtimeAnimatorController.animationClips.Concat(new[]{root.GetComponent<GoblinController>().Settings.slash.animation.PlaybackClip,root.GetComponent<GoblinController>().Settings.charge.animation.PlaybackClip}).Distinct();
            foreach(var clip in clips)
            foreach(float fraction in new[]{0f,.25f,.5f,.75f,1f})
            {
                clip.SampleAnimation(animator.gameObject,clip.length*fraction);
                float bottom=EnemyAnimationRetargeter.ContactHeight(contacts);
                Debug.Log("GROUND_SAMPLE "+clip.name+" "+fraction+" bottom="+bottom+" model="+animator.transform.position+" feet="+string.Join(";",animator.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Foot")).Select(t=>t.name+"="+t.position))+" skin="+string.Join(";",animator.GetComponentsInChildren<SkinnedMeshRenderer>().Select(s=>s.transform.lossyScale.ToString())));
            }
            UnityEngine.Object.DestroyImmediate(root);EditorApplication.Exit(0);
        }
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("ENEMY_RIG_CHECK "+message);}
        static void Set(object instance,string name,object value)=>instance.GetType().GetField(name,Private).SetValue(instance,value);
        public static void RunBatch()
        {
            try
            {
                EnemyAnimationRetargeter.RefreshAll();
                Verify("Assets/Prefabs/Enemies/Goblin.prefab",false);
                Verify("Assets/Prefabs/Enemies/FirstBoss.prefab",true);
                var family=AssetDatabase.LoadAssetAtPath<WeaponFamilyDefinition>("Assets/Data/WeaponFamilies/OneHandSword.asset");
                Check(family.DisplayName=="Arma a una mano"&&family.progressionId=="sword.onehand","Spanish family label preserves mastery ID");
                var copy=UnityEngine.Object.Instantiate(family);copy.displayName="";
                Check(copy.DisplayName=="Arma a una mano","Sword fallback is Spanish even without a custom display name");UnityEngine.Object.DestroyImmediate(copy);
                Debug.Log("ENEMY_RIG_PASS "+count);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Verify(string path,bool isBoss)
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var driver=isBoss?(MonoBehaviour)root.GetComponent<BossAnimationDriver>():root.GetComponent<GoblinAnimationDriver>();
            var settings=isBoss?(ScriptableObject)UnityEngine.Object.Instantiate(root.GetComponent<BossController>().Settings):UnityEngine.Object.Instantiate(root.GetComponent<GoblinController>().Settings);
            var owner=isBoss?(MonoBehaviour)root.GetComponent<BossController>():root.GetComponent<GoblinController>();
            Set(owner,"settings",settings);
            driver.GetType().GetMethod("Awake",Private).Invoke(driver,null);
            var animator=isBoss?((BossAnimationDriver)driver).Animator:((GoblinAnimationDriver)driver).Animator;
            Check(animator!=null&&animator.runtimeAnimatorController!=null,path+" has a connected Animator");
            var playback=(EnemyActionPlayback)driver.GetType().GetField("playback",Private).GetValue(driver);
            var expected=UnityEngine.Object.Instantiate(animator.gameObject);expected.GetComponent<Animator>().enabled=false;
            var contacts=EnemyAnimationRetargeter.FootContacts(expected);
            float ground=EnemyAnimationRetargeter.ContactHeight(contacts);
            try
            {
                object[] attacks=isBoss?new object[]{((BossSettings)settings).frontSlash,((BossSettings)settings).overheadSmash,((BossSettings)settings).straightCharge}
                    :new object[]{((GoblinSettings)settings).slash,((GoblinSettings)settings).charge};
                foreach(var attack in attacks)
                {
                    var binding=attack is GoblinAttack g?g.animation:((BossAttackDefinition)attack).animation;
                    float windup=attack is GoblinAttack ga?ga.windup:((BossAttackDefinition)attack).Windup;
                    Set(owner,"attack",attack);binding.blendSeconds=0;
                    Check(binding.clip!=null&&binding.PlaybackClip!=null&&binding.PlaybackClip!=binding.clip,binding.clip.name+" retains original and has adapted clip");
                    var curves=AnimationUtility.GetCurveBindings(binding.PlaybackClip);
                    Check(curves.Length>0&&curves.All(c=>animator.transform.Find(c.path)!=null),"All adapted curves resolve on actual enemy bones");
                    Check(contacts.Count>0,"Actual sole vertices are available for ground checks");
                    float maxGap=0;
                    for(int frame=0;frame<=120;frame++)
                    {
                        binding.PlaybackClip.SampleAnimation(expected,binding.PlaybackClip.length*frame/120f);
                        maxGap=Mathf.Max(maxGap,Mathf.Abs(EnemyAnimationRetargeter.ContactHeight(contacts)-ground));
                    }
                    Check(maxGap<.006f,"Supporting sole stays grounded throughout "+binding.clip.name+" (maximum error "+maxGap+")");
                    Quaternion[] first=null;var bones=animator.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("UpperArm")||t.name=="Chest").ToArray();
                    foreach(float progress in new[]{.1f,.85f})
                    {
                        if(isBoss){Set(owner,"state",BossState.Telegraph);Set(owner,"stateTimer",windup*(1-progress));}
                        else{Set(owner,"<State>k__BackingField",GoblinState.Telegraph);Set(owner,"duration",windup);Set(owner,"timer",windup*(1-progress));}
                        driver.GetType().GetMethod("Update",Private).Invoke(driver,null);
                        var inner=typeof(EnemyActionPlayback).GetField("playback",Private).GetValue(playback);
                        var graph=(PlayableGraph)typeof(WeaponActionPlayback).GetField("graph",Private).GetValue(inner);
                        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Evaluate(0);
                        binding.PlaybackClip.SampleAnimation(expected,binding.Sample(EnemyAttackPhase.Preparation,progress)*binding.PlaybackClip.length);
                        Check(bones.All(b=>Quaternion.Angle(b.localRotation,expected.transform.Find(AnimationUtility.CalculateTransformPath(b,animator.transform)).localRotation)<.1f),"Driver evaluates the authored pose on visible enemy skeleton");
                        if(first==null)first=bones.Select(b=>b.localRotation).ToArray();
                        else Check(bones.Select((b,i)=>Quaternion.Angle(first[i],b.localRotation)).Sum()>1,"Arms and torso move between attack frames");
                    }
                }
            }
            finally{playback.Dispose();UnityEngine.Object.DestroyImmediate(expected);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(settings);}
        }
        public static void Diagnose()
        {
            foreach(var path in new[]{"Assets/Art/FBX/Goblins/Goblin_Concept_Animated.fbx","Assets/Art/FBX/Characters/Voxel_Adventurer_Animated.fbx"})
            {
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Debug.Log("RIG "+path+" exists="+(root!=null));
                if(root==null)continue;
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(t.name.Contains("Arm")||t.name=="Chest"||t.name=="Spine"||t.name=="Hips"||t.name=="Root"||t.name.Contains("Rig"))
                        Debug.Log("BONE "+AnimationUtility.CalculateTransformPath(t,root.transform)+" rest="+t.localRotation);
            }
            var settings=AssetDatabase.LoadAssetAtPath<GoblinSettings>("Assets/Data/Enemies/BaseGoblin.asset");
            foreach(var attack in new[]{settings.slash,settings.charge})
            {
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/FBX/Goblins/Goblin_Concept_Animated.fbx"));
                var bindings=AnimationUtility.GetCurveBindings(attack.animation.clip);
                Debug.Log("CLIP "+attack.animation.clip.name+" bindings="+bindings.Length+" matched="+bindings.Count(b=>model.transform.Find(b.path)!=null));
                UnityEngine.Object.DestroyImmediate(model);
            }
            EditorApplication.Exit(0);
        }
    }
}
