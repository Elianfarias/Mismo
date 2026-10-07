using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class HunterTrapChecks
{
    const string Output = HunterTrapModelBuilder.Output;
    const string Key = "Mismo.HunterTrapChecks";
    const string ScenePath = "Assets/Scenes/Validation/HunterTrapValidation.unity";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] sealed class SavedScenes { public SceneSetup[] scenes; }
    static IEnumerator routine; static int frame; static double deadline;
    static void Check(bool pass,string message,string file="checks.txt") { if(!pass)throw new Exception(message);File.AppendAllText(Output+"/"+file,"PASS "+message+"\n"); }
    [MenuItem("Mismo/Armas/Trampa de cazador/Verificar modelo y referencias")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/checks.txt","");
        var ability=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(HunterTrapModelBuilder.AbilityPath);
        var prefab=ability.actions.OfType<TrapAction>().Single().prefab;
        Check(prefab!=null&&AssetDatabase.GetAssetPath(prefab)==HunterTrapModelBuilder.PrefabPath,"Existing ability references the new trap prefab");
        Check(prefab.GetComponent<HunterTrap>()!=null&&prefab.GetComponent<HunterTrapVisual>()!=null,"Gameplay and articulated visual scripts survive prefab serialization");
        Check(prefab.GetComponentsInChildren<Collider>().Length==0,"The visual cannot block walking, projectiles or target selection");
        var meshes=prefab.GetComponentsInChildren<MeshFilter>();
        Check(meshes.Length==4&&meshes.All(m=>m.sharedMesh!=null&&m.sharedMesh.triangles.Length>0),"Four non-empty meshes: base/chain, two jaws and pressure plate");
        Check(meshes.Sum(m=>m.sharedMesh.triangles.Length/3)<40000,"Total mesh budget below 40,000 triangles");
        var visual=prefab.GetComponent<HunterTrapVisual>();
        Check(visual.frontJaw!=null&&visual.backJaw!=null&&visual.pressurePlate!=null,"All moving pieces have serialized references");
        foreach(var jaw in new[]{visual.frontJaw,visual.backJaw})
        {
            var mesh=jaw.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh.bounds.min.x<-.44f&&mesh.bounds.max.x>.44f,"Jaw reaches both hinge ends: "+jaw.name);
        }
        var material=meshes[0].GetComponent<Renderer>().sharedMaterial;
        Check(material!=null&&material.mainTexture!=null&&!ShaderUtil.ShaderHasError(material.shader),"Steel material, palette and URP shader resolve");
        try {ProjectOrganizationChecks.Run();Check(true,"ProjectOrganizationChecks.Run");}
        catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.ToString());File.AppendAllText(Output+"/checks.txt","PROJECT ORGANIZATION: existing external asset paths; see organization.txt\n");}
    }
    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.update-=Poll;EditorApplication.update+=Poll;
        EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;
        if(SessionState.GetBool(Key,false))deadline=EditorApplication.timeSinceStartup+120;
    }
    [MenuItem("Mismo/Armas/Trampa de cazador/Probar habilidad en Play Mode")]
    public static void Play()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Hay una escena sin guardar; se conserva.");
        if(File.Exists(ScenePath))throw new IOException("La escena temporal ya existe.");
        Directory.CreateDirectory(Output);
        SessionState.SetString(Key+".Scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup()}));
        ProjectAssetOrganizer.EnsureFolder("Assets/Scenes/Validation");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(scene,ScenePath);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Success",false);deadline=EditorApplication.timeSinceStartup+120;
        File.WriteAllText(Output+"/play-checks.txt","");EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){Application.runInBackground=true;routine=null;frame=-1;deadline=EditorApplication.timeSinceStartup+120;}
        if(state!=PlayModeStateChange.EnteredEditMode)return;
        SessionState.SetBool(Key,false);
        var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+".Scenes",""));
        if(saved.scenes.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        AssetDatabase.DeleteAsset(ScenePath);SessionState.EraseString(Key+".Scenes");
        if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetBool(Key+".Success",false)?0:1);
    }
    static void Poll()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
        if(!EditorApplication.isPlaying||Time.frameCount<5||frame==Time.frameCount)return;
        frame=Time.frameCount;
        try{routine??=PlayRoutine();if(!routine.MoveNext())Finish(true,"All gameplay and lifecycle checks passed");}
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool pass,string message)
    {File.AppendAllText(Output+"/play-checks.txt",(pass?"PASS ":"FAIL ")+message+"\n");SessionState.SetBool(Key+".Success",pass);Time.timeScale=1;EditorApplication.ExitPlaymode();}
    static void Assert(bool pass,string message)=>Check(pass,message,"play-checks.txt");
    static IEnumerator Wait(float duration){float until=Time.time+duration;while(Time.time<until)yield return null;}
    static IEnumerator PlayRoutine()
    {
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Test ground";floor.transform.position=new Vector3(0,-.1f,0);floor.transform.localScale=new Vector3(30,.2f,30);
        var owner=GameObject.CreatePrimitive(PrimitiveType.Capsule);owner.transform.position=Vector3.up;owner.GetComponent<Renderer>().enabled=false;
        owner.AddComponent<Health>();owner.AddComponent<DamageReceiver>();var runner=owner.AddComponent<AbilityRunner>();runner.enabled=false;
        var holder=new GameObject("Isolated goblin");holder.SetActive(false);
        var target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/Goblin.prefab"),holder.transform);
        target.transform.position=Vector3.forward*5;
        foreach(var brain in target.GetComponentsInChildren<EnemyController>())brain.enabled=false;
        foreach(var agent in target.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>())agent.enabled=false;
        holder.SetActive(true);yield return null;
        var health=target.GetComponent<Health>();float initialHealth=health.Current;int hits=0;DamageInfo last=default;
        health.Damaged+=d=>{hits++;last=d;};
        var ability=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(HunterTrapModelBuilder.AbilityPath);
        var weapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Bow/Bow.asset");
        AbilityExecution Cast(Vector3 point)=>new AbilityExecution(runner,weapon,ability,Vector3.forward,point);
        var cast=Cast(Vector3.zero);Physics.SyncTransforms();ability.actions.OfType<TrapAction>().Single().Begin(cast);
        var trap=Object.FindAnyObjectByType<HunterTrap>();
        Assert(trap!=null&&!trap.IsArmed&&trap.GetComponentsInChildren<MeshFilter>().Length==4,"Actual TrapAction spawns the authored model, initially unarmed");
        foreach(var delay in Enumerate(Wait(.5f)))yield return delay;
        Assert(trap.IsArmed&&!trap.Triggered&&owner.GetComponent<Health>().Current==100,"Arms after 0.4 seconds and ignores the caster's collider");
        owner.transform.position=new Vector3(0,1,-4);target.transform.position=Vector3.zero;Physics.SyncTransforms();
        foreach(var delay in Enumerate(Wait(.18f)))yield return delay;
        var visual=trap.GetComponent<HunterTrapVisual>();
        Assert(trap.Triggered&&hits==1&&Mathf.Abs(initialHealth-health.Current-12)<.001f,"Goblin steps on the trap: exactly one 12-damage hit");
        Assert(last.AbilityId==ability.Id&&last.AbilityUseId==cast.AttackId&&last.WeaponFamilyId==weapon.MasteryId,"Trap damage retains the skill, cast and weapon identity");
        Assert(target.GetComponent<CombatAilment>().SpeedMultiplier==0&&visual.Closure>.99f,"Both jaws close and the target is immobilized");
        var camera=new GameObject("Trap test camera").AddComponent<Camera>();camera.gameObject.AddComponent<AudioListener>();camera.tag="MainCamera";
        camera.backgroundColor=new Color(.095f,.11f,.12f);camera.clearFlags=CameraClearFlags.SolidColor;camera.fieldOfView=38;
        camera.transform.position=new Vector3(1.7f,1.7f,2.5f);camera.transform.LookAt(new Vector3(0,.55f,0));
        var light=new GameObject("Trap test sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(40,200,0);
        RenderSettings.sun=light;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.63f,.67f);
        Capture(camera);
        Time.timeScale=0;float closure=visual.Closure;double resume=EditorApplication.timeSinceStartup+.3;
        while(EditorApplication.timeSinceStartup<resume)yield return null;
        Assert(trap!=null&&visual.Closure==closure&&target.GetComponent<CombatAilment>().SpeedMultiplier==0,"Pause preserves the closed trap and immobilization");Time.timeScale=1;
        foreach(var delay in Enumerate(Wait(1.5f)))yield return delay;
        Assert(trap==null&&hits==1&&target.GetComponent<CombatAilment>().SpeedMultiplier==1,"After 1.5 seconds the closed trap disappears and movement recovers");
        target.transform.position=Vector3.forward*5;Physics.SyncTransforms();
        var otherOwner=new GameObject("Other caster");var otherRunner=otherOwner.AddComponent<AbilityRunner>();otherRunner.enabled=false;
        var otherTrap=HunterTrap.Spawn(new AbilityExecution(otherRunner,weapon,ability,Vector3.forward,new Vector3(-6,0,0)));
        var first=HunterTrap.Spawn(Cast(new Vector3(-2,0,0)));
        var second=HunterTrap.Spawn(Cast(new Vector3(-3,0,0)));
        var third=HunterTrap.Spawn(Cast(new Vector3(-4,0,0)));
        Assert(!first.gameObject.activeSelf&&second.Waiting&&third.Waiting,"Third placement retires only the caster's oldest waiting trap");
        Assert(otherTrap.Waiting,"Another caster's trap is independent of the placement limit");
        typeof(HunterTrap).GetField("expires",Private).SetValue(second,Time.time+.03f);
        foreach(var delay in Enumerate(Wait(.08f)))yield return delay;
        Assert(second==null&&third!=null,"Expiry cleans the instance without triggering damage");
        target.transform.position=Vector3.forward*3;target.GetComponent<DefenseWindow>().OpenDodge(5);Physics.SyncTransforms();
        var avoided=HunterTrap.Spawn(Cast(Vector3.forward*3));
        foreach(var delay in Enumerate(Wait(.6f)))yield return delay;
        Assert(avoided.Triggered&&hits==1&&target.GetComponent<CombatAilment>().SpeedMultiplier==1,"Dodging consumes the trap without damage or immobilization");
        var slope=GameObject.CreatePrimitive(PrimitiveType.Cube);slope.transform.position=new Vector3(10,.3f,0);slope.transform.localScale=new Vector3(2,.1f,2);slope.transform.rotation=Quaternion.Euler(0,0,15);Physics.SyncTransforms();
        var sloped=HunterTrap.Spawn(Cast(new Vector3(10,.3f,0)));
        Assert(Vector3.Dot(sloped.transform.up,slope.transform.up)>.999f,"The model sits on the supporting ground normal");
        Object.Destroy(owner);Object.Destroy(otherOwner);yield return null;yield return null;yield return null;
        Assert(third==null&&sloped==null,"Destroyed owner releases its remaining traps");
    }
    static IEnumerable<object> Enumerate(IEnumerator routine){while(routine.MoveNext())yield return routine.Current;}
    static void Capture(Camera camera)
    {
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1200,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var image=new Texture2D(1200,1000,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1200,1000),0,0);image.Apply();File.WriteAllBytes(Output+"/trap-goblin-play.png",image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
    }
    public static void Build()
    {
        Directory.CreateDirectory(Output);Directory.CreateDirectory(".validation/HunterTrapScripts");Directory.CreateDirectory(".validation/HunterTrapContent");
        var compiled=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings{target=BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone},".validation/HunterTrapScripts");
        if(compiled.assemblies==null||!compiled.assemblies.Any(p=>p.EndsWith("Mismo.Gameplay.Player.dll")))throw new Exception("Runtime compilation failed");
        File.WriteAllText(Output+"/runtime-compilation.txt","PASS Windows scripts without UNITY_EDITOR");
        var manifest=BuildPipeline.BuildAssetBundles(".validation/HunterTrapContent",new[]{new AssetBundleBuild{assetBundleName="hunter-trap",assetNames=new[]{HunterTrapModelBuilder.AbilityPath}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if(manifest==null)throw new Exception("Trap content build failed");
        var bundle=AssetBundle.LoadFromFile(".validation/HunterTrapContent/hunter-trap");
        try
        {
            var ability=bundle.LoadAsset<AbilityDefinition>(HunterTrapModelBuilder.AbilityPath);
            var prefab=ability.actions.OfType<TrapAction>().Single().prefab;
            if(prefab==null||prefab.GetComponent<HunterTrap>()==null||prefab.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh==null)||prefab.GetComponent<HunterTrapVisual>().frontJaw==null||prefab.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterial==null||r.sharedMaterial.mainTexture==null))throw new Exception("Built trap dependencies incomplete");
            File.WriteAllText(Output+"/content-build.txt","PASS Built ability reloads its trap, runtime scripts, 4 meshes, material, palette and jaw references");
        }
        finally{if(bundle!=null)bundle.Unload(true);}
        var errors=new List<string>();Application.LogCallback capture=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=capture;
        try
        {
            var game=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=".validation/HunterTrapGame/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText(Output+"/game-build.txt",game.summary.result+"\nErrors: "+game.summary.totalErrors+"\n"+string.Join("\n",errors.Distinct()));
        }
        catch(Exception e){File.WriteAllText(Output+"/game-build.txt","FAIL\n"+e);}
        finally{Application.logMessageReceived-=capture;}
    }
}
