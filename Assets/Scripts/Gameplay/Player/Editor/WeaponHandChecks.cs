using System;
using System.Reflection;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class WeaponHandChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("Mismo/Armas/Verificar manos equipadas (Play Mode)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Ejecutar en Play Mode.");
            var root = new GameObject("Weapon hand checks");
            root.SetActive(false);
            var inventory = root.AddComponent<PlayerInventory>();
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            var dual = ScriptableObject.CreateInstance<WeaponFamilyDefinition>();
            var shieldFamily = ScriptableObject.CreateInstance<WeaponFamilyDefinition>();
            var a = ScriptableObject.CreateInstance<WeaponDefinition>();
            var b = ScriptableObject.CreateInstance<WeaponDefinition>();
            var shield = ScriptableObject.CreateInstance<WeaponDefinition>();
            var visualA = new GameObject("Sword A");
            var visualB = new GameObject("Sword B");
            var visualShield = new GameObject("Shield");
            try
            {
                a.Configure("a", "A", 1); b.Configure("b", "B", 1); shield.Configure("shield", "Shield", 1);
                a.visualPrefab = visualA; b.visualPrefab = visualB; shield.visualPrefab = visualShield;
                a.dualSwordFamily = b.dualSwordFamily = dual;
                a.swordShieldFamily = shieldFamily; shield.isShield = true;
                a.dualWield = true; a.secondaryVisualPrefab = visualShield;
                b.secondaryEquipped.scale = .7f; b.secondaryHolstered.scale = .8f;
                catalog.weapons = new[] { a, b, shield };
                var data = new InventoryProfile { offhands = new string[2] };
                foreach (var definition in catalog.weapons)
                    data.weapons.Add(new OwnedWeapon { instanceId = definition.Id, definitionId = definition.Id });
                data.equipped[0] = "a";
                typeof(PlayerInventory).GetField("catalog", Private).SetValue(inventory, catalog);
                typeof(PlayerInventory).GetField("profile", Private).SetValue(inventory, data);
                var compose = typeof(PlayerInventory).GetMethod("ComposeWeapon", Private);
                Func<int, WeaponDefinition> get = slot => (WeaponDefinition)compose.Invoke(inventory, new object[] { slot });
                var single = get(0);
                Check(!single.dualWield && single.SecondaryVisualPrefab == null, "Empty hand ignores legacy pair");
                data.offhands[0] = "b";
                var pair = get(0);
                Check(pair.visualPrefab == visualA && pair.SecondaryVisualPrefab == visualB, "A + B uses distinct equipped visuals");
                Check(pair.family == dual && pair.equippedOffhand == b, "Pair selects dual family and retains item identity");
                Check(pair.secondaryEquipped.scale == .7f && pair.secondaryHolstered.scale == .8f, "Both secondary poses belong to B");
                data.offhands[0] = "shield";
                pair = get(0);
                Check(pair.SecondaryVisualPrefab == visualShield && pair.family == shieldFamily, "Replacing B with shield changes model and family");
                data.equipped[1] = "b"; data.offhands[1] = "a";
                var reverse = get(1);
                Check(reverse.visualPrefab == visualB && reverse.SecondaryVisualPrefab == visualA, "Other loadout independently composes B + A");
                data.offhands[0] = null;
                Check(get(0).SecondaryVisualPrefab == null, "Unequipping removes the second visual");
                Check(a.dualWield && a.secondaryVisualPrefab == visualShield && a.family != shieldFamily, "Shared definition remains unchanged");
                Debug.Log("WEAPON_HAND_CHECKS_PASS: 8 checks");
            }
            finally
            {
                Object.Destroy(root); Object.Destroy(catalog); Object.Destroy(dual); Object.Destroy(shieldFamily);
                Object.Destroy(a); Object.Destroy(b); Object.Destroy(shield);
                Object.Destroy(visualA); Object.Destroy(visualB); Object.Destroy(visualShield);
            }
        }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Debug.Log("WEAPON_HAND_CHECK " + message);
        }
    }
}
