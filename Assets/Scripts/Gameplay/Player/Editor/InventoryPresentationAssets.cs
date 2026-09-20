using System.IO;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    public static class InventoryPresentationAssets
    {
        [MenuItem("Mismo/Inventory/Create default material previews")]
        public static void CreateDefaults()
        {
            const string folder="Assets/Art/Prefabs/Inventory";
            const string materials="Assets/Art/Materials/Inventory";
            Directory.CreateDirectory(materials);Directory.CreateDirectory("Assets/Art/UI/Inventory");
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            foreach(var definition in Mismo.Core.ProjectAssets.LoadAll<MaterialDefinition>("Materials"))
            {
                if(definition.pickupPrefab!=null)continue;
                var root=new GameObject(definition.displayName);
                var color=definition.id==MaterialCatalog.Herb?new Color(.17f,.46f,.23f):definition.id==MaterialCatalog.Wood?new Color(.38f,.20f,.085f):definition.id==MaterialCatalog.Stone?new Color(.43f,.47f,.49f):new Color(.8f,.75f,.59f);
                string materialPath=materials+"/"+definition.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null){material=new Material(Shader.Find("Standard")){color=color};AssetDatabase.CreateAsset(material,materialPath);}
                if(definition.id==MaterialCatalog.Herb)
                {
                    Part(root,PrimitiveType.Cylinder,new Vector3(0,0,0),new Vector3(.055f,.5f,.055f),Vector3.zero,material);
                    for(int i=0;i<4;i++)Part(root,PrimitiveType.Cube,new Vector3(i%2==0?.16f:-.16f,-.25f+i*.17f,0),new Vector3(.35f,.1f,.17f),new Vector3(0,0,i%2==0?30:-30),material);
                }
                else if(definition.id==MaterialCatalog.Wood)
                {
                    Part(root,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.4f,.7f,.4f),new Vector3(0,0,75),material);
                    Part(root,PrimitiveType.Cylinder,new Vector3(.06f,-.26f,.35f),new Vector3(.36f,.6f,.36f),new Vector3(0,0,90),material);
                }
                else Part(root,PrimitiveType.Cube,Vector3.zero,definition.id==MaterialCatalog.Stone?new Vector3(.9f,.65f,.8f):new Vector3(.25f,.95f,.3f),new Vector3(12,25,18),material);
                definition.pickupPrefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+definition.name+".prefab");
                Object.DestroyImmediate(root);EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Mismo/Inventory/Generate missing grid icons")]
        public static void GenerateGridIcons()
        {
            CreateDefaults();
            foreach(var material in Mismo.Core.ProjectAssets.LoadAll<MaterialDefinition>("Materials"))
                if(material.icon==null&&material.pickupPrefab!=null){material.icon=Icon(material.pickupPrefab,"material-"+material.name,material.inventoryPreviewRotation);EditorUtility.SetDirty(material);}
            var catalog=Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog");
            if(catalog!=null)foreach(var weapon in catalog.weapons)
                if(weapon.inventoryIcon==null&&weapon.visualPrefab!=null){weapon.inventoryIcon=Icon(weapon.visualPrefab,"weapon-"+weapon.name,weapon.inventoryPreviewRotation);EditorUtility.SetDirty(weapon);}
            AssetDatabase.SaveAssets();
        }
        static Sprite Icon(GameObject source,string name,Vector3 rotation)
        {
            using(var preview=new InventoryPreview())
            {
                preview.Show(source,rotation);preview.Render();
                if(!preview.HasModel)return null;
                var old=RenderTexture.active;RenderTexture.active=(RenderTexture)preview.Texture;
                var texture=new Texture2D(preview.Texture.width,preview.Texture.height,TextureFormat.RGBA32,false);
                texture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);texture.Apply();RenderTexture.active=old;
                var pixels=texture.GetPixels32();int minX=texture.width,minY=texture.height,maxX=0,maxY=0;
                for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a>16){minX=Mathf.Min(x,minX);maxX=Mathf.Max(x,maxX);minY=Mathf.Min(y,minY);maxY=Mathf.Max(y,maxY);}
                if(minX>maxX){Object.DestroyImmediate(texture);return null;}
                minX=Mathf.Max(0,minX-4);minY=Mathf.Max(0,minY-4);maxX=Mathf.Min(texture.width-1,maxX+4);maxY=Mathf.Min(texture.height-1,maxY+4);
                var cropped=new Texture2D(maxX-minX+1,maxY-minY+1,TextureFormat.RGBA32,false);
                cropped.SetPixels(texture.GetPixels(minX,minY,cropped.width,cropped.height));cropped.Apply();
                string path="Assets/Art/UI/Inventory/"+name+".png";File.WriteAllBytes(path,cropped.EncodeToPNG());
                Object.DestroyImmediate(texture);Object.DestroyImmediate(cropped);AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        static void Part(GameObject parent,PrimitiveType type,Vector3 position,Vector3 scale,Vector3 rotation,Material material)
        {
            var piece=GameObject.CreatePrimitive(type);piece.transform.SetParent(parent.transform,false);piece.transform.localPosition=position;
            piece.transform.localScale=scale;piece.transform.localEulerAngles=rotation;piece.GetComponent<Renderer>().sharedMaterial=material;
            Object.DestroyImmediate(piece.GetComponent<Collider>());
        }
    }
}
