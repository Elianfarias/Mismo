using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    [InitializeOnLoad]
    public static class CraftpixNatureIntegration
    {
        const string Source="Assets/Art/FBX/Nature";
        public const string Prefabs="Assets/Art/Prefabs/Voxelized/Nature/Craftpix";
        const string Meshes="Assets/Art/Meshes/Voxelized/Nature/Craftpix";
        const string Materials="Assets/Art/Materials/Nature/Craftpix";
        const string Profiles="Assets/Data/World/Biomes";
        const string CatalogPath="Assets/Data/World/WorldContentCatalog.asset";
        static bool rebuildModels;
        static CraftpixNatureIntegration(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            const string request="Temp/CraftpixNature.request";
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
            string command=File.ReadAllText(request).Trim();File.Delete(request);
            try
            {
                if(command=="refresh")AssetDatabase.Refresh();
                else if(command=="check")CraftpixNatureChecks.Run();
                else if(command=="terrain-check")TerrainCoverageChecks.Run();
                else if(command=="build")CraftpixNatureChecks.Build();
                else if(command=="content-build")CraftpixNatureChecks.BuildContent();
                else if(command=="rebuild")
                {
                    rebuildModels=true;
                    try{Setup();}finally{rebuildModels=false;}
                }
                else if(command=="setup")Setup();
                else throw new ArgumentException("Unknown nature operation: "+command);
                File.WriteAllText("Temp/CraftpixNature.result","PASS "+command);
            }
            catch(Exception e){File.WriteAllText("Temp/CraftpixNature.result",e.ToString());Debug.LogException(e);}
        }
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
        }
        static void Move(string source,string target)
        {
            if(source==target)return;
            Folder(Path.GetDirectoryName(target).Replace('\\','/'));
            string guid=AssetDatabase.AssetPathToGUID(source);
            string error=AssetDatabase.MoveAsset(source,target);
            if(error.Length>0||AssetDatabase.AssetPathToGUID(target)!=guid)throw new InvalidOperationException("MoveAsset: "+source+" -> "+target+" "+error);
        }
        [MenuItem("Mismo/World/Preparar naturaleza Craftpix por bioma")]
        public static void Setup()
        {
            Directory.CreateDirectory("output/craftpix-nature");
            Folder(Prefabs);Folder(Meshes);Folder(Materials);Folder(Profiles);
            var catalog=AssetDatabase.LoadAssetAtPath<WorldContentCatalog>(CatalogPath);
            if(catalog==null)throw new InvalidOperationException("WorldContentCatalog missing");
            var generated=new List<WorldAssetEntry>();
            var report=new List<string>();
            foreach(string package in Directory.GetDirectories(Source,"craftpix-*").OrderBy(p=>p))
            {
                string pack=Path.GetFileName(package),textureFolder="Assets/Art/Textures/Nature/Craftpix/"+pack;
                foreach(string texture in Directory.GetFiles(package,"*.png",SearchOption.AllDirectories))
                {
                    string original=texture.Replace('\\','/'),target=textureFolder+"/"+Path.GetFileName(texture);
                    string guid=AssetDatabase.AssetPathToGUID(original);Move(original,target);report.Add("Moved texture (GUID "+guid+"): "+original+" -> "+target);
                }
                string previews=package.Replace('\\','/')+"/Preview";
                if(AssetDatabase.IsValidFolder(previews))
                {
                    Move(previews,textureFolder+"/Preview");report.Add("Moved source previews: "+previews+" -> "+textureFolder+"/Preview");
                }
                string[] textures=AssetDatabase.FindAssets("t:Texture2D",new[]{textureFolder})
                    .Where(g=>Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(g)).Replace('\\','/')==textureFolder).ToArray();
                if(textures.Length!=1)throw new InvalidOperationException("Expected one Craftpix palette: "+pack);
                var palette=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(textures[0]));
                string materialPath=Materials+"/"+palette.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null)
                {
                    material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=palette.name,mainTexture=palette};
                    material.SetFloat("_Smoothness",0);material.enableInstancing=true;AssetDatabase.CreateAsset(material,materialPath);
                }
                foreach(var path in Directory.GetFiles(package,"*",SearchOption.AllDirectories).Where(p=>Path.GetExtension(p).Equals(".fbx",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p))
                {
                    string model=Path.GetFileNameWithoutExtension(path);
                    // Deliberate small palette: avoid preloading every variant in the source packs.
                    if(!Selected(model))continue;
                    WorldAssetKind kind=model.StartsWith("Tree",StringComparison.OrdinalIgnoreCase)?WorldAssetKind.Tree:model.StartsWith("Bush")?WorldAssetKind.Bush:WorldAssetKind.Rock;
                    bool winter=model.IndexOf("winter",StringComparison.OrdinalIgnoreCase)>=0,desert=model.Contains("desert"),hill=model.StartsWith("Hill");
                    float size=kind==WorldAssetKind.Tree?7:kind==WorldAssetKind.Bush?1.4f:hill?5:2.2f;
                    string name="Craftpix_"+model,target=Prefabs+"/"+name+".prefab";
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(target);
                    if(prefab==null||rebuildModels)prefab=Voxelize(path.Replace('\\','/'),name,target,material,size,kind);
                    var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;
                    var entry=new WorldAssetEntry{id=name,prefab=prefab,kind=kind,decorativeOnly=hill||kind==WorldAssetKind.Bush,
                        biomes=desert?new[]{WorldBiome.Desert}:winter?new[]{WorldBiome.Ice,WorldBiome.Mountains}:new[]{WorldBiome.Meadow,WorldBiome.Forest,WorldBiome.Highlands,WorldBiome.Mountains},
                        footprint=kind==WorldAssetKind.Tree?Vector2.one*1.2f:new Vector2(mesh.bounds.size.x,mesh.bounds.size.z),
                        maxSlope=kind==WorldAssetKind.Tree?22:hill?15:28,scaleRange=new Vector2(.85f,1.15f),rotations=Array.Empty<float>()};
                    generated.Add(entry);report.Add(name+" | "+kind+" | "+mesh.vertexCount+" vertices | "+mesh.bounds.size+" | "+string.Join(",",entry.biomes));
                }
            }
            // Ground cover already present under Nature completes the temperate biomes.
            foreach(string model in new[]{"grass_0","grass_1","bush_0","bush_1","flower_rose","flower_white_tulip","trunk_0"})
            {
                string path=Directory.GetFiles(Source+"/Nature",model+".fbx",SearchOption.AllDirectories).Single().Replace('\\','/');
                var kind=model.StartsWith("grass")?WorldAssetKind.Grass:model.StartsWith("bush")?WorldAssetKind.Bush:model.StartsWith("flower")?WorldAssetKind.Flower:WorldAssetKind.Deadwood;
                string name="Nature_"+model,target=Prefabs+"/"+name+".prefab";
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Nature/"+model+".mat");
                if(material==null)throw new InvalidOperationException("Missing ground-cover material: "+model);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(target);
                if(prefab==null||rebuildModels)prefab=Voxelize(path,name,target,material,kind==WorldAssetKind.Deadwood?2.5f:kind==WorldAssetKind.Bush?1.2f:kind==WorldAssetKind.Flower?.45f:.4f,kind);
                var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;
                generated.Add(new WorldAssetEntry{id=name,kind=kind,prefab=prefab,decorativeOnly=kind==WorldAssetKind.Bush||kind==WorldAssetKind.Grass,
                    biomes=new[]{WorldBiome.Meadow,WorldBiome.Forest,WorldBiome.Highlands},
                    footprint=new Vector2(mesh.bounds.size.x,mesh.bounds.size.z),maxSlope=28,
                    rotations=Array.Empty<float>(),scaleRange=new Vector2(.85f,1.15f)});
                report.Add(name+" | "+kind+" | "+mesh.vertexCount+" vertices | "+mesh.bounds.size);
            }
            var profiles=new List<WorldBiomeContent>();
            foreach(WorldBiome biome in Enum.GetValues(typeof(WorldBiome)))
            {
                string path=Profiles+"/"+biome+".asset";
                var profile=AssetDatabase.LoadAssetAtPath<WorldBiomeContent>(path);
                // Re-running setup never overwrites hand-tuned biome settings.
                if(profile==null)
                {
                    profile=ScriptableObject.CreateInstance<WorldBiomeContent>();profile.biome=biome;
                    profile.trees=biome==WorldBiome.Forest?.7f:biome==WorldBiome.Meadow?.08f:biome==WorldBiome.Highlands?.18f:biome==WorldBiome.Ice?.22f:biome==WorldBiome.Mountains?.06f:0;
                    bool temperate=biome==WorldBiome.Meadow||biome==WorldBiome.Forest||biome==WorldBiome.Highlands;
                    profile.grass=temperate?.4f:0;profile.flowers=temperate?.06f:0;profile.deadwood=temperate?.015f:0;
                    profile.bushes=biome==WorldBiome.Ice?.12f:temperate?.1f:0;
                    profile.rocks=biome==WorldBiome.Ocean?0:biome==WorldBiome.Meadow?.06f:biome==WorldBiome.Forest?.05f:.18f;
                    profile.assets=biome==WorldBiome.Ocean?Array.Empty<WorldAssetEntry>():generated.Where(e=>WorldContentCatalog.Allows(e.biomes,biome)&&!(biome==WorldBiome.Mountains&&e.kind==WorldAssetKind.Tree&&!e.id.Contains("Winter"))).ToArray();
                    AssetDatabase.CreateAsset(profile,path);
                }
                profiles.Add(profile);
            }
            // Preserve authored profile assignments and all non-nature content.
            var assigned=(catalog.biomeContents??Array.Empty<WorldBiomeContent>()).Where(p=>p!=null).ToList();
            foreach(var profile in profiles)if(!assigned.Any(p=>p.biome==profile.biome))assigned.Add(profile);
            catalog.biomeContents=assigned.ToArray();
            catalog.assets=catalog.assets.Where(e=>e==null||!WorldBiomeContent.IsNature(e.kind)).ToArray();
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            File.AppendAllLines("output/craftpix-nature/Integration.txt",report);
            CraftpixNatureChecks.Run();
            Selection.activeObject=catalog;
            Debug.Log("CRAFTPIX_NATURE_SETUP_OK: "+generated.Count+" voxelized models, 7 biome profiles");
        }
        static bool Selected(string name)
        {
            string[] prefixes={"Tree_temp_climate_","Tree_Winter_","Bush_winter_","Hill_winter_","Hill_desert_","Stone_mid_"};
            return prefixes.Any(p=>name==p+"001"||name==p+"002"||name==p+"003");
        }
        static GameObject Voxelize(string path,string name,string target,Material material,float size,WorldAssetKind kind)
        {
            // FBX roots can contain an authored axis correction. The voxelizer removes
            // the sampling root rotation, so keep that correction on a child instead.
            var source=new GameObject(name+" sampling root");
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),source.transform,false);
            try
            {
                foreach(var renderer in source.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();
                int resolution=kind==WorldAssetKind.Tree?32:kind==WorldAssetKind.Grass?12:kind==WorldAssetKind.Flower?16:24;
                var prefab=WeaponVoxelizerWindow.ExportModel(source,name,resolution,false);
                string temporaryPrefab=AssetDatabase.GetAssetPath(prefab);
                var generatedMesh=prefab.GetComponent<MeshFilter>().sharedMesh;
                string temporaryMesh=AssetDatabase.GetAssetPath(generatedMesh);
                string meshPath=Meshes+"/"+name+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(mesh==null){Move(temporaryMesh,meshPath);mesh=generatedMesh;temporaryMesh=null;}
                else EditorUtility.CopySerialized(generatedMesh,mesh);
                var palette=prefab.GetComponent<Renderer>().sharedMaterials;
                string fallback="Assets/Art/Materials/"+name+"_Voxel.mat";
                string fallbackTarget=Materials+"/"+name+"_Voxel.mat";
                var stableMaterial=AssetDatabase.LoadAssetAtPath<Material>(fallbackTarget);
                if(stableMaterial==null){Move(fallback,fallbackTarget);fallback=null;}
                else palette[0]=stableMaterial;
                Bounds b=mesh.bounds;bool heightSized=kind==WorldAssetKind.Tree||kind==WorldAssetKind.Bush||kind==WorldAssetKind.Flower||kind==WorldAssetKind.Grass;
                float factor=size/(heightSized?b.size.y:Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z)));
                Vector3 pivot=new Vector3(b.center.x,b.min.y,b.center.z);
                mesh.vertices=mesh.vertices.Select(v=>(v-pivot)*factor).ToArray();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                if(AssetDatabase.LoadAssetAtPath<GameObject>(target)==null){Move(temporaryPrefab,target);temporaryPrefab=null;}
                var root=PrefabUtility.LoadPrefabContents(target);
                try
                {
                    root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterials=palette;
                    foreach(var collider in root.GetComponents<Collider>())Object.DestroyImmediate(collider);
                    if(kind==WorldAssetKind.Tree)
                    {
                        var trunk=root.AddComponent<CapsuleCollider>();trunk.height=size*.55f;trunk.radius=.35f;trunk.center=Vector3.up*trunk.height*.5f;
                        if(root.GetComponent<ClimbableTree>()==null)root.AddComponent<ClimbableTree>();
                    }
                    else if(kind==WorldAssetKind.Rock)
                    {
                        var collider=root.AddComponent<BoxCollider>();collider.center=mesh.bounds.center;collider.size=mesh.bounds.size;
                    }
                    return PrefabUtility.SaveAsPrefabAsset(root,target);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                    // Only intermediate assets created above are deleted; stable assets keep their GUIDs.
                    if(temporaryPrefab!=null)AssetDatabase.DeleteAsset(temporaryPrefab);
                    if(temporaryMesh!=null)AssetDatabase.DeleteAsset(temporaryMesh);
                    if(fallback!=null)AssetDatabase.DeleteAsset(fallback);
                }
            }
            finally{Object.DestroyImmediate(source);}
        }
    }
}
