using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.World.Structures;
using UnityEditor;
using UnityEngine;

public sealed partial class StructureWorkshopWindow
{
    int selectedRoom,selectedContent=-1;
    bool draggingContent;
    static Color ContentColor(StructureContentKind kind)=>kind==StructureContentKind.Enemy?new Color(1,.3f,.3f):kind==StructureContentKind.Chest?new Color(1,.8f,.2f):kind==StructureContentKind.Resource?Color.cyan:Color.white;
    Vector2 ClampOffset(Vector2 offset,StructureRoom room)
    {float r=room.radius-1;return definition.style==StructureStyle.Temple||definition.style==StructureStyle.Ruin?new Vector2(Mathf.Clamp(offset.x,-r,r),Mathf.Clamp(offset.y,-r,r)):Vector2.ClampMagnitude(offset,r);}
    void ContentPanel()
    {
        if(definition.rooms.Count==0)return;
        EditorGUILayout.Space();EditorGUILayout.LabelField("Contenido por sala",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Rojo: monstruo · Amarillo: cofre · Celeste: mena · Blanco: decoracion. Seleccionar y arrastrar los puntos en el plano. La posicion es relativa a la sala.",MessageType.Info);
        selectedRoom=Mathf.Clamp(selectedRoom,0,definition.rooms.Count-1);
        int roomIndex=EditorGUILayout.Popup("Sala",selectedRoom,definition.rooms.Select((r,i)=>i+" · "+r.name).ToArray());
        if(roomIndex!=selectedRoom){selectedRoom=roomIndex;selectedContent=-1;}
        var room=definition.rooms[selectedRoom];
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("+ Monstruo"))AddContent(room,StructureContentKind.Enemy);
        if(GUILayout.Button("+ Cofre"))AddContent(room,StructureContentKind.Chest);
        if(GUILayout.Button("+ Mena"))AddContent(room,StructureContentKind.Resource);
        if(GUILayout.Button("+ Decoracion"))AddContent(room,StructureContentKind.Decoration);
        EditorGUILayout.EndHorizontal();
        if(room.contents.Count>0)
        {
            selectedContent=Mathf.Clamp(selectedContent,0,room.contents.Count-1);
            selectedContent=EditorGUILayout.Popup("Objeto",selectedContent,room.contents.Select((c,i)=>i+" · "+(c?.label??"Vacio")).ToArray());
            var item=room.contents[selectedContent];if(item==null)return;
            Undo.RecordObject(definition,"Editar contenido de sala");EditorGUI.BeginChangeCheck();
            item.label=EditorGUILayout.TextField("Nombre",item.label);
            item.offset=ClampOffset(EditorGUILayout.Vector2Field("Posicion X / Z",item.offset),room);
            item.yaw=EditorGUILayout.Slider("Orientacion",item.yaw,0,360);
            item.scale=EditorGUILayout.Slider("Escala",item.scale,.25f,3);
            if(item.kind!=StructureContentKind.Resource)item.prefab=(GameObject)EditorGUILayout.ObjectField(item.kind==StructureContentKind.Enemy?"Prefab enemigo completo":"Modelo",item.prefab,typeof(GameObject),false);
            if(item.kind==StructureContentKind.Enemy)
            {
                item.enemyLevel=Mathf.Clamp(EditorGUILayout.IntField("Nivel (0 = region)",item.enemyLevel),0,1000);
                item.requiredForClear=EditorGUILayout.Toggle("Cuenta como guardian",item.requiredForClear);
                EditorGUILayout.HelpBox("Elegir un enemigo con IA, vida y navegacion. Un modelo visual solo no puede combatir. Su prefab conserva ataques y recompensas.",MessageType.None);
            }
            if(item.kind==StructureContentKind.Chest)
            {
                item.loot=(MaterialLootTable)EditorGUILayout.ObjectField("Tabla de materiales",item.loot,typeof(MaterialLootTable),false);
                item.weapon=(WeaponDefinition)EditorGUILayout.ObjectField("Arma garantizada",item.weapon,typeof(WeaponDefinition),false);
                if(item.weapon!=null)item.weaponTier=EditorGUILayout.IntSlider("Tier del arma",item.weaponTier,1,5);
                item.experience=Mathf.Clamp(EditorGUILayout.IntField("Experiencia",item.experience),0,1000000);
                item.requirement=(StructureRequirement)EditorGUILayout.Popup("Desbloqueo",(int)item.requirement,new[]{"Siempre","Guardianes de esta sala","Guardianes de toda la estructura"});
                EditorGUILayout.HelpBox("F abre el cofre. El botin queda para recoger y se conserva si la mochila esta llena. Cada cofre se abre una sola vez.",MessageType.None);
            }
            if(item.kind==StructureContentKind.Resource)
            {
                item.resource=(ResourceNodeDefinition)EditorGUILayout.ObjectField("Configuracion de mena",item.resource,typeof(ResourceNodeDefinition),false);
                item.resourceOneTime=EditorGUILayout.Toggle("Se agota para siempre",item.resourceOneTime);
                item.requirement=(StructureRequirement)EditorGUILayout.Popup("Disponible tras",(int)item.requirement,new[]{"Siempre","Guardianes de esta sala","Guardianes de toda la estructura"});
                if(item.resource!=null)
                {
                    EditorGUILayout.LabelField(item.resource.displayName+" · extraccion "+item.resource.harvestSeconds+" s"+(item.resourceOneTime?"":" · regenera en "+item.resource.regenerationSeconds+" s"));
                    EditorGUILayout.BeginHorizontal();
                    if(GUILayout.Button("Crear variante propia"))CloneResource(item);
                    if(GUILayout.Button("Editar configuracion compartida"))Selection.activeObject=item.resource;
                    EditorGUILayout.EndHorizontal();
                }
            }
            if(EditorGUI.EndChangeCheck())EditorUtility.SetDirty(definition);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Duplicar objeto")){Undo.RecordObject(definition,"Duplicar contenido");var copy=item.Duplicate();copy.offset=ClampOffset(copy.offset+Vector2.right*1.5f,room);room.contents.Add(copy);selectedContent=room.contents.Count-1;EditorUtility.SetDirty(definition);}
            if(GUILayout.Button("Quitar objeto")){Undo.RecordObject(definition,"Quitar contenido");room.contents.RemoveAt(selectedContent);selectedContent=-1;EditorUtility.SetDirty(definition);}
            EditorGUILayout.EndHorizontal();
            int targetRoom=EditorGUILayout.Popup("Mover a otra sala",selectedRoom,definition.rooms.Select(r=>r.name).ToArray());
            if(targetRoom!=selectedRoom&&selectedContent>=0)
            {Undo.RecordObject(definition,"Mover contenido a sala");room.contents.Remove(item);var target=definition.rooms[targetRoom];item.offset=ClampOffset(item.offset,target);target.contents.Add(item);selectedRoom=targetRoom;selectedContent=target.contents.Count-1;EditorUtility.SetDirty(definition);}
        }
        EditorGUI.BeginChangeCheck();var requirement=(StructureRequirement)EditorGUILayout.Popup("Recompensa final",(int)definition.finalRequirement,new[]{"Al llegar","Limpiar la sala final","Limpiar toda la estructura"});
        if(EditorGUI.EndChangeCheck()){Undo.RecordObject(definition,"Cambiar condicion final");definition.finalRequirement=requirement;EditorUtility.SetDirty(definition);}
        EditorGUILayout.Space();EditorGUILayout.LabelField("Geometria y configuracion avanzada",EditorStyles.boldLabel);
    }
    void AddContent(StructureRoom room,StructureContentKind kind)
    {
        Undo.RecordObject(definition,"Agregar contenido");
        var entry=StructureContentSetup.DefaultEntry(kind);entry.offset=ClampOffset(new Vector2((room.contents.Count%3-1)*1.5f,1.5f),room);
        room.contents.Add(entry);selectedContent=room.contents.Count-1;EditorUtility.SetDirty(definition);
    }
    void CloneResource(StructureContentEntry entry)
    {
        string folder=StructureWorkshopSetup.DefRoot+"Resources";StructureBaker.Folder(folder);
        string path=EditorUtility.SaveFilePanelInProject("Crear variante de mena","SpecialOre","asset","Nombre de esta mena",folder);if(string.IsNullOrEmpty(path))return;
        if(!path.StartsWith("Assets/Data/")){ShowNotification(new GUIContent("Guardar configuraciones en Assets/Data"));return;}
        Undo.RecordObject(definition,"Crear variante de mena");
        var copy=Instantiate(entry.resource);copy.id="structure-ore-"+System.Guid.NewGuid().ToString("N");
        if(copy.rewards!=null){var loot=Instantiate(copy.rewards);AssetDatabase.CreateAsset(loot,AssetDatabase.GenerateUniqueAssetPath(path.Replace(".asset","-Loot.asset")));copy.rewards=loot;}
        AssetDatabase.CreateAsset(copy,path);entry.resource=copy;EditorUtility.SetDirty(definition);AssetDatabase.SaveAssets();Selection.activeObject=copy;
    }
}
