using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [Serializable]
    public sealed class MaterialLootEntry
    {
        public MaterialDefinition material;
        [Min(1)] public int minimum = 1;
        [Min(1)] public int maximum = 1;
        [Range(0, 1)] public float probability = 1;
    }

    [CreateAssetMenu(menuName = "Mismo/Crafting/Material Loot Table", fileName = "MaterialLoot")]
    public sealed class MaterialLootTable : ScriptableObject
    {
        public MaterialLootEntry[] entries = Array.Empty<MaterialLootEntry>();

        // Roll once per reward, then retain this result while retrying persistence.
        public Dictionary<string, int> Roll()
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (entries == null) return result;
            foreach (var entry in entries)
            {
                if (entry == null || entry.material == null || !MaterialCatalog.ValidId(entry.material.id) ||
                    entry.minimum < 1 || entry.maximum < entry.minimum || entry.maximum == int.MaxValue ||
                    float.IsNaN(entry.probability) || entry.probability <= 0 || UnityEngine.Random.value >= entry.probability) continue;
                int amount = UnityEngine.Random.Range(entry.minimum, entry.maximum + 1);
                result.TryGetValue(entry.material.id, out int previous);
                if (previous > int.MaxValue - amount) throw new InvalidOperationException("Material loot quantity overflow.");
                result[entry.material.id] = previous + amount;
            }
            return result;
        }
    }
}
