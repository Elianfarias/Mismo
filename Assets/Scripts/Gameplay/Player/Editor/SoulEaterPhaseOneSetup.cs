using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Input;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterPhaseOneSetup
    {
        public const string PrefabPath="Assets/Art/Prefabs/DragonBosses/SoulEater_PhaseOne.prefab";
        public const string DataPath="Assets/Data/Enemies/DragonBosses/SoulEater_PhaseOne.asset";
        public const string ScenePath="Assets/Scenes/SoulEaterPhaseOneArena.unity";
        const string AnimFolder="Assets/Art/Animations/DragonBosses/SoulEater";
        const string AudioFolder="Assets/Art/Audio/Enemies/SoulEater";
        const string MaterialFolder="Assets/Art/Materials/DragonBosses/SoulEater";

        [MenuItem("Mismo/Bosses/Soul Eater/Crear o actualizar fase 1")]
        public static void Run()
        {
            try
            {
                foreach(var folder in new[]{AnimFolder,AudioFolder,MaterialFolder,"Assets/Data/Enemies/DragonBosses","Assets/Art/Prefabs/DragonBosses"})Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                var settings=AssetDatabase.LoadAssetAtPath<SoulEaterPhaseOneSettings>(DataPath);
                if(settings==null){settings=ScriptableObject.CreateInstance<SoulEaterPhaseOneSettings>();AssetDatabase.CreateAsset(settings,DataPath);}
                if(Mathf.Approximately(settings.chargeDistance,18))settings.chargeDistance=34;
                if(Mathf.Approximately(settings.tailRange,7)||Mathf.Approximately(settings.tailRange,5))settings.tailRange=4;
                settings.idle=Clip("Idle");settings.walk=Clip("Walk");settings.run=Clip("Run");settings.bite=Clip("Basic Attack");settings.tail=RearTail(Clip("Tail Attack"));
                settings.breath=Clip("Fireball Shoot");settings.roar=Clip("Scream");settings.takeOff=Clip("Take Off");settings.land=Clip("Land");settings.hit=Clip("Get Hit");settings.die=Clip("Die");
                settings.chargePose=Extract(Clip("Defend"),.15f,.5f,1,"Charge_Anticipation");
                settings.brake=Extract(Clip("Land"),.62f,1,1.3f,"Charge_Brake");
                EditorUtility.SetDirty(settings);
                var flame=Material("SoulFlame",Shader.Find("Mismo/SoulEater/Flowing Flame"),Color.white);
                flame.SetFloat("_Intensity",1.25f);
                var ground=Material("ArenaStone",Shader.Find("Universal Render Pipeline/Lit"),new Color(.105f,.14f,.13f));
                var border=Material("ArenaBorder",Shader.Find("Universal Render Pipeline/Lit"),new Color(.22f,.28f,.23f));
                var dust=Material("GroundDust",Shader.Find("Universal Render Pipeline/Particles/Unlit"),new Color(.35f,.42f,.27f,.3f));
                var clips=new AudioClip[7];for(int i=0;i<7;i++)clips[i]=Sound((SoulEaterCue)i);
                var fire=Sound(null);
                GameObject root=new GameObject("SoulEater_PhaseOne");
                GameObject prefab;
                try
                {
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Voxelized/SoulEater_Green_Animated.prefab"));
                    visual.transform.SetParent(root.transform,false);visual.transform.localScale=Vector3.one*.8f;
                    foreach(var collider in visual.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
                    var rig=visual.GetComponent<VoxelRigInstance>();
                    settings.idle.SampleAnimation(rig.animator.gameObject,0);
                    var baked=new Mesh();rig.surface.BakeMesh(baked);
                    float minY=baked.vertices.Min(v=>rig.surface.transform.TransformPoint(v).y);
                    Object.DestroyImmediate(baked);visual.transform.position-=Vector3.up*minY;
                    Transform Bone(string name)=>visual.GetComponentsInChildren<Transform>().First(t=>t.name==name);
                    var body=root.AddComponent<CapsuleCollider>();body.radius=1.4f;body.height=2.8f;body.center=Vector3.up*1.4f;
                    var rigid=root.AddComponent<Rigidbody>();rigid.isKinematic=true;rigid.useGravity=false;
                    root.AddComponent<Health>();root.AddComponent<CombatState>();root.AddComponent<Invulnerability>();root.AddComponent<DamageReceiver>();
                    var head=new GameObject("HeadHurtbox");head.transform.SetParent(root.transform,false);
                    var headCollider=head.AddComponent<SphereCollider>();headCollider.radius=1.15f;
                    head.transform.position=(Bone("UpperMouth").position+Bone("JawTip").position)*.5f;
                    var flameObject=new GameObject("GreenFlame_FromScratch");flameObject.transform.SetParent(root.transform,false);
                    var vfx=flameObject.AddComponent<SoulEaterFlameVfx>();vfx.Configure(flame);
                    var glow=new GameObject("MouthGlow");glow.transform.SetParent(root.transform,false);var light=glow.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.45f,1,.12f);light.range=6;light.enabled=false;
                    var dustObject=new GameObject("LandingDust");dustObject.transform.SetParent(root.transform,false);
                    var particles=dustObject.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=particles.main;main.playOnAwake=false;main.loop=false;main.startLifetime=.65f;main.startSpeed=new ParticleSystem.MinMaxCurve(1,3);main.startSize=new ParticleSystem.MinMaxCurve(.1f,.35f);main.startColor=new Color(.3f,.4f,.24f,.3f);main.gravityModifier=.4f;main.simulationSpace=ParticleSystemSimulationSpace.World;
                    var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=75;shape.radius=1.8f;
                    particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=dust;
                    var voice=Audio(root,"Voice");var loop=Audio(root,"BreathLoop");loop.clip=fire;loop.loop=true;loop.volume=.42f;
                    var effects=root.AddComponent<SoulEaterEffects>();effects.Configure(vfx,light,rig.surface,particles,voice,loop,clips);
                    var nav=root.AddComponent<NavMeshAgent>();nav.enabled=false;nav.radius=1.4f;nav.height=2.8f;nav.acceleration=10;nav.angularSpeed=65;
                    root.AddComponent<SoulEaterPhaseOneController>().Configure(settings,rig,Bone("UpperMouth"),Bone("JawTip"),new[]{Bone("Tail01"),Bone("Tail02"),Bone("Tail03"),Bone("TailEnd")},headCollider,effects);
                    root.AddComponent<EnemyNameplate>();
                    prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                }
                finally{Object.DestroyImmediate(root);}
                CreateArena(prefab,ground,border);
                AssetDatabase.SaveAssets();
                Debug.Log("SOUL_PHASE_ONE_SETUP_OK: "+ScenePath);
                if(Application.isBatchMode)SoulEaterPhaseOneChecks.Run();
            }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
        static AnimationClip Clip(string name)
        {
            string path="Assets/Art/Animations/Voxelized/SoulEater_Animated_Animations/"+name+".anim";
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path)??throw new InvalidOperationException(path);
        }
        static AnimationClip Extract(AnimationClip source,float from,float to,float duration,string name)
        {
            string path=AnimFolder+"/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}clip.ClearCurves();clip.name=name;clip.frameRate=30;
            foreach(var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve=AnimationUtility.GetEditorCurve(source,binding);var keys=new Keyframe[40];
                for(int i=0;i<keys.Length;i++){float t=i/(float)(keys.Length-1);keys[i]=new Keyframe(t*duration,curve.Evaluate(Mathf.Lerp(from,to,t)*source.length));}
                var resampled=new AnimationCurve(keys);for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(resampled,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(resampled,i,AnimationUtility.TangentMode.ClampedAuto);}
                AnimationUtility.SetEditorCurve(clip,binding,resampled);
            }
            clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);return clip;
        }
        static AnimationClip RearTail(AnimationClip source)
        {
            string path=AnimFolder+"/Tail_RearSweep.anim";
            var result=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(result==null){result=Object.Instantiate(source);AssetDatabase.CreateAsset(result,path);}else EditorUtility.CopySerialized(source,result);
            result.name="Tail_RearSweep";
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Voxelized/SoulEater_Green_Animated.prefab"));
            try
            {
                var pelvis=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Root_Pelvis");
                var head=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
                var tail=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Tail01");
                var tip=model.GetComponentsInChildren<Transform>().First(t=>t.name=="TailEnd");
                var animated=model.GetComponent<VoxelRigInstance>().animator.gameObject;
                string bonePath=AnimationUtility.CalculateTransformPath(pelvis,animated.transform);
                source.SampleAnimation(animated,0);Vector3 rest=pelvis.position;
                var curves=Enumerable.Range(0,11).Select(_=>new AnimationCurve()).ToArray();
                for(int frame=0;frame<=48;frame++)
                {
                    float time=source.length*frame/48;source.SampleAnimation(animated,time);
                    Vector3 forward=Vector3.ProjectOnPlane(head.position-pelvis.position,Vector3.up);
                    float yaw=Vector3.SignedAngle(Vector3.forward,forward,Vector3.up);
                    // The original spins the entire body toward the rear. Keep its crouch and
                    // tail articulation, while limiting the torso turn to a readable rear sweep.
                    pelvis.rotation=Quaternion.AngleAxis(-yaw+Mathf.Sin(yaw*Mathf.Deg2Rad)*35,Vector3.up)*pelvis.rotation;
                    pelvis.position=new Vector3(Mathf.Lerp(rest.x,pelvis.position.x,.2f),pelvis.position.y,Mathf.Lerp(rest.z,pelvis.position.z,.2f));
                    float phase=frame/48f;
                    float sweep=phase<.32f?Mathf.Lerp(0,-65,Mathf.SmoothStep(0,1,phase/.32f)):phase<.8f?Mathf.Lerp(-65,65,Mathf.SmoothStep(0,1,(phase-.32f)/.48f)):Mathf.Lerp(65,0,Mathf.SmoothStep(0,1,(phase-.8f)/.2f));
                    Vector3 actual=tip.position-tail.position;
                    Vector3 desired=Quaternion.AngleAxis(sweep,Vector3.up)*Vector3.back;
                    desired.y=-.1f;
                    tail.rotation=Quaternion.FromToRotation(actual,desired)*tail.rotation;
                    Quaternion q=pelvis.localRotation;Vector3 p=pelvis.localPosition;
                    Quaternion tq=tail.localRotation;
                    float[] values={q.x,q.y,q.z,q.w,p.x,p.y,p.z,tq.x,tq.y,tq.z,tq.w};for(int i=0;i<11;i++)curves[i].AddKey(time,values[i]);
                }
                string[] names={"m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"};
                for(int i=0;i<7;i++)AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(bonePath,typeof(Transform),names[i]),curves[i]);
                for(int i=0;i<4;i++)AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(tail,animated.transform),typeof(Transform),names[i]),curves[i+7]);
                result.EnsureQuaternionContinuity();EditorUtility.SetDirty(result);return result;
            }
            finally{Object.DestroyImmediate(model);}
        }
        static Material Material(string name,Shader shader,Color color)
        {
            if(shader==null)throw new InvalidOperationException("Falta shader para "+name);
            string path=MaterialFolder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);if(material.HasProperty("_Color"))material.SetColor("_Color",color);if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.15f);EditorUtility.SetDirty(material);return material;
        }
        static AudioSource Audio(GameObject root,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.minDistance=5;source.maxDistance=42;source.rolloffMode=AudioRolloffMode.Linear;source.volume=.65f;return source;
        }
        // Original procedural placeholder cues: replace individually when final creature audio is available.
        static AudioClip Sound(SoulEaterCue? cue)
        {
            string name=cue.HasValue?cue.Value.ToString():"FlameLoop";string path=AudioFolder+"/SoulEater_"+name+".wav";
            int rate=22050;float seconds=cue==SoulEaterCue.Roar?2.6f:cue==null?2:cue==SoulEaterCue.Inhale?1.5f:cue==SoulEaterCue.Land?.6f:.85f;
            int count=(int)(rate*seconds);var random=new System.Random(100+(int)(cue??SoulEaterCue.Inhale));float low=0;
            using(var w=new BinaryWriter(File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
                for(int i=0;i<count;i++)
                {
                    float t=i/(float)rate,p=t/seconds;float noise=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,noise,.07f);
                    float env=cue==null?Mathf.Min(1,Mathf.Min(p,1-p)*80):Mathf.Sin(Mathf.PI*Mathf.Pow(p,.7f));
                    float tone=Mathf.Sin(t*(cue==SoulEaterCue.Land?55:72)*Mathf.PI*2+Mathf.Sin(t*19)*1.2f);
                    float sample=cue==SoulEaterCue.Roar?(tone*.38f+Mathf.Sin(t*137*Mathf.PI*2)*.13f+low*.85f):cue==null||cue==SoulEaterCue.Inhale?low*.9f+noise*.07f:low*.7f+tone*.23f;
                    w.Write((short)(Mathf.Clamp(sample*env*.7f,-1,1)*32767));
                }
            }
            AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static void CreateArena(GameObject prefab,Material ground,Material border)
        {
            var prior=SceneManager.GetActiveScene();
            var mode=Application.isBatchMode&&string.IsNullOrEmpty(prior.path)?NewSceneMode.Single:NewSceneMode.Additive;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);SceneManager.SetActiveScene(scene);
            try
            {
                Box("Arena floor",new Vector3(0,-.3f,0),new Vector3(66,.6f,66),ground);
                for(int i=0;i<4;i++){bool x=i<2;float side=i%2==0?-33:33;Box("Arena boundary",new Vector3(x?side:0,1.2f,x?0:side),new Vector3(x?1:66,2.4f,x?66:1),border);}
                var sources=new System.Collections.Generic.List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(64,.6f,64),transform=Matrix4x4.TRS(new Vector3(0,-.3f,0),Quaternion.identity,Vector3.one),area=0}};
                var navSettings=NavMesh.GetSettingsByIndex(0);navSettings.agentRadius=1.4f;navSettings.agentHeight=2.8f;
                var navData=NavMeshBuilder.BuildNavMeshData(navSettings,sources,new Bounds(Vector3.zero,new Vector3(70,10,70)),Vector3.zero,Quaternion.identity);
                string navPath="Assets/Data/Enemies/DragonBosses/SoulEater_ArenaNavMesh.asset";var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
                if(existing==null)AssetDatabase.CreateAsset(navData,navPath);else{EditorUtility.CopySerialized(navData,existing);Object.DestroyImmediate(navData);navData=existing;}
                var nav=new GameObject("Arena navigation").AddComponent<SoulEaterArenaNavigation>();nav.Configure(navData);
                var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.35f;light.color=new Color(1,.96f,.84f);light.transform.rotation=Quaternion.Euler(48,-32,0);light.shadows=LightShadows.Soft;
                RenderSettings.ambientLight=new Color(.55f,.64f,.59f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.fog=false;
                var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));
                PrefabUtility.UnpackPrefabInstance(player,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                foreach(var component in player.GetComponents<MonoBehaviour>())if(component!=null&&(component.GetType().Name=="RegionRespawn"||component.GetType().Name=="PlayerInventory"||component.GetType().Name=="InventoryPanel"))Object.DestroyImmediate(component);
                player.transform.SetPositionAndRotation(new Vector3(0,.1f,16),Quaternion.Euler(0,180,0));
                var camera=new GameObject("Main Camera").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.08f,.07f);camera.nearClipPlane=.15f;camera.farClipPlane=180;
                camera.gameObject.AddComponent<AudioListener>();var orbit=camera.gameObject.AddComponent<Mismo.Gameplay.Player.Camera.ThirdPersonCamera>();orbit.Configure(player.transform,player.GetComponent<PlayerInputReader>());
                var serialized=new SerializedObject(orbit);serialized.FindProperty("distance").floatValue=12;serialized.FindProperty("height").floatValue=1.8f;serialized.ApplyModifiedPropertiesWithoutUndo();
                player.GetComponent<PlayerController>().Configure(camera.transform);
                var boss=(GameObject)PrefabUtility.InstantiatePrefab(prefab);boss.transform.position=Vector3.zero;
                new GameObject("Phase one workshop").AddComponent<SoulEaterArena>().Configure(boss.GetComponent<SoulEaterPhaseOneController>(),player.GetComponent<PlayerController>());
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            finally{if(prior.IsValid()){SceneManager.SetActiveScene(prior);EditorSceneManager.CloseScene(scene,true);}}
        }
        static void Box(string name,Vector3 p,Vector3 scale,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;}
        [MenuItem("Mismo/Bosses/Soul Eater/Abrir arena de fase 1")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
    }
}
