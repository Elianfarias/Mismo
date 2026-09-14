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
        void OnDestroy(){SaveClock();ReleaseComposedWeapons();}
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
        bool CraftCandidate(CraftingRecipe recipe,string weaponId,out InventoryProfile next)
        {
            next=null;
            if(!CanManage||!RecipeCost(recipe,out var cost))return false;
            next=profile.Copy();if(!next.TrySpendMaterials(cost))return false;
            if(recipe.upgradeWeapon)
            {
                var item=next.Find(weaponId);
                if(item==null||item.inChest||item.tier!=recipe.fromTier||recipe.toTier<=recipe.fromTier||recipe.toTier>5)return false;
                item.tier=recipe.toTier;
            }
            else if(recipe.weaponResult!=null)
            {
                if(!definitions.Contains(recipe.weaponResult.Id)||next.weapons.Count>=256||recipe.quantity!=1)return false;
                next.weapons.Add(new OwnedWeapon{instanceId=Guid.NewGuid().ToString("N"),definitionId=recipe.weaponResult.Id,tier=1});
            }
            else if(recipe.result==null||!materialDefinitions.ContainsKey(recipe.result.id)||recipe.quantity<=0||!next.TryAddMaterial(recipe.result.id,recipe.quantity))return false;
            return HasGridRoom(next,false);
        }
        public bool CanCraft(CraftingRecipe recipe,string weaponId)=>CraftCandidate(recipe,weaponId,out _);
        public bool TryCraft(CraftingRecipe recipe,string weaponId,CraftingStation station)
        {
            if(station==null||station.recipes==null||Array.IndexOf(station.recipes,recipe)<0||!station.InRange(transform.position)||GetComponent<GatheringPlayer>()?.IsHarvesting==true||!CraftCandidate(recipe,weaponId,out var next))return false;
            return Commit(next,L.Format("Creado: {0}",L.Text(recipe.displayName)),recipe.upgradeWeapon);
        }
        public float PotionCooldownRemaining=>IsReady?(float)Math.Max(0,profile.potionReadyAt-WorldPlaySeconds):0;
        public float WeaponBuffRemaining=>IsReady?(float)Math.Max(0,profile.weaponBuffUntil-WorldPlaySeconds):0;
        public float WeaponConsumableBonus(WeaponDefinition weapon)=>WeaponBuffRemaining>0&&EquippedItem(weapon)?.instanceId==profile.buffedWeaponId?profile.weaponBuffDamage:0;
        public string ConsumableBlockReason(string id)
        {
            var material=Material(id);var health=GetComponent<Health>();
            if(!IsReady||material==null||!material.IsConsumable||MaterialCount(id)<1||health==null||health.IsDead)return L.Text("No disponible.");
            if(!material.usableInCombat&&!CanManage)return L.Text("Solo fuera de combate.");
            if(GetComponent<GatheringPlayer>()?.IsHarvesting==true||loadout.Runner.IsBusy||loadout.Belt!=null&&loadout.Belt.IsActive)return L.Text("Terminá la acción actual.");
            if(material.damageBonus>0)
            {
                if(WeaponBuffRemaining>0)return L.Format("Afilado activo: {0:0} s",WeaponBuffRemaining);
                if(profile.Find(profile.equipped[profile.activeSlot])==null)return L.Text("Equipá un arma.");
            }
            if(material.healingAmount>0)
            {
                if(health.Current>=health.Maximum)return L.Text("Vida completa.");
                if(!material.instantHealing&&GetComponent<ConsumableHealing>()?.Active==true)return L.Text("Ya hay una recuperación activa.");
            }
            if(material.useCooldownSeconds>0&&PotionCooldownRemaining>0)return L.Format("Próximo uso en {0:0} s",PotionCooldownRemaining);
            return null;
        }
        public bool TryUseConsumable(string id)
        {
            string reason=ConsumableBlockReason(id);
            if(reason!=null){Notice=reason;Changed?.Invoke();return false;}
            var material=Material(id);var health=GetComponent<Health>();
            var next=profile.Copy();if(!next.TrySpendMaterials(new Dictionary<string,int>{{id,1}}))return false;
            if(material.useCooldownSeconds>0)next.potionReadyAt=WorldPlaySeconds+material.useCooldownSeconds;
            if(material.damageBonus>0){next.weaponBuffUntil=WorldPlaySeconds+Mathf.Max(1,material.damageBonusSeconds);next.weaponBuffDamage=material.damageBonus;next.buffedWeaponId=next.equipped[next.activeSlot];}
            if(!Commit(next,L.Format("Usado: {0}",L.Text(material.displayName)),false))return false;
            if(material.healingAmount>0)
            {
                if(material.instantHealing)health.Heal(material.healingAmount);
                else {var healing=GetComponent<ConsumableHealing>()??gameObject.AddComponent<ConsumableHealing>();healing.Begin(material.healingAmount,material.healingSeconds);}
            }
            return true;
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
