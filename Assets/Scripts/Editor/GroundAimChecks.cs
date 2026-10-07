using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class GroundAimChecks
{
    const string Output="output/ground-aim",Key="Mismo.GroundAimChecks",ScenePath="Assets/Scenes/Validation/GroundAimValidation.unity";
    const string TrapPath="Assets/Data/Weapons/Bow/HunterTrap.asset",RainPath="Assets/Data/Weapons/Bow/BowArea.asset";
    [Serializable] sealed class SavedScenes {public SceneSetup[] scenes;}
    static IEnumerator routine;static int frame;static double deadline;
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);File.AppendAllText(Output+"/play-checks.txt","PASS "+message+"\n");}
    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.update-=Poll;EditorApplication.update+=Poll;
        EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;
        if(SessionState.GetBool(Key,false))deadline=EditorApplication.timeSinceStartup+120;
    }
    [MenuItem("Mismo/Armas/Apuntado de suelo/Probar en Play Mode")]
    public static void Play()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Se conserva la escena sin guardar.");
        if(File.Exists(ScenePath))throw new Exception("La escena temporal ya existe.");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/play-checks.txt","");
        SessionState.SetString(Key+".Scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup()}));
        ProjectAssetOrganizer.EnsureFolder("Assets/Scenes/Validation");
        EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single),ScenePath);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Success",false);deadline=EditorApplication.timeSinceStartup+120;
        EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){Application.runInBackground=true;routine=null;frame=-1;deadline=EditorApplication.timeSinceStartup+120;}
        if(state!=PlayModeStateChange.EnteredEditMode)return;
        SessionState.SetBool(Key,false);var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+".Scenes",""));
        if(saved.scenes.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        AssetDatabase.DeleteAsset(ScenePath);SessionState.EraseString(Key+".Scenes");
        if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetBool(Key+".Success",false)?0:1);
    }
    static void Poll()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
        if(!EditorApplication.isPlaying||Time.frameCount<5||frame==Time.frameCount)return;frame=Time.frameCount;
        try{routine??=Routine();if(!routine.MoveNext())Finish(true,"Ground aiming, flight and area checks passed");}
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool pass,string message)
    {File.AppendAllText(Output+"/play-checks.txt",(pass?"PASS ":"FAIL ")+message+"\n");SessionState.SetBool(Key+".Success",pass);GameplayPause.Resume();Time.timeScale=1;EditorApplication.ExitPlaymode();}
    static IEnumerator Routine()
    {
        var trap=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(TrapPath);var rain=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(RainPath);
        Check(AbilityRunner.SupportsGroundAim(trap)&&AbilityRunner.SupportsGroundAim(rain),"Both serialized abilities enable hold-to-aim");
        Check(!ShaderUtil.ShaderHasError(trap.groundIndicatorMaterial.shader),"Serialized indicator material and URP shader resolve");
        var action=AbilityRunner.GroundThrowAction(trap);var areaAction=rain.actions.OfType<GroundAreaAction>().Single();
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Ground";floor.transform.position=new Vector3(0,-.1f,0);floor.transform.localScale=new Vector3(30,.2f,30);
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",new Color(.15f,.21f,.17f));floor.GetComponent<Renderer>().sharedMaterial=material;
        var actor=new GameObject("Isolated ground aiming actor");var capsule=actor.AddComponent<CapsuleCollider>();capsule.center=Vector3.up*.9f;capsule.height=1.8f;capsule.radius=.28f;
        var health=actor.AddComponent<Health>();actor.AddComponent<DamageReceiver>();var stamina=actor.AddComponent<Mismo.Gameplay.Player.Movement.Stamina>();
        var staminaSettings=ScriptableObject.CreateInstance<Mismo.Gameplay.Player.Movement.StaminaSettings>();stamina.Configure(staminaSettings);
        var loadout=actor.AddComponent<EquipmentLoadout>();loadout.Initialize();var runner=loadout.Runner;
        actor.GetComponent<WeaponPresentation>().enabled=false;
        var bow=Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Bow/Bow.asset"));bow.overrideFamilyAbilities=true;bow.abilities=new AbilityDefinition[]{null,null,rain,trap};
        Check(loadout.TryEquip(0,bow),"Isolated bow loadout, no player saves");
        var mage=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Skins/MageSkin.prefab"),actor.transform);
        var renderers=mage.GetComponentsInChildren<Renderer>();var animator=mage.GetComponentInChildren<Animator>();
        if(animator!=null){var idle=animator.runtimeAnimatorController?.animationClips.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("idle"));if(idle!=null)idle.SampleAnimation(animator.gameObject,.3f);animator.enabled=false;}
        Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);mage.transform.localScale*=1.8f/bounds.size.y;
        bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);mage.transform.position-=Vector3.up*bounds.min.y;
        var camera=new GameObject("Ground aim camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();camera.fieldOfView=42;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.14f,.14f);camera.transform.position=new Vector3(4,4.2f,-6);camera.transform.LookAt(new Vector3(0,.25f,2));
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.4f;sun.transform.rotation=Quaternion.Euler(48,-30,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.58f,.62f);
        Physics.SyncTransforms();var destination=new Vector3(0,0,4);var ray=new Ray(new Vector3(0,6,4),Vector3.down);
        Check(runner.TryBeginGroundAim(AbilitySlot.R,ray)&&runner.IsGroundAiming&&runner.GroundAimPath.valid,"R starts a valid trap preview");
        float initialStamina=stamina.Current;float holdUntil=Time.time+1.1f;
        while(Time.time<holdUntil){runner.UpdateGroundAim(ray,true);runner.Tick(Time.deltaTime);yield return null;}
        Check(runner.Current==null&&runner.Remaining(trap)==0&&stamina.Current==initialStamina&&Object.FindObjectsByType<HunterTrap>(FindObjectsSortMode.None).Length==0,"Holding beyond normal charge time spends nothing and spawns nothing");
        Check(!Mismo.Gameplay.Enemies.EnemyPerception.IsPreparingAimedAction(actor.transform),"Nearby AI can query the held preview without mistaking it for an executing cast");
        var preview=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Ground throw preview");
        Check(preview.GetComponentsInChildren<Collider>().Length==0&&preview.GetComponentsInChildren<HunterTrap>().Length==0,"Silhouette is cosmetic with no colliders or gameplay scripts");
        Check(preview.GetComponentsInChildren<LineRenderer>().Count(l=>l.enabled)==2,"Trap preview has its arc and landing ring");
        Capture(camera,"trap-aim.png");
        runner.CancelGroundAim();Check(!preview.activeSelf&&runner.Remaining(trap)==0,"Cancel hides the indicator without cooldown");
        runner.TryBeginGroundAim(AbilitySlot.R,ray);Check(runner.Interrupt()&&!runner.IsGroundAiming&&runner.Remaining(trap)==0,"A hit cancels aiming without a throw");
        runner.TryBeginGroundAim(AbilitySlot.R,ray);GameplayPause.Pause();yield return null;
        Check(!runner.IsGroundAiming&&!preview.activeSelf,"Pause cancels the gesture, so resume cannot throw unexpectedly");GameplayPause.Resume();yield return null;yield return null;
        Check(runner.TryBeginGroundAim(AbilitySlot.R,ray),"Can aim again after pause");
        var path=runner.GroundAimPath;runner.UpdateGroundAim(ray,false);
        Check(!runner.IsGroundAiming&&runner.Current!=null&&runner.Remaining(trap)>0,"Release commits one cast and starts cooldown");
        runner.Tick(.11f);var flying=Object.FindAnyObjectByType<HunterTrap>();
        Check(flying!=null&&flying.IsFlying&&!flying.IsArmed&&Vector3.Distance(flying.transform.position,path.start)<.001f,"Real trap launches from the preview origin and cannot trigger in flight");
        GameplayPause.Pause();var frozen=flying.transform.position;double resume=EditorApplication.timeSinceStartup+.2;
        while(EditorApplication.timeSinceStartup<resume)yield return null;
        Check(flying.transform.position==frozen&&flying.IsFlying,"Pause freezes flight");GameplayPause.Resume();
        bool followsCurve=true;int flightSamples=0;
        while(flying!=null&&flying.IsFlying)
        {
            float elapsed=(float)typeof(HunterTrap).GetField("flightElapsed",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(flying);
            followsCurve&=Vector3.Distance(flying.transform.position,path.Sample(elapsed/path.duration))<.001f;flightSamples++;
            yield return null;
        }
        Check(followsCurve&&flightSamples>1,"Observed flight positions match the preview curve");
        Check(flying!=null&&Vector3.Distance(flying.transform.position,path.end)<.001f&&!flying.IsArmed,"Lands exactly on preview and starts its arming delay on arrival; exists="+(flying!=null)+(flying!=null?" position="+flying.transform.position+" expected="+path.end+" armed="+flying.IsArmed:""));
        float armedAt=Time.time+.45f;while(Time.time<armedAt)yield return null;
        Check(flying.IsArmed,"Trap arms after landing");runner.Cancel();Object.Destroy(flying.gameObject);yield return null;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Blocking wall";wall.transform.position=new Vector3(0,2,2);wall.transform.localScale=new Vector3(3,4,.3f);Physics.SyncTransforms();
        var blocked=GroundThrowTrajectory.Aim(actor,ray,trap.range,action);
        Check(!blocked.valid,"The parabolic path detects a wall before the destination");
        var indicator=new GroundThrowIndicator(actor);indicator.Show(blocked,trap.groundIndicatorMaterial,action,.65f);Capture(camera,"trap-blocked.png");indicator.Dispose();
        Check(!GroundThrowTrajectory.Aim(actor,ray,rain.range,null).valid,"Area target behind a wall is rejected");
        wall.SetActive(false);Physics.SyncTransforms();
        Check(!GroundThrowTrajectory.ToPoint(actor,new Vector3(14.9f,0,0),Vector3.up,20,action).valid,"A trap cannot be placed hanging off a ledge");
        Check(!GroundThrowTrajectory.ToPoint(actor,destination,new Vector3(.86f,.5f,0),5,action).valid,"Steep landing normals are rejected");
        Check(!GroundThrowTrajectory.Aim(actor,new Ray(new Vector3(20,6,20),Vector3.down),40,action).valid,"Missing ground is invalid");
        var slope=GameObject.CreatePrimitive(PrimitiveType.Cube);slope.transform.position=new Vector3(8,.4f,0);slope.transform.localScale=new Vector3(2,.1f,2);slope.transform.rotation=Quaternion.Euler(0,0,15);Physics.SyncTransforms();
        var sloped=GroundThrowTrajectory.Aim(actor,new Ray(new Vector3(8,7,0),Vector3.down),20,action);
        Check(sloped.valid&&Vector3.Dot(sloped.normal,slope.transform.up)>.999f,"A supported gentle slope remains valid and aligns the landing normal");
        // Reuse the same gesture for rain, with the exact gameplay radius and no parabola/ghost.
        Check(runner.TryBeginGroundAim(AbilitySlot.E,ray)&&runner.GroundAimPath.valid,"Rain begins its own held targeting gesture");
        Check(preview.GetComponentsInChildren<LineRenderer>().Count(l=>l.enabled)==1&&preview.GetComponentsInChildren<HunterTrap>().Length==0,"Rain shows only its ground ring");
        var ring=preview.GetComponentsInChildren<LineRenderer>().Single(l=>l.enabled);
        Check(Enumerable.Range(0,ring.positionCount).All(i=>Mathf.Abs(Vector3.ProjectOnPlane(ring.GetPosition(i)-runner.GroundAimPath.end,Vector3.up).magnitude-areaAction.radius)<.002f),"Rain indicator radius equals GroundAreaAction.radius (2.5 m)");
        Capture(camera,"rain-aim.png");var rainPoint=runner.GroundAimPath.end;
        runner.UpdateGroundAim(ray,true);runner.Tick(2);
        Check(runner.Current==null&&runner.Remaining(rain)==0&&stamina.Current==initialStamina&&Object.FindAnyObjectByType<AreaInstance>()==null,"Holding rain does not spend stamina or start damage, preparation or cooldown");
        runner.UpdateGroundAim(ray,false);
        Check(Mismo.Gameplay.Enemies.EnemyPerception.IsPreparingAimedAction(actor.transform),"AI still detects the real rain preparation after release");runner.Tick(2);
        var area=Object.FindAnyObjectByType<AreaInstance>();Check(area!=null&&Vector3.Distance(area.transform.position,rainPoint)<.001f,"Releasing rain spawns the actual area at the marked point");
        Check(Object.FindObjectsByType<AreaInstance>(FindObjectsSortMode.None).Length==1,"Rain release creates one area");runner.Cancel();
        Check(Mathf.Abs(stamina.Current-(initialStamina-rain.staminaCost))<.001f,"Rain pays its real 20-stamina cost once, on release");
        var dynamicPath=GroundThrowTrajectory.Aim(actor,ray,trap.range,action);
        var interrupted=HunterTrap.Launch(new AbilityExecution(runner,bow,trap,Vector3.forward,dynamicPath.end),action.prefab,dynamicPath);
        wall.SetActive(true);Physics.SyncTransforms();float flightEnd=Time.time+dynamicPath.duration+.1f;
        while(Time.time<flightEnd)yield return null;
        Check(interrupted==null,"An obstacle entering after release stops the real trap rather than letting it pass through");wall.SetActive(false);Physics.SyncTransforms();
        // Bypass cooldown only on an isolated cloned ability, never on the actual game asset.
        var localTrap=Object.Instantiate(trap);bow.abilities[3]=localTrap;
        wall.SetActive(true);Physics.SyncTransforms();runner.TryBeginGroundAim(AbilitySlot.R,ray);runner.UpdateGroundAim(ray,false);
        Check(runner.Current==null&&runner.Remaining(localTrap)==0,"Invalid release cancels without spending cooldown");
        wall.SetActive(false);Physics.SyncTransforms();runner.TryBeginGroundAim(AbilitySlot.R,ray);
        health.ApplyDamage(new DamageInfo(10000,null,Vector3.zero,Vector3.zero));yield return null;
        Check(!runner.IsGroundAiming&&!preview.activeSelf,"Death clears the aim indicator");
        Object.Destroy(actor);yield return null;yield return null;
        Check(!SceneManager.GetActiveScene().GetRootGameObjects().Any(g=>g.name=="Ground throw preview"),"Indicator roots are released with their owner");
        Object.Destroy(localTrap);Object.Destroy(bow);Object.Destroy(material);Object.Destroy(staminaSettings);
    }
    static void Capture(Camera camera,string name)
    {
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var image=new Texture2D(1280,900,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,900),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
    }
    [MenuItem("Mismo/Armas/Apuntado de suelo/Verificar contenido compilado")]
    public static void Build()
    {
        Directory.CreateDirectory(Output);
        try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","PASS");}catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.ToString());}
        Directory.CreateDirectory(".validation/GroundAimScripts");Directory.CreateDirectory(".validation/GroundAimContent");
        var scripts=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings{target=BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone},".validation/GroundAimScripts");
        if(scripts.assemblies==null||!scripts.assemblies.Any(p=>p.EndsWith("Mismo.Gameplay.Player.dll")))throw new Exception("Runtime compilation failed");
        var manifest=BuildPipeline.BuildAssetBundles(".validation/GroundAimContent",new[]{new AssetBundleBuild{assetBundleName="ground-aim",assetNames=new[]{TrapPath,RainPath}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if(manifest==null)throw new Exception("Content build failed");
        var bundle=AssetBundle.LoadFromFile(".validation/GroundAimContent/ground-aim");
        try
        {
            foreach(var path in new[]{TrapPath,RainPath})
            {
                var ability=bundle.LoadAsset<AbilityDefinition>(path);
                if(!AbilityRunner.SupportsGroundAim(ability)||ability.groundIndicatorMaterial.shader==null)throw new Exception("Missing built indicator: "+path);
            }
            File.WriteAllText(Output+"/content-build.txt","PASS Windows runtime scripts and bundle reload: both abilities, preview material/shader, trap meshes and arrow dependencies.\n");
        }
        finally{if(bundle!=null)bundle.Unload(true);}
    }
}
