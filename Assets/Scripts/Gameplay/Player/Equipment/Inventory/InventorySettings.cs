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
