using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    [CreateAssetMenu(menuName="Mismo/World/Biome content")]
    public sealed class WorldBiomeContent : ScriptableObject
    {
        public WorldBiome biome;
        [Header("Probability per placement cell (0 disables the category)")]
        [Range(0,1)] public float trees, grass, bushes, flowers, rocks, deadwood;
        [Tooltip("Nature used exclusively in this biome. An empty category generates nothing.")]
        public WorldAssetEntry[] assets = Array.Empty<WorldAssetEntry>();

        public float Density(WorldAssetKind kind)
        {
            switch(kind)
            {
                case WorldAssetKind.Tree: return Mathf.Clamp01(trees);
                case WorldAssetKind.Grass: return Mathf.Clamp01(grass);
                case WorldAssetKind.Bush: return Mathf.Clamp01(bushes);
                case WorldAssetKind.Flower: return Mathf.Clamp01(flowers);
                case WorldAssetKind.Rock: return Mathf.Clamp01(rocks);
                case WorldAssetKind.Deadwood: return Mathf.Clamp01(deadwood);
                default: return 0;
            }
        }
        public static bool IsNature(WorldAssetKind kind) => kind == WorldAssetKind.Tree ||
            kind == WorldAssetKind.Grass || kind == WorldAssetKind.Bush || kind == WorldAssetKind.Flower ||
            kind == WorldAssetKind.Rock || kind == WorldAssetKind.Deadwood;
    }
}
