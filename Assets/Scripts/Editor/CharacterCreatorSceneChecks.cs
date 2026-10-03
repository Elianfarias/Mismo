using System;
using System.IO;
using System.Linq;
using Mismo.Menu;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static class CharacterCreatorSceneChecks
{
    const string Output="output/character-creator";
    static System.Collections.IEnumerator inputCheck;
    static Mouse testMouse;
    static int inputFrame=-1;
    [InitializeOnLoadMethod] static void Register(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(inputCheck!=null&&Application.isPlaying&&inputFrame!=Time.frameCount)
        {
            inputFrame=Time.frameCount;
            try{if(inputCheck.MoveNext())return;File.WriteAllText(Output+"/input-check.txt","PASS: left mouse held and moved through the real Input System UI module rotates the animated character; release stops rotation.");}
            catch(Exception e){File.WriteAllText(Output+"/input-check.txt","FAIL\n"+e);Debug.LogException(e);}
            finally{EditorApplication.QueuePlayerLoopUpdate();}
            inputCheck=null;if(testMouse!=null){InputSystem.RemoveDevice(testMouse);testMouse=null;}
        }
        const string request="Temp/CharacterCreatorSceneChecks.request";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
        if(Application.isPlaying&&File.ReadAllText(request).Trim()=="input")
        {File.Delete(request);inputCheck=CheckInput();return;}
        if(Application.isPlaying&&File.ReadAllText(request).Trim()=="live")
        {
            File.Delete(request);var creator=Object.FindAnyObjectByType<CharacterCreatorView>();
            if(creator==null||creator.Stage==null)return;
            var hits=new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=new Vector2(Screen.width*.66f,Screen.height*.5f)},hits);
            File.WriteAllText(Output+"/live.txt","Ready="+creator.Ready+" Rotation="+creator.Stage.Model.transform.eulerAngles+"\n"+string.Join("\n",hits.Select(h=>h.gameObject.name)));
            Capture(creator.GetComponentInParent<Canvas>(),creator.Stage.Camera,"cottage-live");return;
        }
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(File.ReadAllText(request).Trim()=="compile")
        {
            File.Delete(request);
            try
            {
                var result=UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(new UnityEditor.Build.Player.ScriptCompilationSettings{
                    target=BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone},".validation/CharacterCreatorScripts");
                Require(result.assemblies.Any(a=>a.EndsWith("Mismo.Menu.dll")),"Missing menu assembly");
                File.WriteAllText(Output+"/cinematic-compilation.txt","PASS: Windows player scripts with the in-scene cinematic and drag controls.");
            }
            catch(Exception e){File.WriteAllText(Output+"/cinematic-compilation.txt","FAIL\n"+e);}
            return;
        }
        File.Delete(request);
        try{Run();}catch(Exception e){File.WriteAllText(Output+"/scene-checks.txt","FAIL\n"+e);Debug.LogException(e);}
    }
    static void Require(bool test,string message){if(!test)throw new InvalidOperationException(message);}
    static Button Button(CharacterCreatorView view,string name)=>view.GetComponentsInChildren<Button>().First(b=>b.name==name);
    [MenuItem("Mismo/Character/Verificar cinematica del creador")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/MainMenu.unity");
        try
        {
            var menu=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MainMenuView>()).Single();
            var canvas=menu.GetComponent<Canvas>();
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).First(c=>c.enabled&&c.CompareTag("MainCamera"));
            camera.scene=scene;camera.aspect=1600f/900;
            var startPosition=camera.transform.position;var startRotation=camera.transform.rotation;float startSize=camera.orthographicSize;
            Capture(canvas,camera,"cinematic-home");
            bool cancelled=false;string submittedName=null,submittedSkin=null;
            var creator=CharacterCreatorView.Create(menu.transform,(name,skin)=>{submittedName=name;submittedSkin=skin;return null;},()=>cancelled=true);
            creator.Open();Require(creator.Stage!=null,"Scene camera and cottage were not resolved");
            creator.Advance(.8f);
            Require(creator.Stage.Moving&&Vector3.Distance(camera.transform.position,startPosition)>.1f,"Camera must travel before arrival");
            Require(!creator.Ready&&!Button(creator,"Comenzar aventura").interactable,"Cannot submit during travel");
            Capture(canvas,camera,"cinematic-travel");
            creator.Advance(1);
            Require(creator.Ready&&camera.orthographicSize<startSize,"Camera did not zoom into the house");
            Require(creator.Stage.Model.scene==scene&&Vector3.Distance(creator.Stage.Model.transform.position,creator.Stage.Cottage.position)<4,"Character is not in front of the real cottage");
            Require(!Button(creator,"Comenzar aventura").interactable,"Blank name accepted");
            creator.GetComponentInChildren<InputField>().text="Élian";
            Require(Button(creator,"Comenzar aventura").interactable,"Valid name rejected");
            Capture(canvas,camera,"cottage-mage");
            var drag=creator.GetComponentInChildren<CharacterCreatorDragSurface>();
            var events=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EventSystem>()).First();
            var data=new PointerEventData(events){button=PointerEventData.InputButton.Left,delta=new Vector2(120,0)};
            var rotation=creator.Stage.Model.transform.rotation;
            drag.OnBeginDrag(data);drag.OnDrag(data);drag.OnEndDrag(data);
            Require(Quaternion.Angle(rotation,creator.Stage.Model.transform.rotation)>50,"Left drag did not rotate character");
            var after=creator.Stage.Model.transform.rotation;drag.OnDrag(data);
            Require(Quaternion.Angle(after,creator.Stage.Model.transform.rotation)<.01f,"Release did not stop rotation");
            creator.Stage.Model.GetComponent<Animator>().Update(.1f);
            Require(Quaternion.Angle(after,creator.Stage.Model.transform.rotation)<.01f,"Animation overwrote drag rotation");
            data.button=PointerEventData.InputButton.Right;drag.OnBeginDrag(data);drag.OnDrag(data);drag.OnEndDrag(data);
            Require(Quaternion.Angle(after,creator.Stage.Model.transform.rotation)<.01f,"Right click rotates character");
            creator.Drag(-120);
            Button(creator,"›").onClick.Invoke();CheckSelected(creator,PlayerAppearance.Knight);Capture(canvas,camera,"cottage-knight");
            Button(creator,"›").onClick.Invoke();CheckSelected(creator,PlayerAppearance.NinjaFrog);Capture(canvas,camera,"cottage-ninja-frog");
            Button(creator,"›").onClick.Invoke();CheckSelected(creator,PlayerAppearance.Mage);
            Button(creator,"‹").onClick.Invoke();CheckSelected(creator,PlayerAppearance.NinjaFrog);
            Button(creator,"‹").onClick.Invoke();CheckSelected(creator,PlayerAppearance.Knight);
            Button(creator,"›").onClick.Invoke();CheckSelected(creator,PlayerAppearance.NinjaFrog);
            Require(creator.GetComponentInChildren<InputField>().text=="Élian"&&submittedSkin==null,"Switching skin changed the name or submitted early");
            Button(creator,"Volver").onClick.Invoke();creator.Advance(1.3f);
            Require(cancelled&&!creator.gameObject.activeSelf&&creator.Stage==null,"Cancel did not dispose the stage");
            Require(Vector3.Distance(camera.transform.position,startPosition)<.0001f&&Quaternion.Angle(camera.transform.rotation,startRotation)<.001f&&Mathf.Abs(camera.orthographicSize-startSize)<.0001f,"Original camera was not restored");
            creator.Open();creator.Advance(.2f);Button(creator,"Volver").onClick.Invoke();creator.Advance(2);
            Require(Vector3.Distance(camera.transform.position,startPosition)<.0001f,"Cancelling midway did not restore camera");
            creator.Open();creator.Advance(2);CheckSelected(creator,PlayerAppearance.NinjaFrog);
            Button(creator,"Comenzar aventura").onClick.Invoke();
            Require(submittedName=="Élian"&&submittedSkin==PlayerAppearance.NinjaFrog,"Confirmation did not pass the selected frog and name");
            File.WriteAllText(Output+"/scene-checks.txt","PASS: actual main-menu cottage; camera travel and zoom; input gating; all three in-world skins; forward/backward wrap; name preserved; frog confirmed after closing/reopening; left-button drag, release and right-button rejection; return and interrupted-transition restoration. Open editor scenes untouched.");
            Debug.Log("CHARACTER_CREATOR_SCENE_CHECKS_OK");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void CheckSelected(CharacterCreatorView creator,string id)
    {
        Require(creator.Stage.Model.name==PlayerAppearance.Prefab(id).name+"(Clone)","Wrong preview: "+id);
        Require(creator.GetComponentsInChildren<Text>().Any(t=>t.text==PlayerAppearance.SkinName(id)),"Wrong appearance label: "+id);
        var animator=creator.Stage.Model.GetComponent<Animator>();
        Require(animator.isHuman&&animator.avatar.isValid&&animator.runtimeAnimatorController!=null,"Preview animator invalid: "+id);
        if(id==PlayerAppearance.NinjaFrog)Require(creator.Stage.Model.GetComponent<FrogScarfMotion>()?.enabled==true,"Frog preview lost scarf motion");
    }
    static System.Collections.IEnumerator CheckInput()
    {
        var menu=Object.FindAnyObjectByType<MainMenuView>();menu.Begin();
        var creator=Object.FindAnyObjectByType<CharacterCreatorView>();
        while(!creator.Ready)yield return null;
        testMouse=InputSystem.AddDevice<Mouse>();
        var origin=new Vector2(Screen.width*.66f,Screen.height*.5f);
        InputSystem.QueueStateEvent(testMouse,new MouseState{position=origin});
        for(int i=0;i<4;i++)yield return null;
        var before=creator.Stage.Model.transform.rotation;
        InputSystem.QueueStateEvent(testMouse,new MouseState{position=origin}.WithButton(MouseButton.Left));
        for(int i=0;i<4;i++)yield return null;
        for(int i=1;i<=16;i++)
        {
            InputSystem.QueueStateEvent(testMouse,new MouseState{position=origin+Vector2.right*i*8,delta=Vector2.right*8}.WithButton(MouseButton.Left));
            for(int f=0;f<3;f++)yield return null;
        }
        InputSystem.QueueStateEvent(testMouse,new MouseState{position=origin+Vector2.right*128});
        for(int i=0;i<5;i++)yield return null;
        var after=creator.Stage.Model.transform.rotation;
        Require(Quaternion.Angle(before,after)>30,"Input System drag did not rotate the character: "+before.eulerAngles+" -> "+after.eulerAngles);
        InputSystem.QueueStateEvent(testMouse,new MouseState{position=origin,delta=Vector2.left*128});
        for(int i=0;i<5;i++)yield return null;
        Require(Quaternion.Angle(after,creator.Stage.Model.transform.rotation)<.1f,"Released pointer kept rotating character");
        Capture(creator.GetComponentInParent<Canvas>(),creator.Stage.Camera,"cottage-input-drag");
        Button(creator,"Volver").onClick.Invoke();
        while(creator.gameObject.activeSelf)yield return null;
    }
    static void Capture(Canvas canvas,Camera camera,string name)
    {
        var rt=new RenderTexture(1600,900,24);rt.Create();var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;
        try
        {
            camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.3f;
            foreach(var graphic in canvas.GetComponentsInChildren<Graphic>())graphic.SetAllDirty();
            Canvas.ForceUpdateCanvases();camera.Render();camera.Render();RenderTexture.active=rt;
            pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
        }
        finally{canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}
    }
}
