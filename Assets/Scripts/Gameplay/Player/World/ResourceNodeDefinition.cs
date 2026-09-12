using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    [CreateAssetMenu(menuName = "Mismo/Crafting/Resource Node", fileName = "ResourceNode")]
    public sealed class ResourceNodeDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public string actionName = "Recolectar";
        [Min(.1f)] public float harvestSeconds = 1;
        [Min(.1f)] public float interactionRange = 2.5f;
        [Min(0)] public float regenerationSeconds = 300;
        [Min(0)] public float restoreClearance = 8;
        public GameObject availablePrefab;
        public GameObject depletedPrefab;
        public GameObject toolPrefab;
        public AudioClip harvestSound;
        public AudioClip completionSound;
        public MaterialLootTable rewards;
    }
}
