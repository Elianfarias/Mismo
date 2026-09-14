using UnityEngine;
using Mismo.Gameplay.Player.Equipment.Inventory;
namespace Mismo.Gameplay.Player.World
{
    [CreateAssetMenu(menuName="Mismo/Crafting/Surface Settings")]
    public sealed class GatheringSettings:ScriptableObject
    {
        public ResourceNodeDefinition herb,tree,stone;
        [Tooltip("Optional mineral variants. Each can have its own loot table.")]
        public ResourceNodeDefinition[] minerals=System.Array.Empty<ResourceNodeDefinition>();
        public CraftingRecipe[] recipes;
        public GameObject workbenchPrefab;
        public MaterialLootTable monsterLoot;
        [Min(1)] public int generationVersion=1;
        [Range(0,8)] public int groupsPerChunk=2;
        [Range(0,1)] public float groupChance=.45f;
        [Min(2)] public float minimumSeparation=5;
        [Min(2)] public float starterRadius=12;
        public ResourceNodeDefinition Mineral(int seed)
        {
            if(minerals==null||minerals.Length==0)return stone;
            double total=0;foreach(var node in minerals)if(node!=null&&node.distributionWeight>0&& !float.IsInfinity(node.distributionWeight))total+=node.distributionWeight;
            if(total<=0)return stone;
            double pick=((uint)seed/4294967296d)*total;
            foreach(var node in minerals)if(node!=null&&node.distributionWeight>0&&!float.IsInfinity(node.distributionWeight)){pick-=node.distributionWeight;if(pick<0)return node;}
            return stone;
        }
    }
}
