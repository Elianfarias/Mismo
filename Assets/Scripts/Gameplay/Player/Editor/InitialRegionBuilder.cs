using System.Linq;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class InitialRegionBuilder
    {
        public const string ScenePath = "Assets/Scenes/InitialRegion.unity";
        private static Transform geometry;
        private static Material grass, stone, path, wood, gold;

        [MenuItem("Mismo/Prototype/Region/Build Initial Region")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("InitialRegion ya existe; no se sobrescribió. Abrila para editarla.");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.CopyAsset(BossPrototypeTool.ScenePath, ScenePath))
                throw new System.InvalidOperationException("Falta la escena BossArena de referencia.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<PlayerController>() == null && root.GetComponent<UnityEngine.Camera>() == null && root.GetComponent<Light>() == null)
                    Object.DestroyImmediate(root);

            grass = Material("Grass", new Color(.24f, .38f, .22f));
            stone = Material("Stone", new Color(.35f, .40f, .43f));
            path = Material("Path", new Color(.55f, .46f, .30f));
            wood = Material("Wood", new Color(.30f, .18f, .12f));
            gold = Material("Reward", new Color(1f, .72f, .15f));
            var region = new GameObject("Initial Region");
            Undo.RegisterCreatedObjectUndo(region, "Create Initial Region");
            geometry = Group("RegionGeometry", region.transform);
            var village = Group("Village", geometry);
            var encounters = Group("Encounters", region.transform);
            var secret = Group("Secret", region.transform);
            var bossArea = Group("BossArea", region.transform);
            var dynamicObjects = Group("DynamicObjects", region.transform);
            Box("Ground", new Vector3(0,-1,80), new Vector3(110,2,190), grass, geometry);
            Box("West cliffs", new Vector3(-55,8,80), new Vector3(4,16,194), stone, geometry);
            Box("East cliffs", new Vector3(55,8,80), new Vector3(4,16,194), stone, geometry);
            Box("South cliffs", new Vector3(0,8,-15), new Vector3(110,16,4), stone, geometry);
            Box("North cliffs", new Vector3(0,8,175), new Vector3(110,16,4), stone, geometry);
            Path(new Vector3(0,0,0), new Vector3(0,0,30), 6);
            Path(new Vector3(0,0,30), new Vector3(-15,0,65), 6);
            Path(new Vector3(-15,0,65), new Vector3(0,0,105), 6);
            Path(new Vector3(0,0,105), new Vector3(0,0,145), 6);
            Path(new Vector3(0,0,40), new Vector3(35,0,65), 3);
            Path(new Vector3(35,0,80), new Vector3(0,0,95), 3);
            Box("Village square", new Vector3(0,.015f,0), new Vector3(20,.03f,20), path, village);
            foreach (float x in new[] {-9f, 9f})
            {
                Box("House", new Vector3(x,2,0), new Vector3(5,4,7), wood, village);
                Box("Roof", new Vector3(x,4.3f,0), new Vector3(6,.6f,8), stone, village);
            }
            Label("PUEBLO", new Vector3(0,4,-5), village);
            // Large sight breaks keep adjacent encounters from seeing through the region.
            Box("First bend outcrop", new Vector3(6,4,46), new Vector3(10,8,12), stone, geometry);
            Box("Second bend outcrop", new Vector3(-12,4,88), new Vector3(12,8,10), stone, geometry);
            Box("Secret screen", new Vector3(25,4,73), new Vector3(6,8,18), stone, geometry);
            for (int i=0; i<8; i++)
            {
                float x = i % 2 == 0 ? -35 : 45;
                Box("Tree trunk", new Vector3(x,2,15+i*17), new Vector3(1.2f,4,1.2f), wood, geometry);
                Box("Tree crown", new Vector3(x,6,15+i*17), new Vector3(6,5,6), grass, geometry);
            }
            Spawn("Goblin", new Vector3(0,0,30), encounters);
            Spawn("Goblin", new Vector3(-17,0,65), encounters);
            Spawn("Goblin", new Vector3(-12,0,67), encounters);
            Spawn("Goblin", new Vector3(35,0,65), encounters);
            Spawn("GoblinElite", new Vector3(0,0,105), encounters);
            // Open ruin: two low steps and a walkable return ramp, no roof over the camera.
            Box("Secret step 1", new Vector3(35,.4f,72), new Vector3(4,.8f,3), stone, geometry);
            Box("Secret terrace", new Vector3(35,.8f,78), new Vector3(12,1.6f,8), stone, geometry);
            var ramp = Box("Secret return ramp", new Vector3(35,.65f,86), new Vector3(5,.4f,10), stone, geometry);
            ramp.transform.rotation = Quaternion.Euler(10,0,0);
            foreach(float x in new[]{30f,40f}) Box("Ruin pillar",new Vector3(x,3.6f,79),new Vector3(1.2f,4,1.2f),stone,geometry);
            var pickup = Box("Secret restoration", new Vector3(35,2.3f,78), Vector3.one, gold, secret);
            pickup.GetComponent<Collider>().isTrigger = true;
            var body = pickup.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            pickup.AddComponent<SecretRewardPickup>();
            Label("RESTAURACION", new Vector3(35,4,78), secret);
            // Arena perimeter also prevents skipping the victory gate using double jump.
            Box("Arena west",new Vector3(-14,8,147),new Vector3(3,16,32),stone,geometry);
            Box("Arena east",new Vector3(14,8,147),new Vector3(3,16,32),stone,geometry);
            Box("Arena rear left",new Vector3(-9,8,162),new Vector3(10,16,3),stone,geometry);
            Box("Arena rear right",new Vector3(9,8,162),new Vector3(10,16,3),stone,geometry);
            var gate = Box("Boss Passage Gate",new Vector3(0,8,162),new Vector3(8,16,3),wood,dynamicObjects);
            // Extend the rear wall to the region limits so the exit cannot be reached around the arena.
            Box("Exit west ridge",new Vector3(-35,8,162),new Vector3(40,16,3),stone,geometry);
            Box("Exit east ridge",new Vector3(35,8,162),new Vector3(40,16,3),stone,geometry);
            var boss = Spawn("FirstBoss", new Vector3(0,0,145), bossArea).GetComponent<BossController>();
            var reward = Box("Boss Reward Indicator",new Vector3(0,.5f,167),new Vector3(2,1,2),gold,dynamicObjects);
            reward.SetActive(false);
            bossArea.gameObject.AddComponent<BossEncounter>().Configure(boss,gate,reward);
            Label("PASO DEL GUARDIAN",new Vector3(0,7,132),bossArea);
            Label("FIN DE LA REGION",new Vector3(0,4,171),bossArea);
            region.AddComponent<GoblinNavigation>().Configure(geometry);
            var player = Object.FindFirstObjectByType<PlayerController>();
            player.transform.position = new Vector3(0,.1f,0);
            player.gameObject.AddComponent<RegionRespawn>();
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath,true));
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("INITIAL_REGION_BUILD_OK");
        }

        private static Transform Group(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent); return go.transform; }
        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent); go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        private static void Path(Vector3 a, Vector3 b, float width)
        {
            var go = Box("Path", (a+b)*.5f+Vector3.up*.015f,new Vector3(width,.03f,Vector3.Distance(a,b)),path,geometry);
            go.transform.rotation = Quaternion.LookRotation(b-a);
        }
        private static GameObject Spawn(string name, Vector3 position, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/"+name+".prefab");
            if (prefab == null) throw new System.InvalidOperationException("Falta prefab "+name);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position=position; return go;
        }
        private static void Label(string text, Vector3 position, Transform parent)
        {
            var go = new GameObject(text); go.transform.SetParent(parent); go.transform.position=position;
            var label=go.AddComponent<TextMesh>(); label.text=text; label.characterSize=.15f; label.fontSize=48; label.anchor=TextAnchor.MiddleCenter;
        }
        private static Material Material(string name, Color color)
        {
            const string folder="Assets/Art/Materials/InitialRegion";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Art/Materials","InitialRegion");
            string file=folder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(file);
            if(material!=null) return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color=color; AssetDatabase.CreateAsset(material,file); return material;
        }
    }
}
