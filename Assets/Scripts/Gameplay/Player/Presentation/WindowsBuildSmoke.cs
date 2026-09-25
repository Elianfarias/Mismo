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
            var go=new GameObject("Windows smoke verification");DontDestroyOnLoad(go);var check=go.AddComponent<WindowsBuildSmoke>();check.output=path;check.deadline=Time.realtimeSinceStartup+120;Application.runInBackground=true;Application.logMessageReceived+=check.Log;
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Update(){if(!finished&&Time.realtimeSinceStartup>deadline)Finish(false,"Timeout");}
        IEnumerator Start()
        {
            var routine=Run();
            while(!finished)
            {
                bool next;
                try{next=routine.MoveNext();}
                catch(Exception error){Finish(false,error.ToString());yield break;}
                if(!next)yield break;
                yield return routine.Current;
            }
        }
        IEnumerator Run()
        {
            yield return null;
            if(SceneManager.GetActiveScene().name!="MainMenu"){Finish(false,"Missing menu");yield break;}
            yield return new WaitForEndOfFrame();CaptureFrame("Menu.png");
            var menu=FindObjectsByType<MonoBehaviour>().FirstOrDefault(x=>x.GetType().FullName=="Mismo.Menu.MainMenuView");
            if(menu==null){Finish(false,"Missing menu controller");yield break;}menu.SendMessage("Begin");
            while(SceneManager.GetActiveScene().name!="VoxelRegion_7319")yield return null;
            for(int i=0;i<120;i++)yield return null;
            var player=FindAnyObjectByType<PlayerController>();
            if(player==null||WorldSession.Current==null||!player.GetComponent<PlayerInventory>().IsReady){Finish(false,"World or inventory did not initialize");yield break;}
            if(!File.Exists(Path.Combine(output,"active-world.mismo"))){Finish(false,"World not saved to isolated storage");yield break;}
            yield return new WaitForEndOfFrame();CaptureFrame("World.png");yield return null;yield return null;
            var world=FindAnyObjectByType<ExplorationChunks>();
            var intro=FindAnyObjectByType<WorldIntroduction>();
            if(world==null){Finish(false,"Missing generated world");yield break;}
            if(world.Settings.introductionVersion>0)
            {
                if(intro==null||intro.Geometry==null||intro.Geometry.childCount<2||Vector3.Distance(player.transform.position,intro.Layout.Spawn)>6)
                {Finish(false,"New game did not start in the crystal cave with its introduction assets");yield break;}
                var narrator=FindAnyObjectByType<WorldIntroductionNarrator>();
                if(narrator==null||narrator.GetComponent<Quests.NpcGrounding>()==null){Finish(false,"Liria or her grounding component is missing");yield break;}
                // The smoke test samples each streamed location; it does not play or mark the tutorial complete.
                player.GetComponent<TutorialGuide>()?.Skip();intro.enabled=false;player.enabled=false;
                player.GetComponent<Movement.PlayerMotor>().ResetPosition(intro.Layout.Grove+intro.Layout.Facing*new Vector3(0,.3f,-5));
                for(int i=0;i<120;i++)yield return null;
                yield return new WaitForEndOfFrame();CaptureFrame("Grove.png");
                // Keep the motor still while the distant destination streams in. A test teleport
                // bypasses the normal approach that loads collision ahead of a walking player.
                player.GetComponent<Movement.PlayerMotor>().ResetPosition(intro.Layout.Village+Vector3.up*.3f);
                for(int i=0;i<200;i++)yield return null;
                player.enabled=true;for(int i=0;i<20;i++)yield return null;
            }
            var towns=FindObjectsByType<Transform>().Where(t=>t.name.StartsWith("Village ")&&t.GetComponentInParent<ExplorationChunks>()!=null).ToArray();
            var town=towns.FirstOrDefault(t=>t.name=="Village 0, 0")??towns.FirstOrDefault(t=>t.Find("Quest residents")!=null);
            yield return new WaitForEndOfFrame();CaptureFrame("Village.png");
            if(town==null||town.localScale.x<1.99f){Finish(false,"Missing enlarged town at "+player.transform.position+"; loaded candidates: "+string.Join(", ",towns.Select(t=>t.name+" scale="+t.localScale)));yield break;}
            var residents=town.GetComponentsInChildren<Quests.VillageNpcRoutine>();
            if(residents.Length==0||residents.Any(n=>n.GetComponent<Quests.NpcGrounding>()==null)){Finish(false,"Village residents or their grounding components are missing");yield break;}
            int materials=0;
            foreach(var renderer in FindObjectsByType<Renderer>())
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                foreach(var material in renderer.sharedMaterials)
                {
                    if(material==null||material.shader==null||!material.shader.isSupported||material.shader.name=="Hidden/InternalErrorShader")
                    {Finish(false,"Missing or unsupported material on "+renderer.name);yield break;}
                    materials++;
                }
            }
            if(materials<20){Finish(false,"Insufficient rendered world geometry");yield break;}
            yield return new WaitForEndOfFrame();CaptureFrame("Village.png");yield return null;
            Finish(errors.Count==0,"Menu, new world, crystal cave introduction (when enabled), Liria, streamed village, "+residents.Length+" residents with grounding, inventory, isolated save, "+materials+" supported materials and rendered frames. Locations sampled by teleport; this is not a full tutorial playthrough.\n"+string.Join("\n",errors));
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
