using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public sealed class EnemyWorkshopWindow:EditorWindow
    {
        [SerializeField] GameObject prefab;
        WeaponPoseStage stage;
        GameObject preview;
        EnemyEquipment equipment;
        Animator animator;
        Transform[] bones;Vector3[] positions,scales;Quaternion[] rotations;
        ScriptableObject settings;
        Vector2 scroll;
        AnimationClip manualClip;
        bool manual,playing,second,showHitbox=true;
        int attackIndex,previewMotion;
        float progress;
        double lastTime;
        bool equipmentDirty;
        readonly List<string> paths=new List<string>(),labels=new List<string>();
        [MenuItem("Mismo/Enemigos/Taller de armas y animaciones")]
        public static void Open(){var w=GetWindow<EnemyWorkshopWindow>("Taller de enemigos");if(Selection.activeObject is GameObject g&&PrefabUtility.IsPartOfPrefabAsset(g))w.prefab=g;}
        void OnEnable(){SceneView.duringSceneGui+=HandlesGUI;EditorApplication.update+=Tick;Undo.undoRedoPerformed+=UndoChanged;}
        void OnDisable(){SceneView.duringSceneGui-=HandlesGUI;EditorApplication.update-=Tick;Undo.undoRedoPerformed-=UndoChanged;Close();}
        void UndoChanged(){if(equipment!=null){equipment.Rebuild();equipmentDirty=true;Sample();}}
        void Close(){playing=false;if(equipment!=null)equipment.Release();if(stage!=null&&StageUtility.GetCurrentStage()==stage)StageUtility.GoToMainStage();stage=null;preview=null;equipment=null;animator=null;equipmentDirty=false;}
        void OpenPreview()
        {
            Close();stage=CreateInstance<WeaponPoseStage>();stage.Header="Taller de enemigos";StageUtility.GoToStage(stage,true);
            preview=stage.Clone(prefab);preview.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            equipment=preview.GetComponent<EnemyEquipment>()??preview.AddComponent<EnemyEquipment>();equipment.enabled=false;
            animator=equipment.ResolveAnimator();
            if(animator!=null)animator.enabled=false;
            bones=preview.GetComponentsInChildren<Transform>(true);positions=bones.Select(b=>b.localPosition).ToArray();rotations=bones.Select(b=>b.localRotation).ToArray();scales=bones.Select(b=>b.localScale).ToArray();
            var goblin=prefab.GetComponent<GoblinController>();var boss=prefab.GetComponent<BossController>();settings=goblin!=null?(ScriptableObject)goblin.Settings:boss!=null?boss.Settings:null;
            equipment.Rebuild();Sample();SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.up,Vector3.one*4),false);
        }
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Las armas son opcionales. Elegí un prefab enemigo para ajustar equipo o sus clips de ataque. La vista está aislada de la escena del juego.",MessageType.Info);
            using(new EditorGUI.DisabledScope(stage!=null||EditorApplication.isPlaying))prefab=(GameObject)EditorGUILayout.ObjectField("Prefab enemigo",prefab,typeof(GameObject),false);
            if(stage==null)
            {
                using(new EditorGUI.DisabledScope(prefab==null||!PrefabUtility.IsPartOfPrefabAsset(prefab)||PrefabUtility.GetPrefabAssetType(prefab)==PrefabAssetType.Model||EditorApplication.isPlaying))if(GUILayout.Button("Abrir vista de ajuste"))OpenPreview();
                EditorGUILayout.EndScrollView();return;
            }
            if(equipment==null){Close();EditorGUILayout.EndScrollView();return;}
            if(GUILayout.Button("Cerrar vista de ajuste")){if(!equipmentDirty||EditorUtility.DisplayDialog("Cambios de equipo sin guardar","Los cambios del equipo se descartarán. Los cambios en el asset de ataques conservan su estado en el editor.","Descartar equipo","Seguir editando"))Close();EditorGUILayout.EndScrollView();return;}
            EditorGUILayout.LabelField("Equipo opcional del enemigo",EditorStyles.boldLabel);
            var data=new SerializedObject(equipment);data.Update();EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(data.FindProperty("controllerOverride"),new GUIContent("Controller opcional"));
            second=GUILayout.Toolbar(second?1:0,new[]{"Principal","Secundaria"})==1;
            var slot=data.FindProperty(second?"secondary":"primary");
            EditorGUILayout.PropertyField(slot.FindPropertyRelative("weapon"),new GUIContent("Arma existente (opcional)"));
            EditorGUILayout.PropertyField(slot.FindPropertyRelative("visualPrefab"),new GUIContent("Modelo propio (opcional)"));
            EditorGUILayout.PropertyField(slot.FindPropertyRelative("pose"),new GUIContent("Anclaje y agarre de este enemigo"),true);
            if(EditorGUI.EndChangeCheck()){data.ApplyModifiedProperties();equipmentDirty=true;equipment.Rebuild();Sample();}
            if(GUILayout.Button("Elegir hueso de anclaje…")&&animator!=null)
            {
                var menu=new GenericMenu();foreach(var bone in animator.GetComponentsInChildren<Transform>(true))
                {string path=AnimationUtility.CalculateTransformPath(bone,animator.transform);if(string.IsNullOrEmpty(path))continue;menu.AddItem(new GUIContent(path),Slot.pose.bonePath==path,()=>{Undo.RecordObject(equipment,"Elegir hueso del enemigo");Slot.pose.anchor=WeaponAnchor.BonePath;Slot.pose.bonePath=path;equipmentDirty=true;Sample();});}menu.ShowAsContext();
            }
            if(Slot.Prefab!=null&&Slot.pose.Resolve(preview.transform,animator)==null)EditorGUILayout.HelpBox("No se encontró el anclaje. Elegí un hueso válido antes de guardar.",MessageType.Warning);
            if(GUILayout.Button("Ocultar/mostrar arma integrada…")&&animator!=null)
            {
                var menu=new GenericMenu();foreach(var r in animator.GetComponentsInChildren<Renderer>(true))
                {string path=AnimationUtility.CalculateTransformPath(r.transform,animator.transform);menu.AddItem(new GUIContent(string.IsNullOrEmpty(path)?"(Raíz)":path),equipment.hiddenRendererPaths.Contains(path),()=>{Undo.RecordObject(equipment,"Ocultar malla del enemigo");var list=equipment.hiddenRendererPaths.ToList();if(!list.Remove(path))list.Add(path);equipment.hiddenRendererPaths=list.ToArray();equipmentDirty=true;equipment.Rebuild();Sample();});}menu.ShowAsContext();
            }
            EditorGUILayout.HelpBox("W mueve el arma y E la rota en Scene. El agarre pertenece a este prefab enemigo; no cambia el del jugador. El equipo visual no reemplaza la configuración de daño de la IA.",MessageType.Info);
            if(GUILayout.Button("Guardar equipo y animaciones base en el prefab"))SaveEquipment();
            DrawAnimations();
            EditorGUILayout.EndScrollView();
        }
        EnemyWeaponSlot Slot=>second?equipment.secondary:equipment.primary;
        Transform Visual=>second?equipment.SecondaryVisual:equipment.PrimaryVisual;
        void BuildPaths()
        {
            paths.Clear();labels.Clear();
            if(settings is GoblinSettings g)
            {
                if(g.attacks!=null&&g.attacks.Length>0)for(int i=0;i<g.attacks.Length;i++){paths.Add("attacks.Array.data["+i+"]");labels.Add((i+1)+" · "+(g.attacks[i]?.label??"Vacío"));}
                else{paths.Add("slash");labels.Add("Golpe");paths.Add("charge");labels.Add("Carga");}
            }
            else if(settings is BossSettings){paths.AddRange(new[]{"frontSlash","overheadSmash","straightCharge"});labels.AddRange(new[]{"Tajo","Golpe descendente","Carga"});}
            attackIndex=Mathf.Clamp(attackIndex,0,Mathf.Max(0,paths.Count-1));
        }
        void DrawAnimations()
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("Movimiento y reacción al daño",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Clips opcionales por prefab enemigo, también sin armas. Vacío conserva las animaciones actuales. Hit es la reacción visual; el aturdimiento se configura en los datos de combate.",MessageType.Info);
            var baseData=new SerializedObject(equipment);baseData.Update();int playMotion=0;EditorGUI.BeginChangeCheck();
            foreach(var pair in new[]{new[]{"idleClip","Reposo (Idle)"},new[]{"walkClip","Caminar (Walk)"},new[]{"runClip","Correr (Run)"},new[]{"hitClip","Recibir golpe (Hit)"},new[]{"postureBreakClip","Ruptura de postura"},new[]{"parryClip","Parry recibido (vacío usa Hit)"},new[]{"parryDuration","Duración visual del parry (segundos)"},new[]{"hitDuration","Duración de reacción (segundos)"},new[]{"hitMask","Máscara de reacción (opcional)"},new[]{"walkReferenceSpeed","Velocidad de referencia al caminar"},new[]{"runReferenceSpeed","Velocidad de referencia al correr"}})
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    var property=baseData.FindProperty(pair[0]);
                    EditorGUILayout.PropertyField(property,new GUIContent(pair[1]));
                    int motion=pair[0]=="idleClip"?1:pair[0]=="walkClip"?2:pair[0]=="runClip"?3:pair[0]=="hitClip"?4:pair[0]=="postureBreakClip"?5:pair[0]=="parryClip"?6:0;
                    if(motion>0)using(new EditorGUI.DisabledScope((property.objectReferenceValue==null&&(motion!=6||equipment.hitClip==null))||animator==null))
                        if(GUILayout.Button("Reproducir",GUILayout.Width(85)))playMotion=motion;
                }
            }
            if(EditorGUI.EndChangeCheck()){baseData.ApplyModifiedProperties();equipmentDirty=true;Sample();}
            if(playMotion>0){previewMotion=playMotion;manual=false;progress=0;playing=true;lastTime=EditorApplication.timeSinceStartup;Sample();}
            EditorGUI.BeginChangeCheck();previewMotion=EditorGUILayout.Popup("Previsualizar",previewMotion,new[]{"Ataque seleccionado","Reposo","Caminar","Correr","Recibir golpe","Ruptura de postura","Parry recibido"});
            if(EditorGUI.EndChangeCheck()){manual=false;progress=0;Sample();}
            if(previewMotion==5&&!manual)EditorGUILayout.HelpBox("La prueba reproduce la duración original del clip. En el juego se adapta al tiempo de postura rota, con cuerpo completo y prioridad sobre Hit.",MessageType.Info);
            if(previewMotion==6&&!manual)EditorGUILayout.HelpBox("Vacío usa Hit. Esta duración solo controla la animación; no cambia el tiempo de aturdimiento. Una ruptura real de postura tiene prioridad.",MessageType.Info);
            DrawPlayback();
            if(GUILayout.Button("Guardar movimiento y hit en el prefab"))SaveEquipment();
            EditorGUILayout.Space();EditorGUILayout.LabelField("Animaciones y ataques",EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Configuración utilizada",settings,typeof(ScriptableObject),false);
            if(settings==null)EditorGUILayout.HelpBox("Este prefab no utiliza GoblinController o BossController. Podés ajustar su equipo y probar clips; sus ataques deben editarse en su controlador específico.",MessageType.Info);
            else
            {
                EditorGUILayout.HelpBox("Los cambios de ataques se editan en el ScriptableObject compartido y afectan a todos los enemigos que lo usan. Clip vacío conserva la animación del controlador.",MessageType.Info);
                BuildPaths();EditorGUI.BeginChangeCheck();if(paths.Count>0)attackIndex=EditorGUILayout.Popup("Ataque",attackIndex,labels.ToArray());
                if(EditorGUI.EndChangeCheck()){progress=0;Sample();}
                var so=new SerializedObject(settings);so.Update();if(paths.Count>0){EditorGUI.BeginChangeCheck();EditorGUILayout.PropertyField(so.FindProperty(paths[attackIndex]),new GUIContent("Configuración del ataque"),true);if(EditorGUI.EndChangeCheck()){so.ApplyModifiedProperties();Sample();}}
                if(GUILayout.Button("Guardar animaciones y ataques"))AssetDatabase.SaveAssetIfDirty(settings);
                if(GUILayout.Button("Seleccionar configuración completa")){Selection.activeObject=settings;EditorGUIUtility.PingObject(settings);}
            }
        }
        void DrawPlayback()
        {
            EditorGUI.BeginChangeCheck();manual=EditorGUILayout.Toggle("Probar otro clip sin asignarlo",manual);
            if(manual)manualClip=(AnimationClip)EditorGUILayout.ObjectField("Clip de prueba",manualClip,typeof(AnimationClip),false);
            progress=EditorGUILayout.Slider("Progreso de la animación",progress,0,1);showHitbox=EditorGUILayout.Toggle("Mostrar volumen de impacto",showHitbox);
            if(EditorGUI.EndChangeCheck())Sample();
            var clip=GetClip(out _,out _,out _,out _);
            if(clip!=null&&animator!=null&&clip.isHumanMotion!=animator.isHuman)EditorGUILayout.HelpBox("El tipo de rig del clip y el personaje no coincide. Esta herramienta asigna clips; no convierte automáticamente rigs incompatibles.",MessageType.Warning);
            if(animator==null)EditorGUILayout.HelpBox("El prefab necesita un Animator para previsualizar sus clips.",MessageType.Warning);
            else if(clip==null)EditorGUILayout.HelpBox("Asigná un clip a la animación seleccionada para reproducirla. También podés activar ‘Probar otro clip sin asignarlo’. Los campos vacíos conservan el controlador en el juego, pero esta vista necesita un clip explícito.",MessageType.Info);
            using(new EditorGUI.DisabledScope(clip==null||animator==null))
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(playing?"Pausar":"Reproducir animación en bucle")){playing=!playing;lastTime=EditorApplication.timeSinceStartup;}
                if(GUILayout.Button("Reiniciar",GUILayout.Width(85))){progress=0;lastTime=EditorApplication.timeSinceStartup;Sample();}
            }
        }
        AnimationClip GetClip(out float normalized,out float duration,out Vector3 center,out Vector3 size)
        {
            normalized=progress;duration=1;center=new Vector3(0,.85f,1);size=Vector3.one;
            if(manual){duration=manualClip!=null?Mathf.Max(.01f,manualClip.length):1;return manualClip;}
            if(previewMotion>0&&equipment!=null)
            {
                var selected=previewMotion==1?equipment.idleClip:previewMotion==2?equipment.walkClip:previewMotion==3?equipment.runClip:previewMotion==4?equipment.hitClip:previewMotion==5?equipment.postureBreakClip:equipment.ParryClip;
                duration=previewMotion==6?Mathf.Max(.01f,equipment.parryDuration):previewMotion==4?Mathf.Max(.01f,equipment.hitDuration):selected!=null?Mathf.Max(.01f,selected.length):1;
                return selected;
            }
            BuildPaths();if(settings==null||paths.Count==0)return null;
            var so=new SerializedObject(settings);var attack=so.FindProperty(paths[attackIndex]);if(attack==null)return null;
            float windup=attack.FindPropertyRelative("windup").floatValue,active=attack.FindPropertyRelative("active").floatValue,recovery=attack.FindPropertyRelative("recovery").floatValue;
            duration=Mathf.Max(.01f,windup+active+recovery);float t=progress*duration;
            center.z=attack.FindPropertyRelative("forwardOffset").floatValue;var height=attack.FindPropertyRelative("hitHeight");if(height!=null)center.y=height.floatValue;size=attack.FindPropertyRelative("halfExtents").vector3Value*2;
            var anim=attack.FindPropertyRelative("animation");var clip=anim.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
            if(anim.FindPropertyRelative("compatibleSource").objectReferenceValue==clip&&anim.FindPropertyRelative("compatibleClip").objectReferenceValue!=null)clip=(AnimationClip)anim.FindPropertyRelative("compatibleClip").objectReferenceValue;
            float start=anim.FindPropertyRelative("activeStartsAt").floatValue,end=Mathf.Max(start,anim.FindPropertyRelative("recoveryStartsAt").floatValue);
            normalized=t<windup?Mathf.Lerp(0,start,t/Mathf.Max(.01f,windup)):t<windup+active?Mathf.Lerp(start,end,(t-windup)/Mathf.Max(.01f,active)):Mathf.Lerp(end,1,(t-windup-active)/Mathf.Max(.01f,recovery));
            if(settings is CreatureSettings)
            {
                var prep=attack.FindPropertyRelative("preparationClip").objectReferenceValue as AnimationClip;
                var finish=attack.FindPropertyRelative("recoveryClip").objectReferenceValue as AnimationClip;
                float prepDuration=attack.FindPropertyRelative("preparationDuration").floatValue;
                float prepEnd=attack.FindPropertyRelative("preparationEndNormalized").floatValue;
                if(prep!=null&&t<prepDuration){clip=prep;normalized=t/Mathf.Max(.01f,prepDuration)*prepEnd;}
                else if(attack.FindPropertyRelative("loopActiveAnimation").boolValue)
                {
                    if(t<windup||t>=windup+active){clip=prep;normalized=t<windup?prepEnd:Mathf.Lerp(prepEnd,1,(t-windup-active)/Mathf.Max(.01f,recovery));}
                    else normalized=clip!=null?Mathf.Repeat((t-windup)/Mathf.Max(.01f,clip.length),1):0;
                }
                else if(clip!=null){float clipTime=t-prepDuration;if(finish!=null&&clipTime>=clip.length){clipTime-=clip.length;clip=finish;}normalized=Mathf.Clamp01(clipTime/Mathf.Max(.01f,clip.length));}
            }
            return clip;
        }
        void Sample()
        {
            if(preview==null||equipment==null)return;
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null){bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];bones[i].localScale=scales[i];}
            var clip=GetClip(out float t,out _,out _,out _);if(clip!=null&&animator!=null)clip.SampleAnimation(animator.gameObject,t*clip.length);
            equipment.ApplyPose();SceneView.RepaintAll();Repaint();
        }
        void Tick(){if(!playing||stage==null||EditorApplication.isPlaying)return;double now=EditorApplication.timeSinceStartup;if(GetClip(out _,out float duration,out _,out _)==null||animator==null){playing=false;Repaint();return;}progress=Mathf.Repeat(progress+Mathf.Min(.05f,(float)(now-lastTime))/duration,1);lastTime=now;Sample();}
        void HandlesGUI(SceneView view)
        {
            if(stage==null||StageUtility.GetCurrentStage()!=stage||equipment==null)return;
            if(showHitbox&&!manual&&previewMotion==0){GetClip(out _,out _,out Vector3 center,out Vector3 size);Handles.color=new Color(1,.4f,.1f);using(new Handles.DrawingScope(preview.transform.localToWorldMatrix))Handles.DrawWireCube(center,size);}
            if(Visual==null||!Visual.gameObject.activeInHierarchy)return;var anchor=Slot.pose.Resolve(preview.transform,animator);if(anchor==null)return;
            var basis=Slot.pose.Orientation(anchor,animator);EditorGUI.BeginChangeCheck();Vector3 position=Visual.position;Quaternion rotation=Visual.rotation;
            if(Tools.current==Tool.Rotate)rotation=Handles.RotationHandle(rotation,position);else position=Handles.PositionHandle(position,basis);
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(equipment,"Ajustar arma del enemigo");Slot.pose.offset=Quaternion.Inverse(basis)*(position-anchor.position);Slot.pose.rotation=(Quaternion.Inverse(basis)*rotation).eulerAngles;equipmentDirty=true;Sample();}
        }
        void SaveEquipment()
        {
            string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var destination=root.GetComponent<EnemyEquipment>()??root.AddComponent<EnemyEquipment>();
                EditorUtility.CopySerialized(equipment,destination);destination.enabled=true;
                string animatorPath=animator!=null?AnimationUtility.CalculateTransformPath(animator.transform,preview.transform):null;
                destination.animator=animatorPath==null?null:(string.IsNullOrEmpty(animatorPath)?root.transform:root.transform.Find(animatorPath))?.GetComponent<Animator>();
                PrefabUtility.SaveAsPrefabAsset(root,path);equipmentDirty=false;ShowNotification(new GUIContent("Equipo guardado en el prefab"));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
