using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class VillageMapChecks
    {
        const string Pending="Mismo.VillageMapChecks";
        static IEnumerator routine;static int frame=-1;static double deadline;static readonly List<string> checks=new List<string>();
        public static void RunBatch(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(scene,"Assets/Scenes/VillageMapValidation.unity");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Resume(){if(!SessionState.GetBool(Pending,false))return;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Step;}
        static void Step()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
            if(!Application.isPlaying||Time.frameCount<10||frame==Time.frameCount)return;frame=Time.frameCount;
            try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,"Completed");}catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool ok,string message){SessionState.SetBool(Pending,false);EditorApplication.update-=Step;File.WriteAllLines("Docs/Validation/VillageMapChecks.txt",checks.Concat(new[]{(ok?"PASS ":"FAIL ")+message}));Debug.Log("VILLAGE_MAP_"+(ok?"PASS ":"FAIL ")+message);EditorApplication.Exit(ok?0:1);}
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);Debug.Log("VILLAGE_MAP_CHECK "+label);}
        static IEnumerator Run()
        {
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())root.SetActive(false);
            var settings=Object.Instantiate(Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.preserveAuthoredCenter=false;
            var terrain=new ExplorationTerrain(settings);var site=terrain.Site(Vector2Int.zero);var center=ExplorationChunks.Coordinate(site.position);
            var rootTerrain=new GameObject("Test world");var material=new Material(Shader.Find("Mismo/Voxel Landscape"));
            for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++)
            {
                var id=center+new Vector2Int(x,z);var go=new GameObject("Terrain "+id);go.transform.SetParent(rootTerrain.transform);var mesh=ExplorationChunks.BuildTerrain(terrain,id);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            var content=new ExplorationContent(settings,terrain);content.Decorate(center,rootTerrain.transform,material);
            Check(rootTerrain.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Village "))==1,"Village site instantiates exactly one complete village");
            Physics.SyncTransforms();
            var arrival=site.position+settings.content.VillageArrivalOffset;arrival.y=terrain.Height(arrival.x,arrival.z)+.3f;
            var view=Quaternion.Euler(25,settings.content.villageSpawnYaw,0);
            Check(!Physics.SphereCast(arrival+Vector3.up*1.3f,.25f,view*Vector3.back,out _,9,~0,QueryTriggerInteraction.Ignore),"Entrance spawn has full 9 metre camera clearance");
            Check(!Physics.CheckCapsule(arrival+Vector3.up*.35f,arrival+Vector3.up*1.4f,.25f,~0,QueryTriggerInteraction.Ignore),"Entrance spawn capsule is clear of buildings");
            var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(rootTerrain.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(site.position,new Vector3(180,100,180)),Vector3.zero,Quaternion.identity);var instance=NavMesh.AddNavMeshData(nav);
            Check(NavMesh.SamplePosition(site.position,out var courtyard,2,NavMesh.AllAreas),"Player spawn lies in navigable village courtyard");
            int reachable=0;for(int i=0;i<8;i++){var p=site.position+new Vector3(Mathf.Cos(i*Mathf.PI/4)*(terrain.VillageHalfExtent+12),0,Mathf.Sin(i*Mathf.PI/4)*(terrain.VillageHalfExtent+12));p.y=terrain.Height(p.x,p.z);if(!NavMesh.SamplePosition(p,out var outside,2,NavMesh.AllAreas))continue;var path=new NavMeshPath();if(NavMesh.CalculatePath(courtyard.position,outside.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)reachable++;}
            if(reachable<2)
            {
                float best=float.MaxValue;Vector3 anchor=Vector3.zero;
                for(int z=-12;z<=12;z+=2)for(int x=-12;x<=12;x+=2)
                {
                    if(!NavMesh.SamplePosition(site.position+new Vector3(x,0,z),out var candidate,1,NavMesh.AllAreas))continue;
                    for(int i=0;i<8;i++)
                    {
                        var p=site.position+new Vector3(Mathf.Cos(i*Mathf.PI/4)*(terrain.VillageHalfExtent+12),0,Mathf.Sin(i*Mathf.PI/4)*(terrain.VillageHalfExtent+12));p.y=terrain.Height(p.x,p.z);
                        if(!NavMesh.SamplePosition(p,out var outside,2,NavMesh.AllAreas))continue;
                        var path=new NavMeshPath();if(NavMesh.CalculatePath(candidate.position,outside.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)
                        {float distance=(candidate.position-site.position).sqrMagnitude;if(distance<best){best=distance;anchor=candidate.position-site.position;}break;}
                    }
                }
                Debug.Log("VILLAGE_SPAWN_DIAGNOSTIC courtyard="+(courtyard.position-site.position)+" accessible="+anchor+" distance="+best);
            }
            Check(reachable>=2,"Village has traversable paths between courtyard and exterior: "+reachable);
            Check(!Physics.Raycast(site.position+Vector3.up*2,Vector3.down,out var landing,4)||landing.point.y-site.position.y<1,"New-game spawn is not inside a building roof");
            var player=new GameObject("Map test player");player.transform.position=site.position+Vector3.up*.3f;var map=player.AddComponent<WorldMapPanel>();map.Initialize(settings);
            var inputSettings=Object.Instantiate(InputSystem.settings);InputSystem.settings=inputSettings;
            inputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.EnableDevice(keyboard);keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M));yield return null;yield return null;
            Check(map.IsOpen&&WorldMapPanel.BlocksGameplay,"M opens map and blocks player controls");InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            map.SetZoom(1);Check(map.Zoom==40,"Near zoom is bounded");map.SetZoom(99999);Check(map.Zoom==2400,"Far zoom is bounded");map.SetZoom(180);
            var previous=map.Center;map.Pan(new Vector2(45,-20));Check(map.Center==previous+new Vector2(45,-20),"Map pans independently of player");map.Orbit(new Vector2(50,100));Check(map.Pitch==89,"Orbit clamps pitch");map.Orbit(new Vector2(-50,-29));map.Recenter();Check(map.Center==new Vector2(player.transform.position.x,player.transform.position.z),"Recenter returns to player");
            for(int i=0;i<90&&!map.Ready;i++)yield return null;yield return null;
            Check(map.Ready,"Procedural map relief finishes without loading world chunks");
            RenderTexture.active=map.Preview;var image=new Texture2D(map.Preview.width,map.Preview.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();File.WriteAllBytes("Docs/Validation/WorldMapRelief.png",image.EncodeToPNG());RenderTexture.active=null;Object.Destroy(image);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M));yield return null;yield return null;Check(!map.IsOpen,"M closes map");InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            map.Open();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;yield return null;Check(!map.IsOpen,"Escape closes map");
            InputSystem.RemoveDevice(keyboard);instance.Remove();Object.Destroy(nav);
        }
    }
}
