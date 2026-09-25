using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.World.Structures;
using Mismo.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class WorldIntroductionChecks
{
    const string Pending="Mismo.WorldIntroductionChecks";
    const string Output="output/world-introduction";
    static IEnumerator routine;
    static double deadline;
    static int frame=-1;
    static readonly List<string> results=new List<string>(),errors=new List<string>();
    public static void CaptureRefugeEntrance()
    {WorldIntroductionBuilder.FaceCottageToClearing();CaptureRefuge();}
    public static void CaptureRefuge()
    {
        Directory.CreateDirectory(Output);WorldIntroductionBuilder.RemoveRefugePond();
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        SessionState.SetBool(Pending+".capture",true);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);WorldIntroductionBuilder.Build();WorldIntroductionBuilder.RefreshNarrator();
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        if(!SessionState.GetBool(Pending,false))return;
        deadline=EditorApplication.timeSinceStartup+480;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
    }
    static void Log(string message,string stack,LogType type)
    {if(type==LogType.Error||type==LogType.Exception){if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;errors.Add(message+"\n"+stack);}}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline){Finish(new Exception("Timeout"));return;}
        if(!Application.isPlaying||Time.frameCount<6||frame==Time.frameCount)return;
        frame=Time.frameCount;
        try{if(routine==null)routine=SessionState.GetBool(Pending+".capture",false)?Capture():Checks();if(!routine.MoveNext())Finish(null);}catch(Exception e){Finish(e);}
    }
    static IEnumerator Capture()
    {
        string directory=Path.GetFullPath(Output+"/Profiles/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        typeof(WorldSession).GetField("VerificationDirectory",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,directory);
        if(!WorldSession.NewGame())throw new Exception(WorldSession.LastError);
        var template=AssetDatabase.LoadAssetAtPath<ExplorationWorldSettings>("Assets/Data/World/ExplorationWorldSettings.asset");
        var settings=WorldSession.Settings(template);var layout=new ExplorationTerrain(settings).Introduction;
        WorldSession.SaveIntroduction(4,3,position:layout.Grove+layout.Facing*new Vector3(0,.3f,-1),yaw:layout.Facing.eulerAngles.y);
        Object.Destroy(settings);SceneManager.LoadScene("VoxelRegion_7319");yield return null;
        while(Object.FindAnyObjectByType<ExplorationChunks>()==null)yield return null;
        var world=Object.FindAnyObjectByType<ExplorationChunks>();while(!world.NavigationReady)yield return null;
        for(int i=0;i<90;i++)yield return null;
        ScreenCapture.CaptureScreenshot(Output+"/grove.png");for(int i=0;i<8;i++)yield return null;
        results.Add("Refuge visual capture completed; errors="+errors.Count);
    }
    static void Require(bool test,string label){if(!test)throw new Exception(label);results.Add("PASS: "+label);Debug.Log("INTRO_CHECK: "+label);}
    static IEnumerator Checks()
    {
        string directory=Path.GetFullPath(Output+"/Profiles/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        typeof(WorldSession).GetField("VerificationDirectory",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,directory);
        var template=AssetDatabase.LoadAssetAtPath<ExplorationWorldSettings>("Assets/Data/World/ExplorationWorldSettings.asset");
        Require(template!=null,"World settings exist");
        var definition=AssetDatabase.LoadAssetAtPath<WorldIntroductionDefinition>(WorldIntroductionBuilder.DefinitionPath);
        var cave=AssetDatabase.LoadAssetAtPath<StructureDefinition>("Assets/Data/World/Structures/Cave_Crystal_Introduction.asset");
        Require(cave.ValidateLayout()==null&&cave.rooms.Count==6&&cave.Radius>32,"Extended crystal cave validates independently of streamed structures");
        float length=0;foreach(var c in cave.connections)length+=Vector2.Distance(cave.rooms[c.from].center,cave.rooms[c.to].center);
        Require(length>90,"Cave room-to-room route exceeds 90 metres");
        var settings=Object.Instantiate(template);settings.preserveAuthoredCenter=false;settings.generationVersion=2;settings.introductionVersion=1;
        foreach(int seed in new[]{1,17,7319,51923,999999,int.MaxValue})
        {
            settings.seed=seed;var terrain=new ExplorationTerrain(settings);var layout=terrain.Introduction;
            foreach(var p in layout.Route.Concat(new[]{layout.Spawn,layout.Cave}))
                Require(terrain.Plan.Walkable(p.x,p.z)&&Mathf.Abs(terrain.Height(p.x,p.z)-12)<.26f,"Safe intro ground seed "+seed+" at "+p);
            Require(terrain.Reserved(layout.Spawn.x,layout.Spawn.z,2),"No random resources or enemies in cave: "+seed);
        }
        settings.introductionVersion=0;Require(new ExplorationTerrain(settings).Introduction==null,"Existing generator settings do not acquire introduction terrain");Object.Destroy(settings);
        var menu=Object.FindAnyObjectByType<MainMenuView>();Require(menu!=null,"Real MainMenu scene loaded");menu.Begin();
        while(Object.FindAnyObjectByType<WorldIntroduction>()==null)yield return null;
        var intro=Object.FindAnyObjectByType<WorldIntroduction>();var player=Object.FindAnyObjectByType<PlayerController>();var motor=player.GetComponent<PlayerMotor>();
        var guide=player.GetComponent<TutorialGuide>();var inventory=player.GetComponent<PlayerInventory>();var equipment=player.GetComponent<EquipmentLoadout>();
        while(!guide.IsOpen)yield return null;
        Require(SceneManager.GetActiveScene().name=="VoxelRegion_7319","Nueva partida loads real procedural continent");
        Require(Vector3.Distance(player.transform.position,intro.Layout.Spawn)<2,"Nueva partida spawns inside the long cave");
        Require(inventory.IsReady&&!inventory.HasSaveProblem&&WorldSession.ProfilePath.StartsWith(directory),"Persistent inventory is isolated to the test save");
        Require(Time.timeScale==0&&guide.Current==definition.arrival,"Arrival pauses with the existing spotlight guide");
        ScreenCapture.CaptureScreenshot(Output+"/arrival.png");for(int i=0;i<6;i++)yield return null;
        guide.Skip();for(int i=0;i<8;i++)yield return null;
        Require(intro.Stage==1&&WorldSession.Current.introductionStage==1,"Skipped lesson and stage persist");
        var shell=intro.Geometry.GetComponentInChildren<StructureInstance>();var path=new NavMeshPath();
        for(int i=0;i<350;i++)
        {if(NavMesh.CalculatePath(intro.Layout.Spawn,intro.Layout.Exit,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)break;yield return null;}
        Require(NavMesh.CalculatePath(intro.Layout.Spawn,intro.Layout.Exit,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Navigation traverses all cave galleries to the exterior");
        Require(path.corners.All(p=>p.y<13),"Cave navigation stays inside the galleries, below the roof");
        ScreenCapture.CaptureScreenshot(Output+"/cave.png");for(int i=0;i<6;i++)yield return null;
        string worldId=WorldSession.Current.id;
        // Run the complete route through production triggers; teleport only travel, never tutorial state.
        while(!intro.Complete)
        {
            int stage=intro.Stage;motor.ResetPosition(intro.Destination+Vector3.up*.25f);
            if(stage==3)
            {
                for(int i=0;i<10;i++)yield return null;
                var npc=Object.FindAnyObjectByType<WorldIntroductionNarrator>();
                Require(npc.Interact(inventory),"F interaction opens Liria's dialogue");
                guide.Skip();for(int i=0;i<3;i++)yield return null;
                Require(intro.Stage==3,"Skipping dialogue does not accept Liria's request");
                Require(npc.Interact(inventory),"Liria can be approached again after declining");
                ScreenCapture.CaptureScreenshot(Output+"/liria.png");for(int i=0;i<6;i++)yield return null;
                while(guide.IsOpen)guide.Next();
            }
            int safety=0;
            while(intro.Stage==stage)
            {
                if(guide.IsOpen)
                {
                    Require(guide.Current.pages.All(p=>!p.body.Contains("partida temporal")),"World lesson does not describe a temporary save: "+stage);
                    guide.Skip();
                }
                if(stage==7&&equipment.ActiveDefinition!=definition.encounters[3].requiredWeapon)equipment.TrySwap();
                foreach(var enemy in intro.GetComponentsInChildren<Health>())if(!enemy.IsDead)
                {
                    var agent=enemy.GetComponent<NavMeshAgent>();Require(agent==null||agent.isOnNavMesh,"Tutorial enemy starts on navigation: "+stage);
                    enemy.ApplyDamage(new DamageInfo(10000,player.gameObject,enemy.transform.position,Vector3.forward));
                }
                if(++safety>1600)throw new Exception("Stage stalled: "+stage);
                yield return null;
            }
            Require(WorldSession.Current.introductionStage==intro.Stage,"Stage checkpoint persisted: "+stage);
            if(stage==3)
            {
                ScreenCapture.CaptureScreenshot(Output+"/grove.png");for(int i=0;i<6;i++)yield return null;
                Require(WorldSession.Continue()&&WorldSession.Current.introductionStage==4,"Continue retains an unfinished tutorial and accepted dialogue");
                SceneManager.LoadScene("VoxelRegion_7319");yield return null;
                while(Object.FindAnyObjectByType<WorldIntroduction>()==null)yield return null;
                intro=Object.FindAnyObjectByType<WorldIntroduction>();player=Object.FindAnyObjectByType<PlayerController>();
                motor=player.GetComponent<PlayerMotor>();guide=player.GetComponent<TutorialGuide>();inventory=player.GetComponent<PlayerInventory>();equipment=player.GetComponent<EquipmentLoadout>();
                for(int i=0;i<20;i++)yield return null;
                Require(intro.Stage==4&&!guide.IsOpen,"Reload resumes after Liria without replaying arrival or conversation");
            }
        }
        for(int i=0;i<40;i++)yield return null;
        Require(Object.FindObjectsByType<Mismo.Gameplay.Player.Quests.QuestGiver>(FindObjectsSortMode.None).Length>0,"Procedural village contains its real quest NPCs");
        Require(WorldSession.Current.id==worldId&&WorldSession.Current.introductionStage==11,"Tutorial ends in the same persistent world");
        WorldSession.Checkpoint(player.transform.position,player.transform.eulerAngles.y);
        Require(WorldSession.Continue()&&WorldSession.Current.introductionStage==11,"Continue retains completed introduction");
        var saved=new Vector3(WorldSession.Current.x,WorldSession.Current.y,WorldSession.Current.z);
        ScreenCapture.CaptureScreenshot(Output+"/village.png");for(int i=0;i<6;i++)yield return null;
        SceneManager.LoadScene("VoxelRegion_7319");yield return null;
        while(Object.FindAnyObjectByType<WorldIntroduction>()==null)yield return null;
        intro=Object.FindAnyObjectByType<WorldIntroduction>();player=Object.FindAnyObjectByType<PlayerController>();
        for(int i=0;i<25;i++)yield return null;
        Require(intro.Complete&&!player.GetComponent<TutorialGuide>().IsOpen,"Reload does not restart completed tutorials");
        Require(Vector3.Distance(player.transform.position,saved)<2,"Continue retains actual world position");
        Require(errors.Count==0,"No runtime errors");
    }
    static void Finish(Exception e)
    {
        SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        if(e!=null)results.Add("FAIL: "+e);results.AddRange(errors.Select(x=>"ERROR: "+x));
        bool capture=SessionState.GetBool(Pending+".capture",false);SessionState.SetBool(Pending+".capture",false);
        File.WriteAllLines(Output+(capture?"/capture.txt":"/checks.txt"),results);Debug.Log("WORLD_INTRODUCTION_CHECKS: "+(e==null&&errors.Count==0?"PASS":"FAIL"));
        EditorApplication.Exit(e==null&&errors.Count==0?0:1);
    }
    public static void BuildPlayer()
    {
        Directory.CreateDirectory(Output);
        try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","PASS");}
        catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.ToString());}
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            target=BuildTarget.StandaloneWindows64,locationPathName=Output+"/Windows/Mismo.exe",options=BuildOptions.Development});
        File.WriteAllText(Output+"/build.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; warnings="+report.summary.totalWarnings+"; bytes="+report.summary.totalSize);
        EditorApplication.Exit(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded&&report.summary.totalErrors==0?0:1);
    }
}
