using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    [CreateAssetMenu(menuName="Mismo/World/Exploración por chunks")]
    public sealed class ExplorationWorldSettings : ScriptableObject
    {
        public bool streamingEnabled=true;
        public bool preserveAuthoredCenter=true;
        public int seed=7319;
        public int authoredSize=256;
        public float relief=14,stepHeight=.25f;
        [Range(2,5)] public int loadRadius=3;
        [Range(0,1)] public float treeDensity=.65f;
        public Vector2 treeScale=new Vector2(.8f,1.35f);
        [Header("Global geography (outside the saved centre)")]
        [Min(64)] public float transitionWidth=160;
        [Min(128)] public int regionSize=384;
        [Min(64)] public int siteSpacing=128;
        [Header("Rare villages")]
        [Min(4)] public int villageRegionCells=8;
        [Range(0,1)] public float villageRegionChance=.65f;
        [Min(64)] public float macroScale=260;
        [Min(8)] public float detailScale=32;
        [Range(0,1)] public float mountainChance=.22f;
        [Min(128)] public int mountainSpacing=512;
        [Min(16)] public float mountainRadius=110;
        [Min(0)] public float mountainHeight=42;
        public TerrainBiome[] biomes={
            new TerrainBiome{kind=WorldBiome.Meadow,baseHeight=6,heightVariation=8,terrainRoughness=.3f,trees=.08f},
            new TerrainBiome{kind=WorldBiome.Forest,baseHeight=8,heightVariation=12,terrainRoughness=.6f,trees=.7f},
            new TerrainBiome{kind=WorldBiome.Highlands,baseHeight=12,heightVariation=24,terrainRoughness=.9f,trees=.18f}};
        [Header("Content")]
        public WorldContentCatalog content;
        [Range(0,1)] public float encounterChance=.42f;
        [Range(0,100)] public int regionalLevelIncrement=15;
        [Min(0)] public float healthPerLevel=.025f,damagePerLevel=.015f;
    }
}
