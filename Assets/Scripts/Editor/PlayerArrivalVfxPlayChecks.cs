using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class PlayerArrivalVfxPlayChecks
{
    const string Key="Mismo.PlayerArrivalPlayChecks";
    const string ScenePath="Assets/Scenes/Validation/PlayerArrivalValidation.unity";
    const string Output=PlayerArrivalVfxSetup.Output;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] sealed class SavedScenes { public SceneSetup[] scenes; }
    static IEnumerator routine; static int frame; static double deadline;
    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.update-=Poll;EditorApplication.update+=Poll;
        EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;
        if(SessionState.GetBool(Key,false))deadline=EditorApplication.timeSinceStartup+150;
    }
    public static void Run()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);
            if(scene.isDirty||string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("La escena abierta tiene cambios sin guardar; se conserva.");
        }
        if(File.Exists(ScenePath))throw new IOException("La escena de validacion ya existe.");
        Directory.CreateDirectory(Output);
        SessionState.SetString(Key+".Scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup()}));
        ProjectAssetOrganizer.EnsureFolder("Assets/Scenes/Validation");
        var test=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Arrival test ground";floor.transform.position=new Vector3(0,3.5f,0);floor.transform.localScale=new Vector3(300,1,300);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerArrivalVfxSetup.PlayerPath));player.transform.position=Vector3.up*4.15f;
        if(player.GetComponent<RegionRespawn>()==null)player.AddComponent<RegionRespawn>();
        var camera=new GameObject("Arrival test camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(3,6,-5);camera.transform.LookAt(new Vector3(0,5,0));
        camera.backgroundColor=new Color(.06f,.08f,.11f);camera.clearFlags=CameraClearFlags.SolidColor;
        var light=new GameObject("Arrival test sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(35,-30,0);
        EditorSceneManager.SaveScene(test,ScenePath);
        EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
        SessionState.SetBool(Key,true);File.WriteAllText(Output+"/play-checks.txt","Running real teleport and death/scene reload checks.\n");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void IsolateInventory()
    {
        if(!SessionState.GetBool(Key,false))return;
        string directory=Path.GetFullPath(".validation/PlayerArrivalProfiles");Directory.CreateDirectory(directory);
        typeof(PlayerInventory).GetField("BuildCheckRepository",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new ProtectedProfileRepository(Path.Combine(directory,"inventory.mismo")));
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){Application.runInBackground=true;routine=null;frame=-1;deadline=EditorApplication.timeSinceStartup+150;}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).ToArray();
            var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+".Scenes",""));
            EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);AssetDatabase.DeleteAsset(ScenePath);
            SessionState.EraseString(Key+".Scenes");
        }
    }
    static void Poll()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||Time.frameCount<5||frame==Time.frameCount)return;
        frame=Time.frameCount;
        if(EditorApplication.timeSinceStartup>deadline){Finish("FAIL: timeout");return;}
        try {routine??=RunChecks();if(!routine.MoveNext())Finish("PASS: real map teleport, repeated arrival, pause, cleanup, death reload and ordinary reload.");}
        catch(Exception e){Finish("FAIL: "+e);}
    }
    static void Finish(string message){File.AppendAllText(Output+"/play-checks.txt",message+"\n");Debug.Log(message);EditorApplication.ExitPlaymode();}
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);File.AppendAllText(Output+"/play-checks.txt","PASS "+message+"\n");}
    static IEnumerator Wait(float seconds){float end=Time.time+seconds;while(Time.time<end)yield return null;}
    static IEnumerator RunChecks()
    {
        var player=Object.FindAnyObjectByType<PlayerController>();player.enabled=false;
        var effect=player.GetComponent<PlayerArrivalVfx>();
        Check(effect!=null&&!effect.IsPlaying&&effect.PlayCount==0,"Ordinary initial scene load does not play arrival");
        var settings=ScriptableObject.CreateInstance<ExplorationWorldSettings>();settings.preserveAuthoredCenter=true;settings.authoredSize=512;
        var chunks=new GameObject("Arrival test collision neighbourhood").AddComponent<ExplorationChunks>();chunks.enabled=false;
        typeof(ExplorationChunks).GetField("<Settings>k__BackingField",Private).SetValue(chunks,settings);
        var map=player.gameObject.AddComponent<WorldMapPanel>();map.Initialize(settings);map.enabled=false;
        var site=new WorldSite{position=Vector3.zero,kind=WorldSiteKind.Village};
        var terrain=new ExplorationTerrain(settings);float ground=terrain.Height(0,-55);
        GameObject.Find("Arrival test ground").transform.position=new Vector3(0,ground-.5f,0);Physics.SyncTransforms();
        Check(map.TravelTo(site),"Actual WorldMapPanel.TravelTo succeeds");
        Check(effect.IsPlaying&&effect.PlayCount==1,"Teleport starts one effect after moving");
        Check(Mathf.Abs(player.transform.position.z+55)<.1f,"Effect belongs to the village arrival position");
        var camera=Camera.main;camera.transform.position=player.transform.position+new Vector3(3,2,-4);camera.transform.LookAt(player.transform.position+Vector3.up);
        float until=Time.time+1.25f;while(Time.time<until)yield return null;
        ScreenCapture.CaptureScreenshot(Output+"/teleport.png");
        GameplayPause.Pause();float progress=effect.Progress;
        double resumeAt=EditorApplication.timeSinceStartup+.3;while(EditorApplication.timeSinceStartup<resumeAt)yield return null;
        Check(Mathf.Abs(effect.Progress-progress)<.001f,"Pause freezes the reveal");GameplayPause.Resume();
        Check(map.TravelTo(site)&&effect.PlayCount==2,"Second teleport restarts instead of stacking");
        until=Time.time+3.2f;while(Time.time<until)yield return null;
        Check(!effect.IsPlaying,"Body materials restored after reveal");
        effect.Play();effect.enabled=false;Check(!effect.IsPlaying,"Disabling the player restores materials immediately");effect.enabled=true;
        player.GetComponent<Health>().ApplyDamage(new DamageInfo(100000,null,Vector3.zero,Vector3.forward));
        var old=player.gameObject;while(old!=null)yield return null;for(int i=0;i<3;i++)yield return null;
        Object.Destroy(settings);
        player=Object.FindAnyObjectByType<PlayerController>();player.enabled=false;effect=player.GetComponent<PlayerArrivalVfx>();
        Check(player.GetComponent<Health>().Current>0,"Real death reload restored health");
        Check(effect.IsPlaying&&effect.PlayCount==1,"Respawn starts exactly once on the replacement player");
        until=Time.time+1.2f;while(Time.time<until)yield return null;
        ScreenCapture.CaptureScreenshot(Output+"/respawn.png");
        until=Time.time+5;while(Time.time<until)yield return null;
        Check(!effect.IsPlaying&&!SceneManager.GetActiveScene().GetRootGameObjects().Any(g=>g.name=="Player arrival particles"),"Respawn finishes and releases particles");
        old=player.gameObject;SceneManager.LoadScene(ScenePath);while(old!=null)yield return null;for(int i=0;i<3;i++)yield return null;
        effect=Object.FindAnyObjectByType<PlayerArrivalVfx>();
        Check(effect.PlayCount==0&&!effect.IsPlaying,"Respawn flag is consumed; ordinary reload does not replay it");
    }
}
