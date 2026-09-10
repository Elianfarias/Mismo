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
        public OwnedWeapon Copy() => new OwnedWeapon { instanceId = instanceId, definitionId = definitionId, tier=tier, variant=variant };
    }

    /// <summary>Versioned single-player data only. No scene references or combat resources.</summary>
    [Serializable]
    public sealed class InventoryProfile
    {
        public int version = 2;
        public ProgressionData progression = new ProgressionData();
        public List<OwnedWeapon> weapons = new List<OwnedWeapon>();
        public string[] equipped = new string[2];
        public int activeSlot;
        public List<string> claimedRewards = new List<string>();
        public List<DiscoveredRegion> regions = new List<DiscoveredRegion>();
        public List<string> defeatedEnemies = new List<string>();

        public void EnsureWorldData()
        { if(regions==null)regions=new List<DiscoveredRegion>();if(defeatedEnemies==null)defeatedEnemies=new List<string>(); }

        public InventoryProfile Copy()
        {
            var copy = new InventoryProfile { version = version, activeSlot = activeSlot, progression=progression.Copy(),
                equipped = (string[])equipped.Clone(), claimedRewards = new List<string>(claimedRewards) };
            if(regions!=null)foreach(var region in regions)copy.regions.Add(region.Copy());
            copy.defeatedEnemies=new List<string>(defeatedEnemies??new List<string>());
            foreach (var item in weapons) copy.weapons.Add(item.Copy());
            return copy;
        }

        public OwnedWeapon Find(string id) => weapons.Find(item => item.instanceId == id);

        public bool TryEquip(int slot, string id)
        {
            if (slot < 0 || slot > 1 || Find(id) == null || equipped[slot] == id) return false;
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
            var worldIds=new HashSet<string>(StringComparer.Ordinal);
            if(regions!=null){if(regions.Count>4096)return false;foreach(var region in regions)
                if(region==null||string.IsNullOrEmpty(region.id)||region.id.Length>120||region.minimumLevel<1||region.minimumLevel>1100||!worldIds.Add(region.id))return false;}
            worldIds.Clear();
            if(defeatedEnemies!=null){if(defeatedEnemies.Count>16384)return false;foreach(var id in defeatedEnemies)
                if(string.IsNullOrEmpty(id)||id.Length>160||!worldIds.Add(id))return false;}
            if ((version != 1 && version != 2) || weapons == null || weapons.Count < 2 || weapons.Count > 256 ||
                equipped == null || equipped.Length != 2 || activeSlot < 0 || activeSlot > 1 ||
                claimedRewards == null || claimedRewards.Count > 256) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in weapons)
                if (item == null || !Guid.TryParseExact(item.instanceId, "N", out _) ||
                    !ids.Add(item.instanceId) || item.definitionId == null || !definitions.Contains(item.definitionId) ||
                    version==2 && (item.tier<1 || item.tier>5 || (int)item.variant<0 || (int)item.variant>3)) return false;
            if (version==2 && (progression==null || !progression.IsValid())) return false;
            if (equipped[0] == equipped[1] || !ids.Contains(equipped[0]) || !ids.Contains(equipped[1])) return false;
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reward in claimedRewards)
                if (reward == null || !claimed.Add(reward) || !rewards.TryGetValue(reward, out var definition) ||
                    !weapons.Exists(item => item.definitionId == definition)) return false;
            return true;
        }

        public void UpgradeFromVersionOne()
        {
            if (version!=1) return;
            progression=new ProgressionData();
            foreach(var weapon in weapons) { weapon.tier=1; weapon.variant=WeaponVariant.Balanced; }
            version=2;
        }
    }
}
