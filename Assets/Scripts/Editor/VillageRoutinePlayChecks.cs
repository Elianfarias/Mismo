using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Quests;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class VillageRoutinePlayChecks
{
    static GameObject root;
    static VillageNpcRoutine[] residents;
    static Vector3[] start;
    static float[] distance;
    static bool[] animated;
    static NavMeshData data;
    static NavMeshDataInstance navigation;
    static double began;
    static float gameBegan;
    static int stage;
    static VillageRoutinePlayChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    static void Changed(PlayModeStateChange change)
    {
        if(change==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("VillageRoutinePlayChecks",false))Setup();
        if(change==PlayModeStateChange.ExitingPlayMode){navigation.Remove();if(data!=null)Object.Destroy(data);}
    }
    static void Setup()
    {
        try
        {
            Application.runInBackground=true;
            root=new GameObject("NPC isolated runtime checks");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(0,-301,0);floor.transform.localScale=new Vector3(25,1,25);
            var body=floor.GetComponent<BoxCollider>();
            data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),new System.Collections.Generic.List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(floor.transform.position,Quaternion.identity,Vector3.one),size=floor.transform.localScale,area=0}},new Bounds(new Vector3(0,-300,0),new Vector3(30,10,30)),Vector3.zero,Quaternion.identity);
            var settings=AssetDatabase.LoadAssetAtPath<VillageNpcSettings>(VillageNpcIntegration.SettingsPath);
            residents=new VillageNpcRoutine[settings.residents.Length];start=new Vector3[residents.Length];distance=new float[residents.Length];animated=new bool[residents.Length];
            for(int i=0;i<residents.Length;i++)
            {
                var go=Object.Instantiate(settings.residents[i].prefab,new Vector3(-6+i*3,-300.5f,-5),Quaternion.identity,root.transform);go.transform.localScale=Vector3.one*settings.residentScale;
                residents[i]=go.GetComponent<VillageNpcRoutine>();start[i]=go.transform.position;
                residents[i].Initialize(new[]{new Vector3(-6+i*3,-300.5f,5),start[i]},0);
                residents[i].pauseSeconds=new Vector2(.4f,.6f);
            }
            began=EditorApplication.timeSinceStartup;gameBegan=Time.time;stage=0;
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static void Tick()
    {
        const string request="Temp/VillageRoutinePlayChecks.request";
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.Exists(request))
        {
            File.Delete(request);SessionState.SetBool("VillageRoutinePlayChecks",true);EditorApplication.isPlaying=true;return;
        }
        if(!EditorApplication.isPlaying||root==null||!SessionState.GetBool("VillageRoutinePlayChecks",false))return;
        try
        {
            double elapsed=Time.time-gameBegan;
            if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Runtime check timed out; game time="+elapsed+" scale="+Time.timeScale);
            if(stage==0&&elapsed>3){navigation=NavMesh.AddNavMeshData(data);stage=1;}
            for(int i=0;i<residents.Length;i++)
            {
                distance[i]=Mathf.Max(distance[i],Vector3.Distance(start[i],residents[i].transform.position));
                animated[i]|=residents[i].animator.GetFloat("Speed")>.1f;
            }
            if(stage==1&&elapsed>16){navigation.Remove();stage=2;}
            if(stage==2&&elapsed>18){navigation=NavMesh.AddNavMeshData(data);stage=3;}
            if(elapsed>25)
            {
                for(int i=0;i<residents.Length;i++)
                {
                    if(distance[i]<2||!animated[i]||!residents[i].GetComponent<NavMeshAgent>().isOnNavMesh)
                        throw new Exception("NPC did not walk/animate/recover: "+residents[i].name+" distance="+distance[i]+" animated="+animated[i]+" enabled="+residents[i].isActiveAndEnabled+" agent="+residents[i].GetComponent<NavMeshAgent>().enabled+" bound="+residents[i].GetComponent<NavMeshAgent>().isOnNavMesh+" sample="+NavMesh.SamplePosition(start[i],out var sampled,3,NavMesh.AllAreas)+" nav="+data.sourceBounds);
                }
                Finish("PASS: 5 residents waited for delayed NavMesh, walked >2m with Speed animation, and rebound after navigation removal/rebuild.\n"+string.Join("\n",residents.Select((n,i)=>n.name+" distance="+distance[i])));
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
    static void Finish(string result)
    {
        File.WriteAllText(VillageHumanoidUpgrade.Output+"/play-mode.txt",result);
        SessionState.SetBool("VillageRoutinePlayChecks",false);
        if(root!=null)Object.Destroy(root);EditorApplication.isPlaying=false;
    }
}

