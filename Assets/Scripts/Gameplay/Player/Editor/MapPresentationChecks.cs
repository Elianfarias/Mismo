using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Batch-only presentation/integration checks run in the isolated validation project.
    public static class MapPresentationChecks
    {
        const string Pending="Mismo.MapPresentationChecks";
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static IEnumerator routine;static int frame=-1;static double deadline;
        public static void RunBatch()
        {
            MapRefactorChecks.Run();
            if(File.ReadAllText(".validation/map-checks.txt").Contains("FAIL")){EditorApplication.Exit(1);return;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,"Assets/Scenes/MapPresentationValidation.unity");
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]static void Resume()
        {
            if(!SessionState.GetBool(Pending,false))return;deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Step;
        }
        static void Step()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish("FAIL Timeout");return;}
            if(!Application.isPlaying||Time.frameCount<5||frame==Time.frameCount)return;frame=Time.frameCount;
            try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish("PASS presentación y viaje");}
            catch(Exception e){Finish("FAIL "+e);}
        }
        static void Finish(string result){SessionState.SetBool(Pending,false);EditorApplication.update-=Step;File.WriteAllText(".validation/map-play-checks.txt",result);EditorApplication.Exit(result.StartsWith("PASS")?0:1);}
        static IEnumerator Run()
        {
            var settings=Object.Instantiate(Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.preserveAuthoredCenter=false;settings.content=null;
            var terrain=new ExplorationTerrain(settings);var village=terrain.Site(Vector2Int.zero);
            var player=new GameObject("Map validation player");player.transform.position=village.position+new Vector3(20,5,-55);player.AddComponent<PlayerMotor>();
            var camera=new GameObject("Background camera").AddComponent<UnityEngine.Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.075f);camera.cullingMask=0;
            var root=new GameObject("Test streamed world");var geometry=new GameObject("RegionGeometry");geometry.transform.SetParent(root.transform);
            var ground=new GameObject("Terrain_0");ground.transform.SetParent(geometry.transform);ground.AddComponent<MeshRenderer>().sharedMaterial=new Material(Shader.Find("Mismo/World Map"));
            var world=root.AddComponent<ExplorationChunks>();world.Initialize(settings,player.transform);world.enabled=false;
            var map=player.GetComponent<WorldMapPanel>();map.Open();map.SetZoom(180);
            for(int i=0;i<300&&!map.Ready;i++)yield return null;yield return null;
            if(!map.Ready)throw new Exception("Relieve no disponible en Play Mode");
            map.SavePin(new MapPin{id="preview",name="Mi tesoro",typeId="chest",x=village.position.x+70,z=village.position.z-25});
            typeof(WorldMapPanel).GetField("selectedVillage",Private).SetValue(map,(WorldSite?)village);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".validation/map-open.png"));for(int i=0;i<12;i++)yield return null;
            typeof(WorldMapPanel).GetField("selectedVillage",Private).SetValue(map,null);
            typeof(WorldMapPanel).GetField("draft",Private).SetValue(map,new MapPin{id="preview",name="Mi tesoro",typeId="chest",x=village.position.x+70,z=village.position.z-25});
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".validation/map-marker-editor.png"));for(int i=0;i<12;i++)yield return null;
            typeof(WorldMapPanel).GetField("draft",Private).SetValue(map,null);
            // A blocked arrival must not move the player.
            var arrival=village.position+new Vector3(0,0,-55);arrival.y=terrain.Height(arrival.x,arrival.z)+.3f;
            var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.transform.position=arrival+Vector3.up;obstruction.transform.localScale=new Vector3(3,3,3);Physics.SyncTransforms();
            var previous=player.transform.position;if(map.TravelTo(village)||player.transform.position!=previous)throw new Exception("Viaje atraviesa una llegada obstruida");
            Object.Destroy(obstruction);yield return null;Physics.SyncTransforms();
            if(!map.TravelTo(village)||Vector3.Distance(player.transform.position,arrival)>.1f||world.LoadedCount<9)throw new Exception("Viaje no genera destino y reubica al jugador");
            if(map.IsOpen)throw new Exception("Mapa sigue abierto después del viaje");
            map.Close();for(int i=0;i<150&&!map.Ready;i++)yield return null;for(int i=0;i<12;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".validation/map-mini.png"));for(int i=0;i<12;i++)yield return null;
        }
    }
}
