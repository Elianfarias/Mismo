using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEngine;
namespace Mismo.Gameplay.Player.Editor
{
    public static class CraftingExpansionAssets
    {
        static T Ensure<T>(string path,Action<T> setup)where T:ScriptableObject
        {
            var item=AssetDatabase.LoadAssetAtPath<T>(path);if(item!=null)return item;
            item=ScriptableObject.CreateInstance<T>();setup(item);AssetDatabase.CreateAsset(item,path);return item;
        }
        [MenuItem("Mismo/Crafting/Add expedition recipes")]
        public static void Create()
        {
            var settings=Mismo.Core.ProjectAssets.Load<GatheringSettings>("GatheringSettings");
            var nodes=settings.minerals.Where(n=>n!=null).OrderBy(n=>n.name,StringComparer.Ordinal).ToArray();
            if(nodes.Length<3)
            {
                nodes=Mismo.Core.ProjectAssets.LoadAll<ResourceNodeDefinition>("Gathering/Minerals").OrderBy(n=>n.name,StringComparer.Ordinal).ToArray();
                if(nodes.Length<3)throw new InvalidOperationException("Import mineral models first. Loaded nodes: "+nodes.Length);
                settings.minerals=nodes;
            }
            var iron=Material("Iron","MAT-06","Hierro","Metal para fabricar armas y mejorar su tier.",nodes[1].availablePrefab);
            var crystal=Material("ArcaneCrystal","MAT-07","Cristal arcano","Cristal poco frecuente para mejoras avanzadas y pociones.",nodes[2].availablePrefab);
            var potion=Ensure<MaterialDefinition>("Assets/Data/Inventory/Materials/HealthPotion.asset",m=>{
                m.id="MAT-08";m.displayName="Poción de curación";m.description="Recupera 35 de vida al instante, incluso en combate. 20 s entre pociones.";
                m.category=InventoryItemCategory.Consumable;m.stackSize=5;m.healingAmount=35;m.instantHealing=true;m.usableInCombat=true;m.useCooldownSeconds=20;
                m.pickupPrefab=ConsumableModel("HealthPotion",true);
            });
            var whetstone=Ensure<MaterialDefinition>("Assets/Data/Inventory/Materials/Whetstone.asset",m=>{
                m.id="MAT-09";m.displayName="Piedra de afilar";m.description="Prepara el arma activa fuera de combate: +15 % de daño durante 180 s. No se acumula.";
                m.category=InventoryItemCategory.Consumable;m.stackSize=5;m.damageBonus=.15f;m.damageBonusSeconds=180;
                m.pickupPrefab=ConsumableModel("Whetstone",false);
            });
            // Only migrate the original all-stone defaults. Preserve edited loot tables on later runs.
            int crystals=0,irons=0;
            foreach(var node in nodes)
            {
                var entries=node.rewards?.entries;
                if(entries==null||entries.Length!=1||entries[0].material?.id!="MAT-03"||node.displayName!="Piedra")continue;
                var colors=node.availablePrefab.GetComponent<ResourceImpactPalette>()?.colors;
                bool arcane=colors!=null&&colors.Any(c=>c.b>c.r*1.15f&&c.b>c.g*.9f&&c.b>.3f);
                entries[0].material=arcane?crystal:iron;entries[0].minimum=1;entries[0].maximum=arcane?1:2;
                node.displayName=arcane?"Mena de cristal arcano":"Mena de hierro";
                node.distributionWeight=arcane?.45f:1f;
                node.harvestSeconds=arcane?2.5f:2;node.regenerationSeconds=arcane?1200:900;
                if(arcane)crystals++;else irons++;
                EditorUtility.SetDirty(node);EditorUtility.SetDirty(node.rewards);
            }
            if(iron.icon==null){iron.pickupPrefab=nodes.FirstOrDefault(n=>n.rewards?.entries?.Any(e=>e.material==iron)==true)?.availablePrefab??iron.pickupPrefab;EditorUtility.SetDirty(iron);}
            if(crystal.icon==null){crystal.pickupPrefab=nodes.FirstOrDefault(n=>n.rewards?.entries?.Any(e=>e.material==crystal)==true)?.availablePrefab??crystal.pickupPrefab;EditorUtility.SetDirty(crystal);}
            var herb=Mismo.Core.ProjectAssets.Load<MaterialDefinition>("Materials/Herb");var wood=Mismo.Core.ProjectAssets.Load<MaterialDefinition>("Materials/Wood");
            var stone=Mismo.Core.ProjectAssets.Load<MaterialDefinition>("Materials/Stone");var monster=Mismo.Core.ProjectAssets.Load<MaterialDefinition>("Materials/MonsterComponent");
            var list=settings.recipes.ToList();
            Add(list,"HealthPotion","REC-03",potion.displayName,potion.description,new[]{I(herb,3),I(crystal,1)},r=>r.result=potion);
            Add(list,"Whetstone","REC-04",whetstone.displayName,whetstone.description,new[]{I(stone,3),I(iron,1)},r=>r.result=whetstone);
            Add(list,"IronWeaponUpgrade","REC-05","Mejora de hierro","Mejora el arma elegida de T2 a T3 conservando su identidad y variante.",new[]{I(wood,3),I(iron,5),I(monster,2)},r=>{r.upgradeWeapon=true;r.fromTier=2;r.toTier=3;});
            Add(list,"ArcaneWeaponUpgrade","REC-06","Mejora arcana","Mejora el arma elegida de T3 a T4 conservando su identidad y variante.",new[]{I(iron,4),I(crystal,4),I(monster,3)},r=>{r.upgradeWeapon=true;r.fromTier=3;r.toTier=4;});
            var catalog=Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog");
            var sword=catalog.weapons.First(w=>w.MasteryId=="sword.onehand");var bow=catalog.weapons.First(w=>w.MasteryId=="bow");
            Add(list,"BasicSword","REC-07","Espada básica","Crea una espada T1 equilibrada para comenzar otro estilo de combate.",new[]{I(wood,2),I(iron,4)},r=>r.weaponResult=sword);
            Add(list,"BasicBow","REC-08","Arco básico","Crea un arco T1 equilibrado para combatir a distancia.",new[]{I(wood,5),I(iron,2),I(monster,1)},r=>r.weaponResult=bow);
            settings.recipes=list.ToArray();EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
            InventoryPresentationAssets.GenerateGridIcons();TranslationTables.Import();
            Debug.Log("CRAFTING_EXPANSION recipes="+settings.recipes.Length+" iron="+irons+" crystal="+crystals);
        }
        static RecipeIngredient I(MaterialDefinition m,int n)=>new RecipeIngredient{material=m,quantity=n};
        static void Add(System.Collections.Generic.List<CraftingRecipe> list,string name,string id,string title,string description,RecipeIngredient[] cost,Action<CraftingRecipe> setup)
        {
            var r=Ensure<CraftingRecipe>("Assets/Data/Crafting/Recipes/"+name+".asset",x=>{x.id=id;x.displayName=title;x.description=description;x.ingredients=cost;setup(x);});
            if(!list.Contains(r))list.Add(r);
        }
        static MaterialDefinition Material(string name,string id,string title,string description,GameObject prefab)=>Ensure<MaterialDefinition>("Assets/Data/Inventory/Materials/"+name+".asset",m=>{m.id=id;m.displayName=title;m.description=description;m.pickupPrefab=prefab;});
        static GameObject ConsumableModel(string name,bool bottle)
        {
            const string folder="Assets/Art/Prefabs/Gathering";string path=folder+"/"+name+".prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var root=new GameObject(name);
            Part(root,"Body",bottle?new Vector3(.32f,.42f,.32f):new Vector3(.45f,.12f,.25f),new Vector3(0,.22f,0),bottle?new Color(.65f,.06f,.12f):new Color(.38f,.43f,.48f));
            if(bottle){Part(root,"Neck",new Vector3(.17f,.12f,.17f),new Vector3(0,.49f,0),new Color(.8f,.65f,.4f));Part(root,"Label",new Vector3(.23f,.16f,.015f),new Vector3(0,.23f,-.166f),new Color(.95f,.83f,.58f));}
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        static void Part(GameObject parent,string name,Vector3 scale,Vector3 position,Color color)
        {
            Directory.CreateDirectory("Assets/Art/Materials/Gathering");AssetDatabase.Refresh();
            string path="Assets/Art/Materials/Gathering/Expedition-"+ColorUtility.ToHtmlStringRGB(color)+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){color=color};AssetDatabase.CreateAsset(m,path);}
            var p=GameObject.CreatePrimitive(PrimitiveType.Cube);p.name=name;p.transform.SetParent(parent.transform);p.transform.localPosition=position;p.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());p.GetComponent<Renderer>().sharedMaterial=m;
        }
        public static void RunBatch(){if(!Directory.GetCurrentDirectory().Replace("\\","/").Contains("/.validation/"))throw new InvalidOperationException("Use isolated project.");Create();EditorApplication.Exit(0);}
    }
}
