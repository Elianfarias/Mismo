using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class MineralAssets
    {
        const string Art="Assets/Art/Prefabs/Gathering/Minerals";
        const string Materials="Assets/Art/Materials/Gathering/Minerals";
        const string Data="Assets/Data/Gathering/Minerals";
        const string Source="Assets/Art/FBX/Minerals/SimplePolygon_Minerals.fbx";
        static T LoadOrCreate<T>(string path) where T:ScriptableObject
        {var value=AssetDatabase.LoadAssetAtPath<T>(path);if(value!=null)return value;value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);return value;}
        [MenuItem("Mismo/Crafting/Import mineral models and rose")]
        public static void Create()
        {
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Materials);Directory.CreateDirectory(Data);AssetDatabase.Refresh();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if(model==null)throw new InvalidOperationException("Missing mineral FBX.");
            var meshes=model.GetComponentsInChildren<MeshRenderer>(true).OrderBy(r=>r.name,StringComparer.Ordinal).ToArray();
            var palette=AssetDatabase.LoadAssetAtPath<Material>(Materials+"/MineralPalette.mat");
            if(palette==null){palette=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));palette.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/Minerals/SimplePolygon_MultiColorPalette.png");AssetDatabase.CreateAsset(palette,Materials+"/MineralPalette.mat");}
            var depleted=AssetDatabase.LoadAssetAtPath<Material>(Materials+"/Depleted.mat");
            if(depleted==null){depleted=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){color=new Color(.29f,.31f,.34f)};AssetDatabase.CreateAsset(depleted,Materials+"/Depleted.mat");}
            var settings=Mismo.Core.ProjectAssets.Load<GatheringSettings>("GatheringSettings");var stone=settings.stone;
            var baseRock=meshes.OrderBy(r=>r.bounds.size.y).First();
            var exhausted=Prefab(baseRock,"Depleted",depleted,.22f);
            var nodes=new ResourceNodeDefinition[meshes.Length];
            for(int i=0;i<meshes.Length;i++)
            {
                var source=meshes[i];string name=source.name;
                var prefab=Prefab(source,name,palette,0);
                string path=Data+"/"+name+".asset";
                var node=AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path);
                if(node==null){node=Object.Instantiate(stone);node.name=name;node.id="node-mineral-"+name;AssetDatabase.CreateAsset(node,path);
                    var loot=LoadOrCreate<MaterialLootTable>(Data+"/"+name+"-Loot.asset");
                    loot.entries=stone.rewards.entries.Select(e=>new MaterialLootEntry{material=e.material,minimum=e.minimum,maximum=e.maximum,probability=e.probability}).ToArray();EditorUtility.SetDirty(loot);node.rewards=loot;}
                node.availablePrefab=prefab;node.depletedPrefab=exhausted;EditorUtility.SetDirty(node);nodes[i]=node;
            }
            settings.minerals=nodes;stone.availablePrefab=nodes[0].availablePrefab;stone.depletedPrefab=exhausted;
            EditorUtility.SetDirty(stone);EditorUtility.SetDirty(settings);
            var stoneItem=Mismo.Core.ProjectAssets.Load<MaterialDefinition>("Materials/Stone");stoneItem.pickupPrefab=nodes[0].availablePrefab;stoneItem.icon=null;EditorUtility.SetDirty(stoneItem);
            var rose=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/Nature/flower_rose.prefab");
            if(rose==null)throw new InvalidOperationException("Missing flower_rose prefab.");
            var empty=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/RosePicked.prefab");
            if(empty==null){var go=new GameObject("Rose picked - regrowing");empty=PrefabUtility.SaveAsPrefabAsset(go,Art+"/RosePicked.prefab");Object.DestroyImmediate(go);}
            settings.herb.availablePrefab=rose;settings.herb.depletedPrefab=empty;EditorUtility.SetDirty(settings.herb);
            var herb=Mismo.Core.ProjectAssets.Load<MaterialDefinition>("Materials/Herb");herb.pickupPrefab=rose;herb.icon=null;EditorUtility.SetDirty(herb);
            var catalog=Mismo.Core.ProjectAssets.Load<WorldContentCatalog>("WorldContentCatalog");
            foreach(var entry in catalog.assets)if(entry.id=="nature.flower_rose")entry.gatheringNode=settings.herb;
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();InventoryPresentationAssets.GenerateGridIcons();
            Debug.Log("MINERALS_IMPORTED "+nodes.Length);
        }
        static GameObject Prefab(MeshRenderer source,string name,Material material,float flatten)
        {
            string path=Art+"/"+name+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var root=new GameObject(name);var visual=Object.Instantiate(source.gameObject);visual.transform.SetParent(root.transform,true);
            var renderer=visual.GetComponent<MeshRenderer>();renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
            var bounds=renderer.bounds;var floor=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            float scale=1.35f/Mathf.Max(bounds.size.x,bounds.size.z);
            visual.transform.position=(visual.transform.position-floor)*scale;visual.transform.localScale*=scale;
            if(flatten>0){float factor=flatten/(bounds.size.y*scale);var s=visual.transform.localScale;s.y*=factor;visual.transform.localScale=s;var p=visual.transform.localPosition;p.y*=factor;visual.transform.localPosition=p;}
            bounds=renderer.bounds;var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return prefab;
        }
        public static void RunBatch()
        {
            if(!Directory.GetCurrentDirectory().Replace("\\","/").Contains("/.validation/"))throw new InvalidOperationException("Use isolated validation project.");
            Create();var settings=Mismo.Core.ProjectAssets.Load<GatheringSettings>("GatheringSettings");
            if(settings.minerals.Length!=30)throw new Exception("Expected 30 individual minerals.");
            foreach(var node in settings.minerals){var r=node.availablePrefab.GetComponentInChildren<Renderer>();if(r==null||Mathf.Abs(r.bounds.min.y)>.02f||r.bounds.size.x>1.5f||r.bounds.size.z>1.5f||node.rewards.Roll().Count==0)throw new Exception("Invalid node: "+node.name);}
            if(!Mismo.Core.ProjectAssets.Load<WorldContentCatalog>("WorldContentCatalog").assets.Any(a=>a.id=="nature.flower_rose"&&a.gatheringNode==settings.herb))throw new Exception("Rose decoration not integrated");
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            for(int i=0;i<settings.minerals.Length;i++)Object.Instantiate(settings.minerals[i].availablePrefab,new Vector3((i%6)*2.1f,0,(i/6)*2.4f),Quaternion.identity);
            Object.Instantiate(settings.herb.availablePrefab,new Vector3(-2,0,3),Quaternion.identity);
            var camera=new GameObject("Preview").AddComponent<UnityEngine.Camera>();camera.transform.position=new Vector3(14,15,-17);camera.transform.LookAt(new Vector3(4.5f,0,4.5f));camera.orthographic=true;camera.orthographicSize=8;camera.backgroundColor=new Color(.13f,.16f,.19f);camera.clearFlags=CameraClearFlags.SolidColor;
            RenderSettings.ambientLight=Color.gray;var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(55,-25,0);
            var rt=new RenderTexture(1400,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var png=new Texture2D(1400,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1400,1000),0,0);png.Apply();Directory.CreateDirectory("../../output/gathering");File.WriteAllBytes("../../output/gathering/minerals.png",png.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(png);Object.DestroyImmediate(rt);
            File.WriteAllText("../../output/gathering/minerals-checks.txt","PASS 30 separate grounded mineral prefabs, loot tables, rose decoration mapping and icon generation.");EditorApplication.Exit(0);
        }
        public static void RunGameplayBatch()
        {
            if(!Directory.GetCurrentDirectory().Replace("\\","/").Contains("/.validation/"))throw new InvalidOperationException("Use isolated validation project.");
            Create();GatheringChecks.RunBatch();
        }
    }
}
