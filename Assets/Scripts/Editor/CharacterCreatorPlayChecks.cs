using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Menu;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Saves are redirected to an isolated test directory; editor runs restore the scene setup.
public static class CharacterCreatorPlayChecks
{
    const string Active="Mismo.CharacterCreatorChecks",Output="output/character-creator";
    static IEnumerator routine;
    static double deadline;
    static int wait;
    [Serializable] sealed class SavedScenes { public SceneSetup[] scenes; }
    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;
    }
    [MenuItem("Mismo/Character/Verificar creador en Play Mode")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before running the check.");
        var scenes=EditorSceneManager.GetSceneManagerSetup();
        if(!Application.isBatchMode&&scenes.Any(s=>string.IsNullOrEmpty(s.path)||UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
        {File.WriteAllText(Output+"/play.txt","BLOCKED: open scene has unsaved changes; preserved intact.");return;}
        SessionState.SetString(Active+".Scenes",JsonUtility.ToJson(new SavedScenes{scenes=scenes}));
        SessionState.SetBool(Active+".Background",Application.runInBackground);
        SessionState.SetBool(Active+".Finishing",false);
        routine=null;wait=0;
        File.WriteAllText(Output+"/play.txt","RUNNING: isolated character creator check.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Active,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)Application.runInBackground=true;
        if(state!=PlayModeStateChange.EnteredEditMode)return;
        SessionState.SetBool(Active,false);routine=null;
        Application.runInBackground=SessionState.GetBool(Active+".Background",false);
        var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Active+".Scenes",""));
        if(saved!=null&&saved.scenes.Length>0)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
        SessionState.EraseString(Active+".Scenes");
        if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(SessionState.GetInt(Active+".ExitCode",1));
    }
    static void Finish(int exitCode)
    {
        routine=null;SessionState.SetBool(Active+".Finishing",true);SessionState.SetInt(Active+".ExitCode",exitCode);EditorApplication.ExitPlaymode();
    }
    static void Tick()
    {
        const string request="Temp/CharacterCreatorPlayChecks.request";
        if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!BuildPipeline.isBuildingPlayer&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(request))
        {
            File.Delete(request);
            try{Run();}catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/play.txt","FAIL\n"+e);Debug.LogException(e);}
            return;
        }
        if(!SessionState.GetBool(Active,false)||SessionState.GetBool(Active+".Finishing",false)||!Application.isPlaying||EditorApplication.isCompiling)return;
        if(routine==null){routine=Check();deadline=EditorApplication.timeSinceStartup+180;}
        EditorApplication.QueuePlayerLoopUpdate();
        if(wait++%3!=0)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Character creator play check");
            if(routine.MoveNext())return;
            File.WriteAllText(Output+"/play.txt","PASS: New game opens creator without saving; empty names blocked; three previews switch and wrap in both directions; cancel preserves session; confirmed frog/name load into gameplay and survive continue; all runtime skins spawn with valid avatars and secondary motion.");
            Finish(0);
        }
        catch(Exception e){File.WriteAllText(Output+"/play.txt","FAIL\n"+e);Debug.LogException(e);Finish(1);}
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static Button Button(CharacterCreatorView view,string name)=>view.GetComponentsInChildren<Button>().First(b=>b.name==name);
    static IEnumerator Check()
    {
        yield return null;
        typeof(WorldSession).GetField("VerificationDirectory",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Path.GetFullPath(Output+"/test-save-"+Guid.NewGuid().ToString("N")));
        var menu=Object.FindAnyObjectByType<MainMenuView>();Require(menu!=null,"Menu not loaded");
        menu.Begin();yield return null;
        var creator=Object.FindAnyObjectByType<CharacterCreatorView>();Require(creator!=null,"New game did not open creator");
        Require(WorldSession.Current==null,"Opening creator committed a world");
        Require(!Button(creator,"Comenzar aventura").interactable,"Empty name accepted");
        while(!creator.Ready)yield return null;
        var input=creator.GetComponentInChildren<InputField>();input.text="Élian";
        Require(Button(creator,"Comenzar aventura").interactable,"Name did not enable confirmation");
        for(int i=0;i<10;i++)yield return null;
        Capture(creator,"mage");
        Button(creator,"›").onClick.Invoke();
        for(int i=0;i<10;i++)yield return null;
        Capture(creator,"knight");
        Button(creator,"›").onClick.Invoke();
        for(int i=0;i<10;i++)yield return null;
        Require(creator.Stage.Model.GetComponent<FrogScarfMotion>()!=null,"Frog preview missing");
        Capture(creator,"ninja-frog");
        Button(creator,"›").onClick.Invoke();
        Require(creator.Stage.Model.GetComponent<MageHatMotion>()!=null,"Forward wrap failed");
        Button(creator,"‹").onClick.Invoke();
        Require(creator.Stage.Model.GetComponent<FrogScarfMotion>()!=null,"Backward wrap failed");
        Button(creator,"Volver").onClick.Invoke();yield return null;
        while(creator.gameObject.activeSelf)yield return null;
        Require(WorldSession.Current==null,"Cancel committed a world");
        menu.Begin();yield return null;
        while(!creator.Ready)yield return null;
        Button(creator,"Comenzar aventura").onClick.Invoke();
        for(int i=0;i<100;i++)
        {
            yield return null;
            if(Object.FindAnyObjectByType<MainMenuView>()==null&&Object.FindAnyObjectByType<PlayerAnimationDriver>()!=null)break;
        }
        Require(WorldSession.Current?.playerName=="Élian"&&WorldSession.Current.playerSkin==PlayerAppearance.NinjaFrog,"Character not saved");
        var player=Object.FindAnyObjectByType<PlayerAnimationDriver>();Require(player!=null,"Player not spawned");
        Require(player.Animator.GetComponent<FrogScarfMotion>()!=null,"Frog skin not applied in gameplay");
        Require(player.Animator.avatar.isValid&&player.Animator.isHuman,"Runtime avatar invalid");
        Require(WorldSession.Continue()&&WorldSession.Current.playerName=="Élian"&&WorldSession.Current.playerSkin==PlayerAppearance.NinjaFrog,"Continue lost character");
        var savedSkin=WorldSession.Current.playerSkin;
        foreach(var id in PlayerAppearance.Skins)
        {
            WorldSession.Current.playerSkin=id;
            var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AshenWarriorIntegration.Player));
            yield return null;
            var animator=instance.GetComponent<PlayerAnimationDriver>().Animator;
            Require(animator.avatar.isValid&&animator.isHuman,"Runtime avatar invalid: "+id);
            Require(id==PlayerAppearance.Mage?animator.GetComponent<MageHatMotion>()!=null:
                id==PlayerAppearance.NinjaFrog?animator.GetComponent<FrogScarfMotion>()!=null:
                animator.GetComponent<WarriorCapeMotion>()!=null&&animator.GetComponent<WarriorPlumeMotion>()!=null,"Runtime skin not applied: "+id);
            Object.Destroy(instance);WorldSession.Current.playerSkin=savedSkin;
            yield return null;
        }
    }
    static void Capture(CharacterCreatorView creator,string suffix)
    {
        var canvas=creator.GetComponentInParent<Canvas>();
        var camera=creator.Stage.Camera;
        var rt=new RenderTexture(1600,900,24);rt.Create();camera.targetTexture=rt;
        var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;var previous=RenderTexture.active;
        var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
        try
        {
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            foreach(var graphic in canvas.GetComponentsInChildren<Graphic>())graphic.SetAllDirty();
            Canvas.ForceUpdateCanvases();camera.Render();camera.Render();RenderTexture.active=rt;
            pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();File.WriteAllBytes(Output+"/creator-"+suffix+".png",pixels.EncodeToPNG());
        }
        finally{canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(pixels);}
    }
}
