using System.IO;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEngine;
namespace Mismo.Gameplay.Player.Editor
{
    public static class GatheringAssets
    {
        const string Art="Assets/Art/Prefabs/Gathering";
        const string Audio="Assets/Art/Audio/Gathering";
        const string Materials="Assets/Art/Materials/Gathering";
        static T Asset<T>(string path)where T:ScriptableObject
        {var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset!=null)return asset;asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;}
        [MenuItem("Mismo/Crafting/Create surface defaults")]
        public static void Create()
        {
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Audio);Directory.CreateDirectory(Materials);Directory.CreateDirectory("Assets/Data/Crafting/Recipes");AssetDatabase.Refresh();
            var settings=Asset<GatheringSettings>("Assets/Data/Gathering/GatheringSettings.asset");
            settings.herb=Mismo.Core.ProjectAssets.Load<ResourceNodeDefinition>("Gathering/HerbNode");settings.tree=Mismo.Core.ProjectAssets.Load<ResourceNodeDefinition>("Gathering/WoodNode");settings.stone=Mismo.Core.ProjectAssets.Load<ResourceNodeDefinition>("Gathering/StoneNode");
            settings.monsterLoot=Mismo.Core.ProjectAssets.Load<MaterialLootTable>("Gathering/MonsterComponentLoot");
            Node(settings.herb,0);Node(settings.tree,1);Node(settings.stone,2);
            if(settings.workbenchPrefab==null)settings.workbenchPrefab=Model("Workbench",3,false);
            var potion=Asset<MaterialDefinition>("Assets/Data/Inventory/Materials/HerbalSalve.asset");
            potion.id="MAT-05";potion.displayName="Ungüento de hierbas";potion.description="Recupera vida gradualmente fuera de combate. El daño interrumpe el efecto.";
            potion.category=InventoryItemCategory.Consumable;potion.healingAmount=40;potion.healingSeconds=5;potion.stackSize=5;potion.gridWidth=potion.gridHeight=1;
            if(potion.pickupPrefab==null)potion.pickupPrefab=Model("HerbalSalve",4,false);EditorUtility.SetDirty(potion);
            var salve=Asset<CraftingRecipe>("Assets/Data/Crafting/Recipes/HerbalSalve.asset");salve.id="REC-01";salve.displayName=potion.displayName;salve.description=potion.description;
            salve.ingredients=new[]{new RecipeIngredient{material=Mismo.Core.ProjectAssets.Load<MaterialDefinition>("Materials/Herb"),quantity=2}};salve.result=potion;salve.quantity=1;EditorUtility.SetDirty(salve);
            var upgrade=Asset<CraftingRecipe>("Assets/Data/Crafting/Recipes/InitialWeaponUpgrade.asset");upgrade.id="REC-02";upgrade.displayName="Mejora inicial del arma";upgrade.description="Mejora el ejemplar seleccionado conservando su variante y maestría.";
            upgrade.upgradeWeapon=true;upgrade.fromTier=1;upgrade.toTier=2;
            upgrade.ingredients=new[]{new RecipeIngredient{material=Mismo.Core.ProjectAssets.Load<MaterialDefinition>("Materials/Wood"),quantity=2},new RecipeIngredient{material=Mismo.Core.ProjectAssets.Load<MaterialDefinition>("Materials/Stone"),quantity=3},new RecipeIngredient{material=Mismo.Core.ProjectAssets.Load<MaterialDefinition>("Materials/MonsterComponent"),quantity=1}};
            EditorUtility.SetDirty(upgrade);settings.recipes=new[]{salve,upgrade};EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
            InventoryPresentationAssets.GenerateGridIcons();
        }
        static void Node(ResourceNodeDefinition node,int kind)
        {
            if(node==null)return;
            if(node.availablePrefab==null)node.availablePrefab=Model(node.name,kind,false);
            if(node.depletedPrefab==null)node.depletedPrefab=Model(node.name+"Depleted",kind,true);
            if(node.toolPrefab==null&&kind!=0)node.toolPrefab=Model(kind==1?"Axe":"Pick",5+kind,false);
            node.kind=(ResourceNodeKind)kind;EditorUtility.SetDirty(node);
            if(node.harvestSound==null)node.harvestSound=Sound("Harvest-"+kind,kind==2?1100:kind==1?260:650);
            if(node.completionSound==null)node.completionSound=Sound("Harvest-complete",880);
        }
        static AudioClip Sound(string name,int frequency)
        {
            string path=Audio+"/"+name+".wav";
            if(!File.Exists(path))
            {
                const int rate=22050,samples=3307;
                using(var writer=new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);
                    for(int i=0;i<samples;i++){float t=i/(float)rate;writer.Write((short)(Mathf.Sin(t*frequency*Mathf.PI*2)*Mathf.Exp(-t*35)*6500));}
                }
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static GameObject Model(string name,int kind,bool depleted)
        {
            string path=Art+"/"+name+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var root=new GameObject(name);
            if(kind==0)
            {
                Part(root,new Vector3(0,.15f,0),new Vector3(.15f,depleted?.25f:.9f,.15f),new Color(.2f,.4f,.12f),false);
                if(!depleted)for(int i=0;i<4;i++)Part(root,new Vector3(i%2==0?.2f:-.2f,.3f+i*.17f,0),new Vector3(.45f,.14f,.3f),new Color(.3f,.65f,.2f),false);
            }
            else if(kind==1)
            {
                Part(root,new Vector3(0,depleted?.25f:1.3f,0),new Vector3(.65f,depleted?.5f:2.6f,.65f),new Color(.32f,.17f,.07f),true);
                if(!depleted){Part(root,new Vector3(0,3,0),new Vector3(2,1.5f,2),new Color(.19f,.42f,.13f),false);Part(root,new Vector3(.4f,3.9f,0),new Vector3(1.3f,1,1.5f),new Color(.3f,.5f,.14f),false);}
            }
            else if(kind==2)
            {
                Part(root,new Vector3(0,depleted?.2f:.55f,0),new Vector3(1.3f,depleted?.4f:1.1f,1.1f),new Color(.35f,.39f,.44f),true);
                if(!depleted)for(int i=0;i<3;i++)Part(root,new Vector3(-.4f+i*.4f,1.12f,0),new Vector3(.25f,.2f,.4f),new Color(.7f,.78f,.85f),false);
            }
            else if(kind==3)
            {
                Part(root,new Vector3(0,1,0),new Vector3(1.8f,.22f,.9f),new Color(.36f,.22f,.11f),true);
                foreach(float x in new[]{-.7f,.7f})foreach(float z in new[]{-.3f,.3f})Part(root,new Vector3(x,.5f,z),new Vector3(.15f,1,.15f),new Color(.25f,.15f,.07f),true);
            }
            else if(kind==4)
            {Part(root,new Vector3(0,.2f,0),new Vector3(.35f,.4f,.35f),new Color(.2f,.55f,.23f),false);Part(root,new Vector3(0,.44f,0),new Vector3(.4f,.12f,.4f),new Color(.7f,.65f,.4f),false);}
            else
            {Part(root,Vector3.zero,new Vector3(.08f,.8f,.08f),new Color(.4f,.22f,.1f),false);Part(root,new Vector3(.12f,.3f,0),new Vector3(kind==6?.35f:.65f,kind==6?.3f:.1f,.13f),Color.gray,false);}
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return prefab;
        }
        static void Part(GameObject root,Vector3 p,Vector3 scale,Color color,bool collider)
        {
            var piece=GameObject.CreatePrimitive(PrimitiveType.Cube);piece.transform.SetParent(root.transform,false);piece.transform.localPosition=p;piece.transform.localScale=scale;
            string path=Materials+"/Color-"+ColorUtility.ToHtmlStringRGB(color)+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){color=color};AssetDatabase.CreateAsset(mat,path);}
            piece.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)Object.DestroyImmediate(piece.GetComponent<Collider>());
        }
    }
}
