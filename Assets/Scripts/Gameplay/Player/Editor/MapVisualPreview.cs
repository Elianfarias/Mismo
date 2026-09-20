using System.Collections;
using System.IO;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Visual capture only: no assertions, gameplay checks, travel, or save writes.
    // Run on a disposable copy of the project, never on the user's open scene.
    public static class MapVisualPreview
    {
        const string Pending="Mismo.MapVisualPreview";
        static IEnumerator capture;static int frame=-1;
        public static void CaptureBatch()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,"Assets/Scenes/MapVisualPreview.unity");
            EditorApplication.ExecuteMenuItem("Window/General/Game");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]static void Resume(){if(SessionState.GetBool(Pending,false))EditorApplication.update+=Step;}
        static void Step()
        {
            if(!Application.isPlaying||Time.frameCount<5||frame==Time.frameCount)return;frame=Time.frameCount;
            try
            {
                if(capture==null)capture=Capture();
                if(!capture.MoveNext()){SessionState.SetBool(Pending,false);EditorApplication.update-=Step;EditorApplication.Exit(0);}
            }
            catch(System.Exception e){Debug.LogException(e);SessionState.SetBool(Pending,false);EditorApplication.Exit(1);}
        }
        static void SaveTexture(RenderTexture target,string path) { var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous; }
        static IEnumerator Capture()
        {
            Directory.CreateDirectory(".validation");
            var settings=Object.Instantiate(Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));
            settings.preserveAuthoredCenter=false;settings.content=null;
            var terrain=new ExplorationTerrain(settings);var town=terrain.Site(Vector2Int.zero);
            var parent=new GameObject("Visual preview only");parent.SetActive(false);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab");
            var player=Object.Instantiate(prefab,parent.transform);
            foreach(var behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
            foreach(var collider in player.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var animator in player.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            player.transform.position=town.position+new Vector3(20,0,-55);player.transform.position=new Vector3(player.transform.position.x,terrain.Height(player.transform.position.x,player.transform.position.z)+.3f,player.transform.position.z);
            parent.SetActive(true);
            var camera=new GameObject("Preview background").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.055f);camera.cullingMask=0;camera.transform.rotation=Quaternion.Euler(25,35,0);
            var map=player.AddComponent<WorldMapPanel>();map.Initialize(settings);
            for(int i=0;i<12;i++)yield return null;
            map.Open();map.SetZoom(110);map.Orbit(new Vector2(0,35-map.Pitch));
            for(int i=0;i<600&&!map.Ready;i++)yield return null;
            for(int i=0;i<12;i++)yield return null;
            SaveTexture(map.Preview,".validation/map-terrain-open.png");ScreenCapture.CaptureScreenshot(Path.GetFullPath(".validation/map-visual-open.png"));
            for(int i=0;i<15;i++)yield return null;
            map.Close();
            for(int i=0;i<120;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".validation/map-visual-mini.png"));
            for(int i=0;i<15;i++)yield return null;
        }
    }
}

