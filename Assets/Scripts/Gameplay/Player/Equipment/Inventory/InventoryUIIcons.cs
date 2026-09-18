using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    // Direct references include the original art-folder textures in player builds.
    public sealed class InventoryUIIcons : ScriptableObject
    {
        [Header("Live theme preview")]
        [Tooltip("On: olive/brown. Off: slate gray/blue. Updates while the inventory is open in Play mode.")]
        public bool useOliveTheme;
        public Texture2D oliveWeaponSlotBackground, oliveConsumableSlotBackground;
        public Rect oliveWeaponSlotUV=new Rect(0,0,1,1), oliveConsumableSlotUV=new Rect(0,0,1,1);
        public Texture2D close, menu, organize, all, weapons, materials, consumables, favorites, backpack;
        [Header("Inventory backgrounds (optional, standalone textures)")]
        public Texture2D background, slotBackground;
        [Header("Individual slot artwork (optional)")]
        public Texture2D weaponSlotBackground, consumableSlotBackground;
        [Tooltip("Normalized texture region, excluding transparent outer padding.")]
        public Rect weaponSlotUV=new Rect(0,0,1,1), consumableSlotUV=new Rect(0,0,1,1);
        public RectOffset backgroundBorder;
        public RectOffset slotBorder;
        public Color backgroundTint=Color.white, slotTint=Color.white;
        public Texture2D special;
        void OnEnable()
        {
            if(backgroundBorder==null)backgroundBorder=new RectOffset(16,16,16,16);
            if(slotBorder==null)slotBorder=new RectOffset(12,12,12,12);
        }
    }
}
