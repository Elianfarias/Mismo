using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Editor;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

public static class DemoIntroductionChecks
{
    const string Pending="Mismo.DemoIntroductionChecks";
    const string Output="output/demo-introduction";
    static IEnumerator routine;
    static int frame=-1;
    static double deadline;
    static readonly List<string> results=new List<string>();
    static readonly List<string> errors=new List<string>();

    public static void RunBatch()
    {
        Directory.CreateDirectory(Output);
        DemoIntroductionBuilder.Build();
        try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","PASS");}
        catch(Exception exception){File.WriteAllText(Output+"/organization.txt",exception.ToString());}
        EditorSceneManager.OpenScene(DemoIntroductionBuilder.ScenePath);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        SessionState.SetBool(Pending,true);
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if(!SessionState.GetBool(Pending,false))return;
        deadline=EditorApplication.timeSinceStartup+180;
        Application.logMessageReceived+=Log;
        EditorApplication.update+=Tick;
    }
    static void Log(string message,string stack,LogType type)
    {
        if(type!=LogType.Exception&&type!=LogType.Error)return;
        if(stack.Contains("UnityEditor.Search.SearchDatabase"))
        {File.AppendAllText(Output+"/editor-search.txt",message+"\n"+stack+"\n");return;}
        errors.Add(message+"\n"+stack);
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline){Finish(new Exception("Play Mode timeout"));return;}
        if(!Application.isPlaying||Time.frameCount<6||frame==Time.frameCount)return;
        frame=Time.frameCount;
        try{if(routine==null)routine=Checks();if(!routine.MoveNext())Finish(null);}
        catch(Exception exception){Finish(exception);}
    }
    static void Require(bool condition,string label)
    {if(!condition)throw new Exception(label);results.Add("PASS: "+label);}
    static IEnumerator Checks()
    {
        var demo=Object.FindAnyObjectByType<DemoIntroduction>();
        Require(demo!=null,"Authored demo scene loads");
        while(demo.Introducing)yield return null;
        var guide=demo.guide;var player=demo.player;var hud=player.GetComponent<PlayerHUD>();
        var inventory=player.GetComponent<PlayerInventory>();var equipment=player.GetComponent<EquipmentLoadout>();
        Require(inventory.IsReady&&!inventory.HasSaveProblem,"Fresh in-memory inventory initializes");
        for(int i=0;i<5;i++)yield return null;
        Require(guide.IsOpen&&Time.timeScale==0,"Arrival opens paused guide");
        Require(hud.TryGetTutorialRect(TutorialAnchor.Health,out var rect)&&rect.width>0,"Health spotlight follows real HUD");
        ScreenCapture.CaptureScreenshot(Output+"/health.png");for(int i=0;i<5;i++)yield return null;
        int original=guide.PageIndex;guide.Next();Require(guide.PageIndex==original+1,"Next advances one page");
        guide.Previous();Require(guide.PageIndex==original,"Previous returns to prior page");
        var owner=new object();Require(!GameplayPause.TryPause(owner),"Other pause owner cannot acquire active guide pause");
        GameplayPause.Resume();Require(Time.timeScale==0&&guide.IsOpen,"Legacy resume cannot release guide pause");
        var first=guide.Current;guide.Skip();
        Require(!guide.IsOpen&&guide.WasDismissed(first)&&GameplayPause.BlocksInput,"Skip restores game and blocks same-frame input");
        yield return null;
        Require(!GameplayPause.BlocksInput&&Time.timeScale==1,"Game resumes on following frame");
        Require(!guide.TryBegin(first),"Dismissed sequence does not repeat automatically");
        var invalid=ScriptableObject.CreateInstance<TutorialSequence>();
        Require(!guide.TryBegin(invalid),"Invalid sequence cannot freeze the game");Object.Destroy(invalid);
        Time.timeScale=.65f;GameplayPause.Pause();
        Require(!guide.TryBegin(first,true),"Guide defers to an existing menu pause");GameplayPause.Resume();yield return null;
        Require(guide.TryBegin(first,true),"Guide can be replayed explicitly");
        guide.enabled=false;Require(!GameplayPause.IsPaused&&Mathf.Approximately(Time.timeScale,.65f),"Disabling guide releases only its pause and restores prior time scale");
        guide.enabled=true;Time.timeScale=1;yield return null;
        // Walk the authored flow, including bow equipment, real enemy deaths and NPC access.
        int last=-1;
        while(demo.StageIndex<demo.stages.Length)
        {
            var item=demo.stages[demo.StageIndex];
            if(last!=demo.StageIndex)
            {
                last=demo.StageIndex;
                var motor=player.GetComponent<CharacterController>();if(motor!=null)motor.enabled=false;
                player.transform.position=item.destination.position+Vector3.up*.15f;if(motor!=null)motor.enabled=true;
            }
            if(guide.IsOpen)
            {
                if(guide.Current.id=="demo.focus")
                {
                    guide.Next();
                    Require(hud.TryGetTutorialRect(TutorialAnchor.Q,out rect),"Q spotlight resolves after switching to bow");
                    var resolved=guide.ResolveText("{q}|{focusCost}|{basicGain}");
                    Require(!resolved.Contains("{")&&resolved.Contains(equipment.GetAbility(AbilitySlot.Q).DisplayName),"Copy uses equipped ability and current costs");
                    ScreenCapture.CaptureScreenshot(Output+"/focus.png");for(int i=0;i<5;i++)yield return null;
                }
                guide.Skip();
            }
            else if(!GameplayPause.BlocksInput)
            {
                if(item.objective==DemoObjective.EquipWeapon&&equipment.ActiveDefinition!=item.weapon)equipment.TrySwap();
                if(item.encounter!=null&&item.encounter.activeInHierarchy)
                {
                    foreach(var enemy in item.encounter.GetComponentsInChildren<Health>())
                    {
                        var agent=enemy.GetComponent<NavMeshAgent>();
                        Require(agent!=null&&agent.isOnNavMesh,"Encounter "+last+" spawns on navigation");
                        var path=new NavMeshPath();
                        Require(agent.CalculatePath(player.transform.position,path)&&path.status==NavMeshPathStatus.PathComplete,"Encounter "+last+" can reach player");
                        enemy.ApplyDamage(new DamageInfo(10000,player.gameObject,enemy.transform.position,Vector3.forward));
                    }
                }
            }
            yield return null;
        }
        Require(demo.StageIndex==8,"All eight stages reach village");
        var giver=Object.FindObjectsByType<QuestGiver>().FirstOrDefault();
        Require(giver!=null&&giver.quests.Length>0,"Village contains actual quest giver");
        float until=Time.unscaledTime+7;while(Time.unscaledTime<until)yield return null;
        var cc=player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;
        player.transform.position=giver.transform.position+Vector3.back*2;if(cc!=null)cc.enabled=true;
        Require(giver.CanTalk(inventory,null),"Village NPC dialogue is accessible");
        Require(inventory.TryAcceptQuest(giver.quests[0]),"First village mission can be accepted in temporary inventory");
        Require(errors.Count==0,"No runtime errors in tutorial flow");
    }
    static void Finish(Exception exception)
    {
        SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        if(exception!=null)results.Add("FAIL: "+exception);
        File.WriteAllLines(Output+"/checks.txt",results.Concat(errors));
        EditorApplication.Exit(exception==null?0:1);
    }
    public static void BuildPlayer()
    {
        Directory.CreateDirectory(Output);
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            CombatFeedbackChecks.RunBatch();
            File.WriteAllText(Output+"/combat-regression.txt","PASS: existing CombatFeedbackChecks, including pause and hit stop");
            var scripts=UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(
                new UnityEditor.Build.Player.ScriptCompilationSettings{target=BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone},
                "output/demo-introduction/PlayerScripts");
            if(scripts.assemblies==null||!scripts.assemblies.Any(p=>p.EndsWith("Mismo.Gameplay.Player.dll")))throw new Exception("Player scripts missing");
            File.WriteAllText(Output+"/player-compilation.txt","PASS: Windows scripts compiled without UNITY_EDITOR");
            Directory.CreateDirectory(Output+"/Content");
            var assets=AssetDatabase.FindAssets("t:TutorialSequence",new[]{DemoIntroductionBuilder.DataPath}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var manifest=BuildPipeline.BuildAssetBundles(Output+"/Content",new[]{new AssetBundleBuild{assetBundleName="tutorial",assetNames=assets}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
            if(manifest==null)throw new Exception("Tutorial content build failed");
            var bundle=AssetBundle.LoadFromFile(Output+"/Content/tutorial");
            try
            {
                if(bundle==null||bundle.LoadAllAssets<TutorialSequence>().Length!=assets.Length||bundle.LoadAllAssets<TutorialSequence>().Any(s=>!s.IsValid))throw new Exception("Tutorial content is incomplete");
                File.WriteAllText(Output+"/content-build.txt","PASS: "+assets.Length+" tutorial sequences loaded from built bundle");
            }
            finally{if(bundle!=null)bundle.Unload(true);}
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{DemoIntroductionBuilder.ScenePath},
                locationPathName="output/demo-introduction/Windows/MismoDemo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText(Output+"/build.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nBytes: "+report.summary.totalSize);
            EditorApplication.Exit(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded&&report.summary.totalErrors==0?0:1);
        }
        catch(Exception exception){File.WriteAllText(Output+"/build.txt","FAIL: "+exception);EditorApplication.Exit(1);}
    }
}
