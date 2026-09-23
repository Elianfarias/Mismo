using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [CreateAssetMenu(menuName="Mismo/Inventory/Inventory Settings")]
    public sealed class InventorySettings : ScriptableObject
    {
        [Range(2,12)] public int backpackColumns = 8;
        [Range(1,12)] public int backpackRows = 6;
        [Range(2,12)] public int chestColumns = 8;
        [Range(1,12)] public int chestRows = 12;
        [Min(1)] public int defaultStackSize = 20;
        [Min(1)] public float chestRange = 3;
        public Vector3 chestOffset = new Vector3(3, 0, 0);
        public GameObject chestPrefab;
        [Tooltip("Indicador visual del botín en el suelo. Asignar aquí la futura luz/VFX de drop; no controla la recogida ni el guardado.")]
        public GameObject lootMarkerPrefab;
        [Header("Loot glow")]
        public Material lootGlowMaterial;
        public Color[] weaponLootColors = {new Color(.82f,.9f,1),new Color(.3f,1,.25f),new Color(.15f,.48f,1),new Color(.78f,.2f,1),new Color(1,.48f,.06f)};
        public Color componentLootColor = new Color(.12f,.85f,.8f);
        public Color consumableLootColor = new Color(1,.23f,.25f);
        static InventorySettings current;
        public static InventorySettings Current
        {
            get
            {
                if(current==null) current=Mismo.Core.ProjectAssets.Load<InventorySettings>("InventorySettings");
                if(current==null) current=CreateInstance<InventorySettings>();
                return current;
            }
        }
    }
}
