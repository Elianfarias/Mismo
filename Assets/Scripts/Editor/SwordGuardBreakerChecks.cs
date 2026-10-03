using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Runs in an isolated Unity project: imported poses, content build and real player playback.</summary>
public static class SwordGuardBreakerChecks
{
    const string Output="output/guard-breaker",Active="Mismo.GuardBreakerChecks";
    static IEnumerator routine;
    static int lastFrame=-1;
    static double deadline;

    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;
        EditorApplication.update-=Update;EditorApplication.update+=Update;
    }
    static void Update()
    {
        if(!SessionState.GetBool(Active,false)||routine==null||!Application.isPlaying)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish(new TimeoutException("Guard breaker play checks"));return;}
        EditorApplication.QueuePlayerLoopUpdate();
        if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
        try{if(!routine.MoveNext())Finish(null);}catch(Exception e){Finish(e);}
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Active,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {deadline=EditorApplication.timeSinceStartup+120;routine=PlayChecks();Time.captureDeltaTime=.01f;}
        if(state==PlayModeStateChange.EnteredEditMode)
        {SessionState.SetBool(Active,false);EditorApplication.delayCall+=()=>EditorApplication.Exit(SessionState.GetInt(Active+".Exit",1));}
    }
    static void Finish(Exception error)
    {
        routine=null;Time.captureDeltaTime=0;GameplayPause.Resume();CombatTimeFeedback.CancelForPause();Time.timeScale=1;
        if(error!=null){File.WriteAllText(Output+"/failure.txt",error.ToString());Debug.LogException(error);}
        SessionState.SetInt(Active+".Exit",error==null?0:1);EditorApplication.ExitPlaymode();
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void Log(string message)=>File.AppendAllText(Output+"/checks.txt",message+"\n");

    public static void RunBatch()
    {
        Require(Application.isBatchMode&&Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"),"Run in an isolated validation project, never over the user's scene or save.");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/checks.txt","");
        try
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(SwordGuardBreakerAnimationSetup.ClipPath);
            var set=AssetDatabase.LoadAssetAtPath<WeaponAnimationSet>(SwordGuardBreakerAnimationSetup.SetPath);
            var ability=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(SwordGuardBreakerAnimationSetup.AbilityPath);
            var binding=set.Find(ability);
            Require(binding!=null&&binding.ability==ability&&binding.clip==clip&&clip.isHumanMotion&&!clip.isLooping,"Missing dedicated Humanoid binding");
            Require(binding.maskMode==ActionMaskMode.FullBody&&!binding.useAnimationMovement,"Strike must use the full body without moving the capsule");
            Require(Mathf.Approximately(clip.length,.9f)&&Mathf.Approximately(binding.activeStartsAt*clip.length,.5f)&&Mathf.Approximately(binding.recoveryStartsAt*clip.length,.7f),"Impact/recovery out of sync");
            var melee=ability.actions.OfType<MeleeAction>().Single();
            Require(ability.preparation==.5f&&ability.active==.2f&&ability.recovery==.2f&&ability.cooldown==8&&melee.damage==20&&melee.postureDamage==90,"Existing gameplay values changed");
            Require(ability.weaponVfx.All(v=>v==null||v.phase!=WeaponVfxPhase.Preparation),"Guard breaker still has the preparation ring");
            CheckPoses(clip);
            ConfigureIsolatedCatalog();
            try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","PASS");}
            catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.ToString());}
            string bundlePath=".validation/GuardBreakerContent";Directory.CreateDirectory(bundlePath);
            var manifest=BuildPipeline.BuildAssetBundles(bundlePath,new[]{new AssetBundleBuild{assetBundleName="guard-breaker",assetNames=new[]{SwordGuardBreakerAnimationSetup.SetPath,SwordGuardBreakerAnimationSetup.AbilityPath}}},BuildAssetBundleOptions.ChunkBasedCompression,EditorUserBuildSettings.activeBuildTarget);
            Require(manifest!=null,"Content build failed");
            var bundle=AssetBundle.LoadFromFile(bundlePath+"/guard-breaker");Require(bundle!=null,"Cannot reload content build");
            try
            {
                var builtSet=bundle.LoadAsset<WeaponAnimationSet>(SwordGuardBreakerAnimationSetup.SetPath);
                var builtAbility=bundle.LoadAsset<AbilityDefinition>(SwordGuardBreakerAnimationSetup.AbilityPath);
                Require(builtSet.Find(builtAbility)?.clip?.name==clip.name,"Built content loses the strike animation");
            }
            finally{bundle.Unload(true);}
            File.WriteAllText(Output+"/content-build.txt","PASS: animation and dedicated ability binding included in the Windows content bundle and reloaded. This is not a full game player build.\n");
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single),"Assets/Scenes/GuardBreakerCheck.unity");
            SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }

    static void CheckPoses(AnimationClip clip)
    {
        var weapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
        foreach(string skin in new[]{"Mage","Warrior","NinjaFrog"})
        {
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Skins/"+skin+"Skin.prefab"));
            try
            {
                var animator=actor.GetComponentInChildren<Animator>();animator.Rebind();
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);var support=animator.GetBoneTransform(HumanBodyBones.LeftHand);
                float gap=0,minEdgeAlignment=1;AnimationMode.StartAnimationMode();
                for(int i=12;i<=70;i++)
                {
                    AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(animator.gameObject,clip,i*.01f);AnimationMode.EndSampling();
                    Vector3 blade=hand.rotation*Quaternion.Euler(weapon.poseProfile.equipped.rotation)*Vector3.up;
                    gap=Mathf.Max(gap,Vector3.Distance(support.position,hand.position-blade*.13f));
                    if(i>=40&&i<=59)
                    {
                        // Actual sword geometry: broad face in local XY, thickness along Z.
                        // Its face normal must be perpendicular to the vertical swing plane.
                        Vector3 faceNormal=hand.rotation*Quaternion.Euler(weapon.poseProfile.equipped.rotation)*Vector3.forward;
                        minEdgeAlignment=Mathf.Min(minEdgeAlignment,Mathf.Abs(Vector3.Dot(faceNormal,Vector3.right)));
                    }
                }
                Require(gap<.05f,skin+": support hand detached from the hilt ("+gap+")");
                Require(minEdgeAlignment>.98f,skin+": sword strikes with its flat face (alignment="+minEdgeAlignment+")");
                Log("PASS "+skin+": 59 held-grip samples, maximum retargeting drift "+(gap*100).ToString("F2")+" cm");
                Log("PASS "+skin+": cutting plane alignment "+minEdgeAlignment.ToString("F4")+" across the entire downswing");
            }
            finally{if(AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();Object.DestroyImmediate(actor);}
        }
    }

    static void ConfigureIsolatedCatalog()
    {
        Directory.CreateDirectory("Assets/Data/System");
        var weapons=AssetDatabase.LoadAssetAtPath<WeaponSetDefinition>("Assets/Data/System/TestWeapons.asset");
        if(weapons==null){weapons=ScriptableObject.CreateInstance<WeaponSetDefinition>();AssetDatabase.CreateAsset(weapons,"Assets/Data/System/TestWeapons.asset");}
        weapons.primary=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
        var catalog=AssetDatabase.LoadAssetAtPath<Mismo.Core.RuntimeAssetCatalog>("Assets/Data/System/RuntimeAssetCatalog.asset");
        if(catalog==null){catalog=ScriptableObject.CreateInstance<Mismo.Core.RuntimeAssetCatalog>();AssetDatabase.CreateAsset(catalog,"Assets/Data/System/RuntimeAssetCatalog.asset");}
        catalog.entries=new[]{new Mismo.Core.RuntimeAssetCatalog.Entry{key="StartingWeapons",assets=new Object[]{weapons}},
            new Mismo.Core.RuntimeAssetCatalog.Entry{key="CombatParticles",assets=new Object[]{AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/CombatParticles.shader")}},
            new Mismo.Core.RuntimeAssetCatalog.Entry{key="UI/TextSettings",assets=new Object[]{AssetDatabase.LoadAssetAtPath<TMPro.TMP_Settings>("Assets/Data/UI/Adventure Text Settings.asset")}}};
        EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(weapons);AssetDatabase.SaveAssetIfDirty(catalog);AssetDatabase.SaveAssetIfDirty(weapons);PlayerSettings.SetPreloadedAssets(new Object[]{catalog});
    }

    static IEnumerator PlayChecks()
    {
        var holder=new GameObject("Inactive setup");holder.SetActive(false);
        var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"),holder.transform);
        Object.DestroyImmediate(actor.GetComponent<PlayerController>());
        Object.DestroyImmediate(actor.GetComponent<Mismo.Gameplay.Player.World.RegionRespawn>());
        // Select the skill on an ephemeral weapon, without unlocking or writing a real profile.
        Object.DestroyImmediate(actor.GetComponent<Mismo.Gameplay.Player.Equipment.Inventory.PlayerInventory>());
        actor.transform.SetParent(null);Object.Destroy(holder);
        var weapon=Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset"));
        var ability=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(SwordGuardBreakerAnimationSetup.AbilityPath);
        weapon.dualWield=false;weapon.overrideFamilyAbilities=true;weapon.abilities=new[]{weapon.GetAbility(AbilitySlot.Basic),ability,ability,ability};
        var loadout=actor.GetComponent<EquipmentLoadout>();loadout.Initialize();Require(loadout.TryEquip(0,weapon),"Cannot equip test sword");
        var runner=loadout.Runner;var motor=actor.GetComponent<PlayerMotor>();
        var camera=new GameObject("Review camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
        camera.transform.position=new Vector3(3,2.2f,4);camera.transform.LookAt(Vector3.up*1.4f);camera.orthographic=true;camera.orthographicSize=2.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.17f,.2f,.21f);
        var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(35,-35,0);RenderSettings.ambientLight=Color.gray;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=Vector3.down*.5f;floor.transform.localScale=new Vector3(10,1,10);
        var target=new GameObject("Posture target");target.transform.position=Vector3.forward*1.25f;target.transform.rotation=Quaternion.Euler(0,180,0);
        var collider=target.AddComponent<CapsuleCollider>();collider.center=Vector3.up;collider.height=1.8f;collider.radius=.3f;
        var health=target.AddComponent<Health>();var receiver=target.AddComponent<DamageReceiver>();var state=target.GetComponent<CombatState>();state.ConfigurePosture(80);
        int hits=0;receiver.Resolved+=(damage,result)=>hits++;
        yield return null;yield return null;yield return null;
        var animator=actor.GetComponent<PlayerAnimationDriver>().Animator;
        var right=animator.GetBoneTransform(HumanBodyBones.RightHand);var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var visual=actor.GetComponent<WeaponPresentation>().ActiveVisual;
        Require(runner.TryUse(AbilitySlot.Q,Vector3.forward,Vector3.zero),"Guard breaker did not start");
        Directory.CreateDirectory(Output+"/frames");
        float firstHit=-1,maxGap=0,minEdgeAlignment=1;Vector3 origin=actor.transform.position;
        for(int i=0;i<96;i++)
        {
            float time=i*.01f;
            if(i>0)runner.Tick(.01f);
            yield return null;
            if(hits>0&&firstHit<0)firstHit=time;
            if(time<.49f)Require(hits==0,"Damage fired during the wind-up");
            if(time>=.13f&&time<=.69f)maxGap=Mathf.Max(maxGap,Vector3.Distance(left.position,right.position-visual.up*.13f));
            if(time>=.40f&&time<=.59f)minEdgeAlignment=Mathf.Min(minEdgeAlignment,Mathf.Abs(Vector3.Dot(visual.forward,Vector3.right)));
            if(time<.49f)Require(!actor.GetComponentsInChildren<ParticleSystem>().Any(p=>p.name=="Gathering ring"),"Preparation ring is still visible");
            if(i%3==0)Capture(camera,Output+"/frames/"+(i/3).ToString("D3")+".png");
            if(i==35)
            {
                GameplayPause.Pause();Vector3 paused=right.position;yield return null;yield return null;
                Require(Vector3.Distance(paused,right.position)<.002f,"Animation moved during pause");GameplayPause.Resume();
            }
        }
        Require(maxGap<.06f,"Runtime two-handed grip detached: "+maxGap);
        Require(minEdgeAlignment>.98f,"Runtime blade is not edge-first: "+minEdgeAlignment);
        Require(firstHit>=.49f&&firstHit<=.52f&&hits==1,"Melee impact is early, late or duplicated: "+firstHit+", count="+hits);
        Require(state.Posture<=0||state.Broken,"Strike failed to break the posture target");
        Require(Vector3.Distance(origin,actor.transform.position)<.02f,"In-place strike displaced the capsule");
        Require(runner.Current==null,"Recovery did not finish");
        Log("PASS real player: impact at "+firstHit.ToString("F2")+" s; one hit; 80 posture broken by existing 90 posture damage; pause/resume; recovery; in-place capsule; max grip drift "+(maxGap*100).ToString("F2")+" cm");
        Log("PASS real player: edge-first downswing, alignment "+minEdgeAlignment.ToString("F4")+"; no preparation ring");
        // Cancellation must immediately relinquish the action layer and leave a usable next action.
        ((System.Collections.IDictionary)typeof(AbilityRunner).GetField("readyAt",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(runner)).Clear();
        Require(runner.TryUse(AbilitySlot.Q,Vector3.forward,Vector3.zero),"Cannot restart strike");runner.Tick(.25f);yield return null;runner.Cancel();
        for(int i=0;i<8;i++)yield return null;
        Require(actor.GetComponent<PlayerAnimationDriver>().ActionClip==null,"Canceled action kept ownership of the pose");
        Log("PASS cancellation releases the action clip");
        File.WriteAllText(Output+"/playmode.txt","PASS: real Player prefab, single equipped sword, full-body Humanoid playback, grip, edge-first strike, no preparation ring, pause/resume, synchronized single posture hit, recovery and cancellation.\n");
    }
    static void Capture(Camera camera,string path)
    {
        var rt=RenderTexture.GetTemporary(500,500,24);var previous=RenderTexture.active;var target=camera.targetTexture;
        var texture=new Texture2D(500,500,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,500,500),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{camera.targetTexture=target;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(texture);}
    }
}
