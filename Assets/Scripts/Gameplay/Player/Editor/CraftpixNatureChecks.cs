using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class CraftpixNatureChecks
    {
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        [MenuItem("Mismo/World/Verificar naturaleza por bioma")]
        public static void Run()
        {
            Directory.CreateDirectory("output/craftpix-nature");
            var catalog=AssetDatabase.LoadAssetAtPath<WorldContentCatalog>("Assets/Data/World/WorldContentCatalog.asset");
            Require(catalog!=null,"Catalog exists");
            Require(catalog.biomeContents.Length==7&&catalog.biomeContents.Select(p=>p.biome).Distinct().Count()==7,"Exactly one profile per biome");
            foreach(var profile in catalog.biomeContents)
            {
                foreach(var entry in profile.assets)
                {
                    Require(entry.prefab!=null&&entry.weight>0,"Valid weighted prefab: "+entry.id);
                    Require(WorldContentCatalog.Allows(entry.biomes,profile.biome),"Climate mismatch: "+entry.id);
                    var mesh=entry.prefab.GetComponent<MeshFilter>().sharedMesh;
                    Require(mesh!=null&&mesh.vertexCount>0&&Mathf.Abs(mesh.bounds.min.y)<.001f,"Grounded generated mesh: "+entry.id);
                    Require(AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/Art/Meshes/Voxelized/Nature/"),"Independent voxel mesh");
                    foreach(var material in entry.prefab.GetComponent<Renderer>().sharedMaterials)
                        Require(material!=null&&material.shader!=null,"Valid material: "+entry.id);
                    Require(entry.prefab.GetComponent<Renderer>().sharedMaterials.Any(m=>m.mainTexture!=null),"Palette retained: "+entry.id);
                }
                foreach(WorldAssetKind kind in Enum.GetValues(typeof(WorldAssetKind)))
                {
                    if(!WorldBiomeContent.IsNature(kind))continue;
                    for(int seed=0;seed<100;seed++)
                    {
                        var chosen=catalog.Asset(kind,profile.biome,seed);
                        Require(chosen==catalog.Asset(kind,profile.biome,seed),"Deterministic selection");
                        Require(chosen==null||profile.assets.Contains(chosen),"No legacy nature fallback");
                    }
                }
            }
            Require(catalog.Asset(WorldAssetKind.Tree,WorldBiome.Ice,7)!=null,"Winter trees available");
            Require(catalog.Asset(WorldAssetKind.Tree,WorldBiome.Desert,7)==null,"Desert excludes temperate trees");
            Require(catalog.Asset(WorldAssetKind.Rock,WorldBiome.Ocean,7)==null,"Ocean stays empty");
            Require(catalog.assets.All(a=>a==null||!WorldBiomeContent.IsNature(a.kind)),"Legacy nature dependencies removed from active catalog");
            CheckImportedOrientation(catalog);
            CheckOverrides(catalog);
            Type.GetType("ProjectOrganizationChecks, Assembly-CSharp-Editor",true).GetMethod("Run").Invoke(null,null);
            FiniteWorldChecks.Run();
            Render(catalog);
            RenderWorld();
            File.WriteAllText("output/craftpix-nature/Checks.txt","PASS: biome selection, empty categories, weights, deterministic seeds, winter tree density, grounded voxel meshes, palette references, organization, finite-world regression checks and production chunk previews for five biomes.");
            Debug.Log("CRAFTPIX_NATURE_CHECKS_OK");
        }
        static void CheckImportedOrientation(WorldContentCatalog catalog)
        {
            foreach(var entry in catalog.biomeContents.SelectMany(p=>p.assets).Where(e=>e.id.StartsWith("Craftpix_Tree_temp_climate")).GroupBy(e=>e.id).Select(g=>g.First()))
            {
                string name=entry.id.Substring("Craftpix_".Length);
                string sourcePath=AssetDatabase.FindAssets(name,new[]{"Assets/Art/FBX/Nature"}).Select(AssetDatabase.GUIDToAssetPath).First(p=>Path.GetFileNameWithoutExtension(p)==name);
                var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
                try
                {
                    var renderers=source.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    var actual=entry.prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                    Require(Mathf.Abs(actual.x/actual.y-bounds.size.x/bounds.size.y)<.12f&&Mathf.Abs(actual.z/actual.y-bounds.size.z/bounds.size.y)<.12f,"FBX axis correction preserved: "+entry.id);
                }
                finally{Object.DestroyImmediate(source);}
            }
        }
        static void CheckOverrides(WorldContentCatalog source)
        {
            var catalog=Object.Instantiate(source);
            var ice=Object.Instantiate(source.BiomeContent(WorldBiome.Ice));
            var settings=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationWorldSettings>("Assets/Data/World/ExplorationWorldSettings.asset"));
            try
            {
                catalog.biomeContents=new[]{ice};settings.content=catalog;settings.generationVersion=2;settings.preserveAuthoredCenter=false;
                var terrain=new ExplorationTerrain(settings);
                var point=terrain.Plan.SitePosition(terrain.Plan.ObjectiveCell(2));
                Require(terrain.Biome(point.x,point.y)==WorldBiome.Ice,"Ice sample");
                ice.trees=.37f;Require(Mathf.Abs(terrain.TreeDensity(point.x,point.y)-.37f)<.0001f,"Configured winter tree density respected");
                ice.trees=0;Require(terrain.TreeDensity(point.x,point.y)==0,"Zero density disables trees");
                ice.assets=Array.Empty<WorldAssetEntry>();Require(catalog.Asset(WorldAssetKind.Tree,WorldBiome.Ice,1)==null,"Empty profile cannot leak legacy assets");
                var entry=source.BiomeContent(WorldBiome.Ice).assets[0];
                ice.assets=new[]{new WorldAssetEntry{kind=entry.kind,prefab=entry.prefab,weight=0}};
                Require(catalog.Asset(entry.kind,WorldBiome.Ice,1)==null,"Zero weight disables asset");
            }
            finally{Object.DestroyImmediate(settings);Object.DestroyImmediate(ice);Object.DestroyImmediate(catalog);}
        }
        public static void Build()
        {
            Directory.CreateDirectory("output/craftpix-nature/build");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName="output/craftpix-nature/build/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText("output/craftpix-nature/Build.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; bytes="+report.summary.totalSize);
            Require(report.summary.result==BuildResult.Succeeded,"Player build failed: "+report.summary.result);
        }
        public static void BuildContent()
        {
            const string output="output/craftpix-nature/content-build";
            Directory.CreateDirectory(output);
            var catalog=AssetDatabase.LoadAssetAtPath<WorldContentCatalog>("Assets/Data/World/WorldContentCatalog.asset");
            var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{
                assetBundleName="nature",assetNames=catalog.biomeContents.Select(AssetDatabase.GetAssetPath).ToArray()}},
                BuildAssetBundleOptions.ForceRebuildAssetBundle|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
            Require(manifest!=null,"Nature content build succeeds");
            var bundle=AssetBundle.LoadFromFile(output+"/nature");
            Require(bundle!=null,"Built nature bundle loads");
            try
            {
                var profiles=bundle.LoadAllAssets<WorldBiomeContent>();
                Require(profiles.Length==7,"Seven profiles included in the content build");
                foreach(var entry in profiles.SelectMany(p=>p.assets))
                {
                    Require(entry.prefab!=null,"Built prefab reference: "+entry.id);
                    Require(entry.prefab.GetComponent<MeshFilter>().sharedMesh!=null,"Built mesh reference: "+entry.id);
                    Require(entry.prefab.GetComponent<Renderer>().sharedMaterials.All(m=>m!=null),"Built material references: "+entry.id);
                    Require(entry.prefab.GetComponent<Renderer>().sharedMaterials.Any(m=>m.mainTexture!=null),"Built texture reference: "+entry.id);
                }
                File.WriteAllText("output/craftpix-nature/ContentBuild.txt","PASS: Windows content bundle built and loaded; seven biome profiles and all prefab, mesh, material and palette references resolve.");
            }
            finally{bundle.Unload(true);}
        }
        static void Render(WorldContentCatalog catalog)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            RenderTexture rt=null;Texture2D image=null;
            var previous=RenderTexture.active;
            try
            {
                var camera=new GameObject("Nature preview camera").AddComponent<UnityEngine.Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.23f,.28f);camera.orthographic=true;camera.orthographicSize=5;
                camera.transform.position=new Vector3(8,7,-12);camera.transform.LookAt(new Vector3(0,3,0));
                var light=new GameObject("Nature preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(40,-35,0);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);
                rt=new RenderTexture(440,440,24);camera.targetTexture=rt;
                image=new Texture2D(440,440,TextureFormat.RGB24,false);
                foreach(var entry in catalog.biomeContents.SelectMany(p=>p.assets).GroupBy(e=>e.id).Select(g=>g.First()))
                {
                    var go=Object.Instantiate(entry.prefab);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
                    try
                    {
                        var bounds=go.GetComponent<Renderer>().bounds;
                        camera.orthographicSize=Mathf.Max(bounds.size.y,bounds.size.x)*.7f;
                        camera.transform.position=bounds.center+new Vector3(8,6,-12);camera.transform.LookAt(bounds.center);
                        camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,440,440),0,0);image.Apply();
                        File.WriteAllBytes("output/craftpix-nature/"+entry.id+".png",image.EncodeToPNG());
                    }
                    finally{Object.DestroyImmediate(go);}
                }
                camera.targetTexture=null;
            }
            finally
            {
                RenderTexture.active=previous;if(image!=null)Object.DestroyImmediate(image);
                if(rt!=null){rt.Release();Object.DestroyImmediate(rt);}EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        static void RenderWorld()
        {
            var settings=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationWorldSettings>("Assets/Data/World/ExplorationWorldSettings.asset"));
            settings.generationVersion=2;settings.preserveAuthoredCenter=false;settings.seed=7319;
            var field=new ExplorationTerrain(settings);
            var scene=EditorSceneManager.NewPreviewScene();
            var previous=RenderTexture.active;
            var rt=new RenderTexture(960,640,24);var image=new Texture2D(960,640,TextureFormat.RGB24,false);
            try
            {
                var camera=new GameObject("Biome camera").AddComponent<UnityEngine.Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.4f,.55f,.67f);camera.orthographic=true;camera.orthographicSize=29;camera.farClipPlane=500;camera.targetTexture=rt;
                var light=new GameObject("Biome sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-35,0);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);
                foreach(var biome in new[]{WorldBiome.Meadow,WorldBiome.Forest,WorldBiome.Desert,WorldBiome.Ice,WorldBiome.Mountains})
                {
                    Vector2 point=default;bool found=false;float best=float.MinValue;
                    var bounds=field.Plan.Bounds;
                    for(float z=bounds.yMin+96;z<bounds.yMax-96;z+=64)
                    for(float x=bounds.xMin+96;x<bounds.xMax-96;x+=64)
                    {
                        if(field.Biome(x,z)!=biome||field.Reserved(x,z,12))continue;
                        float h=field.Height(x,z),spread=Mathf.Abs(field.Height(x+20,z)-h)+Mathf.Abs(field.Height(x,z+20)-h);
                        float score=-spread;if(score<=best)continue;best=score;point=new Vector2(x,z);found=true;
                    }
                    Require(found,"Renderable biome: "+biome);
                    var root=new GameObject("Biome sample "+biome);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                    var chunks=root.AddComponent<ExplorationChunks>();
                    // Exercise the production chunk builder without starting a game session or touching scenes.
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    typeof(ExplorationChunks).GetProperty("Settings").SetValue(chunks,settings);
                    typeof(ExplorationChunks).GetField("field",flags).SetValue(chunks,field);
                    typeof(ExplorationChunks).GetField("trees",flags).SetValue(chunks,Array.Empty<Mesh>());
                    typeof(ExplorationChunks).GetField("content",flags).SetValue(chunks,new ExplorationContent(settings,field));
                    typeof(ExplorationChunks).GetField("material",flags).SetValue(chunks,Mismo.Core.ProjectAssets.Load<Material>("TerrainSurface"));
                    try
                    {
                        var center=ExplorationChunks.Coordinate(new Vector3(point.x,0,point.y));
                        for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)chunks.CreateChunk(center+new Vector2Int(x,z));
                        foreach(var node in root.GetComponentsInChildren<GatheringNode>())
                        {
                            var visual=(GameObject)typeof(GatheringNode).GetField("worldPrefab",flags).GetValue(node);
                            if(visual==null&&node.definition!=null)visual=node.definition.availablePrefab;
                            if(visual!=null)Object.Instantiate(visual,node.transform).transform.localPosition=Vector3.zero;
                        }
                        var target=new Vector3(point.x,field.Height(point.x,point.y)+2,point.y);
                        camera.transform.position=target+new Vector3(35,42,-45);camera.transform.LookAt(target);camera.Render();
                        RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();
                        File.WriteAllBytes("output/craftpix-nature/Biome_"+biome+".png",image.EncodeToPNG());
                    }
                    finally
                    {
                        // Chunk destruction normally owns these generated meshes in play mode.
                        var meshes=root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).Where(m=>m!=null&&!AssetDatabase.Contains(m)).Distinct().ToArray();
                        ((System.Collections.IDictionary)typeof(ExplorationChunks).GetField("chunks",flags).GetValue(chunks)).Clear();
                        typeof(ExplorationChunks).GetProperty("Settings").SetValue(chunks,null);
                        foreach(var owned in root.GetComponentsInChildren<GeneratedWorldMesh>())owned.Value=null;
                        Object.DestroyImmediate(root);foreach(var mesh in meshes)if(mesh!=null)Object.DestroyImmediate(mesh);
                    }
                }
                camera.targetTexture=null;
            }
            finally
            {
                RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(scene);Object.DestroyImmediate(settings);
            }
        }
    }
}
