using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [CreateAssetMenu(menuName = "Mismo/Inventory/Item catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        public WeaponDefinition[] weapons;
        public WeaponDefinition bossReward;
        public const string BossRewardId = "region.7319.boss.weapon";

        public WeaponDefinition Find(string id)
        {
            if (weapons != null)
                foreach (var weapon in weapons) if (weapon != null && weapon.Id == id) return weapon;
            return null;
        }
    }
}
