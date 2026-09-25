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
        [MenuItem("Mismo/Inventory/Regenerate weapon grid icons")]
        public static void RegenerateWeaponIcons()
        {
            var catalog=Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog");
            if(catalog==null)throw new System.InvalidOperationException("No se encontró ItemCatalog.");
            foreach(var weapon in catalog.weapons)
                if(weapon!=null&&weapon.visualPrefab!=null)RegenerateWeaponIcon(weapon);
            AssetDatabase.SaveAssets();
        }

        public static void RegenerateWeaponIcon(Equipment.WeaponDefinition weapon,GameObject source=null)
        {
            if(weapon==null)throw new System.ArgumentNullException(nameof(weapon));
            source=source!=null?source:weapon.visualPrefab;
            if(source==null)throw new System.InvalidOperationException("El arma no tiene modelo visual.");
            string path=AssetDatabase.GetAssetPath(weapon.inventoryIcon);
            // Only overwrite this generator's own standalone images, never an atlas or imported art.
            if(!path.StartsWith("Assets/Art/UI/Inventory/")||!path.EndsWith(".png")||
                !(AssetImporter.GetAtPath(path) is TextureImporter importer)||importer.spriteImportMode!=SpriteImportMode.Single)
                path="Assets/Art/UI/Inventory/weapon-"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(weapon))+".png";
            var icon=InventoryIconCapture.Capture(source,path,weapon.inventoryPreviewRotation);
            if(icon==null)throw new System.InvalidOperationException("No se pudo fotografiar "+weapon.name);
            Undo.RecordObject(weapon,"Actualizar modelo e icono del arma");
            weapon.visualPrefab=source;
            weapon.inventoryIcon=icon;
            EditorUtility.SetDirty(weapon);
        }

        static Sprite Icon(GameObject source,string name,Vector3 rotation)
            => InventoryIconCapture.Capture(source,"Assets/Art/UI/Inventory/"+name+".png",rotation);
        static void Part(GameObject parent,PrimitiveType type,Vector3 position,Vector3 scale,Vector3 rotation,Material material)
        {
            var piece=GameObject.CreatePrimitive(type);piece.transform.SetParent(parent.transform,false);piece.transform.localPosition=position;
            piece.transform.localScale=scale;piece.transform.localEulerAngles=rotation;piece.GetComponent<Renderer>().sharedMaterial=material;
            Object.DestroyImmediate(piece.GetComponent<Collider>());
        }
    }
}
