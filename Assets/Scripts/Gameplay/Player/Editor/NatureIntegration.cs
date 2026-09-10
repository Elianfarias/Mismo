using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class NatureIntegration
    {
        const string Source="Assets/Art/FBX/Nature/Nature";
        const string Output="Assets/Prefabs/World/Nature";
        const string Materials="Assets/Art/Materials/Nature";
        static readonly List<string> report=new List<string>();
        static Bounds BoundsOf(GameObject go)
        {var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new Exception("No mesh: "+go.name);var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
        static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
        static GameObject Model(string path,float height)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation=false;importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));go.name=Path.GetFileNameWithoutExtension(path);
            var b=BoundsOf(go);float scale=height/Mathf.Max(.001f,b.size.y);go.transform.localScale*=scale;
            b=BoundsOf(go);go.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
            string palette=Path.ChangeExtension(path,".png");
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(palette);if(texture==null)throw new Exception("Missing palette "+palette);
            // Several supplied PNGs are preview atlases, while FBX UVs address a
            // 256x1 MagicaVoxel palette. Recover the original colors from VOX.
            bool paletteUVs=go.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.uv.Length>0&&f.sharedMesh.uv.All(uv=>Mathf.Abs(uv.y-.5f)<.001f));
            if(texture.height>1&&paletteUVs)
            {
                byte[] vox=File.ReadAllBytes(Path.ChangeExtension(path,".vox"));bool found=false;
                for(int offset=20;offset+12<=vox.Length;)
                {
                    int size=BitConverter.ToInt32(vox,offset+4);
                    if(System.Text.Encoding.ASCII.GetString(vox,offset,4)=="RGBA")
                    {
                        var colors=new Color32[256];for(int i=0;i<256;i++){int p=offset+12+i*4;colors[i]=new Color32(vox[p],vox[p+1],vox[p+2],255);}
                        var recovered=new Texture2D(256,1,TextureFormat.RGBA32,false);recovered.SetPixels32(colors);recovered.Apply();
                        palette=Materials+"/"+go.name+"_Palette.png";File.WriteAllBytes(palette,recovered.EncodeToPNG());Object.DestroyImmediate(recovered);AssetDatabase.ImportAsset(palette);found=true;break;
                    }
                    if(size<0)break;offset+=12+size;
                }
                if(!found)throw new Exception("Missing VOX palette for "+path);
            }
            var ti=(TextureImporter)AssetImporter.GetAtPath(palette);ti.filterMode=FilterMode.Point;ti.wrapMode=TextureWrapMode.Clamp;ti.mipmapEnabled=false;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();
            string matPath=Materials+"/"+go.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,matPath);}
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(palette);mat.SetFloat("_Glossiness",0);mat.enableInstancing=true;EditorUtility.SetDirty(mat);
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=Enumerable.Repeat(mat,renderer.sharedMaterials.Length).ToArray();
            return go;
        }
        static WorldAssetEntry Save(GameObject root,WorldAssetKind kind)
        {
            var b=BoundsOf(root);bool small=kind==WorldAssetKind.Grass||kind==WorldAssetKind.Flower||kind==WorldAssetKind.Bush;
            if((!small&&kind!=WorldAssetKind.Tree)||kind==WorldAssetKind.Grass)
            {foreach(var filter in root.GetComponentsInChildren<MeshFilter>()){var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}}
            if(small)foreach(var renderer in root.GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=ShadowCastingMode.Off;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+root.name+".prefab");
            report.Add(root.name+" | "+kind+" | "+b.size.ToString("F2"));
            var entry=new WorldAssetEntry{id="nature."+root.name,kind=kind,prefab=prefab,footprint=new Vector2(b.size.x,b.size.z),maxSlope=small?28:kind==WorldAssetKind.House?12:20,
                biomes=kind==WorldAssetKind.Deadwood?new[]{WorldBiome.Forest}:Array.Empty<WorldBiome>(),scaleRange=kind==WorldAssetKind.House||kind==WorldAssetKind.Blacksmith?Vector2.one:new Vector2(.85f,1.15f)};
            Object.DestroyImmediate(root);return entry;
        }
        [MenuItem("Mismo/World/Integrate Nature assets")]
        public static void Apply()
        {
            report.Clear();Folder(Output);Folder(Materials);
            var entries=new List<WorldAssetEntry>();
            foreach(string path in Directory.GetFiles(Source,"*.fbx",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).OrderBy(p=>p))
            {
                string name=Path.GetFileNameWithoutExtension(path);if(name.StartsWith("canopy_")||name.StartsWith("trunk_"))continue;
                var kind=name.StartsWith("grass")?WorldAssetKind.Grass:name.StartsWith("flower")?WorldAssetKind.Flower:name.StartsWith("bush")?WorldAssetKind.Bush:WorldAssetKind.Rock;
                float height=kind==WorldAssetKind.Grass?.35f:kind==WorldAssetKind.Flower?.65f:kind==WorldAssetKind.Bush?1.2f:1.5f;
                var root=new GameObject(name);Model(path,height).transform.SetParent(root.transform,true);entries.Add(Save(root,kind));
            }
            for(int i=0;i<8;i++)
            {
                var root=new GameObject("Tree_"+i);float height=4.5f+(i%3)*.55f;
                Model(Source+"/trunks/trunk_"+i+"/trunk_"+i+".fbx",height).transform.SetParent(root.transform,true);
                int canopy=i%6;var crown=Model(Source+"/canopies/canopy_"+canopy+"/canopy_"+canopy+".fbx",3.8f);
                crown.transform.position+=Vector3.up*(height-1.6f);crown.transform.SetParent(root.transform,true);
                foreach(var filter in crown.GetComponentsInChildren<MeshFilter>()){var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}
                var trunk=root.AddComponent<BoxCollider>();trunk.center=Vector3.up*(height*.5f);trunk.size=new Vector3(.8f,height,.8f);root.AddComponent<ClimbableTree>();entries.Add(Save(root,WorldAssetKind.Tree));
            }
            for(int i=0;i<8;i++)
            {
                var root=new GameObject("Deadwood_"+i);var trunk=Model(Source+"/trunks/trunk_"+i+"/trunk_"+i+".fbx",2.5f);
                trunk.transform.SetParent(root.transform,true);root.transform.rotation=Quaternion.Euler(0,0,90);
                var b=BoundsOf(root);root.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
                var wrapper=new GameObject(root.name);root.transform.SetParent(wrapper.transform,true);entries.Add(Save(wrapper,WorldAssetKind.Deadwood));
            }
            string housePath="Assets/Art/FBX/Town/Medieval Town - Free Sample-2-House.obj";
            var houseImporter=(ModelImporter)AssetImporter.GetAtPath(housePath);houseImporter.isReadable=true;houseImporter.SaveAndReimport();
            foreach(var kind in new[]{WorldAssetKind.House,WorldAssetKind.Blacksmith})
            {
                var root=new GameObject(kind==WorldAssetKind.House?"MedievalHouse":"VillageWorkshop");
                var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(housePath));var b=BoundsOf(model);
                model.transform.localScale*=8/Mathf.Max(b.size.x,b.size.z);b=BoundsOf(model);model.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);model.transform.SetParent(root.transform,true);
                var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/FBX/Town/TownVoxelPalette.mat");if(mat==null)throw new Exception("Town palette missing");
                foreach(var r in model.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
                entries.Add(Save(root,kind));
            }
            var catalog=Resources.Load<WorldContentCatalog>("WorldContentCatalog");var existing=catalog.assets.ToList();
            foreach(var entry in entries){int index=existing.FindIndex(e=>e.id==entry.id);if(index<0)existing.Add(entry);else existing[index].prefab=entry.prefab;}
            catalog.assets=existing.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            Validate(catalog);Directory.CreateDirectory("Docs/Validation");File.WriteAllLines("Docs/Validation/NatureIntegration.txt",report);Debug.Log("NATURE_INTEGRATION_PASS "+entries.Count);
        }
        static void Validate(WorldContentCatalog catalog)
        {
            foreach(var asset in catalog.assets.Where(a=>a.id.StartsWith("nature.")))
            {
                if(asset.prefab==null)throw new Exception("Missing prefab "+asset.id);
                foreach(var collider in asset.prefab.GetComponentsInChildren<MeshCollider>())if(!collider.sharedMesh.isReadable)throw new Exception("Runtime navigation cannot read "+asset.id);
                foreach(var r in asset.prefab.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)
                    if(m==null||m.mainTexture==null||!m.shader.isSupported)throw new Exception("Invalid material "+asset.id);
                if(asset.kind==WorldAssetKind.Flower&&asset.prefab.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Flowers unexpectedly block navigation");
                if((asset.kind==WorldAssetKind.Grass||asset.kind==WorldAssetKind.Tree)&&asset.prefab.GetComponentsInChildren<MeshCollider>().Length==0)throw new Exception("Missing solid vegetation "+asset.id);
            }
            var settings=Object.Instantiate(Resources.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.preserveAuthoredCenter=false;
            var terrain=new ExplorationTerrain(settings);var content=new ExplorationContent(settings,terrain);
            int supported=0,rejected=0;
            for(int z=0;z<128;z+=3)for(int x=0;x<128;x+=3)
            {
                if(ExplorationContent.TrySupport(terrain,new Vector2(x,z),new Vector2(2.5f,1.5f),37,out float ground))supported++;else rejected++;
            }
            if(supported==0||rejected==0)throw new Exception("Log support must accept flat terraces and reject steps");
            report.Add("PASS log footprint support: "+supported+" flat placements; "+rejected+" uneven placements rejected");
            var first=new GameObject("Scatter A");var second=new GameObject("Scatter B");
            for(int z=0;z<4;z++)for(int x=0;x<4;x++){content.Scatter(new Vector2Int(x,z),first.transform);content.Scatter(new Vector2Int(x,z),second.transform);}
            if(first.transform.childCount==0||first.transform.childCount!=second.transform.childCount)throw new Exception("Missing/non deterministic scatter");
            for(int i=0;i<first.transform.childCount;i++)
            {var a=first.transform.GetChild(i);var b=second.transform.GetChild(i);if(a.name!=b.name||a.position!=b.position||a.localScale!=b.localScale)throw new Exception("Scatter determinism");if(terrain.Reserved(a.position.x,a.position.z))throw new Exception("Vegetation on reserved path");}
            report.Add("PASS deterministic ground cover: "+first.transform.childCount+" placements; clear reserved paths; palettes; solid grass/canopies; nonblocking flowers");Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(settings);
        }
        public static void RunBatch(){try{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Apply();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void RepairCaptureBatch(){try{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Apply();CaptureBatch();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void CaptureBatch()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var entries=Resources.Load<WorldContentCatalog>("WorldContentCatalog").assets.Where(a=>a.id.StartsWith("nature.")).ToArray();
                for(int i=0;i<entries.Length;i++){var go=Object.Instantiate(entries[i].prefab);go.transform.position=new Vector3(i%6*11,0,i/6*11);}
                var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-35,0);sun.intensity=1.1f;
                RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
                var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.transform.position=new Vector3(65,65,-65);camera.transform.LookAt(new Vector3(27,1,25));camera.orthographic=true;camera.orthographicSize=43;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.2f,.23f);
                var rt=new RenderTexture(1600,1200,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1600,1200,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1200),0,0);pixels.Apply();File.WriteAllBytes("Docs/Validation/NatureOverview.png",pixels.EncodeToPNG());
                foreach(var root in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))root.enabled=false;
                var cover=entries.Where(a=>a.kind==WorldAssetKind.Grass||a.kind==WorldAssetKind.Flower).ToArray();
                for(int i=0;i<cover.Length;i++){var go=Object.Instantiate(cover[i].prefab);go.transform.position=new Vector3(i%3*2,0,i/3*2);}
                camera.transform.position=new Vector3(5,4,-7);camera.transform.LookAt(new Vector3(2,.3f,1));camera.orthographicSize=3.4f;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,1200),0,0);pixels.Apply();File.WriteAllBytes("Docs/Validation/NatureGroundCover.png",pixels.EncodeToPNG());
                RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(pixels);rt.Release();Object.DestroyImmediate(rt);Debug.Log("NATURE_CAPTURE_PASS");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
