using System;
using Mismo.Gameplay.Player.World.Structures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed partial class StructureWorkshopWindow : EditorWindow
{
    StructureDefinition definition;
    UnityEditor.Editor inspector;
    Vector2 scroll;
    int dragging=-1;
    [MenuItem("Mismo/World/Taller de estructuras")]
    public static void Open()=>GetWindow<StructureWorkshopWindow>("Taller de estructuras");
    void OnEnable(){minSize=new Vector2(620,800);definition=AssetDatabase.LoadAssetAtPath<StructureDefinition>(StructureWorkshopSetup.DefRoot+"Cave_Moss.asset");}
    void OnDisable(){if(inspector!=null)DestroyImmediate(inspector);}
    void OnGUI()
    {
        EditorGUILayout.LabelField("Taller de estructuras",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Editar el plano, generar una variante y recorrerla. Arrastrar salas en el plano; las conexiones se editan en el Inspector. Guardar reemplaza solo el prefab de esta definición.",MessageType.Info);
        var next=(StructureDefinition)EditorGUILayout.ObjectField("Definicion",definition,typeof(StructureDefinition),false);
        if(next!=definition){definition=next;if(inspector!=null)DestroyImmediate(inspector);inspector=null;}
        if(GUILayout.Button("Crear definicion nueva"))
        {
            string path=EditorUtility.SaveFilePanelInProject("Nueva estructura","Structure","asset","Guardar en Assets/Data/World/Structures",StructureWorkshopSetup.DefRoot.TrimEnd('/'));
            if(!string.IsNullOrEmpty(path))
            {
                if(!path.StartsWith("Assets/Data/")){ShowNotification(new GUIContent("Usar una carpeta dentro de Assets/Data"));return;}
                if(inspector!=null)DestroyImmediate(inspector);
                definition=definition!=null?Instantiate(definition):CreateInstance<StructureDefinition>();definition.name=System.IO.Path.GetFileNameWithoutExtension(path);AssetDatabase.CreateAsset(definition,path);Selection.activeObject=definition;inspector=null;
            }
        }
        if(definition==null){EditorGUILayout.HelpBox("Seleccionar o crear una definicion.",MessageType.Warning);return;}
        if(definition.EnsureContentIds())EditorUtility.SetDirty(definition);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Generar plano"))Run(()=>{Undo.RecordObject(definition,"Generar plano");definition.GenerateLayout();EditorUtility.SetDirty(definition);});
            if(GUILayout.Button("Otra semilla"))Run(()=>{Undo.RecordObject(definition,"Otra semilla");definition.layoutSeed++;definition.GenerateLayout();EditorUtility.SetDirty(definition);});
            if(GUILayout.Button("Variar decoracion")){Undo.RecordObject(definition,"Variar decoracion");definition.decorationSeed++;EditorUtility.SetDirty(definition);}
            EditorGUILayout.EndHorizontal();
            string error=definition.ValidateLayout();if(error!=null)EditorGUILayout.HelpBox(error,MessageType.Warning);
            using(new EditorGUI.DisabledScope(error!=null))
            {
                if(GUILayout.Button("Guardar prefab y crear escena explorable"))Run(()=>{var p=StructureBaker.Bake(definition);StructureWorkshopSetup.CreatePlayground(definition,p);AssetDatabase.SaveAssets();ShowNotification(new GUIContent("Prefab y escena guardados"));});
                if(GUILayout.Button("Abrir vista 3D en el editor"))Run(()=>OpenPreview(false));
                if(GUILayout.Button("Abrir escena para recorrer (Play)"))Run(()=>OpenPreview(true));
                if(GUILayout.Button("Registrar prefab en mundo"))Run(()=>{StructureWorkshopSetup.Register(definition,StructureBaker.Bake(definition));ShowNotification(new GUIContent("Registrado en WorldContentCatalog"));});
            }
        }
        Map();
        if(GUILayout.Button("Mostrar / ocultar carcasa de la estructura seleccionada"))
        {
            var instance=Selection.activeGameObject!=null?Selection.activeGameObject.GetComponentInParent<StructureInstance>():null;
            if(instance!=null){var renderers=instance.transform.Find("Shell")?.GetComponentsInChildren<Renderer>();if(renderers!=null&&renderers.Length>0){bool show=!renderers[0].enabled;foreach(var r in renderers){Undo.RecordObject(r,"Mostrar carcasa");r.enabled=show;}}}
        }
        scroll=EditorGUILayout.BeginScrollView(scroll);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))ContentPanel();
        if(inspector==null)inspector=UnityEditor.Editor.CreateEditor(definition);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))inspector.OnInspectorGUI();EditorGUILayout.EndScrollView();
    }
    void Run(Action action){try{action();}catch(Exception e){Debug.LogException(e);ShowNotification(new GUIContent(e.Message));}}
    void OpenPreview(bool play)
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        string path=StructureWorkshopSetup.ScenePath(definition);var p=StructureBaker.Bake(definition);StructureWorkshopSetup.CreatePlayground(definition,p);EditorSceneManager.OpenScene(path);
        if(play)EditorApplication.EnterPlaymode();else{Selection.activeObject=FindAnyObjectByType<StructureInstance>()?.gameObject;SceneView.lastActiveSceneView?.FrameSelected();}
    }
    void Map()
    {
        if(definition.rooms==null||definition.connections==null)return;
        float side=Mathf.Min(position.width-20,340);var rect=GUILayoutUtility.GetRect(side,side);rect.width=side;rect.x=(position.width-side)*.5f;
        EditorGUI.DrawRect(rect,new Color(.10f,.14f,.18f));
        Vector2 ToGUI(Vector2 p)=>rect.center+new Vector2(p.x,-p.y)*(side/72);
        Handles.BeginGUI();Handles.color=new Color(.35f,.65f,.70f);
        foreach(var c in definition.connections)if(c!=null&&c.from>=0&&c.to>=0&&c.from<definition.rooms.Count&&c.to<definition.rooms.Count&&definition.rooms[c.from]!=null&&definition.rooms[c.to]!=null)Handles.DrawAAPolyLine(3,ToGUI(definition.rooms[c.from].center),ToGUI(definition.rooms[c.to].center));
        for(int i=0;i<definition.rooms.Count;i++)
        {
            var r=definition.rooms[i];if(r==null)continue;var p=ToGUI(r.center);float radius=r.radius*side/72;
            Handles.color=r.role==StructureRoomRole.Final?new Color(1,.75f,.25f):r.role==StructureRoomRole.Entrance?Color.green:new Color(.35f,.85f,.85f);
            Handles.DrawWireDisc(p,Vector3.forward,radius);GUI.Label(new Rect(p.x-radius,p.y-9,radius*2,22),i+" "+r.name,EditorStyles.miniLabel);
            if(r.contents!=null)for(int j=0;j<r.contents.Count;j++)
            {
                var item=r.contents[j];if(item==null)continue;var at=ToGUI(r.center+item.offset);
                EditorGUI.DrawRect(new Rect(at.x-6,at.y-6,12,12),ContentColor(item.kind));
                Handles.color=ContentColor(item.kind);var forward=new Vector2(Mathf.Sin(item.yaw*Mathf.Deg2Rad),-Mathf.Cos(item.yaw*Mathf.Deg2Rad));
                Handles.DrawAAPolyLine(2,at,at+forward*18);Handles.DrawAAPolyLine(2,at+forward*12+new Vector2(-forward.y,forward.x)*4,at+forward*18,at+forward*12-new Vector2(-forward.y,forward.x)*4);
                if(i==selectedRoom&&j==selectedContent)Handles.DrawWireDisc(at,Vector3.forward,9);
            }
        }
        Handles.EndGUI();var ev=Event.current;
        if(EditorApplication.isPlayingOrWillChangePlaymode){dragging=-1;return;}
        if(ev.type==EventType.MouseDown&&ev.button==0&&rect.Contains(ev.mousePosition))
        {
            for(int i=0;i<definition.rooms.Count;i++)if(definition.rooms[i]?.contents!=null)for(int j=0;j<definition.rooms[i].contents.Count;j++)
            {var item=definition.rooms[i].contents[j];if(item!=null&&Vector2.Distance(ev.mousePosition,ToGUI(definition.rooms[i].center+item.offset))<9){selectedRoom=i;selectedContent=j;draggingContent=true;Undo.RecordObject(definition,"Mover contenido");ev.Use();Repaint();return;}}
            for(int i=0;i<definition.rooms.Count;i++)if(definition.rooms[i]!=null&&Vector2.Distance(ev.mousePosition,ToGUI(definition.rooms[i].center))<definition.rooms[i].radius*side/72){selectedRoom=i;selectedContent=-1;dragging=i;Undo.RecordObject(definition,"Mover sala");ev.Use();break;}
        }
        if(ev.type==EventType.MouseDrag&&draggingContent)
        {var p=(ev.mousePosition-rect.center)*72/side;var room=definition.rooms[selectedRoom];room.contents[selectedContent].offset=ClampOffset(new Vector2(Mathf.Round(p.x*2)/2,-Mathf.Round(p.y*2)/2)-room.center,room);EditorUtility.SetDirty(definition);ev.Use();Repaint();}
        if(ev.type==EventType.MouseDrag&&dragging>=0)
        {var p=(ev.mousePosition-rect.center)*72/side;definition.rooms[dragging].center=new Vector2(Mathf.Round(p.x*2)/2,-Mathf.Round(p.y*2)/2);EditorUtility.SetDirty(definition);ev.Use();Repaint();}
        if(ev.type==EventType.MouseUp){dragging=-1;draggingContent=false;}
    }
}
