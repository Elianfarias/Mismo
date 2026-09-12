using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [CreateAssetMenu(menuName = "Mismo/Crafting/Material", fileName = "Material")]
    public sealed class MaterialDefinition : ScriptableObject
    {
        [Tooltip("Identidad persistente. No cambiar después de distribuir guardados.")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public Vector3 inventoryPreviewRotation;
        public GameObject pickupPrefab;
        [Min(1)] public int stackSize = 20;
        [Range(1,12)] public int gridWidth = 1;
        [Range(1,12)] public int gridHeight = 1;
        public bool canRotate = true;
        public InventoryItemCategory category = InventoryItemCategory.Material;
        public bool canDiscard = true;
        public bool canSell = true;
        [Min(0)] public int sellValue = 1;
    }
    public enum InventoryItemCategory { Material, Consumable }
}
