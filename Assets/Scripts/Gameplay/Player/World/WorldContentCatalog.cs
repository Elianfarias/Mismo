using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    // Keep existing numeric values: catalogs and old saves serialize these as integers.
    public enum WorldBiome { Meadow=0, Forest=1, Highlands=2, Desert=3, Ice=4, Mountains=5, Ocean=6 }
    public enum WorldSiteKind { Clearing, Village, Ruin, BossArena, Secret }
    public enum WorldAssetKind { House, Blacksmith, Tree, Rock, Ruin, Landmark, Well, Fence, Grass, Bush, Flower, Deadwood, Village }
    [Serializable] public sealed class TerrainBiome
    {
        public WorldBiome kind;
        public float baseHeight=6,heightVariation=8,terrainRoughness=.3f;
        [Range(0,1)] public float trees=.2f;
    }
    [Serializable] public sealed class WorldAssetEntry
    {
        public string id;
        public WorldAssetKind kind;
        public WorldBiome[] biomes=Array.Empty<WorldBiome>();
        public GameObject prefab;
        [Tooltip("Solo decoracion: no permite recolectar este asset, incluso si su categoria o Gathering Node define un recurso.")]
        public bool decorativeOnly;
        [Tooltip("Desactiva viento y respuesta al jugador para esta entrada vegetal. No cambia la recoleccion.")]
        public bool disableVegetationMotion;
        [Tooltip("Optional harvestable replacement for this decoration.")]
        public ResourceNodeDefinition gatheringNode;
        [Min(0)] public float weight=1;
        public Vector2 footprint=new Vector2(6,6);
        [Range(0,45)] public float maxSlope=15;
        public float[] rotations={0,90,180,270};
        public Vector2 scaleRange=new Vector2(.85f,1.15f);
    }
    [Serializable] public sealed class WorldEncounterEntry
    {
        public string id;
        public WorldSiteKind site=WorldSiteKind.Clearing;
        public WorldBiome[] biomes=Array.Empty<WorldBiome>();
        public GameObject prefab,elitePrefab;
        [Min(0)] public float weight=1;
        [Range(0f,100f), Tooltip("Porcentaje de probabilidad de que esta entrada participe en la selección cuando es elegible. 0 = nunca, 100 = siempre.")]
        public float appearanceChancePercent=100f;
        [Range(1,5)] public int minimumCount=2,maximumCount=3;
        [Range(0,1)] public float eliteChance=.025f;
        public bool guaranteedElite;
        [Min(1)] public int minimumLevel=1;
        public Vector2 altitude=new Vector2(-100,200);
        [Min(0)] public float minimumPlayerDistance=18;
    }
    [CreateAssetMenu(menuName="Mismo/World/Content catalog")]
    public sealed class WorldContentCatalog : ScriptableObject
    {
        public WorldIntroductionDefinition introduction;
        [Header("Village arrival (new worlds)")]
        [Min(1)] public float villageSizeMultiplier=2.6f;
        public float villageGroundOffset=-.19670273f;
        public Vector3 VillageArrivalOffset=>villageSpawnOffset*Mathf.Max(1,villageSizeMultiplier);
        public Vector3 villageSpawnOffset=new Vector3(0,0,-29);
        public float villageSpawnYaw=0;
        [Header("Atmosphere")]
        public GroveWorldStyle groveStyle;
        [Header("Reusable structures (baked in the structure workshop)")]
        [Range(0,1)] public float structureChance=.7f;
        public WorldStructureEntry[] structures=Array.Empty<WorldStructureEntry>();
        public WorldStructureEntry Structure(WorldSiteKind kind,WorldBiome biome,int seed)
        {
            var random=new System.Random(seed);if(structureChance<=0||random.NextDouble()>=structureChance)return null;
            var candidates=structures??Array.Empty<WorldStructureEntry>();double total=0;
            foreach(var e in candidates)if(e!=null&&e.prefab!=null&&e.placement==Structures.StructurePlacement.CompatibleSites&&e.site==kind&&Allows(e.biomes,biome))total+=Math.Max(0,e.weight);
            double roll=random.NextDouble()*total;
            foreach(var e in candidates)if(e!=null&&e.prefab!=null&&e.placement==Structures.StructurePlacement.CompatibleSites&&e.site==kind&&Allows(e.biomes,biome)&&e.weight>0){roll-=e.weight;if(roll<0)return e;}
            return null;
        }
        public VegetationMotionSettings vegetationMotion = new VegetationMotionSettings();
        public bool fogEnabled=true;
        public Color fogColor=new Color(.62f,.72f,.73f);
        [Min(0)] public float fogStart=28;
        [Min(1)] public float fogEnd=90;
        [Header("Encounter density (also applies to continued worlds)")]
        [Range(0,1)] public float clearingChance=.85f;
        [Range(0,1)] public float roamingChance=.75f;
        [Min(32)] public int roamingSpacing=64;
        public void ApplyAtmosphere(int loadRadius)
        {
            RenderSettings.fog=fogEnabled;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=fogColor;
            RenderSettings.fogEndDistance=Mathf.Min(Mathf.Max(10,fogEnd),Mathf.Max(2,loadRadius)*32-8);
            RenderSettings.fogStartDistance=Mathf.Clamp(fogStart,0,RenderSettings.fogEndDistance-1);
        }
        [Header("Nature per biome")]
        [Tooltip("Overrides nature assets and densities for each assigned biome. Buildings and encounters remain below.")]
        public WorldBiomeContent[] biomeContents=Array.Empty<WorldBiomeContent>();
        public WorldBiomeContent BiomeContent(WorldBiome biome)
        {
            if(biomeContents!=null)foreach(var profile in biomeContents)
                if(profile!=null&&profile.biome==biome)return profile;
            return null;
        }
        public float Density(WorldAssetKind kind,WorldBiome biome)
        {
            var profile=BiomeContent(biome);if(profile!=null)return profile.Density(kind);
            switch(kind)
            {
                case WorldAssetKind.Grass:return grassDensity;
                case WorldAssetKind.Bush:return bushDensity;
                case WorldAssetKind.Flower:return flowerDensity;
                case WorldAssetKind.Rock:return rockDensity;
                case WorldAssetKind.Deadwood:return deadwoodDensity;
                default:return 0;
            }
        }
        [Header("Legacy ground density (biomes without a profile)")]
        [Range(0,1)] public float grassDensity=.55f,bushDensity=.14f,flowerDensity=.12f,rockDensity=.07f,deadwoodDensity=.035f;
        public WorldAssetEntry[] assets=Array.Empty<WorldAssetEntry>();
        public WorldEncounterEntry[] encounters=Array.Empty<WorldEncounterEntry>();
        public static bool Allows(WorldBiome[] allowed,WorldBiome biome)=>allowed==null||allowed.Length==0||Array.IndexOf(allowed,biome)>=0;
        public WorldAssetEntry Asset(WorldAssetKind kind,WorldBiome biome,int seed)
        {
            var profile=WorldBiomeContent.IsNature(kind)?BiomeContent(biome):null;
            var candidates=(profile!=null?profile.assets:assets)??Array.Empty<WorldAssetEntry>();
            double total=0;foreach(var a in candidates)if(a!=null&&a.prefab!=null&&a.kind==kind&&Allows(a.biomes,biome))total+=Math.Max(0,a.weight);
            double roll=new System.Random(seed).NextDouble()*total;
            foreach(var a in candidates)if(a!=null&&a.prefab!=null&&a.kind==kind&&Allows(a.biomes,biome)&&a.weight>0){roll-=a.weight;if(roll<0)return a;}
            return null;
        }
        public WorldEncounterEntry Encounter(WorldSiteKind site,WorldBiome biome,int level,float height,int seed)
        {
            var random=new System.Random(seed);
            var candidates=new System.Collections.Generic.List<WorldEncounterEntry>();
            foreach(var e in encounters)
            {
                if(!Eligible(e,site,biome,level,height))continue;
                float chance=Mathf.Clamp01(e.appearanceChancePercent/100f);
                if(chance>=1f||random.NextDouble()<chance)candidates.Add(e);
            }

            double total=0;foreach(var e in candidates)total+=e.weight;
            double roll=random.NextDouble()*total;
            foreach(var e in candidates)if(e.weight>0){roll-=e.weight;if(roll<0)return e;}
            return null;
        }
        static bool Eligible(WorldEncounterEntry e,WorldSiteKind site,WorldBiome biome,int level,float height)=>
            e!=null&&e.prefab!=null&&e.weight>0&&e.site==site&&Allows(e.biomes,biome)&&level>=e.minimumLevel&&height>=e.altitude.x&&height<=e.altitude.y;
    }
    [Serializable] public sealed class WorldStructureEntry
    {
        public string id;
        public WorldSiteKind site=WorldSiteKind.Secret;
        public Structures.StructurePlacement placement;
        public WorldBiome[] biomes=Array.Empty<WorldBiome>();
        public Structures.StructureInstance prefab;
        [Min(0)] public float weight=1;
    }
}
