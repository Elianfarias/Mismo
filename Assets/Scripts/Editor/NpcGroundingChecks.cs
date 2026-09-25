using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class NpcGroundingChecks
{
    const string Pending="Mismo.NpcGroundingChecks",Output="output/npc-grounding";
    static IEnumerator routine;
    static double deadline;
    static int frame=-1;
    static readonly List<string> lines=new List<string>(),errors=new List<string>();
    static int supportedSamples;
    static float largestGap,smallestGap=float.MaxValue,maximumSpeed;
    public static void Run()
    {
        Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void Resume()
    {if(!SessionState.GetBool(Pending,false))return;deadline=EditorApplication.timeSinceStartup+240;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}
    static void Log(string message,string stack,LogType type)
    {if((type==LogType.Error||type==LogType.Exception)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))errors.Add(message+"\n"+stack);}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline){Finish(new Exception("Timeout"));return;}
        if(!Application.isPlaying||Time.frameCount<6||frame==Time.frameCount)return;
        frame=Time.frameCount;
        try{if(routine==null)routine=Inspect();if(!routine.MoveNext())Finish(null);}catch(Exception e){Finish(e);}
    }
    static IEnumerator Inspect()
    {
        string dir=Path.GetFullPath(Output+"/Profiles/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        typeof(WorldSession).GetField("VerificationDirectory",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,dir);
        if(!WorldSession.NewGame())throw new Exception(WorldSession.LastError);
        var settings=WorldSession.Settings(AssetDatabase.LoadAssetAtPath<ExplorationWorldSettings>("Assets/Data/World/ExplorationWorldSettings.asset"));
        var layout=new ExplorationTerrain(settings).Introduction;
        WorldSession.SaveIntroduction(4,3,position:layout.Grove+layout.Facing*new Vector3(0,.3f,-2),yaw:layout.Facing.eulerAngles.y);
        Object.Destroy(settings);SceneManager.LoadScene("VoxelRegion_7319");yield return null;
        while(Object.FindAnyObjectByType<ExplorationChunks>()==null)yield return null;
        while(!Object.FindAnyObjectByType<ExplorationChunks>().NavigationReady)yield return null;
        for(int i=0;i<30;i++)yield return null;
        var liria=Object.FindAnyObjectByType<WorldIntroductionNarrator>();
        for(int i=0;i<120;i++){if(i%10==0)Measure(liria.transform);yield return null;}
        View(liria.transform);ScreenCapture.CaptureScreenshot(Output+"/liria.png");for(int i=0;i<6;i++)yield return null;
        var player=Object.FindAnyObjectByType<PlayerController>();player.GetComponent<PlayerMotor>().ResetPosition(layout.Village+Vector3.up*.3f);
        QuestGiver[] villagers;
        while((villagers=Object.FindObjectsByType<QuestGiver>(FindObjectsSortMode.None)).Length<5)yield return null;
        for(int i=0;i<60;i++)yield return null;
        foreach(var npc in villagers)
        {
            var walking=npc.GetComponent<VillageNpcRoutine>();
            typeof(VillageNpcRoutine).GetField("nextDecision",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(walking,0f);
            typeof(VillageNpcRoutine).GetField("nextStop",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(walking,2);
        }
        float until=Time.unscaledTime+8;
        for(int i=0;Time.unscaledTime<until;i++)
        {
            if(i%10==0)foreach(var npc in villagers)Measure(npc.transform);
            yield return null;
        }
        var iria=villagers.FirstOrDefault(n=>n.npc.id=="iria")??villagers[0];
        player.GetComponent<PlayerMotor>().ResetPosition(iria.transform.position+new Vector3(-2.5f,0,-1.5f));
        for(int i=0;i<20;i++)yield return null;
        View(iria.transform);ScreenCapture.CaptureScreenshot(Output+"/village.png");for(int i=0;i<6;i++)yield return null;
        lines.Add("SUMMARY: supported="+supportedSamples+" minGap="+smallestGap+" maxGap="+largestGap+" maxWalkingSpeed="+maximumSpeed);
        if(supportedSamples<30||maximumSpeed<.3f||smallestGap<-.06f||largestGap>.08f)throw new Exception("NPC support verification failed");
    }
    static void Measure(Transform npc)
    {
        float bottom=float.MaxValue;
        foreach(var skin in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            // Reference skinning in world coordinates avoids BakeMesh scale compensation on scaled residents.
            var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var bindPoses=mesh.bindposes;var bones=skin.bones;
            var matrices=bones.Select((b,i)=>b.localToWorldMatrix*bindPoses[i]).ToArray();
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var v=vertices[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0;
                if(w.weight1>0)p+=matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1;
                if(w.weight2>0)p+=matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2;
                if(w.weight3>0)p+=matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                bottom=Mathf.Min(bottom,p.y);
            }
        }
        var hits=Physics.RaycastAll(npc.position+Vector3.up,Vector3.down,4,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>h.normal.y>.5f&&!h.transform.IsChildOf(npc)&&h.collider.GetComponentInParent<Health>()==null&&h.collider.GetComponentInParent<QuestInteractable>()==null).OrderBy(h=>h.distance).ToArray();
        if(hits.Length==0){lines.Add(npc.name+": no ground");return;}
        var agent=npc.GetComponent<NavMeshAgent>();var animator=npc.GetComponentInChildren<Animator>();
        supportedSamples++;smallestGap=Mathf.Min(smallestGap,bottom-hits[0].point.y);largestGap=Mathf.Max(largestGap,bottom-hits[0].point.y);
        if(agent!=null)maximumSpeed=Mathf.Max(maximumSpeed,agent.velocity.magnitude);
        var lf=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var rf=animator.GetBoneTransform(HumanBodyBones.RightFoot);
        lines.Add(npc.name+" root="+npc.position.y.ToString("F4")+" sole="+bottom.ToString("F4")+" ground="+hits[0].point.y.ToString("F4")+" gap="+(bottom-hits[0].point.y).ToString("F4")+
            " bones="+lf.position.y.ToString("F4")+","+rf.position.y.ToString("F4")+" speed="+(agent!=null?agent.velocity.magnitude:0).ToString("F2"));
    }
    static void View(Transform npc)
    {
        var camera=UnityEngine.Camera.main;var follow=camera.GetComponent<Mismo.Gameplay.Player.Camera.ThirdPersonCamera>();if(follow!=null)follow.enabled=false;
        camera.transform.position=npc.position+new Vector3(3,1.2f,-5);camera.transform.LookAt(npc.position+Vector3.up);
    }
    static void Finish(Exception exception)
    {
        SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        if(exception!=null)lines.Add("FAIL: "+exception);lines.AddRange(errors);
        File.WriteAllLines(Output+"/measurements.txt",lines);Debug.Log("NPC_GROUNDING_CAPTURE: "+(exception==null&&errors.Count==0?"PASS":"FAIL"));
        EditorApplication.Exit(exception==null&&errors.Count==0?0:1);
    }
}
