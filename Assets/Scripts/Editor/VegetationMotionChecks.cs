using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class VegetationMotionChecks
{
    const string Output = "output/vegetation-motion";
    const string CatalogPath = "Assets/Data/World/WorldContentCatalog.asset";
    static int checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++; Debug.Log("VEGETATION_OK: " + message);
    }
    [MenuItem("Mismo/World/Verificar viento y vegetacion")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Ejecutar la verificacion fuera de Play Mode.");
        Directory.CreateDirectory(Output); checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Vegetation checks");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var source = AssetDatabase.LoadAssetAtPath<WorldContentCatalog>(CatalogPath);
        var catalog = Object.Instantiate(source);
        bool oldFog = RenderSettings.fog;
        try
        {
            Check(catalog.vegetationMotion.shader != null, "Catalog directly references vegetation shader");
            Check(AssetDatabase.GetDependencies(CatalogPath, true).Contains("Assets/Art/Shaders/VegetationMotion.shader"), "Shader is a build dependency");
            var player = new GameObject("Interaction target"); player.transform.SetParent(root.transform); player.transform.position = Vector3.one * 100;
            var world = root.AddComponent<VegetationMotionWorld>(); world.Initialize(catalog, player.transform);
            var profile = catalog.BiomeContent(WorldBiome.Meadow);
            var grass = profile.assets.First(a => a.kind == WorldAssetKind.Grass && a.prefab != null);
            var original = grass.prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            var entry = new WorldAssetEntry { prefab = grass.prefab, kind = WorldAssetKind.Grass, decorativeOnly = true };
            var plant = GatheringDistribution.PlaceAsset(entry, "vegetation-test", Vector3.zero, Quaternion.identity, root.transform);
            plant.GetComponent<VegetationMotionBinding>().Apply();
            var renderer = plant.GetComponentInChildren<MeshRenderer>();
            Check(plant.GetComponent<GatheringNode>() == null, "Decorative plants remain non-harvestable");
            Check(renderer.sharedMaterials.All(m => m.shader == catalog.vegetationMotion.shader), "Actual grass materials support motion");
            Check(original.SequenceEqual(grass.prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials), "Original prefab materials unchanged");
            var sourceBounds = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            Check(renderer.localBounds.size.x > sourceBounds.size.x, "Runtime culling bounds include sway");
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            Check(block.GetVector("_VegetationShape").z == 0 && block.GetVector("_VegetationResponse").x == 1, "Grass roots anchored with player response");
            int count = world.MaterialCount;
            for (int i=0;i<100;i++)
            {
                var extra = GatheringDistribution.PlaceAsset(entry,"vegetation-test-"+i,new Vector3(100+i,0,100),Quaternion.identity,root.transform);
                extra.GetComponent<VegetationMotionBinding>().Apply();
                Object.DestroyImmediate(extra);
            }
            Check(world.MaterialCount == count, "100 stream reloads reuse the same materials");
            entry.disableVegetationMotion = true;
            var disabled = GatheringDistribution.PlaceAsset(entry,"disabled",Vector3.zero,Quaternion.identity,root.transform);
            Check(disabled.GetComponent<VegetationMotionBinding>() == null, "Per-entry motion opt-out works");
            Object.DestroyImmediate(disabled);
            Check(!VegetationMotionBinding.Supports(WorldAssetKind.Rock) && !VegetationMotionBinding.Supports(WorldAssetKind.Deadwood), "Rocks and deadwood remain rigid");
            entry.disableVegetationMotion = false; entry.decorativeOnly = false;
            entry.gatheringNode = AssetDatabase.LoadAssetAtPath<GatheringSettings>("Assets/Data/Gathering/GatheringSettings.asset")?.herb;
            var harvestable = GatheringDistribution.PlaceAsset(entry,"harvestable-test",new Vector3(30,0,0),Quaternion.identity,root.transform);
            var node = harvestable.GetComponent<GatheringNode>();
            Check(node != null && harvestable.GetComponent<VegetationMotionBinding>() != null,"Harvestable vegetation keeps both systems");
            var show = typeof(GatheringNode).GetMethod("Show",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            show.Invoke(node,null);
            Check(harvestable.GetComponentInChildren<MeshRenderer>().sharedMaterial.shader == catalog.vegetationMotion.shader,"Deferred harvest visual receives motion");
            // Avoid invoking the node's play-only destruction path in edit mode.
            Object.DestroyImmediate(harvestable);
            var treeEntry = profile.assets.First(a => a.kind == WorldAssetKind.Tree && a.prefab != null);
            var tree = Object.Instantiate(treeEntry.prefab, new Vector3(5,0,3), Quaternion.identity,root.transform);
            VegetationMotionBinding.Attach(tree,treeEntry).GetComponent<VegetationMotionBinding>().Apply();
            tree.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(block);
            Check(block.GetVector("_VegetationShape").z > .5f && block.GetVector("_VegetationResponse").x == 0,"Trees have a rigid lower trunk and no player push");
            tree.SetActive(false);
            player.transform.position = new Vector3(.35f,0,0); world.Tick(.1f);
            Check(Shader.GetGlobalVectorArray("_MismoPlayerTrail")[0].w == 1,"Player position reaches GPU");
            player.transform.position = new Vector3(100,0,100); world.Tick(.01f);
            Check(Shader.GetGlobalVectorArray("_MismoPlayerTrail").Skip(1).All(p=>p.w==0),"Teleport clears previous influences");
            catalog.vegetationMotion.enabled = false; world.Tick(0);
            Check(Shader.GetGlobalVector("_MismoWind").z == 0,"Master switch disables wind");
            Check(Shader.GetGlobalVector("_MismoInteraction").y == 0,"Master switch disables interaction");
            catalog.vegetationMotion.enabled = true;
            RenderSettings.fog = false;
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(root.transform); sun.type=LightType.Directional;sun.intensity=1.5f;sun.transform.rotation=Quaternion.Euler(45,25,0);
            var camera = new GameObject("Camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.2f,.24f);camera.nearClipPlane=.01f;
            camera.transform.position=new Vector3(.9f,.65f,-1.1f);camera.transform.LookAt(new Vector3(0,.19f,0));camera.orthographic=true;camera.orthographicSize=.42f;
            catalog.vegetationMotion.windStrength=0;world.Tick(0);
            var still=Capture(camera,"grass-still");
            catalog.vegetationMotion.windStrength=1;world.Tick(1.3f);
            var wind=Capture(camera,"grass-wind");
            Check(Difference(still,wind)>50,"GPU wind visibly changes actual grass");
            catalog.vegetationMotion.windStrength=0;player.transform.position=new Vector3(.35f,0,0);world.Tick(.1f);
            var pushed=Capture(camera,"grass-player");
            Check(Difference(still,pushed)>50,"GPU player interaction visibly changes actual grass");
            player.transform.position=new Vector3(3,0,0);world.Tick(1.6f);
            var recovered=Capture(camera,"grass-recovered");
            Check(Difference(still,recovered)<20,"Grass recovers after player leaves");
            tree.SetActive(true);plant.SetActive(false);
            camera.transform.position=new Vector3(13,5,-9);camera.transform.LookAt(new Vector3(5,3,3));camera.orthographicSize=4.5f;
            catalog.vegetationMotion.windStrength=1;world.Tick(2);
            Capture(camera,"tree-wind");
            var errors=ShaderUtil.GetShaderMessages(catalog.vegetationMotion.shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            Check(errors.Length==0,"Shader compiles without errors: "+string.Join(";",errors.Select(e=>e.message)));
            File.WriteAllText(Output+"/checks.txt",checks+" checks passed. Actual catalog grass and tree rendered; source assets unchanged.\n");
            try { ProjectOrganizationChecks.Run(); File.WriteAllText(Output+"/organization.txt","Passed"); }
            catch(Exception e) { File.WriteAllText(Output+"/organization.txt",e.Message); throw; }
        }
        finally
        {
            RenderSettings.fog=oldFog;
            Object.DestroyImmediate(root); Object.DestroyImmediate(catalog);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    static Color32[] Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(800,700,24);var previous=RenderTexture.active;var pixels=new Texture2D(800,700,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
            RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,800,700),0,0);pixels.Apply();
            File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();
        }
        finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}
    }
    static int Difference(Color32[] a,Color32[] b)
    {
        int changed=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>15)changed++;
        return changed;
    }
    public static void RunBatch()
    {
        try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void Build()
    {
        Directory.CreateDirectory(Output+"/build");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=Output+"/build/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText(Output+"/build.txt",report.summary.result+" errors="+report.summary.totalErrors);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Vegetation build failed");
    }
    public static void ValidateAndBuildBatch()
    {
        bool success=true;
        try { Run(); } catch(Exception e) { Debug.LogException(e); success=false; }
        try { Build(); } catch(Exception e) { Debug.LogException(e); success=false; }
        EditorApplication.Exit(success?0:1);
    }
}
