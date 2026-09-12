using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment.Inventory;
namespace Mismo.Gameplay.Player.Presentation
{
    // Explicit command-line smoke test; world and inventory use disposable storage.
    public sealed class WindowsBuildSmoke : MonoBehaviour
    {
        string output;float deadline;bool finished;readonly System.Collections.Generic.List<string> errors=new System.Collections.Generic.List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-mismo-windows-smoke");if(i<0||i+1>=args.Length)return;
            string path=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(path);WorldSession.VerificationDirectory=path;
            PlayerInventory.BuildCheckRepository=new ProtectedProfileRepository(Path.Combine(path,"inventory.mismo"));
            var go=new GameObject("Windows smoke verification");DontDestroyOnLoad(go);var check=go.AddComponent<WindowsBuildSmoke>();check.output=path;check.deadline=Time.realtimeSinceStartup+90;Application.runInBackground=true;Application.logMessageReceived+=check.Log;
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Update(){if(!finished&&Time.realtimeSinceStartup>deadline)Finish(false,"Timeout");}
        IEnumerator Start()
        {
            yield return null;
            if(SceneManager.GetActiveScene().name!="MainMenu"){Finish(false,"Missing menu");yield break;}
            yield return new WaitForEndOfFrame();CaptureFrame("Menu.png");
            var menu=FindObjectsByType<MonoBehaviour>().FirstOrDefault(x=>x.GetType().FullName=="Mismo.Menu.MainMenuView");
            if(menu==null){Finish(false,"Missing menu controller");yield break;}menu.SendMessage("Begin");
            while(SceneManager.GetActiveScene().name!="VoxelRegion_7319")yield return null;
            for(int i=0;i<120;i++)yield return null;
            var town=FindObjectsByType<Transform>().FirstOrDefault(t=>t.name.StartsWith("Village ")&&t.GetComponentInParent<ExplorationChunks>()!=null);
            if(town==null||town.localScale.x<1.99f){Finish(false,"Missing enlarged town");yield break;}
            var player=FindAnyObjectByType<PlayerController>();
            if(player==null||WorldSession.Current==null||!player.GetComponent<PlayerInventory>().IsReady){Finish(false,"World or inventory did not initialize");yield break;}
            if(!File.Exists(Path.Combine(output,"active-world.mismo"))){Finish(false,"World not saved to isolated storage");yield break;}
            yield return new WaitForEndOfFrame();CaptureFrame("World.png");yield return null;yield return null;
            Finish(errors.Count==0,"Menu, new world, doubled village, inventory, isolated save and rendered frames.\n"+string.Join("\n",errors));
        }
        void CaptureFrame(string file)
        {
            var camera=UnityEngine.Camera.main;if(camera==null)return;
            var canvases=FindObjectsByType<Canvas>().Where(c=>c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,file),image.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;foreach(var canvas in canvases)canvas.renderMode=RenderMode.ScreenSpaceOverlay;rt.Release();Destroy(rt);Destroy(image);
        }
        void Finish(bool success,string details){if(finished)return;finished=true;Application.logMessageReceived-=Log;File.WriteAllText(Path.Combine(output,"Checks.txt"),(success?"PASS ":"FAIL ")+details);Debug.Log("WINDOWS_SMOKE_"+(success?"PASS":"FAIL"));Application.Quit(success?0:1);}
    }
}
