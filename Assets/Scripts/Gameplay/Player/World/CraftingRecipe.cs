using System;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    [Serializable] public sealed class RecipeIngredient { public MaterialDefinition material; [Min(1)] public int quantity=1; }
    [CreateAssetMenu(menuName="Mismo/Crafting/Recipe")]
    public sealed class CraftingRecipe:ScriptableObject
    {
        public string id,displayName;
        [TextArea] public string description;
        public RecipeIngredient[] ingredients=Array.Empty<RecipeIngredient>();
        public MaterialDefinition result;
        [Min(1)] public int quantity=1;
        public bool upgradeWeapon;
        [Range(1,4)] public int fromTier=1;
        [Range(2,5)] public int toTier=2;
    }
}
