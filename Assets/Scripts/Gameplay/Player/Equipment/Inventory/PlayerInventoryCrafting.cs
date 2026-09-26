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
        float pendingLootCleanupAt;
        public double WorldPlaySeconds=>Math.Max(worldClock,profile?.worldPlaySeconds??0);
        public double NodeReadyAt(string id)=>profile?.harvestedNodes?.Find(n=>n.id==id)?.readyAt??0;
        void Update()
        {
            if(!IsReady)return;
            worldClock+=Time.deltaTime;
            if(Time.unscaledTime>=pendingLootCleanupAt){pendingLootCleanupAt=Time.unscaledTime+1;RemoveExpiredPendingLoot();}
            if(Time.unscaledTime>=clockSaveAt){clockSaveAt=Time.unscaledTime+15;SaveClock();}
        }
        void RemoveExpiredPendingLoot()
        {
            if(profile?.pendingLoot==null||!profile.pendingLoot.Exists(loot=>loot==null||loot.IsExpired(WorldPlaySeconds)))return;
            var next=profile.Copy();next.pendingLoot.RemoveAll(loot=>loot==null||loot.IsExpired(WorldPlaySeconds));
            Commit(next,"",false);
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
        public bool TryHarvest(string nodeId,ResourceNodeDefinition node,IDictionary<string,int> loot,bool oneTime=false)
        {
            var health=GetComponent<Health>();
            if(!IsReady||health==null||health.IsDead||node==null||string.IsNullOrWhiteSpace(nodeId)||loot==null||NodeReadyAt(nodeId)>WorldPlaySeconds||oneTime&&IsWorldEnemyDefeated(nodeId))return false;
            var next=profile.Copy();
            foreach(var entry in loot)if(!materialDefinitions.ContainsKey(entry.Key)||!next.TryAddMaterial(entry.Key,entry.Value))return false;
            if(!Fits(next,false))return false;
            next.harvestedNodes.RemoveAll(n=>n.id==nodeId||n.readyAt<=WorldPlaySeconds);
            if(oneTime){next.EnsureWorldData();next.defeatedEnemies.Add(nodeId);}
            else next.harvestedNodes.Add(new HarvestState{id=nodeId,readyAt=WorldPlaySeconds+Math.Max(0,node.regenerationSeconds)});
            var items=new List<string>();foreach(var entry in loot)items.Add("+"+entry.Value+" "+L.Text(Material(entry.Key).displayName));
            return Commit(next,string.Join(" · ",items),false,GameSound.ResourceCollected);
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
            if(!CanManage||!KnowsRecipe(recipe)||!RecipeCost(recipe,out var cost))return false;
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
        public string CraftBlockReason(CraftingRecipe recipe,string weaponId,CraftingStation station,bool inWorld)
        {
            if(recipe==null)return "Elegí una receta.";
            if(!KnowsRecipe(recipe))return "Todavía no aprendiste esta receta.";
            if(!CanManage||GetComponent<Health>()?.IsDead==true)return "Solo fuera de combate.";
            if(GetComponent<GatheringPlayer>()?.IsHarvesting==true||CompanionPlayer.IsRiding(gameObject))return "Terminá la acción actual.";
            if(inWorld)
            {
                if(!recipe.craftInWorld||recipe.upgradeWeapon)return "Requiere mesa de crafteo.";
                if(Array.IndexOf(Mismo.Core.ProjectAssets.LoadAll<CraftingRecipe>("Recipes"),recipe)<0)return "Receta no disponible.";
            }
            else if(station==null||!station.InRange(transform.position)||station.recipes==null||Array.IndexOf(station.recipes,recipe)<0)
                return "Receta no disponible en esta mesa.";
            if(!RecipeCost(recipe,out var cost))return "Receta no disponible.";
            if(recipe.upgradeWeapon)
            {
                var item=Item(weaponId);
                if(item==null||item.inChest||item.tier!=recipe.fromTier)return "Elegí un arma de nivel T"+recipe.fromTier+".";
            }
            foreach(var entry in cost)if(MaterialCount(entry.Key)<entry.Value)return "Faltan materiales.";
            return CraftCandidate(recipe,weaponId,out _)?null:"No hay espacio o el resultado no está disponible.";
        }
        public bool TryCraftInWorld(CraftingRecipe recipe)=>CompleteCraft(recipe,null,null,true);
        public bool TryCraft(CraftingRecipe recipe,string weaponId,CraftingStation station)
            =>CompleteCraft(recipe,weaponId,station,false);
        bool CompleteCraft(CraftingRecipe recipe,string weaponId,CraftingStation station,bool inWorld)
        {
            if(CraftBlockReason(recipe,weaponId,station,inWorld)!=null||!CraftCandidate(recipe,weaponId,out var next))
            { GameAudio.Play(GameSound.CraftFailed); return false; }
            RecordQuestCraft(next,recipe);
            bool saved = Commit(next,L.Format("Creado: {0}",L.Text(recipe.displayName)),recipe.upgradeWeapon,GameSound.CraftSuccess);
            if (!saved) GameAudio.Play(GameSound.CraftFailed);
            return saved;
        }
        public float PotionCooldownRemaining=>IsReady?(float)Math.Max(0,profile.potionReadyAt-WorldPlaySeconds):0;
        public float WeaponBuffRemaining=>IsReady?(float)Math.Max(0,profile.weaponBuffUntil-WorldPlaySeconds):0;
        public float WeaponConsumableBonus(WeaponDefinition weapon)=>WeaponBuffRemaining>0&&EquippedItem(weapon)?.instanceId==profile.buffedWeaponId?profile.weaponBuffDamage:0;
        public string ConsumableSlot(int slot)
        {
            var slots=profile?.consumableSlots;
            return slots!=null&&slot>=0&&slot<slots.Length&&!string.IsNullOrEmpty(slots[slot])?slots[slot]:null;
        }
        public bool CanAssignConsumable(string id)=>CanManage&&Material(id)?.IsConsumable==true&&MaterialCount(id)>0;
        public bool AssignConsumable(int slot,string id)
        {
            if(!IsReady||!CanManage||slot<0||slot>=4||id!=null&&!CanAssignConsumable(id))return false;
            var next=profile.Copy();
            if(next.consumableSlots==null||next.consumableSlots.Length==0)next.consumableSlots=new string[4];
            for(int i=0;i<4;i++)if(id!=null&&next.consumableSlots[i]==id)next.consumableSlots[i]=null;
            next.consumableSlots[slot]=id;
            return Commit(next,"",false);
        }
        public bool TryUseConsumableSlot(int slot)
        {
            var id=ConsumableSlot(slot);
            return !string.IsNullOrEmpty(id)&&TryUseConsumable(id);
        }
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
            if(!Commit(next,L.Format("Usado: {0}",L.Text(material.displayName)),false,GameSound.ConsumableUsed))return false;
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
