using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Quests;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Edits locomotion per resident without changing the shared Humanoid clips/controller.</summary>
public sealed class NpcWorkshopWindow : EditorWindow
{
    GameObject prefab, previewObject;
    Animator animator;
    AnimationClip idle, walk;
    PreviewRenderUtility preview;
    float speed=1.35f, pauseMin=5, pauseMax=12, progress, viewAngle=150;
    bool playing, previewWalk;
    double lastTime;
    string message;
    Vector2 scroll;
    [MenuItem("Mismo/NPCs/Taller de NPCs")]
    static void Open() => GetWindow<NpcWorkshopWindow>("Taller de NPCs");
    public static void OpenResident()
    {
        var window=GetWindow<NpcWorkshopWindow>("Taller de NPCs");
        window.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(VillageNpcIntegration.Residents+"/mara.prefab");
        window.Load();window.Show();
    }
    void OnEnable() { minSize=new Vector2(420,650);EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;lastTime=EditorApplication.timeSinceStartup;EditorApplication.delayCall+=RestorePreview; }
    void OnDisable() { EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;EditorApplication.delayCall-=RestorePreview;ClearPreview(); }
    void RestorePreview(){if(this!=null&&!EditorApplication.isPlayingOrWillChangePlaymode&&prefab!=null)Load();}
    void PlayState(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode)ClearPreview();if(state==PlayModeStateChange.EnteredEditMode)RestorePreview();}
    public static void CheckRoundTrip()
    {
        var window=CreateInstance<NpcWorkshopWindow>();
        try
        {
            window.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(VillageNpcIntegration.Residents+"/mara.prefab");window.Load();
            var idle=window.idle;var walk=window.walk;float speed=window.speed;
            window.Save();window.Load();
            if(window.idle!=idle||window.walk!=walk||Mathf.Abs(window.speed-speed)>.001f)throw new Exception("NPC workshop did not preserve its saved values");
            var saved=window.prefab.GetComponentInChildren<Animator>().runtimeAnimatorController;
            if(!(saved is AnimatorOverrideController))throw new Exception("NPC workshop did not create a resident override");
            File.WriteAllText(VillageHumanoidUpgrade.Output+"/workshop.txt","PASS: preview created, Humanoid Idle/Walk and walking speed saved and reloaded through per-resident override.");
        }
        finally{DestroyImmediate(window);}
    }
    void ClearPreview()
    {
        playing=false;
        if(preview!=null)preview.Cleanup();
        preview=null;previewObject=null;animator=null;
    }
    void Load()
    {
        ClearPreview();message=null;
        if(prefab==null)return;
        if(!AssetDatabase.GetAssetPath(prefab).EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))
        {message="Seleccioná un asset .prefab de habitante.";return;}
        var routine=prefab.GetComponent<VillageNpcRoutine>();
        var source=prefab.GetComponentInChildren<Animator>();
        if(routine==null||source==null||source.avatar==null||!source.avatar.isHuman||!source.avatar.isValid)
        {message="Elegí un prefab de habitante con VillageNpcRoutine y Avatar Humanoid válido.";return;}
        speed=routine.walkingSpeed;pauseMin=routine.pauseSeconds.x;pauseMax=routine.pauseSeconds.y;
        var clips=source.runtimeAnimatorController!=null?source.runtimeAnimatorController.animationClips:Array.Empty<AnimationClip>();
        var overrides=source.runtimeAnimatorController as AnimatorOverrideController;
        idle=overrides!=null?overrides["Quaternius_Idle"]:clips.FirstOrDefault(c=>c.name.Contains("Idle"));
        walk=overrides!=null?overrides["Quaternius_Walk"]:clips.FirstOrDefault(c=>c.name.Contains("Walk"));
        preview=new PreviewRenderUtility();
        preview.camera.fieldOfView=32;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
        preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.12f,.15f,.18f);
        preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);
        preview.lights[1].intensity=.6f;preview.ambientColor=new Color(.45f,.45f,.45f);
        previewObject=Object.Instantiate(prefab);previewObject.hideFlags=HideFlags.HideAndDontSave;
        foreach(var component in previewObject.GetComponentsInChildren<MonoBehaviour>())component.enabled=false;
        foreach(var agent in previewObject.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>())agent.enabled=false;
        preview.AddSingleGO(previewObject);
        animator=previewObject.GetComponentInChildren<Animator>();animator.enabled=true;animator.Rebind();progress=0;Sample();
        preview.camera.transform.position=new Vector3(2.3f,1.45f,4.2f);
        preview.camera.transform.LookAt(new Vector3(0,.95f,0));
    }
    void OnGUI()
    {
        scroll=EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Animaciones y paseos",EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        prefab=(GameObject)EditorGUILayout.ObjectField("Prefab del NPC",prefab,typeof(GameObject),false);
        if(EditorGUI.EndChangeCheck())Load();
        if(GUILayout.Button("Usar prefab seleccionado"))
        {
            prefab=Selection.activeObject as GameObject;Load();
        }
        EditorGUILayout.HelpBox("Seleccioná un habitante de Art/Prefabs/Quests/Village. Los cambios se guardan sólo en ese NPC; sus misiones y las animaciones originales se conservan.",MessageType.Info);
        if(message!=null)EditorGUILayout.HelpBox(message,MessageType.Info);
        if(previewObject!=null)
        {
            EditorGUI.BeginChangeCheck();
            idle=(AnimationClip)EditorGUILayout.ObjectField("Reposo (Idle)",idle,typeof(AnimationClip),false);
            walk=(AnimationClip)EditorGUILayout.ObjectField("Caminar (Walk)",walk,typeof(AnimationClip),false);
            if(EditorGUI.EndChangeCheck()){progress=0;Sample();}
            speed=EditorGUILayout.Slider("Velocidad (m/s)",speed,.3f,3);
            pauseMin=EditorGUILayout.FloatField("Pausa mínima (s)",pauseMin);
            pauseMax=EditorGUILayout.FloatField("Pausa máxima (s)",pauseMax);
            bool compatible=idle!=null&&walk!=null&&idle.humanMotion&&walk.humanMotion;
            if(!compatible)EditorGUILayout.HelpBox("Idle y Walk deben ser clips Humanoid. No se pueden guardar clips Generic en este rig.",MessageType.Error);
            EditorGUI.BeginChangeCheck();
            previewWalk=EditorGUILayout.Popup("Previsualizar",previewWalk?1:0,new[]{"Idle","Walk"})==1;
            progress=EditorGUILayout.Slider("Tiempo",progress,0,1);
            viewAngle=EditorGUILayout.Slider("Girar vista",viewAngle,-180,180);
            if(EditorGUI.EndChangeCheck())Sample();
            if(GUILayout.Button(playing?"Pausar":"Reproducir")){playing=!playing;lastTime=EditorApplication.timeSinceStartup;}
            var rect=GUILayoutUtility.GetRect(200,330,GUILayout.ExpandWidth(true));
            if(Event.current.type==EventType.Repaint)
            {
                float angle=viewAngle*Mathf.Deg2Rad;preview.camera.transform.position=new Vector3(Mathf.Sin(angle)*4.8f,1.45f,Mathf.Cos(angle)*4.8f);preview.camera.transform.LookAt(new Vector3(0,.95f,0));
                preview.BeginPreview(rect,GUIStyle.none);preview.Render(true);GUI.DrawTexture(rect,preview.EndPreview(),ScaleMode.StretchToFill,false);
            }
            using(new EditorGUI.DisabledScope(!compatible||EditorApplication.isPlaying))
                if(GUILayout.Button("Guardar animaciones y comportamiento en el NPC"))Save();
        }
        if(GUILayout.Button("Seleccionar recorridos y escala del pueblo"))
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<VillageNpcSettings>(VillageNpcIntegration.SettingsPath);
        EditorGUILayout.EndScrollView();
    }
    void Sample()
    {
        var clip=previewWalk?walk:idle;
        if(animator==null||clip==null||!clip.humanMotion)return;
        var p=animator.transform.localPosition;var r=animator.transform.localRotation;
        clip.SampleAnimation(animator.gameObject,progress*clip.length);
        animator.transform.localPosition=p;animator.transform.localRotation=r;Repaint();
    }
    void Tick()
    {
        double now=EditorApplication.timeSinceStartup;
        if(playing&&previewObject!=null)
        {
            var clip=previewWalk?walk:idle;
            if(clip!=null){progress=Mathf.Repeat(progress+(float)Math.Min(.1,now-lastTime)/Mathf.Max(.01f,clip.length),1);Sample();}
        }
        lastTime=now;
    }
    void Save()
    {
        string prefabPath=AssetDatabase.GetAssetPath(prefab);
        if(!prefabPath.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))return;
        var root=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var target=root.GetComponentInChildren<Animator>();
            var basis=target.runtimeAnimatorController;
            if(basis is AnimatorOverrideController previous)basis=previous.runtimeAnimatorController;
            if(basis==null||!basis.animationClips.Any(c=>c.name=="Quaternius_Idle")||!basis.animationClips.Any(c=>c.name=="Quaternius_Walk"))
                throw new InvalidOperationException("El NPC necesita el controller VillageLocomotion compatible con Idle/Walk.");
            string path="Assets/Art/Animations/Voxelized/NPC/Craftpix/"+Path.GetFileNameWithoutExtension(prefabPath)+".overrideController";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if(controller==null){controller=new AnimatorOverrideController(basis);AssetDatabase.CreateAsset(controller,path);}
            Undo.RecordObject(controller,"Configurar animaciones de NPC");
            controller["Quaternius_Idle"]=idle;controller["Quaternius_Walk"]=walk;EditorUtility.SetDirty(controller);
            target.runtimeAnimatorController=controller;
            var routine=root.GetComponent<VillageNpcRoutine>();routine.animator=target;routine.walkingSpeed=Mathf.Max(.3f,speed);
            routine.pauseSeconds=new Vector2(Mathf.Max(0,pauseMin),Mathf.Max(Mathf.Max(0,pauseMin),pauseMax));
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);AssetDatabase.SaveAssets();
            message="Animaciones y comportamiento guardados en "+prefab.name+".";
        }
        catch(Exception e){message=e.Message;Debug.LogException(e);}
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
}
