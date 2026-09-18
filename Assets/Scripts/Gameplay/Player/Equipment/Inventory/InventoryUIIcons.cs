using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    // Direct references include the original art-folder textures in player builds.
    public sealed class InventoryUIIcons : ScriptableObject
    {
        [Header("Live theme preview")]
        [Tooltip("On: olive/brown. Off: slate gray/blue. Updates while the inventory is open in Play mode.")]
        public bool useOliveTheme;
        [Header("Opacity — live preview")]
        [Tooltip("Fondo de inventario, personaje y sus pestañas. 0 transparente; 1 opaco.")]
        [Range(0,1)] public float panelOpacity=1f;
        [Tooltip("Iconos de navegación y del menú radial. No modifica los textos ni las imágenes de objetos.")]
        [Range(0,1)] public float navigationIconOpacity=1f;
        public Texture2D oliveWeaponSlotBackground, oliveConsumableSlotBackground;
        public Rect oliveWeaponSlotUV=new Rect(0,0,1,1), oliveConsumableSlotUV=new Rect(0,0,1,1);
        public Texture2D close, menu, organize, all, weapons, materials, consumables, favorites, backpack;
        public Texture2D chest;
        [Header("Pause menu and settings icons")]
        public Texture2D pauseResume, pauseOptions, pauseMainMenu;
        public Texture2D settingsSound, settingsDisplay, settingsGraphics, settingsControls;
        [Header("Bestiary navigation")]
        public Texture2D previousPage, nextPage;
        [Header("Radial menu — configurable icons")]
        public Texture2D radialCharacter, radialInventory, radialMap, radialMounts, radialBestiary, radialSkills;
        [Tooltip("Vacío utiliza Close (cross).")]
        public Texture2D radialCancel;
        [Header("Radial menu — hover sound")]
        [Tooltip("Sonido al entrar a una opción. Vacío reutiliza Hover del catálogo GameSounds.")]
        public AudioClip radialHoverSound;
        [Range(0,1)] public float radialHoverVolume=.7f;
        [Min(0)] public float radialHoverCooldown=.08f;
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
        [Header("Shared HUD and ability icons")]
        public Texture2D hudHealth, hudStamina, hudFocus;
        public Texture2D hudVitalsFrame;
        [Header("Painted fantasy resource bars")]
        public Texture2D hudPaintedFrames, hudPaintedFill;
        [System.NonSerialized] Texture2D defaultPaintedFrames,defaultPaintedFill;
        public Texture2D PaintedFrames => hudPaintedFrames!=null?hudPaintedFrames:defaultPaintedFrames!=null?defaultPaintedFrames:defaultPaintedFrames=Resources.Load<Texture2D>("UI/HUD/VitalsPaintedFrames");
        public Texture2D PaintedFill => hudPaintedFill!=null?hudPaintedFill:defaultPaintedFill!=null?defaultPaintedFill:defaultPaintedFill=Resources.Load<Texture2D>("UI/HUD/VitalsPaintedFill");
        public Texture2D hudMinimapFrame;
        [System.NonSerialized] Texture2D defaultMinimapFrame;
        public Texture2D MinimapFrame => hudMinimapFrame!=null?hudMinimapFrame:
            defaultMinimapFrame!=null?defaultMinimapFrame:defaultMinimapFrame=Resources.Load<Texture2D>("UI/HUD/MinimapFrame-Silver");
        public Texture2D hudVitalsBackground;
        [System.NonSerialized] Texture2D defaultVitalsBackground;
        public Texture2D VitalsBackground => hudVitalsBackground!=null?hudVitalsBackground:
            defaultVitalsBackground!=null?defaultVitalsBackground:defaultVitalsBackground=Resources.Load<Texture2D>("UI/HUD/VitalsBackground-Slate");
        [System.NonSerialized] Texture2D defaultVitalsFrame;
        public Texture2D VitalsFrame => hudVitalsFrame!=null?hudVitalsFrame:
            defaultVitalsFrame!=null?defaultVitalsFrame:defaultVitalsFrame=Resources.Load<Texture2D>("UI/HUD/VitalsContainer-Clean-v3");
        [Range(0,1)] public float hudBackgroundOpacity=.65f;
        public Texture2D[] abilityIcons;
        void OnEnable()
        {
            if(backgroundBorder==null)backgroundBorder=new RectOffset(16,16,16,16);
            if(slotBorder==null)slotBorder=new RectOffset(12,12,12,12);
        }
    }
}
