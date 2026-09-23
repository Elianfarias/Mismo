using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Only runs in an isolated validation project, never in the user's scene or save.</summary>
public static class AdventureFeedbackPlayChecks
{
    const string Key="Mismo.AdventurePlayChecks";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static IEnumerator routine;
    static int frame=-1;
    static double deadline;
    public static void RunBatch()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Use an isolated validation project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]static void Register(){deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update-=Step;EditorApplication.update+=Step;}
    static void Step()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("FAIL timeout",1);return;}
        if(!Application.isPlaying||Time.frameCount==frame)return;frame=Time.frameCount;
        try { if(routine==null)routine=Run();if(!routine.MoveNext())Finish("PASS: Feel animates EXP and level cards, pause hides/suspends them, resume restores playback, and cards expire.",0); }
        catch(Exception e){Finish("FAIL\n"+e,1);}
    }
    static void Finish(string result,int code)
    {
        SessionState.SetBool(Key,false);Directory.CreateDirectory("output/adventure-feedback");
        File.WriteAllText("output/adventure-feedback/play-checks.txt",result);EditorApplication.Exit(code);
    }
    static void CheckResourceReach()
    {
        var resource=GameObject.CreatePrimitive(PrimitiveType.Cube);
        resource.transform.position=new Vector3(1000,1,0);resource.transform.localScale=new Vector3(4,2,4);
        var definition=ScriptableObject.CreateInstance<Mismo.Gameplay.Player.World.ResourceNodeDefinition>();definition.interactionRange=2.1f;
        var node=resource.AddComponent<Mismo.Gameplay.Player.World.GatheringNode>();node.definition=definition;node.enabled=false;
        var player=new GameObject("Reach test");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(1003,1,0);wall.transform.localScale=new Vector3(.2f,3,3);wall.SetActive(false);
        try
        {
            for(int side=0;side<4;side++)
            {
                player.transform.position=new Vector3(1000,0,0)+Quaternion.Euler(0,90*side,0)*Vector3.right*4;
                Physics.SyncTransforms();
                if(!node.InRange(player.transform))throw new Exception("Large resource cannot be reached from side "+side);
            }
            player.transform.position=new Vector3(1004,0,0);wall.SetActive(true);Physics.SyncTransforms();
            if(node.InRange(player.transform))throw new Exception("Gathering reached through a wall");
            wall.SetActive(false);player.transform.position=new Vector3(1005,0,0);Physics.SyncTransforms();
            if(node.InRange(player.transform))throw new Exception("Gathering exceeded surface reach");
            File.AppendAllText("output/adventure-feedback/checks.txt","PASS: large resource reachable from four sides, walls block gathering, surface range enforced.\n");
        }
        finally {UnityEngine.Object.Destroy(resource);UnityEngine.Object.Destroy(player);UnityEngine.Object.Destroy(wall);UnityEngine.Object.Destroy(definition);}
    }
    static IEnumerator Run()
    {
        Application.runInBackground=true;
        var owner=new GameObject("Feedback validation");
        var inventory=owner.AddComponent<PlayerInventory>();inventory.enabled=false;
        var loadout=owner.AddComponent<EquipmentLoadout>();loadout.enabled=false;
        var runner=owner.AddComponent<AbilityRunner>();runner.enabled=false;
        typeof(EquipmentLoadout).GetField("runner",Private).SetValue(loadout,runner);
        typeof(PlayerInventory).GetField("loadout",Private).SetValue(inventory,loadout);
        typeof(PlayerInventory).GetField("profile",Private).SetValue(inventory,new InventoryProfile());
        owner.AddComponent<PlayerInteraction>();
        var view=owner.AddComponent<AdventureFeedback>();
        var canvas=owner.GetComponentInChildren<Canvas>();
        if(canvas==null)throw new Exception("HUD did not initialize");
        for(int i=0;i<12;i++)yield return null;
        CheckResourceReach();
        var before=new ProgressionData{level=1,experience=50};var after=new ProgressionData{level=2,experience=15};
        typeof(AdventureFeedback).GetMethod("Committed",Private).Invoke(view,new object[]{before,after,""});
        float until=Time.unscaledTime+.4f;while(Time.unscaledTime<until)yield return null;
        var xp=canvas.transform.Find("Experience").GetComponent<CanvasGroup>();
        var level=canvas.transform.Find("Level and mastery").GetComponent<CanvasGroup>();
        if(xp.alpha<.5f||level.alpha<.5f)throw new Exception("Feel did not fade the notifications in: XP="+xp.alpha+", level="+level.alpha+", canvas="+canvas.enabled+", frame="+Time.frameCount);
        var rewards=canvas.transform.Find("Rewards");
        if(rewards.GetComponent<CanvasGroup>().alpha>.01f)throw new Exception("Silent victory displayed a reward notice");
        GameplayPause.Pause();yield return null;yield return null;
        if(canvas.enabled)throw new Exception("HUD remained visible while paused");
        float alpha=level.alpha;
        until=Time.unscaledTime+3.6f;while(Time.unscaledTime<until)yield return null;
        if(Mathf.Abs(level.alpha-alpha)>.05f)throw new Exception("Feedback continued during pause");
        GameplayPause.Resume();yield return null;yield return null;yield return null;
        if(!canvas.enabled)throw new Exception("HUD did not return after pause");
        until=Time.unscaledTime+4;while(Time.unscaledTime<until)yield return null;
        if(xp.alpha>.01f||level.alpha>.01f)throw new Exception("Notifications did not finish");
        UnityEngine.Object.Destroy(owner);
    }
}
