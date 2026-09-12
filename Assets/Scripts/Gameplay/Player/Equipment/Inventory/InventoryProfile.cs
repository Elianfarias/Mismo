using System;
using System.Collections.Generic;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [Serializable] public sealed class DiscoveredRegion
    {
        public string id;
        public int minimumLevel;
        public DiscoveredRegion Copy()=>(DiscoveredRegion)MemberwiseClone();
    }
    [Serializable]
    public sealed class OwnedWeapon
    {
        public string instanceId;
        public string definitionId;
        public int tier = 1;
        public WeaponVariant variant;
        public bool inChest;
        public bool favorite;
        public OwnedWeapon Copy() => (OwnedWeapon)MemberwiseClone();
    }

    /// <summary>Versioned single-player data only. No scene references or combat resources.</summary>
    [Serializable]
    public sealed class InventoryProfile
    {
        public int version = 4;
        public ProgressionData progression = new ProgressionData();
        public List<OwnedWeapon> weapons = new List<OwnedWeapon>();
        public string[] equipped = new string[2];
        public int activeSlot;
        public List<string> claimedRewards = new List<string>();
        public List<DiscoveredRegion> regions = new List<DiscoveredRegion>();
        public List<string> defeatedEnemies = new List<string>();
        public List<MaterialStack> materials = new List<MaterialStack>();
        public List<MaterialStack> chestMaterials = new List<MaterialStack>();
        public List<string> favoriteMaterials = new List<string>();
        public List<PendingInventoryLoot> pendingLoot = new List<PendingInventoryLoot>();
        public List<GridPlacement> gridPlacements = new List<GridPlacement>();

        public int MaterialCount(string id) => materials?.Find(item => item.id == id)?.quantity ?? 0;

        public bool TryAddMaterial(string id, int quantity)
        {
            if (!MaterialCatalog.ValidId(id) || quantity <= 0 || MaterialCount(id) > int.MaxValue - quantity) return false;
            if (materials == null) materials = new List<MaterialStack>();
            var stack = materials.Find(item => item.id == id);
            if (stack == null) materials.Add(new MaterialStack { id = id, quantity = quantity });
            else stack.quantity += quantity;
            return true;
        }

        // Validate the entire cost before changing any balance.
        public bool TrySpendMaterials(IDictionary<string, int> cost)
        {
            if (cost == null || cost.Count == 0) return false;
            foreach (var entry in cost)
                if (!MaterialCatalog.ValidId(entry.Key) || entry.Value <= 0 || MaterialCount(entry.Key) < entry.Value) return false;
            foreach (var entry in cost) materials.Find(item => item.id == entry.Key).quantity -= entry.Value;
            materials.RemoveAll(item => item.quantity == 0);
            return true;
        }

        public void EnsureWorldData()
        { if(regions==null)regions=new List<DiscoveredRegion>();if(defeatedEnemies==null)defeatedEnemies=new List<string>(); }

        public InventoryProfile Copy()
        {
            var copy = new InventoryProfile { version = version, activeSlot = activeSlot, progression=progression.Copy(),
                equipped = (string[])equipped.Clone(), claimedRewards = new List<string>(claimedRewards) };
            if(regions!=null)foreach(var region in regions)copy.regions.Add(region.Copy());
            copy.defeatedEnemies=new List<string>(defeatedEnemies??new List<string>());
            if (materials != null) foreach (var stack in materials) copy.materials.Add(stack.Copy());
            if (chestMaterials != null) foreach (var stack in chestMaterials) copy.chestMaterials.Add(stack.Copy());
            copy.favoriteMaterials=new List<string>(favoriteMaterials??new List<string>());
            if(pendingLoot!=null)foreach(var loot in pendingLoot)copy.pendingLoot.Add(loot.Copy());
            if(gridPlacements!=null)foreach(var placement in gridPlacements)copy.gridPlacements.Add(placement.Copy());
            foreach (var item in weapons) copy.weapons.Add(item.Copy());
            return copy;
        }

        public OwnedWeapon Find(string id) => weapons.Find(item => item.instanceId == id);

        public bool TryEquip(int slot, string id)
        {
            if (slot < 0 || slot > 1 || Find(id) == null || Find(id).inChest || equipped[slot] == id) return false;
            int other = 1 - slot;
            if (equipped[other] == id) equipped[other] = equipped[slot];
            equipped[slot] = id;
            return true;
        }

        public bool TryClaim(string rewardId, string definitionId)
        {
            if (string.IsNullOrEmpty(rewardId) || string.IsNullOrEmpty(definitionId) ||
                claimedRewards.Contains(rewardId) || weapons.Count >= 256) return false;
            weapons.Add(new OwnedWeapon { instanceId = Guid.NewGuid().ToString("N"), definitionId = definitionId });
            claimedRewards.Add(rewardId);
            return true;
        }

        public bool IsValid(ISet<string> definitions, IDictionary<string, string> rewards)
        {
            var gridKeys=new HashSet<string>();
            if(gridPlacements!=null)
            {
                if(gridPlacements.Count>512)return false;
                foreach(var p in gridPlacements)if(p==null||string.IsNullOrEmpty(p.key)||p.key.Length>200||p.x<0||p.y<0||p.x>=128||p.y>=128||!gridKeys.Add(p.chest+":"+p.key))return false;
            }
            if(!ValidStacks(chestMaterials))return false;
            var favorites=new HashSet<string>();
            if(favoriteMaterials!=null)foreach(var id in favoriteMaterials)
                if(!MaterialCatalog.ValidId(id)||!favorites.Add(id)||favorites.Count>4096)return false;
            var lootIds=new HashSet<string>();
            var pendingWeaponIds=new HashSet<string>();
            var pendingRewards=new HashSet<string>();
            if(pendingLoot!=null)
            {
                if(pendingLoot.Count>4096)return false;
                foreach(var loot in pendingLoot)
                {
                    if(loot==null||!Guid.TryParseExact(loot.id,"N",out _)||!lootIds.Add(loot.id)||
                        !Finite(loot.x)||!Finite(loot.y)||!Finite(loot.z)||!ValidStacks(loot.materials))return false;
                    if(!loot.HasWeapon&&loot.weapon!=null&&!string.IsNullOrEmpty(loot.weapon.definitionId))return false;
                    var w=loot.HasWeapon?loot.weapon:null;
                    if(w!=null&&(!Guid.TryParseExact(w.instanceId,"N",out _)||!pendingWeaponIds.Add(w.instanceId)||
                        !definitions.Contains(w.definitionId??"")||w.tier<1||w.tier>5||(int)w.variant<0||(int)w.variant>3||w.inChest))return false;
                    if(w==null&&(loot.materials==null||loot.materials.Count==0))return false;
                    if(!string.IsNullOrEmpty(loot.rewardId)&&(!rewards.TryGetValue(loot.rewardId,out var rewardDefinition)||w==null||w.definitionId!=rewardDefinition||
                        !pendingRewards.Add(loot.rewardId)||claimedRewards?.Contains(loot.rewardId)==true))return false;
                }
            }
            var materialIds = new HashSet<string>(StringComparer.Ordinal);
            if (materials != null)
            {
                if (materials.Count > 4096) return false;
                foreach (var stack in materials)
                    if (stack == null || !MaterialCatalog.ValidId(stack.id) || stack.quantity <= 0 || !materialIds.Add(stack.id)) return false;
            }
            var worldIds=new HashSet<string>(StringComparer.Ordinal);
            if(regions!=null){if(regions.Count>4096)return false;foreach(var region in regions)
                if(region==null||string.IsNullOrEmpty(region.id)||region.id.Length>120||region.minimumLevel<1||region.minimumLevel>1100||!worldIds.Add(region.id))return false;}
            worldIds.Clear();
            if(defeatedEnemies!=null){if(defeatedEnemies.Count>16384)return false;foreach(var id in defeatedEnemies)
                if(string.IsNullOrEmpty(id)||id.Length>160||!worldIds.Add(id))return false;}
            if ((version < 1 || version > 4) || weapons == null || weapons.Count < 2 || weapons.Count > 256 ||
                equipped == null || equipped.Length != 2 || activeSlot < 0 || activeSlot > 1 ||
                claimedRewards == null || claimedRewards.Count > 256) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in weapons)
                if (item == null || !Guid.TryParseExact(item.instanceId, "N", out _) ||
                    !ids.Add(item.instanceId) || item.definitionId == null || !definitions.Contains(item.definitionId) ||
                    version>=2 && (item.tier<1 || item.tier>5 || (int)item.variant<0 || (int)item.variant>3)) return false;
            if (version>=2 && (progression==null || !progression.IsValid())) return false;
            if (equipped[0] == equipped[1] || !ids.Contains(equipped[0]) || !ids.Contains(equipped[1])) return false;
            if(Find(equipped[0]).inChest||Find(equipped[1]).inChest||ids.Overlaps(pendingWeaponIds))return false;
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reward in claimedRewards)
                if (reward == null || !claimed.Add(reward) || !rewards.TryGetValue(reward, out var definition) ||
                    !weapons.Exists(item => item.definitionId == definition)) return false;
            return true;
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<=1000000;
        static bool ValidStacks(List<MaterialStack> stacks)
        {
            if(stacks==null)return true;
            if(stacks.Count>4096)return false;
            var ids=new HashSet<string>();
            foreach(var s in stacks)if(s==null||!MaterialCatalog.ValidId(s.id)||s.quantity<=0||!ids.Add(s.id))return false;
            return true;
        }

        public void UpgradeFromVersionOne()
        {
            if (version!=1) return;
            progression=new ProgressionData();
            foreach(var weapon in weapons) { weapon.tier=1; weapon.variant=WeaponVariant.Balanced; }
            version=2;
        }
        public void UpgradeToCurrent(){UpgradeFromVersionOne();if(version==2||version==3)version=4;}
    }
}
