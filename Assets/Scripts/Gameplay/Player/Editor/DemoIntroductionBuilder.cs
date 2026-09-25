using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Camera;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class DemoIntroductionBuilder
    {
        public const string ScenePath="Assets/Scenes/DemoIntroduction.unity";
        public const string DataPath="Assets/Data/Tutorial";
        const string MaterialsPath="Assets/Art/Materials/DemoIntroduction";

        [MenuItem("Mismo/Demo/Prototipo aislado (anterior)")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Build();EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Mismo/Demo/Probar guia de interfaz en Play")]
        public static void Preview()
        {
            if(!Application.isPlaying){Debug.Log("Entrá en Play y volvé a elegir Probar guía de interfaz.");return;}
            var player=Object.FindAnyObjectByType<PlayerController>();if(player==null)return;
            var guide=player.GetComponent<TutorialGuide>()??player.gameObject.AddComponent<TutorialGuide>();
            var sequence=AssetDatabase.LoadAssetAtPath<TutorialSequence>(DataPath+"/Interface.asset");
            if(sequence==null){Debug.LogWarning("Primero creá la introducción desde Mismo > Demo.");return;}
            if(!guide.TryBegin(sequence,true))Debug.Log("Cerrá los menús o la guía actual antes de repetirla.");
        }

        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Salir de Play antes de crear la demo.");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)return;
            Folder(DataPath);Folder(MaterialsPath);
            var life=Sequence("Arrival",Page("Tu vida", "Esta barra muestra cuánta vida te queda. Los golpes enemigos la reducen. En esta demo, si caés, volvés a comenzar en la cueva.",TutorialAnchor.Health),
                Page("Tu estamina","Correr, esquivar y algunas acciones gastan estamina. Dejá de gastarla un momento para recuperarla; reservá un poco para defenderte.",TutorialAnchor.Stamina),
                Page("Salí de la cueva","Movete con WASD y mirá alrededor con el mouse. Seguí el sendero: al fondo te espera el primer pueblo.",TutorialAnchor.None));
            var basics=Sequence("FirstFight",Page("Un goblin en el camino","Acercate y atacá con el botón izquierdo del mouse. Observá su preparación antes del golpe y dejá espacio para reaccionar.",TutorialAnchor.Basic),
                Page("Esquivar","C activa la acción especial de tu cinturón. Usala mientras te movés para salir del ataque del goblin. Revisá la estamina y el tiempo de recarga antes de repetirla.",TutorialAnchor.Dash));
            var posture=Sequence("Stagger",Page("Rompé su postura","Los golpes también dañan la postura del enemigo. Cuando se rompe, el goblin queda vulnerable durante un momento: ese es el stagger. Aprovechá esa apertura para atacar.",TutorialAnchor.None));
            var parry=Sequence("Parry",Page("Parry con espada","Con la espada inicial, E activa la parada. Usala justo antes de que conecte el golpe del goblin. Un parry acertado te da una oportunidad de contraatacar. Podés ganar este encuentro aunque todavía no te salga.",TutorialAnchor.E));
            var weapons=Sequence("Weapons",Page("Cambiar de arma","Tab alterna entre tus dos conjuntos de armas. Al cambiar, también cambian sus habilidades. Equipá el arco para el próximo tramo.",TutorialAnchor.Weapons),
                Page("Habilidades del arma","M1 es tu ataque básico. Q, E y R son los espacios de habilidades del arma equipada. Sus iconos muestran recarga y disponibilidad.",TutorialAnchor.Skills));
            var focus=Sequence("Focus",Page("Generar Focus con el arco","Acertar un disparo básico del arco genera {basicGain} Focus. Los disparos fallidos no lo generan. El borde de las habilidades que consumen Focus muestra cuánto te falta para poder usarlas.",TutorialAnchor.Basic),
                Page("Tu habilidad Q","Ahora tenés {q} en Q. Cuesta {focusCost} Focus: acertá disparos básicos para reunirlo y después presioná Q. La habilidad consume ese recurso.",TutorialAnchor.Q));
            var free=Sequence("Combine",Page("Ponelo en práctica","Dos goblins custodian el último tramo. Alterná espada y arco, esquivá, aprovechá el stagger y usá tus habilidades cuando estén disponibles.",TutorialAnchor.Weapons));
            var village=Sequence("Village",Page("El primer pueblo","Acercate a un habitante y presioná F para conversar y aceptar un pedido. J abre tu diario. Esta demo usa una partida temporal: tu aventura guardada queda intacta.",TutorialAnchor.None));
            Sequence("Interface",life.pages.Concat(weapons.pages).Concat(focus.pages).ToArray());
            var source="Assets/Scenes/BossArena.unity";
            if(!AssetDatabase.CopyAsset(source,ScenePath))throw new InvalidOperationException("Falta la escena base BossArena.");
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach(var existingRoot in scene.GetRootGameObjects())
                    if(existingRoot.GetComponentInChildren<PlayerController>(true)==null&&existingRoot.GetComponentInChildren<UnityEngine.Camera>(true)==null&&existingRoot.GetComponent<Light>()==null)Object.DestroyImmediate(existingRoot);
                var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerController>(true)).Single();
                if(player.GetComponent<RegionRespawn>()!=null)Object.DestroyImmediate(player.GetComponent<RegionRespawn>());
                player.transform.SetPositionAndRotation(new Vector3(0,.15f,0),Quaternion.identity);
                var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ThirdPersonCamera>(true)).First();
                camera.Configure(player.transform,player.GetComponent<Input.PlayerInputReader>());
                var guide=player.GetComponent<TutorialGuide>()??player.gameObject.AddComponent<TutorialGuide>();
                var root=new GameObject("Demo Introduction");
                var geometry=Group("RegionGeometry",root.transform);
                var grass=Material("Grass",new Color(.22f,.32f,.21f));var stone=Material("Stone",new Color(.25f,.29f,.30f));
                var dirt=Material("Path",new Color(.49f,.40f,.27f));var wood=Material("Wood",new Color(.30f,.19f,.11f));
                Box("Ground",new Vector3(0,-1,90),new Vector3(56,2,220),grass,geometry);
                Box("Sendero al pueblo",new Vector3(0,.025f,94),new Vector3(7,.05f,198),dirt,geometry);
                Box("Ladera oeste",new Vector3(-27,7,90),new Vector3(4,14,220),stone,geometry);
                Box("Ladera este",new Vector3(27,7,90),new Vector3(4,14,220),stone,geometry);
                Box("Fondo de la cueva",new Vector3(0,7,-17),new Vector3(54,14,3),stone,geometry);
                Box("Cueva oeste",new Vector3(-11,6,0),new Vector3(12,12,32),stone,geometry);
                Box("Cueva este",new Vector3(11,6,0),new Vector3(12,12,32),stone,geometry);
                Box("Techo de la cueva",new Vector3(0,12,0),new Vector3(30,2,32),stone,geometry);
                Box("Límite del pueblo",new Vector3(0,7,197),new Vector3(54,14,3),stone,geometry);
                for(int i=0;i<9;i++)
                {
                    float x=i%2==0?-17:18,z=27+i*17;
                    Box("Tronco",new Vector3(x,2,z),new Vector3(1,4,1),wood,geometry);
                    Box("Copa provisional",new Vector3(x,5,z),new Vector3(5,4,5),grass,geometry);
                }
                var intro=root.AddComponent<DemoIntroduction>();intro.player=player;intro.guide=guide;intro.view=camera.GetComponent<UnityEngine.Camera>();
                var stages=new List<DemoStage>();
                stages.Add(Stage(root.transform,"Salir de la cueva","WASD · Moverte\nMouse · Mirar alrededor",0,life));
                stages.Add(Stage(root.transform,"Primer combate","Atacá con M1 y esquivá con C.\nH · Volver a leer la explicación",30,basics,Encounter(root.transform,44,90,60,1)));
                stages.Add(Stage(root.transform,"Romper postura","Atacá al goblin y aprovechá el stagger.\nH · Volver a leer la explicación",57,posture,Encounter(root.transform,68,145,32,1)));
                stages.Add(Stage(root.transform,"Practicar parry","Espada: E justo antes del impacto.\nDerrotá al goblin para seguir.",81,parry,Encounter(root.transform,92,110,55,1)));
                var swap=Stage(root.transform,"Equipar el arco","Presioná Tab para cambiar al arco.",104,weapons);
                swap.objective=DemoObjective.EquipWeapon;swap.weapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Bow/Bow.asset");stages.Add(swap);
                stages.Add(Stage(root.transform,"Arco y Focus","Acertá disparos con M1 y usá Q.\nDerrotá al goblin para seguir.",104,focus,Encounter(root.transform,119,180,70,1)));
                stages.Add(Stage(root.transform,"Último encuentro","Combiná tus armas y habilidades.\nEl pueblo está al final del sendero.",135,free,Encounter(root.transform,147,80,55,2)));
                stages.Add(Stage(root.transform,"Llegar al pueblo","Seguí el camino hasta las casas.",174,village));intro.stages=stages.ToArray();
                for(int i=0;i<4;i++)
                {
                    float x=i%2==0?-12:12,z=171+(i/2)*16;
                    Box("Casa provisional",new Vector3(x,2.5f,z),new Vector3(8,5,10),wood,geometry);
                    Box("Techo",new Vector3(x,5.5f,z),new Vector3(9,1,11),stone,geometry);
                }
                AddVillagers(root.transform,wood);
                root.AddComponent<GoblinNavigation>().Configure(geometry);
                EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                var buildScenes=EditorBuildSettings.scenes.ToList();
                if(!buildScenes.Any(s=>s.path==ScenePath)){buildScenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=buildScenes.ToArray();}
                Debug.Log("DEMO_INTRODUCTION_CREATED: abrir Assets/Scenes/DemoIntroduction.unity y entrar en Play.");
            }
            finally{EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }

        static DemoStage Stage(Transform parent,string title,string instruction,float z,TutorialSequence lesson,GameObject encounter=null)
        {
            var marker=Group(title,parent);marker.position=new Vector3(0,0,z);
            return new DemoStage{title=title,instruction=instruction,destination=marker,radius=8,lesson=lesson,encounter=encounter,objective=encounter!=null?DemoObjective.Defeat:DemoObjective.Reach};
        }
        static GameObject Encounter(Transform parent,float z,float hp,float posture,int count)
        {
            var group=Group("Encuentro "+z,parent).gameObject;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/Goblin.prefab");
            if(prefab==null)throw new InvalidOperationException("Falta el prefab Goblin.");
            string path=DataPath+"/Goblin"+z+".asset";
            var settings=AssetDatabase.LoadAssetAtPath<GoblinSettings>(path);
            if(settings==null)
            {
                settings=Object.Instantiate(prefab.GetComponent<GoblinController>().Settings);
                settings.name="Goblin de práctica "+z;settings.health=hp;settings.posture=posture;
                settings.detectionRange=22;settings.loseRange=28;settings.leashRange=24;
                settings.slash.windup=1.05f;settings.slash.damage=5;settings.slash.recovery=1.3f;
                settings.charge.windup=1.3f;settings.charge.damage=7;settings.chargeCooldown=9;
                AssetDatabase.CreateAsset(settings,path);
            }
            for(int i=0;i<count;i++)
            {
                var enemy=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group.transform);
                enemy.transform.position=new Vector3(count==1?0:(i==0?-3:3),.1f,z+i*2);
                var serialized=new SerializedObject(enemy.GetComponent<GoblinController>());
                serialized.FindProperty("settings").objectReferenceValue=settings;serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            group.SetActive(false);return group;
        }
        static void AddVillagers(Transform parent,Material material)
        {
            var catalog=AssetDatabase.LoadAssetAtPath<QuestCatalog>("Assets/Data/Quests/QuestCatalog.asset");
            if(catalog==null)throw new InvalidOperationException("Falta QuestCatalog para los habitantes.");
            var groups=catalog.quests.Where(q=>q!=null&&q.npc!=null&&(q.prerequisites==null||q.prerequisites.Length==0)).GroupBy(q=>q.npc).Take(2).ToArray();
            for(int i=0;i<groups.Length;i++)
            {
                var npc=GameObject.CreatePrimitive(PrimitiveType.Capsule);npc.name=groups[i].Key.displayName+" (provisional)";
                npc.transform.SetParent(parent);npc.transform.position=new Vector3(i==0?-3:3,1,180);
                npc.GetComponent<Renderer>().sharedMaterial=material;
                var giver=npc.AddComponent<QuestGiver>();giver.npc=groups[i].Key;giver.quests=groups[i].ToArray();
                giver.markerSettings=catalog.villageNpcs;giver.interactionLabel="Conversar";
            }
        }
        static TutorialPage Page(string title,string body,TutorialAnchor anchor)=>new TutorialPage{title=title,body=body,highlight=anchor};
        static TutorialSequence Sequence(string id,params TutorialPage[] pages)
        {
            var path=DataPath+"/"+id+".asset";var value=AssetDatabase.LoadAssetAtPath<TutorialSequence>(path);if(value!=null)return value;
            value=ScriptableObject.CreateInstance<TutorialSequence>();value.id="demo."+id.ToLowerInvariant();value.pages=pages;AssetDatabase.CreateAsset(value,path);return value;
        }
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;int index=path.LastIndexOf('/');Folder(path.Substring(0,index));AssetDatabase.CreateFolder(path.Substring(0,index),path.Substring(index+1));
        }
        static Material Material(string name,Color color)
        {
            string path=MaterialsPath+"/"+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(path);if(value!=null)return value;
            value=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){color=color};AssetDatabase.CreateAsset(value,path);return value;
        }
        static Transform Group(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent);return go.transform;}
        static GameObject Box(string name,Vector3 position,Vector3 size,Material material,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
    }
}
