using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class DragonArcSetup
    {
        public const string DataPath="Assets/Data/World/DragonArc/DragonArc.asset";
        const string Prefabs="Assets/Art/Prefabs/World/DragonArc";
        public static T Load<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing "+path);
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        static T Data<T>(string path) where T:ScriptableObject
        {
            var data=AssetDatabase.LoadAssetAtPath<T>(path);if(data!=null)return data;
            Folder(Path.GetDirectoryName(path).Replace('\\','/'));data=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(data,path);return data;
        }
        static QuestObjective Signal(string id,string label,string signal,bool previous=false)=>new QuestObjective{id=id,label=label,kind=QuestObjectiveKind.Signal,signal=signal,requirePrevious=previous,consumeOnDelivery=false};
        [MenuItem("Mismo/Historia/Preparar arco del dragón")]
        public static void Run()
        {
            Folder(Prefabs);
            var data=Data<DragonArcDefinition>(DataPath);
            var npc=Data<QuestNpcDefinition>("Assets/Data/Quests/NPCs/dragon-king.asset");
            if(string.IsNullOrEmpty(npc.id)){npc.id="dragon-king";npc.displayName="El rey";npc.occupation="Protector del pueblo";EditorUtility.SetDirty(npc);}
            var audience=Data<QuestDefinition>("Assets/Data/Quests/Missions/Q-DRAGON-AUDIENCE.asset");
            if(string.IsNullOrEmpty(audience.id))
            {
                audience.id="Q-DRAGON-AUDIENCE";audience.title="Una sombra sobre el pueblo";audience.npc=npc;audience.category=QuestCategory.Eventos;audience.offerInJournal=false;
                audience.description="Después de despejar la entrada, viste a Soul Eater sobrevolar el pueblo. Escuchá al vigía y hablá con el rey para entender qué amenaza a la región.";
                audience.location="Primer pueblo";audience.objectives=new[]{Signal("warning","Escuchá al vigía junto a la entrada","dragon.warning"),Signal("audience","Hablá con el rey","dragon.audience",true)};
                audience.completedText="El rey te confió el ritual de los tres faros.";EditorUtility.SetDirty(audience);
            }
            var awakening=Data<QuestDefinition>("Assets/Data/Quests/Missions/Q-DRAGON-AWAKENING.asset");
            if(string.IsNullOrEmpty(awakening.id))
            {
                awakening.id="Q-DRAGON-AWAKENING";awakening.title="El despertar de la montaña";awakening.npc=npc;awakening.category=QuestCategory.Eventos;awakening.offerInJournal=false;
                awakening.description="Los monstruos avanzan y Soul Eater ya no responde a las ofrendas. Reactivá los tres faros antiguos en cualquier orden. Después, preparate y confirmá el ritual en el altar para llamar al dragón.";
                awakening.location="Región del primer pueblo";awakening.prerequisites=new[]{audience};
                awakening.objectives=new[]{Signal("forest","Reactivá el faro del bosque",DragonArcRules.BeaconSignals[0]),Signal("ruins","Reactivá el faro de las ruinas",DragonArcRules.BeaconSignals[1]),Signal("gorge","Reactivá el faro de la quebrada",DragonArcRules.BeaconSignals[2]),Signal("summon","Invocá a Soul Eater desde el altar","dragon.summoned",true)};
                awakening.clue="El rey marcó las ubicaciones en tu mapa (M). Acercate a cada faro y presioná F. Los faros activos se conservan si caés o continuás la partida. Entrar al claro no inicia el combate: confirmá el ritual en el altar.";
                awakening.completedText="El ritual funcionó: Soul Eater respondió a la llamada. Esta misión registra la invocación; el combate todavía debe resolverse.";EditorUtility.SetDirty(awakening);
            }
            data.audience=audience;data.awakening=awakening;
            if(string.IsNullOrEmpty(npc.greeting)){npc.greeting="Los caminos ya no son seguros. Necesitamos entender qué alteró a Soul Eater.";EditorUtility.SetDirty(npc);}
            if(string.IsNullOrEmpty(audience.offerText)){audience.offerText=data.warning;EditorUtility.SetDirty(audience);}
            if(string.IsNullOrEmpty(awakening.offerText)){awakening.offerText=data.audienceText;EditorUtility.SetDirty(awakening);}
            if(data.king==null)data.king=Npc("DragonKing","king",2.85f);
            if(data.lookout==null)data.lookout=Npc("VillageLookout","peasant_3",2.65f);
            data.flyingVisual=Load<GameObject>("Assets/Art/Prefabs/Voxelized/SoulEater_Green_Animated.prefab");
            data.flight=Load<AnimationClip>("Assets/Art/Animations/Voxelized/SoulEater_Animated_Animations/Fly Forward.anim");
            if(data.altar==null)data.altar=Altar();
            string encounterPath=Prefabs+"/SoulEaterSummoning.prefab";
            if(!File.Exists(encounterPath))
            {
                var root=new GameObject("Soul Eater summoning");
                try
                {
                    var encounter=root.AddComponent<DragonRegionalEncounter>();encounter.bossPrefab=Load<GameObject>("Assets/Art/Prefabs/DragonBosses/SoulEater_PhaseOne.prefab").GetComponent<SoulEaterPhaseOneController>();
                    encounter.arrivalVisual=data.flyingVisual;encounter.flight=data.flight;
                    PrefabUtility.SaveAsPrefabAsset(root,encounterPath);
                }
                finally{Object.DestroyImmediate(root);}
            }
            var encounterContents=PrefabUtility.LoadPrefabContents(encounterPath);
            try
            {
                var encounter=encounterContents.GetComponent<DragonRegionalEncounter>();
                if(encounter.landingSound==null)encounter.landingSound=Load<AudioClip>("Assets/Art/Audio/Enemies/SoulEater/SoulEater_Land.wav");
                if(encounter.roarSound==null)encounter.roarSound=Load<AudioClip>("Assets/Art/Audio/Enemies/SoulEater/SoulEater_Roar.wav");
                PrefabUtility.SaveAsPrefabAsset(encounterContents,encounterPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(encounterContents);}
            data.encounterPrefab=Load<GameObject>(encounterPath);EditorUtility.SetDirty(data);
            var quests=Load<QuestCatalog>("Assets/Data/Quests/QuestCatalog.asset");
            quests.quests=quests.quests.Concat(new[]{audience,awakening}).Distinct().ToArray();EditorUtility.SetDirty(quests);
            var catalog=Load<WorldContentCatalog>("Assets/Data/World/WorldContentCatalog.asset");catalog.dragonArc=data;EditorUtility.SetDirty(catalog);
            if(!data.Valid)throw new InvalidOperationException("Invalid dragon arc definitions");
            AssetDatabase.SaveAssets();
            Debug.Log("DRAGON_ARC_SETUP_OK · Soul Eater / 2 phases / no guardians / beacon art pending");
            DragonArcChecks.Run();
        }
        static GameObject Npc(string name,string source,float height)
        {
            string path=Prefabs+"/"+name+".prefab";if(File.Exists(path))return Load<GameObject>(path);
            var root=new GameObject(name);
            try
            {
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Art/Prefabs/Voxelized/NPC/Craftpix/NPC_"+source+".prefab"),root.transform);
                foreach(var c in visual.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
                foreach(var a in visual.GetComponentsInChildren<Animator>())a.enabled=false;
                foreach(var bone in visual.GetComponentsInChildren<Transform>().Where(t=>t.name=="Upperarm_L"||t.name=="Upperarm_R"))
                {
                    var lower=bone.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("Lowerarm_"));if(lower==null)continue;
                    float side=Mathf.Sign((lower.position-bone.position).x);bone.rotation=Quaternion.FromToRotation(lower.position-bone.position,new Vector3(side*.18f,-1,.04f))*bone.rotation;
                }
                var skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();skin.BakeMesh(mesh);var bounds=mesh.bounds;Object.DestroyImmediate(mesh);
                float scale=height/bounds.size.y;visual.transform.localScale=Vector3.one*scale;visual.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;
                var body=root.AddComponent<CapsuleCollider>();body.center=Vector3.up*height*.5f;body.height=height;body.radius=.4f;
                root.AddComponent<NpcGrounding>();var idle=root.AddComponent<VillageNpcIdle>();
                idle.spine=visual.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Spine_02");idle.head=visual.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Neck_01");
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{Object.DestroyImmediate(root);}
        }
        static GameObject Altar()
        {
            string path=Prefabs+"/DragonSummoningAltar.prefab";if(File.Exists(path))return Load<GameObject>(path);
            Folder("Assets/Art/Materials/World/DragonArc");
            Material Mat(string name,Color color)
            {
                string p="Assets/Art/Materials/World/DragonArc/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m!=null)return m;
                m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(m,p);return m;
            }
            var stone=Mat("RitualStone",new Color(.15f,.19f,.17f));var metal=Mat("RitualInlay",new Color(.63f,.52f,.27f));
            var root=new GameObject("Altar de la llamada");
            try
            {
                void Block(string label,Vector3 p,Vector3 size,Material m)
                {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=label;g.transform.SetParent(root.transform,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;}
                Block("Plinto",new Vector3(0,.2f,0),new Vector3(3,.4f,3),stone);
                Block("Mesa del ritual",new Vector3(0,.75f,0),new Vector3(1.7f,.7f,1.7f),stone);
                for(int i=0;i<3;i++)Block("Sello "+i,new Vector3((i-1)*.45f,1.12f,0),new Vector3(.24f,.04f,.5f),metal);
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
