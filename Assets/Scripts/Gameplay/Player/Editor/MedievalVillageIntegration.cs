using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Mismo.Gameplay.Player.World;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class MedievalVillageIntegration
    {
        const string Source="Assets/Art/FBX/Town/Medieval Assets/OBJ_OP";
        const string Output="Assets/Prefabs/World/Villages";
        const string Art="Assets/Art/Materials/MedievalVillage";
        static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        static Bounds BoundsOf(GameObject root){var rs=root.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);return bounds;}
        [MenuItem("Mismo/World/Integrate medieval village")]
        public static void Apply()
        {
            Folder(Output);Folder(Art);
            string path=Art+"/MedievalVillage.obj";
            int removedFaces=0;bool omit=false;var obj=new List<string>();
            foreach(string line in File.ReadLines(Source+"/Medieval_Voxel_Assets_Example.obj"))
            {
                if(line.StartsWith("mtllib ")){obj.Add("mtllib MedievalVillage.mtl");continue;}
                if(line.StartsWith("usemtl ")){string name=line.Substring(7);omit=name.StartsWith("Ground")||name.StartsWith("Wall_Entrance_Door")||name.StartsWith("Entrance_Bridge_Close")||name.StartsWith("Entrance_Bridge_MidOpen");}
                if(omit&&line.StartsWith("f ")){removedFaces++;continue;}obj.Add(line);
            }
            File.WriteAllLines(path,obj);File.WriteAllLines(Art+"/MedievalVillage.mtl",File.ReadLines(Source+"/Medieval_Voxel_Assets_Example.mtl").Where(line=>!line.StartsWith("map_")));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
            var root=new GameObject("MedievalVillage");var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));model.name="Example layout";
            var b=BoundsOf(model);float scale=44/Mathf.Max(b.size.x,b.size.z);model.transform.localScale*=scale;b=BoundsOf(model);model.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);model.transform.SetParent(root.transform,true);
            int removed=removedFaces;var notes=new List<string>();
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();var materials=renderer.sharedMaterials;
                var mesh=filter.sharedMesh;
                for(int slot=0;slot<materials.Length;slot++)
                {
                    string name=materials[slot]!=null?materials[slot].name:"Ground";name=name.Replace(" (Instance)","");int dot=name.IndexOf('.');if(dot>=0)name=name.Substring(0,dot);
                    string materialPath=Art+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,materialPath);}
                    var texturePath=Directory.GetFiles(Source,name+".png",SearchOption.AllDirectories).FirstOrDefault();
                    if(texturePath!=null)
                    {
                        texturePath=texturePath.Replace('\\','/');var ti=(TextureImporter)AssetImporter.GetAtPath(texturePath);ti.filterMode=FilterMode.Point;ti.mipmapEnabled=true;ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
                        material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);material.color=new Color(.8f,.8f,.8f);
                    }
                    else if(!name.StartsWith("Ground"))throw new Exception("Missing texture for "+name);
                    else material.color=new Color(.38f,.4f,.4f);
                    material.SetFloat("_Glossiness",0);material.enableInstancing=true;EditorUtility.SetDirty(material);materials[slot]=material;
                    notes.Add(name+" texture="+(texturePath??"stone tint"));
                }
                filter.sharedMesh=mesh;renderer.sharedMaterials=materials;filter.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            b=BoundsOf(model);model.transform.localScale*=44/Mathf.Max(b.size.x,b.size.z);b=BoundsOf(model);model.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
            // Keep the world's spawn origin in the connected plaza, outside the
            // small enclosed structure at the geometric centre of the example.
            model.transform.position+=Vector3.forward*2.3f;
            // The levelled terrain is the village floor; no artificial platform.
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Output+"/MedievalVillage.prefab");Object.DestroyImmediate(root);
            var catalog=Resources.Load<WorldContentCatalog>("WorldContentCatalog");var entries=catalog.assets.ToList();var entry=entries.Find(a=>a.id=="medieval.village");
            if(entry==null){entry=new WorldAssetEntry{id="medieval.village",kind=WorldAssetKind.Village,weight=1,footprint=new Vector2(44,44),scaleRange=Vector2.one,rotations=new[]{0f}};entries.Add(entry);}
            entry.prefab=prefab;catalog.assets=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            if(removed==0)throw new Exception("No closed gates removed");
            notes.Add("PASS village catalog registered, "+removed+" closed gate/bridge and backdrop faces removed");
            Directory.CreateDirectory("Docs/Validation");File.WriteAllLines("Docs/Validation/MedievalVillage.txt",notes);
            Debug.Log("MEDIEVAL_VILLAGE_IMPORTED");
        }
        public static void RunBatch(){try{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Apply();Capture();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        static void Capture()
        {
            var village=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/MedievalVillage.prefab"));
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(45,-35,0);
            RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);
            var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.transform.position=new Vector3(45,48,-55);camera.transform.LookAt(Vector3.up*3);camera.orthographic=true;camera.orthographicSize=32;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.2f);
            var rt=new RenderTexture(1400,1100,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1400,1100,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1100),0,0);image.Apply();File.WriteAllBytes("Docs/Validation/MedievalVillage.png",image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);
            Debug.Log("MEDIEVAL_VILLAGE_CAPTURED");
        }
    }
}
