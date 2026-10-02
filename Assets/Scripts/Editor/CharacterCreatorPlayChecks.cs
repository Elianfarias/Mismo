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

// Batch-only integration check. Saves are redirected to an isolated test directory.
public static class CharacterCreatorPlayChecks
{
    const string Active="Mismo.CharacterCreatorChecks",Output="output/character-creator";
    static IEnumerator routine;
    static double deadline;
    static int wait;
    [InitializeOnLoadMethod] static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    public static void Run()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Run this check in batch mode.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!Application.isPlaying||EditorApplication.isCompiling)return;
        if(routine==null){routine=Check();deadline=EditorApplication.timeSinceStartup+180;}
        EditorApplication.QueuePlayerLoopUpdate();
        if(wait++%3!=0)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Character creator play check");
            if(routine.MoveNext())return;
            File.WriteAllText(Output+"/play.txt","PASS: New game opens creator without saving; empty names blocked; both previews switch; cancel preserves session; confirmed name/skin load into gameplay and survive continue; both runtime skins spawn with valid avatars.");
            SessionState.SetBool(Active,false);EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Output+"/play.txt","FAIL\n"+e);SessionState.SetBool(Active,false);Debug.LogException(e);EditorApplication.Exit(1);}
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
        Require(WorldSession.Current?.playerName=="Élian"&&WorldSession.Current.playerSkin==PlayerAppearance.Knight,"Character not saved");
        var player=Object.FindAnyObjectByType<PlayerAnimationDriver>();Require(player!=null,"Player not spawned");
        Require(player.Animator.GetComponent<WarriorCapeMotion>()!=null,"Knight skin not applied in gameplay");
        Require(player.Animator.avatar.isValid&&player.Animator.isHuman,"Runtime avatar invalid");
        Require(WorldSession.Continue()&&WorldSession.Current.playerName=="Élian"&&WorldSession.Current.playerSkin==PlayerAppearance.Knight,"Continue lost character");
        var savedSkin=WorldSession.Current.playerSkin;WorldSession.Current.playerSkin=PlayerAppearance.Mage;
        var mage=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AshenWarriorIntegration.Player));
        yield return null;
        var mageAnimator=mage.GetComponent<PlayerAnimationDriver>().Animator;
        Require(mageAnimator.GetComponent<MageHatMotion>()!=null&&mageAnimator.avatar.isValid,"Mage skin not applied in gameplay");
        Object.Destroy(mage);WorldSession.Current.playerSkin=savedSkin;
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
