using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Mismo.Menu.Editor
{
    public static class MainMenuBuilder
    {
        public const string ScenePath="Assets/Scenes/MainMenu.unity";
        const string Pending="Mismo.MenuChecks";
        static IEnumerator routine;
        static double deadline;
        static int frame=-1;
        [MenuItem("Mismo/Menu/Create main menu")]
        public static void Create()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Menu camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(19,13,-19);camera.transform.LookAt(new Vector3(0,1,6));camera.fieldOfView=48;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.35f,.4f);
            var light=new GameObject("Sun",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            RenderSettings.ambientLight=new Color(.45f,.53f,.55f);RenderSettings.fog=true;RenderSettings.fogColor=camera.backgroundColor;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=35;RenderSettings.fogEndDistance=85;
            Directory.CreateDirectory("Assets/Art/Materials/Menu");
            var grass=Material("Grass",new Color(.26f,.39f,.23f));var stone=Material("Stone",new Color(.22f,.29f,.3f));var leaves=Material("Leaves",new Color(.15f,.32f,.23f));var wood=Material("Wood",new Color(.28f,.22f,.16f));var path=Material("Path",new Color(.56f,.49f,.33f));
            Block("Island",new Vector3(0,-3,9),new Vector3(45,5,45),stone);
            Block("Meadow",new Vector3(0,-.7f,9),new Vector3(45,1,45),grass);
            Block("Path",new Vector3(3,-.15f,5),new Vector3(3,.2f,36),path);
            for(int i=0;i<15;i++)
            {
                float x=(i%5)*8-17,z=(i/5)*11+5;
                Block("Hill",new Vector3(x,1+i%3,z+16),new Vector3(8,3+i%3*2,8),grass);
                if(Mathf.Abs(x-3)<4)continue;
                Block("Trunk",new Vector3(x,1.6f,z),new Vector3(.6f,3.2f,.6f),wood);
                Block("Crown",new Vector3(x,3.8f,z),new Vector3(3.8f,2.5f,3.8f),leaves);
                Block("Crown top",new Vector3(x,5.2f,z),new Vector3(2.5f,.6f,2.5f),leaves);
            }
            var root=new GameObject("Main menu",typeof(RectTransform));root.AddComponent<MainMenuView>().CreateLayout();
            root.AddComponent<MusicController>();
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
            AssetDatabase.SaveAssets();Debug.Log("MAIN_MENU_CREATED");
        }
        static Material Material(string name,Color color)
        {
            string path="Assets/Art/Materials/Menu/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
            material.color=color;return material;
        }
        static void Block(string name,Vector3 position,Vector3 scale,Material material)
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name=name;cube.transform.position=position;cube.transform.localScale=scale;cube.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(cube.GetComponent<Collider>());
        }
        public static void CreateAndCheck()
        {
            Create();SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Resume()
        {
            if(!SessionState.GetBool(Pending,false))return;
            deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
            if(!Application.isPlaying||Time.frameCount<10||frame==Time.frameCount)return;frame=Time.frameCount;
            try{if(routine==null)routine=Check();if(!routine.MoveNext())Finish(true,"Menu, sliders, persistence, gameplay transition");}
            catch(Exception error){Finish(false,error.ToString());}
        }
        static IEnumerator Check()
        {
            var menu=Object.FindFirstObjectByType<MainMenuView>();
            Require(menu!=null&&!menu.OptionsVisible,"Menu opens on home screen");
            Require(EditorBuildSettings.scenes[0].path==ScenePath,"Menu is first build scene");
            Capture(menu,"main-menu.png");menu.ShowOptions(true);yield return null;yield return null;
            Require(menu.OptionsVisible,"Options open");
            var sliders=new[]{menu.Music,menu.Sfx,menu.UI};string[] parameters={"VolumeMusic","VolumeSFX","VolumeUI"};
            for(int i=0;i<sliders.Length;i++)
            {
                float original=sliders[i].value;
                sliders[i].value=.7f;yield return null;
                Require(AudioRuntime.Mixer.GetFloat(parameters[i],out float gain)&&Mathf.Abs(gain-(-1.54902f))<.02f,"70 percent remains audible: "+parameters[i]);
                Require(Mathf.Abs(PlayerPrefs.GetFloat(new[]{"Mismo.Music","Mismo.SFX","Mismo.UI"}[i])-.7f)<.001f,"Volume preference saved: "+parameters[i]);
                sliders[i].value=0;yield return null;
                Require(AudioRuntime.Mixer.GetFloat(parameters[i],out gain)&&gain<=-79,"Zero mutes: "+parameters[i]);
                sliders[i].value=original;
            }
            yield return null;
            Capture(menu,"main-menu-options.png");menu.ShowOptions(false);menu.ShowOptions(true);yield return null;
            Require(menu.OptionsVisible,"Options reopen");menu.ShowOptions(false);menu.Begin();
            float end=Time.time+25;while(SceneManager.GetActiveScene().name!="VoxelRegion_7319"&&Time.time<end)yield return null;
            yield return null;
            Require(SceneManager.GetActiveScene().name=="VoxelRegion_7319","Begin loads gameplay");
            Require(Object.FindObjectsByType<AudioRuntime>(FindObjectsSortMode.None).Length==1,"Audio persists without duplicates");
            Require(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(s=>s.outputAudioMixerGroup==AudioRuntime.SfxGroup),"Gameplay effects route through SFX mixer");
        }
        static void Capture(MainMenuView menu,string file)
        {
            Directory.CreateDirectory("Docs/Validation");var canvas=menu.GetComponent<Canvas>();var camera=Camera.main;
            var rt=new RenderTexture(1600,900,24);var old=RenderTexture.active;camera.targetTexture=rt;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();
            camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();File.WriteAllBytes("Docs/Validation/"+file,texture.EncodeToPNG());
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;camera.targetTexture=null;RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(texture);
        }
        static void Require(bool value,string message){if(!value)throw new Exception(message);Debug.Log("MENU_CHECK "+message);}
        static void Finish(bool ok,string message){SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Debug.Log("MENU_CHECKS_"+(ok?"OK ":"FAILED ")+message);EditorApplication.Exit(ok?0:1);}
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Playtest");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath,"Assets/Scenes/VoxelRegion_7319.unity"},locationPathName="Builds/Playtest/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("MENU_BUILD_OK "+report.summary.totalSize);
        }
    }
}
