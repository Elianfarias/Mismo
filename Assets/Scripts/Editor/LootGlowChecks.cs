using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.World;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class LootGlowChecks
{
    const string Output="output/loot-glow";
    public static void RunBatch()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Run in isolated validation project");
        Directory.CreateDirectory(Output);
        try
        {
            var settings=AssetDatabase.LoadAssetAtPath<InventorySettings>("Assets/Data/Inventory/InventorySettings.asset");
            var sourceMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/LootGlow.mat");
            var sourceShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/LootGlow.shader");
            if(settings.lootGlowMaterial==null||settings.lootGlowMaterial.shader==null)throw new Exception("Loot shader reference missing: settings="+JsonUtility.ToJson(settings)+", material="+sourceMaterial+", shader="+sourceShader+", material shader="+(sourceMaterial!=null?sourceMaterial.shader:null));
            if(settings.weaponLootColors.Length!=5)throw new Exception("Expected five tier colors");
            for(int tier=2;tier<=5;tier++)if(LootMarker.ColorFor(settings,0,tier)==LootMarker.ColorFor(settings,0,tier-1))throw new Exception("Tier colors overlap");
            CheckKinds();
            Preview(settings);
            if(ShaderUtil.ShaderHasError(settings.lootGlowMaterial.shader))throw new Exception("Shader compilation error");
            File.WriteAllText(Output+"/checks.txt","PASS: five tier colors, component/consumable classification, mixed loot prioritizes weapon, shader renders without errors.\n");
            try {ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","PASS");}
            catch(Exception e){File.WriteAllText(Output+"/organization.txt","Isolated project omits existing art dependencies.\n"+e);}
            Directory.CreateDirectory(".validation/LootContent");
            var manifest=BuildPipeline.BuildAssetBundles(".validation/LootContent",new[]{new AssetBundleBuild{assetBundleName="loot-glow",assetNames=new[]{"Assets/Data/Inventory/InventorySettings.asset"}}},BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
            if(manifest==null)throw new Exception("Content build failed");
            var bundle=AssetBundle.LoadFromFile(".validation/LootContent/loot-glow");
            if(bundle==null)throw new Exception("Built bundle missing");
            try {var built=bundle.LoadAllAssets<InventorySettings>().First();if(built.lootGlowMaterial==null||built.lootGlowMaterial.shader==null)throw new Exception("Built shader dependency missing");}
            finally {bundle.Unload(true);}
            File.AppendAllText(Output+"/checks.txt","PASS: Windows content bundle builds and reloads InventorySettings with material and shader dependencies.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Output+"/checks.txt","FAIL\n"+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void CheckKinds()
    {
        var owner=new GameObject("Loot types");var inventory=owner.AddComponent<PlayerInventory>();
        var material=ScriptableObject.CreateInstance<MaterialDefinition>();material.id="test.consumable";material.category=InventoryItemCategory.Consumable;
        try
        {
            var definitions=(System.Collections.Generic.Dictionary<string,MaterialDefinition>)typeof(PlayerInventory).GetField("materialDefinitions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(inventory);
            definitions.Add(material.id,material);
            var loot=new PendingInventoryLoot();loot.materials.Add(new MaterialStack{id=material.id,quantity=2});
            if(LootMarker.Kind(inventory,loot)!=2)throw new Exception("Consumable misclassified");
            material.category=InventoryItemCategory.Material;
            if(LootMarker.Kind(inventory,loot)!=1)throw new Exception("Component misclassified");
            loot.weapon=new OwnedWeapon{instanceId="test",tier=4};
            if(LootMarker.Kind(inventory,loot)!=0)throw new Exception("Mixed weapon loot misclassified");
        }
        finally{Object.DestroyImmediate(owner);Object.DestroyImmediate(material);}
    }
    static void Preview(InventorySettings settings)
    {
        var scene=EditorSceneManager.NewPreviewScene();RenderTexture target=null;Texture2D image=null;
        try
        {
            var cameraObject=new GameObject("Loot preview camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.038f,.055f);
            camera.transform.position=new Vector3(0,3,-10);camera.transform.LookAt(new Vector3(0,.7f,0));camera.orthographic=true;camera.orthographicSize=2.3f;
            for(int i=0;i<7;i++)
            {
                var marker=new GameObject("Loot preview "+i);SceneManager.MoveGameObjectToScene(marker,scene);marker.transform.position=new Vector3((i-3)*1.6f,0,0);
                marker.AddComponent<LootMarker>().Configure(settings,i<5?0:i-4,i<5?i+1:1);
                marker.transform.Find("Loot glow").rotation=camera.transform.rotation;
                if(marker.GetComponentsInChildren<Collider>().Any(c=>c.enabled))throw new Exception("Loot VFX collider blocks player");
            }
            target=new RenderTexture(1600,650,24);camera.targetTexture=target;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;image=new Texture2D(1600,650,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,650),0,0);image.Apply();RenderTexture.active=previous;
            File.WriteAllBytes(Output+"/preview.png",image.EncodeToPNG());
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);if(target!=null)Object.DestroyImmediate(target);if(image!=null)Object.DestroyImmediate(image);}
    }
}
