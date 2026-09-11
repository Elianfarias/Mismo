using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // A narrowly scoped, one-shot request lets an already open Editor execute its own asset APIs.
    public static class ForestCreatureIntegration
    {
        const string Request="Temp/ForestCreatureIntegration.request";
        const string Report="Docs/Validation/ForestCreatureIntegration.txt";
        public const string Prefabs="Assets/Prefabs/Enemies/ForestCreatures";
        const string Data="Assets/Data/Enemies/ForestCreatures";
        const string Animations="Assets/Art/Animations/ForestCreatures";
        static readonly string[] Boars={"Boar_Standard","Boar_Dark_Fur","Boar_Forest_Moss"};
        static readonly string[] Spiders={"Spider_Standard","Spider_Forest_Moss","Spider_Dark_Cave","Spider_Albino"};
        static readonly List<string> log=new List<string>();
        static double next;
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request))return;File.Delete(Request);
            try{Apply();}catch(Exception e){Directory.CreateDirectory("Docs/Validation");File.WriteAllText(Report,"FAIL\n"+string.Join("\n",log)+"\n"+e);Debug.LogException(e);}
        }
        [MenuItem("Mismo/Enemies/Integrate forest creatures")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before importing creatures.");
            log.Clear();Folder(Prefabs);Folder(Data);Folder(Animations);
            foreach(var name in Boars)Import("Boar",name,name.Substring(5)+"_Palette");
            foreach(var name in Spiders)Import("Spider",name,name.Substring(7)+"_Palette");
            Import("Golem","Forest_Golem_Stylized","Golem_Palette");
            Import("Golem","Rock_Projectile","Golem_Palette",false);
            var rock=BuildRock();
            var boar=Settings("Boar",rock);var spider=Settings("Spider",rock);var golem=Settings("Golem",rock);
            foreach(var name in Boars)BuildCreature("Boar",name,boar,1.2f);
            foreach(var name in Spiders)BuildCreature("Spider",name,spider,1.04f);
            BuildCreature("Golem","Forest_Golem_Stylized",golem,5);
            RegisterEncounters();BuildTestScene();AssetDatabase.SaveAssets();
            Validate();Directory.CreateDirectory("Docs/Validation");File.WriteAllText(Report,"PASS ASSET INTEGRATION\n"+string.Join("\n",log));
            Debug.Log("FOREST_CREATURE_INTEGRATION_PASS");
        }
        static void Folder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
        static string Model(string species,string name)=>"Assets/Art/Creatures/"+species+"/"+name+".fbx";
        static AnimationClip Clip(string species,string name)
        {
            string model=species=="Boar"?Boars[0]:species=="Spider"?Spiders[0]:"Forest_Golem_Stylized";
            return AssetDatabase.LoadAllAssetsAtPath(Model(species,model)).OfType<AnimationClip>()
                .Single(c=>!c.name.StartsWith("__preview")&&c.name.Split('|').Last()==name);
        }
        static Material Material(string species,string palette)
        {return AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Creatures/"+species+"/"+palette+".mat");}
        static void Import(string species,string name,string palette,bool animated=true)
        {
            string folder="Assets/Art/Creatures/"+species+"/",texturePath=folder+palette+".png";
            AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
            var texture=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            texture.filterMode=FilterMode.Point;texture.mipmapEnabled=false;texture.textureCompression=TextureImporterCompression.Uncompressed;texture.wrapMode=TextureWrapMode.Clamp;texture.SaveAndReimport();
            string mp=folder+palette+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);
            if(mat==null){mat=new Material(Shader.Find("Standard")??throw new Exception("Missing Standard shader"));AssetDatabase.CreateAsset(mat,mp);}
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);mat.color=Color.white;mat.SetFloat("_Glossiness",.08f);EditorUtility.SetDirty(mat);
            string path=Model(species,name);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=animated?ModelImporterAnimationType.Generic:ModelImporterAnimationType.None;
            importer.avatarSetup=animated?ModelImporterAvatarSetup.CreateFromThisModel:ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation=animated;importer.optimizeGameObjects=false;importer.importNormals=ModelImporterNormals.Import;
            importer.animationCompression=ModelImporterAnimationCompression.Off;importer.isReadable=true;importer.importCameras=false;importer.importLights=false;
            if(animated)
            {
                importer.motionNodeName="Root";
                var clips=importer.defaultClipAnimations;
                foreach(var clip in clips)
                {string semantic=clip.name.Split('|').Last();clip.loopTime=semantic=="Idle"||semantic=="Walk"||semantic=="Run"||semantic=="Charge";
                    clip.loopPose=false;clip.lockRootRotation=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;}
                importer.clipAnimations=clips;
            }
            importer.SaveAndReimport();
            foreach(var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source.name),mat);
            importer.SaveAndReimport();
            log.Add(name+": "+string.Join(", ",AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).Select(c=>c.name+"="+c.length.ToString("F3")+"s")));
        }
        static AnimatorController Controller(string species)
        {
            string path=Animations+"/"+species+".controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller!=null)return controller;
            controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Motion",AnimatorControllerParameterType.Int);controller.AddParameter("ActionTime",AnimatorControllerParameterType.Float);controller.AddParameter("PlaybackRate",AnimatorControllerParameterType.Float);
            var machine=controller.layers[0].stateMachine;var names=new[]{"Idle","Walk",species=="Golem"?"Walk":"Run"};
            for(int i=0;i<3;i++)
            {
                var state=machine.AddState(i==2?"Run":names[i]);state.motion=Clip(species,names[i]);if(i==0)machine.defaultState=state;
                else{state.speedParameter="PlaybackRate";state.speedParameterActive=true;}
                var transition=machine.AddAnyStateTransition(state);transition.hasExitTime=false;transition.duration=species=="Golem"?.025f:.06f;transition.hasFixedDuration=true;
                transition.canTransitionToSelf=false;transition.AddCondition(AnimatorConditionMode.Equals,i,"Motion");
            }
            return controller;
        }
        // JsonUtility populates these fields from the animation event file.
        [Serializable] sealed class EventFile {public int fps=0;public EventClips clips=null;}
        [Serializable] sealed class EventClips {public EventMarker[] Pick_Up_Rock=null,Throw_Rock=null,Ground_Slam=null,Sweep_Attack=null,Recover=null;}
        [Serializable] sealed class EventMarker {public string @event=null;public int frame=0;public float time_seconds=0;}
        static float EventTime(EventMarker[] markers,string name)
        {var marker=markers.Single(e=>e.@event==name);if(Mathf.Abs(marker.time_seconds-(marker.frame-1)/30f)>.001f)throw new Exception("Invalid event time "+name);return marker.time_seconds;}
        static GoblinAttack Action(string species,string clip,float range,float damage,float impact,float active,float cooldown)
        {
            var animation=Clip(species,clip);
            return new GoblinAttack{label=clip,range=range,damage=damage,windup=impact,active=active,recovery=Mathf.Max(.05f,animation.length-impact-active),cooldown=cooldown,
                animation=new EnemyAttackAnimation{clip=animation,activeStartsAt=impact/animation.length,recoveryStartsAt=(impact+active)/animation.length,blendSeconds=.025f}};
        }
        static CreatureSettings Settings(string species,GameObject rock)
        {
            string path=Data+"/"+species+".asset";var settings=AssetDatabase.LoadAssetAtPath<CreatureSettings>(path);if(settings!=null)return settings;
            settings=ScriptableObject.CreateInstance<CreatureSettings>();settings.displayName=species=="Boar"?"JABALÍ":species=="Spider"?"ARAÑA DEL BOSQUE":"GÓLEM DEL BOSQUE";
            if(species=="Boar")
            {
                settings.health=100;settings.speed=4.2f;settings.preferredRange=1.8f;settings.positioningSpeed=1.8f;settings.decisionPause=.45f;settings.posture=90;
                var bite=Action(species,"Attack",2.2f,14,.4f,.16f,1);bite.hitHeight=.65f;bite.forwardOffset=1.1f;bite.halfExtents=new Vector3(.65f,.65f,1);
                var charge=Action(species,"Charge",9,24,.65f,.7f,5);charge.minimumRange=3;charge.travel=6;charge.recovery=1.1f;
                charge.preparationClip=Clip(species,"Attack");charge.preparationDuration=.65f;charge.preparationEndNormalized=.3f;charge.loopActiveAnimation=true;
                charge.hitHeight=.65f;charge.forwardOffset=1.1f;charge.halfExtents=new Vector3(.7f,.65f,1.1f);settings.attacks=new[]{bite,charge};
            }
            else if(species=="Spider")
            {
                settings.health=65;settings.speed=4.5f;settings.positioningSpeed=2.6f;settings.preferredRange=1.8f;settings.decisionPause=.4f;settings.posture=50;
                var bite=Action(species,"Attack_Bite",2.2f,11,13/30f,.13f,.8f);bite.hitHeight=.5f;bite.forwardOffset=1.1f;bite.halfExtents=new Vector3(.8f,.55f,1);
                var jump=Action(species,"Jump",7,18,10/30f,21/30f,4);jump.minimumRange=3;jump.travel=4.5f;jump.damageStartsAt=.98f;
                jump.hitHeight=.55f;jump.forwardOffset=.9f;jump.halfExtents=new Vector3(1,.7f,1.2f);
                var web=Action(species,"Spit_Web",10,0,13/30f,.05f,6);web.enabled=false;web.kind=CreatureAttackKind.Projectile;web.projectileSocket="Web_Origin";
                settings.attacks=new[]{bite,jump,web};
            }
            else
            {
                var events=JsonUtility.FromJson<EventFile>(File.ReadAllText("ArtSource/Creatures/Golem/Animation_Events.json"));
                if(events.fps!=30)throw new Exception("Expected golem events at 30 FPS");
                settings.isBoss=true;settings.health=700;settings.posture=230;settings.speed=2.4f;settings.positioningSpeed=1.2f;settings.preferredRange=4.4f;
                settings.detectionRange=25;settings.loseRange=38;settings.leashRange=38;settings.sightHeight=3.2f;settings.allowedHeightDifference=3;settings.decisionPause=.35f;
                var slam=Action(species,"Ground_Slam",6,38,EventTime(events.clips.Ground_Slam,"Slam_Impact"),.1f,4);slam.hitHeight=.9f;slam.forwardOffset=3.2f;slam.halfExtents=new Vector3(2.7f,1.4f,2.7f);
                float start=EventTime(events.clips.Sweep_Attack,"Sweep_Active_Start"),end=EventTime(events.clips.Sweep_Attack,"Sweep_Active_End");
                var sweep=Action(species,"Sweep_Attack",6.2f,30,start,end-start,3);sweep.hitHeight=1.1f;sweep.forwardOffset=2.4f;sweep.halfExtents=new Vector3(4,1.5f,2.5f);
                float release=EventTime(events.clips.Throw_Rock,"Release_Rock");
                var throwing=Action(species,"Throw_Rock",28,34,release,.06f,8);throwing.minimumRange=8;throwing.kind=CreatureAttackKind.Projectile;
                throwing.preparationClip=Clip(species,"Pick_Up_Rock");throwing.preparationDuration=throwing.preparationClip.length;throwing.windup+=throwing.preparationDuration;
                throwing.grabAt=EventTime(events.clips.Pick_Up_Rock,"Grab_Rock");throwing.projectileVisual=rock;throwing.projectileRadius=.8f;throwing.projectileRange=40;throwing.projectileSpeed=14;
                foreach(var action in new[]{slam,sweep,throwing}){action.recoveryClip=Clip(species,"Recover");action.recovery+=action.recoveryClip.length;}
                settings.attacks=new[]{slam,sweep,throwing};
            }
            AssetDatabase.CreateAsset(settings,path);return settings;
        }
        static T GetOrAdd<T>(GameObject go) where T:Component
        {var component=go.GetComponent<T>();return component!=null?component:go.AddComponent<T>();}
        static Bounds BoundsOf(GameObject model)
        {var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new Exception("Model has no renderers");var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
        static GameObject BuildRock()
        {
            string path=Prefabs+"/RockProjectile.prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("RockProjectile");SceneManager.MoveGameObjectToScene(root,scene);
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model("Golem","Rock_Projectile")),root.transform);
                var b=BoundsOf(model);float scale=2/Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z));model.transform.localScale*=scale;model.transform.localPosition-=b.center*scale;
                foreach(var r in model.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(Material("Golem","Golem_Palette"),r.sharedMaterials.Length).ToArray();
                log.Add("Rock diameter normalized to 2 m");return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void BuildCreature(string species,string name,CreatureSettings settings,float height)
        {
            string path=Prefabs+"/"+name+".prefab";bool exists=AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null;
            var scene=EditorSceneManager.NewPreviewScene();var root=exists?PrefabUtility.LoadPrefabContents(path):new GameObject(name);
            if(!exists)SceneManager.MoveGameObjectToScene(root,scene);
            try
            {
                var old=root.transform.Find("Visual");if(old!=null)Object.DestroyImmediate(old.gameObject);
                var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model(species,name)),visual.transform);
                Clip(species,"Idle").SampleAnimation(model,0);
                var head=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Head");
                var body=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Body");
                if(species!="Golem"&&head!=null&&body!=null)
                {Vector3 forward=Vector3.ProjectOnPlane(head.position-body.position,Vector3.up);if(forward.sqrMagnitude>.001f)visual.transform.rotation=Quaternion.FromToRotation(forward.normalized,Vector3.forward);}
                var b=BoundsOf(model);float scale=height/b.size.y;visual.transform.localScale=Vector3.one*scale;
                b=BoundsOf(model);visual.transform.localPosition-=new Vector3(b.center.x,b.min.y,b.center.z);
                string palette=species=="Golem"?"Golem_Palette":name.Substring(species=="Boar"?5:7)+"_Palette";
                foreach(var r in model.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(Material(species,palette),r.sharedMaterials.Length).ToArray();
                var animator=GetOrAdd<Animator>(model);animator.runtimeAnimatorController=Controller(species);animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var collider=GetOrAdd<CapsuleCollider>(root);
                if(!exists){collider.radius=species=="Golem"?1.25f:species=="Spider"?.65f:.55f;collider.height=Mathf.Max(collider.radius*2,species=="Golem"?4.8f:species=="Spider"?1:1.2f);collider.center=Vector3.up*collider.height*.5f;}
                var nav=GetOrAdd<NavMeshAgent>(root);nav.height=collider.height;nav.radius=collider.radius;nav.speed=settings.speed;nav.baseOffset=0;nav.angularSpeed=360;nav.acceleration=20;
                if(root.GetComponent<Health>()==null)root.AddComponent<Health>();
                if(root.GetComponent<DamageReceiver>()==null)root.AddComponent<DamageReceiver>();
                var dealer=GetOrAdd<DamageDealer>(root);
                var ai=GetOrAdd<GoblinController>(root);if(!exists||ai.Settings==null)ai.Configure(settings,dealer);
                var driver=GetOrAdd<CreatureAnimationDriver>(root);driver.Configure(animator);
                PrefabUtility.SaveAsPrefabAsset(root,path);log.Add(name+": height="+BoundsOf(model).size.y.ToString("F2")+"m; generic rig; palette="+palette);
            }
            finally{if(exists)PrefabUtility.UnloadPrefabContents(root);EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void RegisterEncounters()
        {
            var catalog=Resources.Load<WorldContentCatalog>("WorldContentCatalog");if(catalog==null)throw new Exception("Missing world content catalog");
            var entries=catalog.encounters.ToList();
            foreach(string name in Boars.Concat(Spiders).Concat(new[]{"Forest_Golem_Stylized"}))
            {
                string id="forest."+name;if(entries.Any(e=>e.id==id))continue;
                bool boss=name.StartsWith("Forest_Golem");bool boar=name.StartsWith("Boar");
                entries.Add(new WorldEncounterEntry{id=id,prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/"+name+".prefab"),biomes=new[]{WorldBiome.Forest},
                    site=boss?WorldSiteKind.BossArena:WorldSiteKind.Clearing,weight=boss?4:boar?10:name.EndsWith("Albino")?2:8,
                    minimumCount=1,maximumCount=boss?1:boar?2:3,eliteChance=0});
            }
            catalog.encounters=entries.ToArray();EditorUtility.SetDirty(catalog);
        }
        static void BuildTestScene()
        {
            const string path="Assets/Scenes/ForestCreaturesTest.unity";if(File.Exists(path))return;
            var old=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Test ground";ground.transform.position=new Vector3(0,-.5f,18);ground.transform.localScale=new Vector3(110,1,100);
                var navigation=new GameObject("Test navigation").AddComponent<GoblinNavigation>();navigation.Configure(ground.transform);
                var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));player.transform.position=new Vector3(0,.2f,-20);
                var camera=new GameObject("Camera",typeof(UnityEngine.Camera),typeof(AudioListener));camera.tag="MainCamera";
                var cameraControl=camera.AddComponent<Mismo.Gameplay.Player.Camera.ThirdPersonCamera>();cameraControl.Configure(player.transform,player.GetComponent<Input.PlayerInputReader>());player.GetComponent<PlayerController>().Configure(camera.transform);
                var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-30,0);
                int i=0;foreach(string name in Boars.Concat(Spiders).Concat(new[]{"Forest_Golem_Stylized"}))
                {var mob=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/"+name+".prefab"));mob.transform.position=new Vector3((i%4-1.5f)*23,0,10+(i/4)*32);i++;}
                EditorSceneManager.SaveScene(scene,path);
            }
            finally{SceneManager.SetActiveScene(old);EditorSceneManager.CloseScene(scene,true);}
        }
        static void Validate()
        {
            foreach(string name in Boars.Concat(Spiders).Concat(new[]{"Forest_Golem_Stylized"}))
            {
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/"+name+".prefab");
                if(root==null||root.GetComponent<GoblinController>()?.Settings==null||root.GetComponent<CreatureAnimationDriver>()==null)throw new Exception("Invalid prefab "+name);
                foreach(var component in root.GetComponentsInChildren<Component>(true))if(component==null)throw new Exception("Missing script "+name);
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
                    if(material==null||material.mainTexture==null||material.shader==null)throw new Exception("Missing palette "+name);
                if(name=="Forest_Golem_Stylized"&&!root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Rock_Carry"))throw new Exception("Missing Rock_Carry");
                var animator=root.GetComponentInChildren<Animator>(true);
                if(animator==null||animator.avatar==null||!animator.avatar.isValid||animator.applyRootMotion)throw new Exception("Invalid Generic avatar "+name);
                var clips=root.GetComponent<GoblinController>().Settings.attacks.SelectMany(a=>new[]{a.animation.PlaybackClip,a.preparationClip,a.recoveryClip})
                    .Concat(animator.runtimeAnimatorController.animationClips).Where(c=>c!=null).Distinct();
                foreach(var clip in clips)foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                    if(!string.IsNullOrEmpty(binding.path)&&animator.transform.Find(binding.path)==null)throw new Exception(name+" missing animation binding "+binding.path);
            }
            log.Add("8 prefabs, materials, scripts, configurations and Rock_Carry references validated");
        }
        public static void RunBatch(){try{EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");Apply();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void CaptureBatch()
        {
            try
            {
                Directory.CreateDirectory("Docs/Validation/ForestCreatures");
                foreach(string name in Boars.Concat(Spiders).Concat(new[]{"Forest_Golem_Stylized"}))
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                    var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/"+name+".prefab"));
                    string species=name.StartsWith("Boar")?"Boar":name.StartsWith("Spider")?"Spider":"Golem";
                    var animator=root.GetComponentInChildren<Animator>();Clip(species,"Idle").SampleAnimation(animator.gameObject,0);
                    var bounds=BoundsOf(root);float extent=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
                    var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=Vector3.down*.1f;ground.transform.localScale=new Vector3(30,.1f,30);
                    var mat=new Material(Shader.Find("Standard"));mat.color=new Color(.2f,.27f,.18f);ground.GetComponent<Renderer>().sharedMaterial=mat;
                    var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(45,-35,0);
                    RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.7f,.7f,.7f);
                    var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.transform.position=bounds.center+new Vector3(-1,.7f,1.6f)*extent;
                    camera.transform.LookAt(bounds.center);camera.orthographic=true;camera.orthographicSize=extent*.7f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.19f);
                    var rt=new RenderTexture(720,720,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                    var image=new Texture2D(720,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,720,720),0,0);image.Apply();
                    File.WriteAllBytes("Docs/Validation/ForestCreatures/"+name+".png",image.EncodeToPNG());
                    RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(mat);
                }
                Debug.Log("FOREST_CAPTURE_PASS 8");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
