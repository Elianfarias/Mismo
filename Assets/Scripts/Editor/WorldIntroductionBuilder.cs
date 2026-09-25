using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.World.Structures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class WorldIntroductionBuilder
{
    public const string DefinitionPath="Assets/Data/Tutorial/WorldIntroduction.asset";
    const string Prefabs="Assets/Art/Prefabs/World/Introduction";
    const float RefugeCottageYaw=45;
    [MenuItem("Mismo/Demo/Abrir inicio real (Nueva partida)")]
    public static void Open()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        Build();EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        Debug.Log("Entrá en Play y elegí Nueva partida para comenzar en la cueva de cristal. Continuar conserva tu mundo guardado.");
    }
    public static void Build()
    {
        var data=AssetDatabase.LoadAssetAtPath<WorldIntroductionDefinition>(DefinitionPath);
        if(data==null||data.narrator==null||data.encounters.Length!=6)
        {
            StructureBaker.Folder(Prefabs);
            if(data==null){data=ScriptableObject.CreateInstance<WorldIntroductionDefinition>();AssetDatabase.CreateAsset(data,DefinitionPath);}
            data.cave=BuildCave(out float radius);data.caveRadius=radius;
            data.grove=BuildGrove();data.narrator=BuildNarrator();
            data.arrival=Sequence("WorldArrival",Page("Tu vida","Despertaste en las galerías de cristal. Esta barra muestra tu vida. Si caés, reaparecés en el último tramo completado de esta introducción.",TutorialAnchor.Health),
                Page("Tu estamina","Correr con Shift, esquivar y algunas acciones gastan estamina. Dejá de gastarla un momento para recuperarla y reservá un poco para defenderte.",TutorialAnchor.Stamina),
                Page("Un camino hacia la superficie","Movete con WASD y mirá con el mouse. Seguí las salas iluminadas por cristales hasta encontrar la salida.",TutorialAnchor.None));
            data.sprint=Sequence("Sprint",Page("Correr con Shift","Mantené Shift mientras te movés con WASD para correr. Soltá Shift para caminar y recuperar estamina. Atravesá las siguientes galerías hasta la luz del día.",TutorialAnchor.Stamina));
            data.story=Sequence("Liria",Page("Liria · Guardiana del claro","Al fin alguien salió de las galerías. Soy Liria. Este claro todavía está a salvo, pero los caminos ya no lo están: los goblins han aislado nuestros pueblos.",TutorialAnchor.None),
                Page("El continente te necesita","Más allá de este bosque hay desiertos, montañas y tierras heladas. La amenaza crece en todas ellas. Ayudanos a salvar a sus habitantes y a recuperar los caminos que unen el continente.",TutorialAnchor.None),
                Page("Empezá por el pueblo","Seguí el sendero, despejá el paso de los goblins y hablá con los habitantes del primer pueblo. Sus encargos serán el comienzo de tu viaje. Llevás espada y arco: aprenderás a usarlos en el camino.",TutorialAnchor.None));
            data.story.completionLabel="Te ayudaré";
            data.village=Sequence("WorldVillage",Page("El primer pueblo","Este es el primer pueblo de tu continente. Acercate a sus habitantes y presioná F para conversar y aceptar encargos. J abre el diario de misiones. Tu inventario, ubicación y avance se guardan en esta partida.",TutorialAnchor.None));
            var focus=Sequence("WorldFocus",Page("Tensar el arco","Mantené el clic izquierdo para tensar el arco y aumentar la potencia del disparo. Soltalo para disparar; al llegar a la carga máxima la flecha se dispara automáticamente.",TutorialAnchor.Basic),
                Page("Generar Focus","Acertar un disparo básico genera {basicGain} Focus. Los disparos fallidos no lo generan. El borde de las habilidades que consumen Focus muestra cuánto te falta.",TutorialAnchor.Basic),
                Page("La habilidad Q del arco","Ahora tenés {q} en Q. Cuesta {focusCost} Focus: acertá disparos básicos para reunirlo y después presioná Q.",TutorialAnchor.Q));
            data.encounters=new[]{
                Encounter("Primer combate","M1 · Atacar    C · Esquivar\nDerrotá al goblin para despejar el camino.",-12,-217,"FirstFight",44),
                Encounter("Romper postura","Atacá al goblin y aprovechá su stagger.",14,-191,"Stagger",68),
                Encounter("Practicar parry","Espada: E justo antes del impacto.\nDerrotá al goblin para seguir.",-12,-165,"Parry",92),
                new WorldIntroductionEncounter{title="Equipar el arco",instruction="Tab alterna tus armas. Equipá el arco; I permite revisar tus conjuntos.",offset=new Vector2(8,-152),lesson=LoadLesson("Weapons"),requiredWeapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Bow/Bow.asset")},
                Encounter("Arco y Focus","Mantené M1 para tensar y soltá para disparar. Acertá para generar Focus y usar Q.",12,-139,null,119),
                Encounter("El último tramo","Combiná espada, arco y habilidades. Despejá el camino hasta el pueblo.",0,-114,"Combine",147,2)};
            data.encounters[4].lesson=focus;data.encounters[4].lessonWeapon=data.encounters[3].requiredWeapon;
            EditorUtility.SetDirty(data);EditorUtility.SetDirty(data.story);
        }
        var catalog=AssetDatabase.LoadAssetAtPath<WorldContentCatalog>("Assets/Data/World/WorldContentCatalog.asset");
        if(catalog.introduction!=data){catalog.introduction=data;EditorUtility.SetDirty(catalog);}
        DressCave(data);AssetDatabase.SaveAssets();
        Debug.Log("WORLD_INTRODUCTION_READY: MainMenu > Nueva partida > galerías de cristal > EnchantedGrove > pueblo procedural.");
    }
    static GameObject BuildCave(out float radius)
    {
        const string path="Assets/Data/World/Structures/Cave_Crystal_Introduction.asset";
        var existing=AssetDatabase.LoadAssetAtPath<StructureDefinition>(path);
        var baked=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/Structures/Cave_Crystal_Introduction.prefab");
        if(existing!=null&&baked!=null){radius=existing.Radius;return baked;}
        var d=Object.Instantiate(AssetDatabase.LoadAssetAtPath<StructureDefinition>("Assets/Data/World/Structures/Cave_Crystal.asset"));
        d.name="Cave_Crystal_Introduction";d.displayName="Galerías del despertar";d.persistentIntroduction=true;d.placement=StructurePlacement.CompatibleSites;
        d.voxelSize=.65f;d.mountainHeight=22;d.decorationDensity=.65f;d.finalExperience=0;d.rewardVisual=null;
        d.rooms=new List<StructureRoom>();d.connections=new List<StructureConnection>();
        var centers=new[]{new Vector2(0,-36),new Vector2(-9,-22),new Vector2(8,-7),new Vector2(-8,8),new Vector2(8,23),new Vector2(0,36)};
        for(int i=0;i<centers.Length;i++)
        {
            d.rooms.Add(new StructureRoom{name=i==5?"El despertar":i==0?"Luz del día":"Galería de cristales "+i,center=centers[i],radius=i==5?8:7,height=8,
                role=i==0?StructureRoomRole.Entrance:i==5?StructureRoomRole.Final:StructureRoomRole.Chamber});
            if(i>0)d.connections.Add(new StructureConnection{from=i-1,to=i,width=6});
        }
        AssetDatabase.CreateAsset(d,path);radius=d.Radius;
        var prefab=StructureBaker.Bake(d);string prefabPath=AssetDatabase.GetAssetPath(prefab);
        var root=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var reward=root.transform.Find("Final room reward");if(reward!=null)Object.DestroyImmediate(reward.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }
    static GameObject BuildNarrator()
    {
        var root=new GameObject("Liria");
        try
        {
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Voxelized/NPC/Craftpix/NPC_peasant_5.prefab"),root.transform);
            Fit(visual,1.85f);Ground(visual,Vector3.zero);
            var collider=root.AddComponent<CapsuleCollider>();collider.height=1.8f;collider.radius=.28f;collider.center=Vector3.up*.9f;
            return PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/Liria.prefab");
        }
        finally{Object.DestroyImmediate(root);}
    }
    public static void RefreshNarrator()
    {var data=AssetDatabase.LoadAssetAtPath<WorldIntroductionDefinition>(DefinitionPath);data.narrator=BuildNarrator();EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();}
    static void DressCave(WorldIntroductionDefinition data)
    {
        string path=AssetDatabase.GetAssetPath(data.cave);var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            if(root.transform.Find("Guiding crystals")!=null)return;
            var d=AssetDatabase.LoadAssetAtPath<StructureDefinition>("Assets/Data/World/Structures/Cave_Crystal_Introduction.asset");
            var decoration=root.transform.Find("Decoration");
            if(decoration!=null&&d.finalLandmark!=null)
                foreach(Transform item in decoration.Cast<Transform>().ToArray())
                    if(PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject)==d.finalLandmark)Object.DestroyImmediate(item.gameObject);
            var crystals=new GameObject("Guiding crystals").transform;crystals.SetParent(root.transform,false);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/CaveKit/C03_Racimo_alto.prefab");
            foreach(var room in d.rooms)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(source,crystals);Fit(go,2.1f);
                Ground(go,new Vector3(room.center.x+room.radius-1.3f,.05f,room.center.y));
                foreach(var c in go.GetComponentsInChildren<Collider>())c.enabled=false;
            }
            root.GetComponent<StructureInstance>().ambientMultiplier=.24f;
            foreach(var light in root.GetComponentsInChildren<Light>()){light.intensity=7;light.color=new Color(.36f,.66f,1);}
            d.finalLandmark=null;d.interiorAmbient=.24f;EditorUtility.SetDirty(d);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        data.encounters[4].lessonWeapon=data.encounters[3].requiredWeapon;EditorUtility.SetDirty(data);
    }
    static GameObject BuildGrove()
    {
        var root=new GameObject("EnchantedGrove refuge");
        try
        {
            Place(root,"Cottage_Exterior",new Vector3(8,0,7),RefugeCottageYaw,5.5f);
            Place(root,"Ruin_Arch",new Vector3(-8,0,-8),20,5.5f);
            Place(root,"Ruin_Wall",new Vector3(-12,0,5),0,2.8f);
            Place(root,"Ruin_Pillar",new Vector3(-10,0,11),20,4);
            Place(root,"Tree_Sage",new Vector3(-14,0,12),0,10);
            Place(root,"Tree_Amber",new Vector3(13,0,-9),30,8);
            Place(root,"Tree_Blossom",new Vector3(3,0,15),0,8);
            Place(root,"Tree_Blossom",new Vector3(-13,0,-5),20,7);
            Place(root,"Tree_Cypress",new Vector3(16,0,8),0,9);
            Place(root,"Lantern_Post",new Vector3(-2,0,-3),0,2.5f);
            Place(root,"Lantern_Ground",new Vector3(3,0,1),0,.8f);
            Place(root,"Fireflies",new Vector3(-5,1,5),0,1);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16;var p=new Vector3(Mathf.Cos(a)*17,0,Mathf.Sin(a)*17);
                Place(root,i%3==0?"Shrub_Hydrangea":i%3==1?"Flowers_Coral":"Fern",p,i*31,i%3==0?1.8f:.8f);
            }
            return PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/EnchantedGrove_Refuge.prefab");
        }
        finally{Object.DestroyImmediate(root);}
    }
    static void Place(GameObject root,string asset,Vector3 position,float yaw,float height,float width=0)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/EnchantedGrove/"+asset+".prefab");
        if(source==null)throw new InvalidOperationException("Falta EnchantedGrove/"+asset);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);go.transform.localRotation=Quaternion.Euler(0,yaw,0);
        if(width>0){var b=Bounds(go);go.transform.localScale*=width/Mathf.Max(.01f,b.size.x,b.size.z);}else Fit(go,height);
        Ground(go,position);
        if(asset=="Fireflies")go.transform.localPosition=position;
    }
    public static void FaceCottageToClearing()
    {
        string path=Prefabs+"/EnchantedGrove_Refuge.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var cottage=root.transform.Find("Cottage_Exterior");
            if(cottage==null)throw new InvalidOperationException("Falta la casa del refugio.");
            // The existing door and windows are on local -Z. Face them toward Liria at the centre.
            cottage.localRotation=Quaternion.Euler(0,RefugeCottageYaw,0);
            Ground(cottage.gameObject,new Vector3(8,0,7));
            PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
            Debug.Log("REFUGE_COTTAGE_FACING_CLEARING: "+cottage.localEulerAngles.y);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static void RemoveRefugePond()
    {
        string path=Prefabs+"/EnchantedGrove_Refuge.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var names=new[]{"Pond_Water","Lily_Pads","Footbridge"};int removed=0;
            foreach(var child in root.transform.Cast<Transform>().ToArray())
                if(names.Contains(child.name)){Object.DestroyImmediate(child.gameObject);removed++;}
            PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
            Debug.Log("REFUGE_POND_REMOVED: "+removed+" objects; remaining="+root.transform.Cast<Transform>().Count(t=>names.Contains(t.name)));
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static Bounds Bounds(GameObject go)
    {var renderers=go.GetComponentsInChildren<Renderer>();var bounds=new Bounds(go.transform.position,Vector3.zero);if(renderers.Length>0){bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);}return bounds;}
    static void Fit(GameObject go,float height){float h=Bounds(go).size.y;if(h>.01f)go.transform.localScale*=height/h;}
    static void Ground(GameObject go,Vector3 position){var b=Bounds(go);go.transform.position+=position-new Vector3(b.center.x,b.min.y,b.center.z);}
    static TutorialPage Page(string title,string body,TutorialAnchor anchor)=>new TutorialPage{title=title,body=body,highlight=anchor};
    static TutorialSequence Sequence(string name,params TutorialPage[] pages)
    {string path="Assets/Data/Tutorial/"+name+".asset";var s=AssetDatabase.LoadAssetAtPath<TutorialSequence>(path);if(s!=null)return s;s=ScriptableObject.CreateInstance<TutorialSequence>();s.id="world.introduction."+name.ToLowerInvariant();s.pages=pages;AssetDatabase.CreateAsset(s,path);return s;}
    static TutorialSequence LoadLesson(string name)=>AssetDatabase.LoadAssetAtPath<TutorialSequence>("Assets/Data/Tutorial/"+name+".asset");
    static WorldIntroductionEncounter Encounter(string title,string instruction,float x,float z,string lesson,int settingsId,int count=1)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/Goblin.prefab");
        var root=(GameObject)PrefabUtility.InstantiatePrefab(source);
        GameObject prefab;
        try
        {
            var serialized=new SerializedObject(root.GetComponent<GoblinController>());
            serialized.FindProperty("settings").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GoblinSettings>("Assets/Data/Tutorial/Goblin"+settingsId+".asset");serialized.ApplyModifiedPropertiesWithoutUndo();
            prefab=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/Goblin_"+settingsId+".prefab");
        }
        finally{Object.DestroyImmediate(root);}
        return new WorldIntroductionEncounter{title=title,instruction=instruction,offset=new Vector2(x,z),lesson=lesson==null?null:LoadLesson(lesson),prefab=prefab,count=count};
    }
}
