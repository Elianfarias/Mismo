using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

// Uses the authored player, weapon, animation and VFX, without starting a profile.
public static class ParryRegressionPlayChecks
{
    const string Active="Mismo.ParryRegression",ScenePath="Assets/Scenes/ParryRegressionTemp.unity",Output="output/parry-regression";
    [Serializable] sealed class SavedScenes { public SceneSetup[] scenes; }
    static IEnumerator routine;static double deadline;
    static NavMeshData navData;static NavMeshDataInstance nav;
    [InitializeOnLoadMethod] static void Register()
    {EditorApplication.update-=Poll;EditorApplication.update+=Poll;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists("Temp/ParryRegressionRestore.request"))
        {
            if(EditorApplication.isPlaying&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().path==ScenePath)
            {SessionState.SetBool(Active,false);routine=null;EditorApplication.ExitPlaymode();return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete("Temp/ParryRegressionRestore.request");RestoreInterruptedCheck();return;
        }
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists("Temp/ParryRegression.request"))
        {File.Delete("Temp/ParryRegression.request");Run();return;}
        if(!SessionState.GetBool(Active,false)||!Application.isPlaying||routine==null)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish(new TimeoutException("Parry regression"));return;}
        EditorApplication.QueuePlayerLoopUpdate();
    }
    public static void Step()
    {if(routine==null)return;try{if(!routine.MoveNext())Finish(null);}catch(Exception error){Finish(error);}}
    static void RestoreInterruptedCheck()
    {
        var scenes=EditorSceneManager.GetSceneManagerSetup();
        if(scenes.Length!=1||scenes[0].path!=ScenePath||UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath).isDirty)
        {Debug.LogWarning("Parry check cleanup preserved the current scene setup.");return;}
        SessionState.SetBool(Active,false);routine=null;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Single);
        AssetDatabase.DeleteAsset(ScenePath);SessionState.EraseString(Active+".Scenes");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/cleanup.txt","Restored MainMenu and removed the interrupted temporary scene.");
    }
    [MenuItem("Mismo/Combate/Verificar Parada con el jugador real")]
    public static void Run()
    {
        CheckImportedContent();
        Directory.CreateDirectory(Output);
        foreach(var scene in EditorSceneManager.GetSceneManagerSetup())
            if(string.IsNullOrEmpty(scene.path)||UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty)
            {File.WriteAllText(Output+"/checks.txt","BLOCKED: escena sin guardar, conservada intacta.");return;}
        if(File.Exists(ScenePath))throw new InvalidOperationException("Existing validation scene preserved.");
        SessionState.SetString(Active+".Scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup()}));
        EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single),ScenePath);
        File.WriteAllText(Output+"/checks.txt","Real player parry / "+DateTime.Now.ToString("s")+"\n");SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
    }
    public static void CheckImportedContent()
    {
        var parry=AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Data/Weapons/Sword/SwordParry.asset");
        if(parry==null||parry.actions.Length!=1||!(parry.actions[0] is ParryAction)||
            parry.preparation!=0||!Mathf.Approximately(parry.active,.5f)||!Mathf.Approximately(parry.recovery,.12f)||
            !Mathf.Approximately(parry.cooldown,.45f)||parry.pose!=AbilityPose.Parry||parry.weaponVfx.Length!=1||
            parry.weaponVfx[0].prefab==null||parry.weaponVfx[0].phase!=WeaponVfxPhase.Active)
            throw new InvalidOperationException("Unity did not import the complete Parry ability: inspect its serialized asset, including blank lines. Defense, timing, pose and VFX must all be present.");
    }
    public static void RunBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Use the editor menu.");
        if(Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))
        {
            Directory.CreateDirectory("Assets/Data/System");
            var weapons=AssetDatabase.LoadAssetAtPath<WeaponSetDefinition>("Assets/Data/System/TestWeapons.asset");
            if(weapons==null){weapons=ScriptableObject.CreateInstance<WeaponSetDefinition>();AssetDatabase.CreateAsset(weapons,"Assets/Data/System/TestWeapons.asset");}
            weapons.primary=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
            var catalog=AssetDatabase.LoadAssetAtPath<Mismo.Core.RuntimeAssetCatalog>("Assets/Data/System/RuntimeAssetCatalog.asset");
            if(catalog==null){catalog=ScriptableObject.CreateInstance<Mismo.Core.RuntimeAssetCatalog>();AssetDatabase.CreateAsset(catalog,"Assets/Data/System/RuntimeAssetCatalog.asset");}
            catalog.entries=new[]{new Mismo.Core.RuntimeAssetCatalog.Entry{key="StartingWeapons",assets=new Object[]{weapons}},
                new Mismo.Core.RuntimeAssetCatalog.Entry{key="CombatParticles",assets=new Object[]{AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/CombatParticles.shader")}}};
            EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(weapons);AssetDatabase.SaveAssets();PlayerSettings.SetPreloadedAssets(new Object[]{catalog});
            Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single),"Assets/Scenes/TestEmpty.unity");
        }
        else EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Single);
        Run();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Active,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {deadline=EditorApplication.timeSinceStartup+90;routine=Checks();Time.timeScale=1;Application.runInBackground=true;new GameObject("Parry checks clock").AddComponent<ParryRegressionFrameDriver>();}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Active,false);routine=null;
            EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Active+".Scenes","")).scenes);
            AssetDatabase.DeleteAsset(ScenePath);SessionState.EraseString(Active+".Scenes");
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(SessionState.GetInt(Active+".ExitCode",1));
        }
    }
    static void Log(string text)=>File.AppendAllText(Output+"/checks.txt",text+"\n");
    static IEnumerator Checks()
    {
        var holder=new GameObject("Inactive test setup");holder.SetActive(false);
        var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"),holder.transform);
        Object.DestroyImmediate(actor.GetComponent<PlayerController>());
        Object.DestroyImmediate(actor.GetComponent<Mismo.Gameplay.Player.World.RegionRespawn>());
        actor.transform.SetParent(null);Object.Destroy(holder);
        var loadout=actor.GetComponent<EquipmentLoadout>();loadout.Initialize();
        var weapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");loadout.TryEquip(0,weapon);
        var runner=loadout.Runner;var motor=actor.GetComponent<PlayerMotor>();var receiver=actor.GetComponent<DamageReceiver>();
        var parry=loadout.GetAbility(AbilitySlot.E);var vfx=actor.GetComponent<WeaponAbilityVfx>();
        var camera=new GameObject("Validation camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.gameObject.AddComponent<AudioListener>();
        camera.transform.position=new Vector3(3,2.4f,4);camera.transform.LookAt(Vector3.up*1.1f);camera.backgroundColor=new Color(.15f,.18f,.21f);camera.clearFlags=CameraClearFlags.SolidColor;
        var light=new GameObject("Validation light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,-35,0);
        RenderSettings.ambientLight=Color.gray;
        var source=new GameObject("Incoming sword");source.AddComponent<BoxCollider>().isTrigger=true;var dealer=source.AddComponent<DamageDealer>();
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=Vector3.down*.5f;floor.transform.localScale=new Vector3(20,1,20);
        navData=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=floor.transform.localToWorldMatrix,size=Vector3.one}},new Bounds(Vector3.zero,new Vector3(20,5,20)),Vector3.zero,Quaternion.identity);
        nav=NavMesh.AddNavMeshData(navData);
        yield return null;yield return null;
        Log("Ability="+parry.name+" actions="+parry.actions.Length+" active="+parry.active+" animator="+actor.GetComponent<PlayerAnimationDriver>().Animator.name);
        foreach(bool effects in new[]{true,false})foreach(float contactAt in new[]{.04f,.2f,.4f})
        {
            runner.Cancel();vfx.enabled=effects;actor.GetComponent<Invulnerability>().Cancel();
            var cooldowns=(System.Collections.IDictionary)typeof(AbilityRunner).GetField("readyAt",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(runner);cooldowns.Clear();
            if(!runner.TryUse(AbilitySlot.E,motor.Facing,Vector3.zero))throw new Exception("E failed to start");
            float elapsed=0;
            while(elapsed<contactAt){float dt=Mathf.Min(Time.deltaTime,contactAt-elapsed);runner.Tick(dt);elapsed+=dt;yield return null;}
            source.transform.position=actor.transform.position+motor.Facing*1.2f;
            float before=receiver.Health.Current;bool open=actor.GetComponent<DefenseWindow>().HasParry;
            int particleCount=0;foreach(var particle in actor.GetComponentsInChildren<ParticleSystem>())if(particle.transform.root==actor.transform&&particle.name!="Combat Feedback Particles")particleCount+=particle.particleCount;
            if(Mathf.Approximately(contactAt,.2f))Capture(camera,effects?"parry-with-vfx":"parry-without-vfx");
            dealer.Configure(10);dealer.ApplyTo(actor,actor.transform.position+Vector3.up,-motor.Facing);
            Log("VFX="+effects+" t="+contactAt+" defense="+open+" cast="+runner.Current?.Began+" facing="+motor.Facing+" outcome="+receiver.LastResult.Outcome+" damage="+(before-receiver.Health.Current)+" particles="+particleCount);
            if(receiver.LastResult.Outcome!=HitOutcome.Parry&&receiver.LastResult.Outcome!=HitOutcome.PerfectParry)throw new Exception("Frontal contact was not parried");
            yield return null;
        }
        runner.Cancel();CombatTimeFeedback.CancelForPause();Time.timeScale=1;vfx.enabled=true;
        var goblin=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/Goblin.prefab"),actor.transform.position+motor.Facing*1.4f,Quaternion.LookRotation(-motor.Facing));
        var enemy=goblin.GetComponent<EnemyController>();yield return null;enemy.enabled=false;
        foreach(bool defending in new[]{false,true})
        {
            runner.Cancel();CombatTimeFeedback.CancelForPause();Time.timeScale=1;actor.GetComponent<Invulnerability>().Cancel();
            enemy.enabled=true;
            enemy.SetTarget(actor.transform);enemy.GetComponent<CombatState>().ResetCombat();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(EnemyController).GetField("attack",flags).SetValue(enemy,enemy.Settings.slash);
            typeof(EnemyController).GetField("attackDirection",flags).SetValue(enemy,-motor.Facing);
            typeof(EnemyController).GetMethod("Enter",flags).Invoke(enemy,new object[]{EnemyState.Telegraph,.01f});
            if(defending)
            {
                ((System.Collections.IDictionary)typeof(AbilityRunner).GetField("readyAt",flags).GetValue(runner)).Clear();
                if(!runner.TryUse(AbilitySlot.E,motor.Facing,Vector3.zero))throw new Exception("Goblin test: E failed to start");runner.Tick(.02f);
            }
            float hp=receiver.Health.Current;Physics.SyncTransforms();enemy.Tick(.02f);
            Log("GOBLIN defending="+defending+" outcome="+receiver.LastResult.Outcome+" damage="+(hp-receiver.Health.Current)+" enemy="+enemy.State);
            if(defending?receiver.Health.Current!=hp||enemy.State!=EnemyState.Stagger:receiver.Health.Current>=hp)throw new Exception("Goblin melee parry regression");
            enemy.enabled=false;
            yield return null;
        }
        Log("PASS ALL real player frontal parries, VFX playback and goblin melee contact");
    }
    static void Capture(Camera camera,string name)
    {
        var target=new RenderTexture(800,800,24);var old=RenderTexture.active;camera.targetTexture=target;
        var image=new Texture2D(800,800,TextureFormat.RGB24,false);
        try{camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,800,800),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(image);Object.DestroyImmediate(target);}
    }
    static void Finish(Exception error){routine=null;Log(error==null?"COMPLETE":"FAIL "+error);SessionState.SetInt(Active+".ExitCode",error==null?0:1);CombatTimeFeedback.CancelForPause();Time.timeScale=1;if(nav.valid)nav.Remove();if(navData!=null)Object.Destroy(navData);EditorApplication.ExitPlaymode();}
}

public sealed class ParryRegressionFrameDriver : MonoBehaviour
{void Update()=>ParryRegressionPlayChecks.Step();}
