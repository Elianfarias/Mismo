using System;
using System.Linq;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class ProceduralWorldChecks
    {
        [MenuItem("Mismo/World/Configure procedural content")]
        public static void Configure()
        {
            const string path="Assets/Data/World/WorldContentCatalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<WorldContentCatalog>(path);
            if(catalog==null)
            {
                catalog=ScriptableObject.CreateInstance<WorldContentCatalog>();
                var goblin=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/Goblin.prefab");
                var elite=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/GoblinElite.prefab");
                var boss=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/FirstBoss.prefab");
                catalog.encounters=new[]{
                    new WorldEncounterEntry{id="Goblin patrol",prefab=goblin,elitePrefab=elite,weight=50},
                    new WorldEncounterEntry{id="Goblin camp",prefab=goblin,elitePrefab=elite,site=WorldSiteKind.Ruin,minimumCount=3,maximumCount=3,guaranteedElite=true},
                    new WorldEncounterEntry{id="Wandering elite",prefab=elite,weight=1,minimumCount=1,maximumCount=1,biomes=new[]{WorldBiome.Forest,WorldBiome.Highlands}},
                    new WorldEncounterEntry{id="Sanctuary guardian",prefab=boss,site=WorldSiteKind.BossArena,minimumCount=1,maximumCount=1}};
                AssetDatabase.CreateAsset(catalog,path);
            }
            var settings=Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings");
            if(settings==null)throw new Exception("Missing exploration settings");
            if(settings.content==null){settings.content=catalog;EditorUtility.SetDirty(settings);}
            AssetDatabase.SaveAssets();
        }
        static int count;
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("WORLD_CHECK "+message);}
        public static void RunBatch()
        {
            try
            {
                Configure();count=0;
                var settings=Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings");
                var field=new ExplorationTerrain(settings);var old=new World.VoxelRegionHeightfield(settings.seed,settings.authoredSize,settings.relief,settings.stepHeight);
                foreach(int sign in new[]{-1,1})for(int i=-128;i<=128;i+=8)
                {Check(field.Height(sign*128.5f,i+.5f)==old.Height(sign*128.5f,i+.5f),"Saved centre X seam");Check(field.Height(i+.5f,sign*128.5f)==old.Height(i+.5f,sign*128.5f),"Saved centre Z seam");}
                var chunk=new Vector2Int(16,-17);var a=ExplorationChunks.BuildTerrain(field,chunk);var b=ExplorationChunks.BuildTerrain(new ExplorationTerrain(settings),chunk);
                Check(a.vertices.SequenceEqual(b.vertices)&&a.triangles.SequenceEqual(b.triangles),"Independent chunk regeneration deterministic");Object.DestroyImmediate(a);Object.DestroyImmediate(b);
                int gentle=0,total=0;float worst=0;
                for(int z=-1600;z<1600;z+=13)for(int x=-1600;x<1600;x+=13)
                {if(!field.IsExterior(x,z))continue;float h=field.Height(x,z);float delta=Mathf.Max(Mathf.Abs(field.Height(x+1,z)-h),Mathf.Abs(field.Height(x,z+1)-h));if(delta<=.5f)gentle++;worst=Mathf.Max(worst,delta);total++;}
                Debug.Log("WORLD_METRICS gentle="+(float)gentle/total+" worst_1m_step="+worst);
                Check((float)gentle/total>.65f,"At least 65% sampled exterior supports gentle movement");
                var site=field.Site(new Vector2Int(5,5));Check(site.position==new ExplorationTerrain(settings).Site(site.cell).position,"POI stable across field recreation");
                float before=field.Height(600,800);var copy=Object.Instantiate(settings);copy.content=null;
                Check(before==new ExplorationTerrain(copy).Height(600,800),"Catalog replacement does not change geography");Object.DestroyImmediate(copy);
                var regions=new InventoryProfile();regions.regions.Add(new DiscoveredRegion{id="test",minimumLevel=25});regions.defeatedEnemies.Add("enemy:1");var cloned=regions.Copy();cloned.regions[0].minimumLevel=30;
                Check(regions.regions[0].minimumLevel==25&&cloned.defeatedEnemies.Contains("enemy:1"),"World persistence copy is independent");
                var json=JsonUtility.ToJson(regions);var restored=JsonUtility.FromJson<InventoryProfile>(json);Check(restored.regions[0].minimumLevel==25&&restored.defeatedEnemies.Contains("enemy:1"),"Region and defeat survive serialization");
                Check(settings.content.encounters.All(e=>e.prefab!=null),"Default encounters reference real prefabs");
                Check(settings.content.Encounter(WorldSiteKind.Village,WorldBiome.Forest,20,10,1)==null,"Village is safe");
                Check(settings.content.Encounter(WorldSiteKind.BossArena,WorldBiome.Forest,20,10,1).site==WorldSiteKind.BossArena,"Boss selection restricted to arena");
                var fresh=Object.Instantiate(settings);fresh.preserveAuthoredCenter=false;fresh.seed=WorldSaveData.FreshSeed(settings.seed);
                var freshTerrain=new ExplorationTerrain(fresh);var village=freshTerrain.Site(Vector2Int.zero);
                Check(village.kind==WorldSiteKind.Village&&freshTerrain.IsExterior(0,0),"New game starts in a fully procedural safe village");
                Check(Mathf.Abs(freshTerrain.Height(village.position.x,village.position.z)-village.position.y)<=fresh.stepHeight,"New world spawn lies on generated ground");
                var snapshot=JsonUtility.ToJson(fresh);var restoredSettings=Object.Instantiate(settings);JsonUtility.FromJsonOverwrite(snapshot,restoredSettings);
                Check(new ExplorationTerrain(restoredSettings).Height(80,90)==freshTerrain.Height(80,90),"Saved settings reproduce new-world terrain");
                Object.DestroyImmediate(fresh);Object.DestroyImmediate(restoredSettings);
                Debug.Log("PROCEDURAL_WORLD_PASS "+count);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
