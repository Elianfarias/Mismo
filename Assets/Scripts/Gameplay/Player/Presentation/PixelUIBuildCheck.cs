using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Core;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Opt-in rendered UI verification with an isolated save directory.</summary>
    public sealed class PixelUIBuildCheck : MonoBehaviour
    {
        string output;
        float deadline;
        readonly System.Collections.Generic.List<string> errors=new System.Collections.Generic.List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-mismo-pixel-ui-check");
            if(index<0||index+1>=args.Length)return;
            string path=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(path);
            WorldSession.VerificationDirectory=path;
            PlayerInventory.BuildCheckRepository=new ProtectedProfileRepository(Path.Combine(path,"inventory.mismo"));
            var go=new GameObject("Pixel UI verification");DontDestroyOnLoad(go);
            var check=go.AddComponent<PixelUIBuildCheck>();check.output=path;check.deadline=Time.realtimeSinceStartup+180;
            Application.runInBackground=true;Application.logMessageReceived+=check.Log;
        }
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Update(){if(Time.realtimeSinceStartup>deadline)Finish("Timeout");}
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.4f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));
            yield return new WaitForSecondsRealtime(.4f);
        }
        IEnumerator Start()
        {
            yield return null;
            foreach(var name in new[]{"Frame","Panel","Crosshair"})
                if(ProjectAssets.Load<Texture2D>("UI/PixelFrames/"+name)==null){Finish("Missing build dependency: "+name);yield break;}
            var theme=RecipeUITheme.Load();
            if(theme==null||theme.radialSurface==null||theme.radialOutline==null||theme.radialSelected==null||theme.radialSelected.Length!=7||theme.radialSelected.Any(t=>t==null)||theme.pieces.Any(p=>p.texture==null))
            {Finish("Missing recipe/radial build dependencies");yield break;}
            yield return Capture("01-menu");
            var menu=FindObjectsByType<MonoBehaviour>().FirstOrDefault(x=>x.GetType().FullName=="Mismo.Menu.MainMenuView");
            if(menu==null){Finish("Missing menu");yield break;}
            menu.SendMessage("Begin");
            while(SceneManager.GetActiveScene().name!="VoxelRegion_7319")yield return null;
            PlayerInventory inventory=null;
            while(inventory==null||!inventory.IsReady){inventory=FindAnyObjectByType<PlayerInventory>();yield return null;}
            yield return new WaitForSecondsRealtime(1);
            yield return Capture("02-hud");
            var panel=inventory.GetComponent<InventoryPanel>();
            if(panel==null||!panel.TryOpen()){Finish("Cannot open inventory");yield break;}
            yield return Capture("03-inventory");
            var pageType=typeof(InventoryPanel).GetNestedType("Page",BindingFlags.NonPublic);
            var open=typeof(InventoryPanel).GetMethod("OpenPage",BindingFlags.NonPublic|BindingFlags.Instance);
            foreach(var page in new[]{"Character","Skills","Menu","Recipes"})
            {
                if(!(bool)open.Invoke(panel,new[]{Enum.Parse(pageType,page)})){Finish("Cannot open "+page);yield break;}
                yield return Capture("04-"+page.ToLowerInvariant());
            }
            panel.Close();
            var station=new GameObject("Recipe UI verification station").AddComponent<CraftingStation>();
            station.transform.position=inventory.transform.position;station.recipes=ProjectAssets.LoadAll<CraftingRecipe>("Recipes");
            var gathering=inventory.GetComponent<GatheringPlayer>();
            if(gathering==null){Finish("Missing gathering player");yield break;}
            if(!gathering.TryOpenStation(station)){Finish("Cannot open station recipes");yield break;}
            yield return Capture("05-station-recipes");
            Finish(null);
        }
        void Finish(string failure)
        {
            if(failure!=null)errors.Add(failure);
            Application.logMessageReceived-=Log;
            File.WriteAllText(Path.Combine(output,"Checks.txt"),errors.Count==0?"PASS: catalog dependencies, menu, HUD, inventory, character, skills, seven-sector radial, world recipes and station recipes rendered in Windows build.":"FAIL\n"+string.Join("\n",errors));
            enabled=false;StopAllCoroutines();Application.Quit(errors.Count==0?0:1);
        }
    }
}
