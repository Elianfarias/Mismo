using System;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.World;
using UnityEngine;
using L=Mismo.Gameplay.Player.Localization.GameLanguage;
namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        double worldClock;
        float clockSaveAt;
        public double WorldPlaySeconds=>Math.Max(worldClock,profile?.worldPlaySeconds??0);
        public double NodeReadyAt(string id)=>profile?.harvestedNodes?.Find(n=>n.id==id)?.readyAt??0;
        void Update()
        {
            if(!IsReady)return;
            worldClock+=Time.deltaTime;
            if(Time.unscaledTime>=clockSaveAt){clockSaveAt=Time.unscaledTime+15;SaveClock();}
        }
        void OnApplicationPause(bool paused){if(paused)SaveClock();}
        void OnApplicationQuit()=>SaveClock();
        void OnDestroy()=>SaveClock();
        void SaveClock()
        {
            if(!IsReady||!writable||WorldPlaySeconds-profile.worldPlaySeconds<.1)return;
            var next=profile.Copy();next.worldPlaySeconds=WorldPlaySeconds;
            // Keep the clock and all inventory mutations in the same protected file.
            try{repository.Write(JsonUtility.ToJson(next));profile=next;}
            catch(Exception e)when(e is System.IO.IOException||e is UnauthorizedAccessException||e is System.Security.Cryptography.CryptographicException)
            {Debug.LogWarning("World clock save: "+e.Message,this);}
        }
        public bool TryHarvest(string nodeId,ResourceNodeDefinition node,IDictionary<string,int> loot)
        {
            var health=GetComponent<Health>();
            if(!IsReady||health==null||health.IsDead||node==null||string.IsNullOrWhiteSpace(nodeId)||loot==null||NodeReadyAt(nodeId)>WorldPlaySeconds)return false;
            var next=profile.Copy();
            foreach(var entry in loot)if(!materialDefinitions.ContainsKey(entry.Key)||!next.TryAddMaterial(entry.Key,entry.Value))return false;
            if(!Fits(next,false))return false;
            next.harvestedNodes.RemoveAll(n=>n.id==nodeId||n.readyAt<=WorldPlaySeconds);
            next.harvestedNodes.Add(new HarvestState{id=nodeId,readyAt=WorldPlaySeconds+Math.Max(0,node.regenerationSeconds)});
            var items=new List<string>();foreach(var entry in loot)items.Add("+"+entry.Value+" "+L.Text(Material(entry.Key).displayName));
            return Commit(next,string.Join(" · ",items),false);
        }
        bool RecipeCost(CraftingRecipe recipe,out Dictionary<string,int> cost)
        {
            cost=new Dictionary<string,int>();
            if(recipe==null||recipe.ingredients==null||recipe.ingredients.Length==0)return false;
            foreach(var ingredient in recipe.ingredients)
            {
                if(ingredient?.material==null||ingredient.quantity<=0||!materialDefinitions.ContainsKey(ingredient.material.id))return false;
                cost.TryGetValue(ingredient.material.id,out int existing);
                if(existing>int.MaxValue-ingredient.quantity)return false;
                cost[ingredient.material.id]=existing+ingredient.quantity;
            }
            return true;
        }
        public bool CanCraft(CraftingRecipe recipe,string weaponId)
        {
            if(!CanManage||!RecipeCost(recipe,out var cost))return false;
            foreach(var ingredient in cost)if(MaterialCount(ingredient.Key)<ingredient.Value)return false;
            if(recipe.upgradeWeapon)
            {
                var item=profile.Find(weaponId);
                return item!=null&&!item.inChest&&item.tier==recipe.fromTier&&recipe.toTier>recipe.fromTier&&recipe.toTier<=5;
            }
            if(recipe.result==null||!materialDefinitions.ContainsKey(recipe.result.id)||recipe.quantity<=0)return false;
            var candidate=profile.Copy();
            return candidate.TrySpendMaterials(cost)&&candidate.TryAddMaterial(recipe.result.id,recipe.quantity)&&HasGridRoom(candidate,false);
        }
        public bool TryCraft(CraftingRecipe recipe,string weaponId,CraftingStation station)
        {
            if(station==null||station.recipes==null||Array.IndexOf(station.recipes,recipe)<0||!station.InRange(transform.position)||GetComponent<GatheringPlayer>()?.IsHarvesting==true||!CanCraft(recipe,weaponId))return false;
            var next=profile.Copy();RecipeCost(recipe,out var cost);
            if(!next.TrySpendMaterials(cost))return false;
            if(recipe.upgradeWeapon)next.Find(weaponId).tier=recipe.toTier;
            else if(!next.TryAddMaterial(recipe.result.id,recipe.quantity)||!Fits(next,false))return false;
            return Commit(next,L.Format("Creado: {0}",L.Text(recipe.displayName)),recipe.upgradeWeapon);
        }
        public bool TryUseConsumable(string id)
        {
            var material=Material(id);var health=GetComponent<Health>();
            var healing=GetComponent<ConsumableHealing>();
            if(!CanManage||material==null||material.healingAmount<=0||health==null||health.IsDead||health.Current>=health.Maximum||
                healing!=null&&healing.Active||GetComponent<GatheringPlayer>()?.IsHarvesting==true)return false;
            var next=profile.Copy();if(!next.TrySpendMaterials(new Dictionary<string,int>{{id,1}})||!Commit(next,L.Text("Recuperación iniciada."),false))return false;
            if(healing==null)healing=gameObject.AddComponent<ConsumableHealing>();
            healing.Begin(material.healingAmount,material.healingSeconds);return true;
        }
    }
    public sealed class ConsumableHealing:MonoBehaviour
    {
        Health health;float remaining,rate;
        public bool Active=>remaining>0;
        void Awake(){health=GetComponent<Health>();health.Damaged+=Cancel;}
        public void Begin(float amount,float duration){remaining=Mathf.Max(.1f,duration);rate=amount/remaining;}
        void Cancel(DamageInfo damage)=>remaining=0;
        void Update(){if(!Active||health.IsDead)return;float dt=Mathf.Min(remaining,Time.deltaTime);remaining-=dt;health.Heal(rate*dt);}
        void OnDestroy(){if(health!=null)health.Damaged-=Cancel;}
    }
}
