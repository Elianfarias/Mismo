using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class GroveWorldIntegration
{
    const string Root="Assets/Art/Prefabs/World/EnchantedGrove/";
    const string StylePath="Assets/Data/World/GroveWorldStyle.asset";
    const string CatalogPath="Assets/Data/World/WorldContentCatalog.asset";
    const string Output="output/grove-world";
    static GameObject Prefab(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>(Root+name+".prefab")??throw new InvalidOperationException("Missing "+name);
    static WorldAssetEntry Entry(string name,WorldAssetKind kind,float weight,Vector2 footprint,float low=.8f,float high=1.15f,bool decorative=false)
        =>new WorldAssetEntry{id="grove."+name,kind=kind,prefab=Prefab(name),weight=weight,footprint=footprint,maxSlope=kind==WorldAssetKind.Tree?18:kind==WorldAssetKind.Rock?26:15,rotations=Array.Empty<float>(),scaleRange=new Vector2(low,high),decorativeOnly=decorative};
    static void Biome(WorldBiome biome)
    {
        var profile=AssetDatabase.LoadAssetAtPath<WorldBiomeContent>("Assets/Data/World/Biomes/"+biome+".asset");
        if(profile==null)throw new InvalidOperationException("Missing biome "+biome);
        bool forest=biome==WorldBiome.Forest,highland=biome==WorldBiome.Highlands;
        var entries=new List<WorldAssetEntry>();
        entries.Add(Entry("Tree_Sage",WorldAssetKind.Tree,forest?6:3,Vector2.one*1.3f,.88f,1.25f));
        entries.Add(Entry("Tree_Blossom",WorldAssetKind.Tree,forest?.8f:2,Vector2.one*1.3f,.8f,1.1f));
        entries.Add(Entry("Tree_Amber",WorldAssetKind.Tree,highland?4:1,Vector2.one*1.3f,.85f,1.2f));
        entries.Add(Entry("Tree_Cypress",WorldAssetKind.Tree,forest?2:highland?4:.5f,Vector2.one*1.1f,.85f,1.2f));
        entries.Add(Entry("Grass_Tuft",WorldAssetKind.Grass,2,Vector2.one*.8f,.7f,1,true));
        entries.Add(Entry("Fern",WorldAssetKind.Grass,forest?4:1,Vector2.one*1.3f,.65f,.95f,true));
        entries.Add(Entry("Shrub_Sage",WorldAssetKind.Bush,3,Vector2.one*1.5f,.65f,1,true));
        entries.Add(Entry("Shrub_Hydrangea",WorldAssetKind.Bush,forest?1:2,Vector2.one*1.5f,.65f,.95f,true));
        entries.Add(Entry("Flowers_Ivory",WorldAssetKind.Flower,3,Vector2.one*.8f,.65f,.95f));
        entries.Add(Entry("Flowers_Lavender",WorldAssetKind.Flower,2,Vector2.one*.8f,.65f,.9f));
        entries.Add(Entry("Flowers_Coral",WorldAssetKind.Flower,1,Vector2.one*.8f,.65f,.9f));
        entries.Add(Entry("Mushrooms",WorldAssetKind.Flower,forest?3:.5f,Vector2.one*.65f,.85f,1.1f));
        entries.Add(Entry("Rock_Moss_Small",WorldAssetKind.Rock,3,Vector2.one*1.1f,.8f,1.2f));
        entries.Add(Entry("Rock_Moss_Large",WorldAssetKind.Rock,1,Vector2.one*2,.8f,1.1f));
        // The kit has no fallen logs; keep the existing dedicated deadwood entries.
        entries.AddRange((profile.assets??Array.Empty<WorldAssetEntry>()).Where(a=>a!=null&&a.kind==WorldAssetKind.Deadwood));
        profile.assets=entries.ToArray();profile.trees=forest?.55f:highland?.2f:.18f;
        profile.grass=forest?.4f:.34f;profile.bushes=forest?.24f:.16f;profile.flowers=forest?.2f:.28f;profile.rocks=highland?.2f:.1f;
        EditorUtility.SetDirty(profile);
    }
    static GameObject Ruin()
    {
        var root=new GameObject("Grove_Ruin_Group");
        try
        {
            void Part(string name,Vector3 p,float yaw,float scale)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(Prefab(name));go.transform.SetParent(root.transform,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
            }
            Part("Ruin_Arch",Vector3.zero,0,1);Part("Ruin_Pillar",new Vector3(-2.8f,0,1.6f),12,.8f);Part("Ruin_Wall",new Vector3(2.6f,0,1.7f),90,.7f);
            Part("Vines_Short",new Vector3(-1.5f,2.6f,-.57f),0,.75f);Part("Fern",new Vector3(-2.7f,0,-.6f),22,.7f);Part("Mushrooms",new Vector3(2.5f,0,-.2f),0,.8f);
            return PrefabUtility.SaveAsPrefabAsset(root,Root+root.name+".prefab");
        }
        finally{Object.DestroyImmediate(root);}
    }
    [MenuItem("Mismo/World/Enchanted Grove/Integrar en mundo procedural")]
    public static void Integrate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before integrating assets");
        var catalog=AssetDatabase.LoadAssetAtPath<WorldContentCatalog>(CatalogPath);
        var style=AssetDatabase.LoadAssetAtPath<GroveWorldStyle>(StylePath);
        if(style==null){style=ScriptableObject.CreateInstance<GroveWorldStyle>();AssetDatabase.CreateAsset(style,StylePath);}
        style.shortVines=Prefab("Vines_Short");style.longVines=Prefab("Vines_Long");style.fireflies=Prefab("Fireflies");EditorUtility.SetDirty(style);catalog.groveStyle=style;
        const string waterPath="Assets/Art/Materials/Environment/EnchantedGrove/WorldWater.mat";
        var water=AssetDatabase.LoadAssetAtPath<Material>(waterPath);
        if(water==null){water=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/EnchantedGrove/PondWater.mat"));AssetDatabase.CreateAsset(water,waterPath);}
        water.SetFloat("_WorldSpaceSurface",1);EditorUtility.SetDirty(water);style.waterMaterial=water;
        foreach(var biome in new[]{WorldBiome.Meadow,WorldBiome.Forest,WorldBiome.Highlands})Biome(biome);
        foreach(string name in new[]{"Grass_Tuft","Fern","Flowers_Ivory","Flowers_Coral","Flowers_Lavender","Mushrooms"})
        {
            var root=PrefabUtility.LoadPrefabContents(Root+name+".prefab");
            try{var lod=root.GetComponent<LODGroup>();if(lod==null)lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.02f,root.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();PrefabUtility.SaveAsPrefabAsset(root,Root+name+".prefab");}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var entries=catalog.assets.Where(a=>a!=null&&a.id!="grove.ruins").ToList();
        foreach(var entry in entries.Where(a=>a.kind==WorldAssetKind.House)){entry.prefab=Prefab("Cottage_Exterior");entry.footprint=new Vector2(5.3f,5.3f);}
        entries.Add(new WorldAssetEntry{id="grove.ruins",prefab=Ruin(),kind=WorldAssetKind.Ruin,biomes=new[]{WorldBiome.Meadow,WorldBiome.Forest,WorldBiome.Highlands},footprint=new Vector2(7,4),maxSlope=8,rotations=new[]{0f,90f,180f,270f},decorativeOnly=true});
        catalog.assets=entries.ToArray();EditorUtility.SetDirty(catalog);
        foreach(var pair in new[]{("WoodNode","Tree_Sage"),("StoneNode","Rock_Moss_Small"),("HerbNode","Flowers_Ivory")})
        {
            var resource=AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>("Assets/Data/Gathering/"+pair.Item1+".asset");resource.availablePrefab=Prefab(pair.Item2);EditorUtility.SetDirty(resource);
        }
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/TerrainSurface.mat");
        material.SetColor("_GrassColor",style.meadowLow);material.SetColor("_DirtColor",style.path);material.SetFloat("_DetailStrength",.28f);material.SetFloat("_TextureScale",3);EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();RuntimeCatalogBuilder.Refresh();
    }
    static void Require(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    static void Checks()
    {
        var source=AssetDatabase.LoadAssetAtPath<ExplorationWorldSettings>("Assets/Data/World/ExplorationWorldSettings.asset");
        var settings=Object.Instantiate(source);var oldCatalog=Object.Instantiate(source.content);oldCatalog.groveStyle=null;
        var oldSettings=Object.Instantiate(source);oldSettings.content=oldCatalog;
        try
        {
            int samples=0;
            foreach(int version in new[]{0,1,2})foreach(int seed in new[]{7319,14857})
            {
                settings.seed=oldSettings.seed=seed;settings.generationVersion=oldSettings.generationVersion=version;settings.preserveAuthoredCenter=oldSettings.preserveAuthoredCenter=version==0;
                var terrain=new ExplorationTerrain(settings);var oldTerrain=new ExplorationTerrain(oldSettings);
                for(int z=-128;z<256;z+=17)for(int x=-128;x<256;x+=19)
                {
                    Require(terrain.Height(x,z)==oldTerrain.Height(x,z),"Appearance changed terrain height");Require(terrain.Reserved(x,z,2)==oldTerrain.Reserved(x,z,2),"Appearance changed reservations");samples++;
                    var color=terrain.Top(x,z,terrain.Height(x,z));Require(float.IsFinite(color.r)&&float.IsFinite(color.g),"Invalid terrain palette");
                }
            }
            foreach(var biome in new[]{WorldBiome.Meadow,WorldBiome.Forest,WorldBiome.Highlands})foreach(var kind in new[]{WorldAssetKind.Tree,WorldAssetKind.Grass,WorldAssetKind.Bush,WorldAssetKind.Flower,WorldAssetKind.Rock})
            {
                var entry=source.content.Asset(kind,biome,73);Require(entry!=null&&entry.id.StartsWith("grove."),"Unconverted "+biome+" "+kind);
                Require(source.content.Asset(kind,biome,73)==entry,"Nondeterministic selection");
            }
            var deps=AssetDatabase.GetDependencies(CatalogPath,true);
            Require(deps.Any(p=>p.EndsWith("GrovePalette.png"))&&deps.Any(p=>p.EndsWith("Tree_Sage_LOD2.asset")),"Missing runtime dependencies");
            File.WriteAllText(Output+"/checks.txt","PASS: "+samples+" height/reservation comparisons across generation versions 0/1/2 and two seeds; deterministic catalog selection; serialized palette and LOD dependencies.\n");
        }
        finally{Object.DestroyImmediate(settings);Object.DestroyImmediate(oldSettings);Object.DestroyImmediate(oldCatalog);}
        Mismo.Gameplay.Player.Editor.TerrainCoverageChecks.Run();
    }
    static void Preview()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var settings=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationWorldSettings>("Assets/Data/World/ExplorationWorldSettings.asset"));settings.preserveAuthoredCenter=false;settings.generationVersion=2;
        var terrain=new ExplorationTerrain(settings);var content=new ExplorationContent(settings,terrain);
        Vector2Int center=default;bool found=false;
        for(int z=-8;z<16&&!found;z++)for(int x=-8;x<16&&!found;x++)
        {
            float px=x*32+16,pz=z*32+16;
            if(terrain.Biome(px,pz)==WorldBiome.Forest&&!terrain.Reserved(px,pz,12)){center=new Vector2Int(x,z);found=true;}
        }
        Require(found,"No forest sample");
        var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/TerrainSurface.mat");int plants=0;
        for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
        {
            var chunk=center+new Vector2Int(x,z);var root=new GameObject("Procedural chunk "+chunk);var mesh=ExplorationChunks.BuildTerrain(terrain,chunk);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=mat;
            // Use the real scatter/placement queries; resolve harvest visuals for an editor-only image.
            content.Decorate(chunk,root.transform,mat);
            var random=new System.Random(ExplorationTerrain.Hash(settings.seed,chunk.x,chunk.y,377));
            for(int dz=4;dz<32;dz+=8)for(int dx=4;dx<32;dx+=8)
            {
                float px=chunk.x*32+dx,pz=chunk.y*32+dz;if(random.NextDouble()>settings.treeDensity*terrain.TreeDensity(px,pz))continue;
                var entry=settings.content.Asset(WorldAssetKind.Tree,terrain.Biome(px,pz),ExplorationTerrain.Hash(settings.seed,(int)px,(int)pz,300));if(entry==null)continue;
                float yaw=ExplorationContent.Rotation(entry,random);if(!ExplorationContent.TryPlaceNature(terrain,entry,new Vector2(px,pz),1,yaw,out float py))continue;
                Object.Instantiate(entry.prefab,new Vector3(px,py,pz),Quaternion.Euler(0,yaw,0),root.transform);plants++;
            }
            foreach(var node in root.GetComponentsInChildren<GatheringNode>())
            {
                var field=typeof(GatheringNode).GetField("worldPrefab",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var prefab=(GameObject)field.GetValue(node)??node.definition.availablePrefab;Object.Instantiate(prefab,node.transform);
            }
        }
        var point=new Vector3(center.x*32+16,0,center.y*32+16);point.y=terrain.Height(point.x,point.z);
        var sun=new GameObject("Validation sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.7f;sun.color=new Color(1,.95f,.87f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(42,-35,0);
        var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(new Color(.24f,.29f,.3f));RenderSettings.ambientProbe=probe;RenderSettings.fog=false;
        var camera=new GameObject("Validation camera").AddComponent<Camera>();camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.67f,.76f,.78f);camera.farClipPlane=250;camera.fieldOfView=55;
        camera.transform.position=point+new Vector3(25,22,-32);camera.transform.LookAt(point+Vector3.up*2);
        var target=new RenderTexture(1500,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var image=new Texture2D(1500,1000,TextureFormat.RGB24,false);
        bool batching=GraphicsSettings.useScriptableRenderPipelineBatching;GraphicsSettings.useScriptableRenderPipelineBatching=false;
        foreach(var r in Object.FindObjectsByType<MeshRenderer>())if(r.sharedMaterial.HasProperty("_BaseMap")){var b=new MaterialPropertyBlock();b.SetTexture("_BaseMap",r.sharedMaterial.GetTexture("_BaseMap")??Texture2D.whiteTexture);b.SetColor("_BaseColor",r.sharedMaterial.GetColor("_BaseColor"));r.SetPropertyBlock(b);}
        try{camera.targetTexture=target;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1500,1000),0,0);image.Apply();File.WriteAllBytes(Output+"/procedural-forest.png",image.EncodeToPNG());}
        finally{RenderTexture.active=null;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);Object.DestroyImmediate(settings);GraphicsSettings.useScriptableRenderPipelineBatching=batching;}
        Require(plants>0,"No trees placed");
    }
    public static void RunBatch()
    {
        try{Directory.CreateDirectory(Output);Integrate();Checks();Preview();try{ProjectOrganizationChecks.Run();}catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.Message);}Debug.Log("GROVE_WORLD_OK");EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void BuildBatch()
    {
        try
        {
            Directory.CreateDirectory(Output+"/build");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=Output+"/build/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText(Output+"/build.txt",result.summary.result+" errors="+result.summary.totalErrors+"\n"+string.Join("\n",result.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.content)));
            EditorApplication.Exit(result.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:1);
        }
        catch(Exception e){File.WriteAllText(Output+"/build.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
}

