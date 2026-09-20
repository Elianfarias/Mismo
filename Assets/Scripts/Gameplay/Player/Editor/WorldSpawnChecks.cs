using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class WorldSpawnChecks
    {
        public static void ConfigureBalanceBatch()
        {
            var catalog=Mismo.Core.ProjectAssets.Load<WorldContentCatalog>("WorldContentCatalog");
            catalog.clearingChance=.85f;catalog.roamingChance=.75f;catalog.roamingSpacing=64;
            catalog.fogEnabled=true;catalog.fogStart=28;catalog.fogEnd=90;
            foreach(var entry in catalog.encounters)
            {
                if(entry.id=="Goblin patrol")entry.weight=8;
                if(entry.id.StartsWith("forest.")&&entry.site==WorldSiteKind.Clearing)
                {entry.minimumCount=2;entry.maximumCount=3;}
            }
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();RunBatch();
        }
        static void Check(bool value,string label)
        {if(!value)throw new Exception(label);Debug.Log("SPAWN_CHECK "+label);}
        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var template=Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings");
                Check(template!=null&&template.content!=null,"World resource loads its encounter catalog");
                var settings=Object.Instantiate(template);settings.preserveAuthoredCenter=false;
                var terrain=new ExplorationTerrain(settings);
                settings.content.ApplyAtmosphere(settings.loadRadius);
                Check(RenderSettings.fog&&RenderSettings.fogEndDistance<=settings.loadRadius*32-8&&RenderSettings.fogStartDistance<RenderSettings.fogEndDistance,"Fog covers streaming horizon");
                int fauna=0,goblins=0;var variants=new HashSet<string>();
                for(int i=0;i<2000;i++)
                {
                    var choice=settings.content.Encounter(WorldSiteKind.Clearing,WorldBiome.Forest,16,10,i);
                    if(choice.id.StartsWith("forest.")){fauna++;variants.Add(choice.id);}else goblins++;
                }
                Check(fauna>goblins*4&&variants.Count==7,"Forest selection favors all seven creature variants over goblins");
                var candidates=new ExplorationContent(settings,terrain);int roaming=0,sites=0;
                for(int z=0;z<24;z++)for(int x=0;x<24;x++)foreach(var encounter in candidates.Encounters(new Vector2Int(x,z)))
                {
                    if(encounter.roaming){roaming++;Check(encounter.kind==WorldSiteKind.Clearing,"Roaming never selects a boss site");}else sites++;
                }
                Check(roaming>sites,"Roaming adds more encounter locations than the original sparse sites");
                var legacy=Object.Instantiate(template);legacy.preserveAuthoredCenter=true;
                var legacyContent=new ExplorationContent(legacy,new ExplorationTerrain(legacy));int legacyCount=0;
                for(int z=-4;z<4;z++)for(int x=-4;x<4;x++)legacyCount+=legacyContent.Encounters(new Vector2Int(x,z)).Count(s=>s.roaming);
                Check(legacyCount>0,"Direct scene Play and legacy centre also receive roaming encounters");
                WorldSite site=default;bool found=false;
                for(int z=0;z<12&&!found;z++)for(int x=0;x<12&&!found;x++)
                {
                    var candidate=terrain.Site(new Vector2Int(x,z));
                    int seed=ExplorationTerrain.Hash(settings.seed,x,z,200);
                    if(candidate.kind!=WorldSiteKind.Clearing||terrain.Biome(candidate.position.x,candidate.position.z)!=WorldBiome.Forest)continue;
                    if(new System.Random(seed).NextDouble()>settings.content.clearingChance)continue;
                    var entry=settings.content.Encounter(candidate.kind,WorldBiome.Forest,16,candidate.position.y,ExplorationTerrain.Hash(settings.seed,x,z,201));
                    if(entry==null||!entry.id.StartsWith("forest."))continue;
                    site=candidate;found=true;
                }
                Check(found,"Natural forest encounter exists with saved density and weights");
                var chunk=ExplorationChunks.Coordinate(site.position);
                var root=new GameObject("Generated terrain");
                var sources=new List<NavMeshBuildSource>();
                for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
                {
                    var mesh=ExplorationChunks.BuildTerrain(terrain,chunk+new Vector2Int(x,z));
                    sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,sourceObject=mesh,transform=Matrix4x4.identity,area=0});
                }
                var data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(site.position,new Vector3(100,100,100)),Vector3.zero,Quaternion.identity);
                Check(data!=null,"Generated voxel terrain builds navigation");
                var instance=NavMesh.AddNavMeshData(data);
                var player=new GameObject("In-memory player");player.transform.position=site.position+Vector3.right*40;
                var inventory=player.AddComponent<PlayerInventory>();var profile=new InventoryProfile();
                typeof(PlayerInventory).GetField("profile",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(inventory,profile);
                var content=new ExplorationContent(settings,terrain);
                content.Populate(chunk,root.transform,inventory);
                Check(root.GetComponentsInChildren<WorldEnemyIdentity>().Length==0,"Undiscovered region waits without consuming encounter");
                profile.regions.Add(new DiscoveredRegion{id=terrain.RegionId(site.position.x,site.position.z),minimumLevel=16});
                content.Populate(chunk,root.transform,inventory);
                var actors=root.GetComponentsInChildren<WorldEnemyIdentity>();
                Check(actors.Length>0,"Forest creatures actually instantiate on generated terrain");
                Check(actors.All(a=>NavMesh.SamplePosition(a.transform.position,out _,.2f,NavMesh.AllAreas)),"Spawned actors stand on navigation");
                content.Populate(chunk,root.transform,inventory);
                Check(root.GetComponentsInChildren<WorldEnemyIdentity>().Length==actors.Length,"Repeated streaming population creates no duplicates");
                foreach(var actor in actors)profile.defeatedEnemies.Add(actor.Id);
                Object.DestroyImmediate(root);root=new GameObject("Reloaded terrain");content.Unload(chunk);
                content.Populate(chunk,root.transform,inventory);
                Check(root.GetComponentsInChildren<WorldEnemyIdentity>().Length==0,"Defeated encounters remain absent after reload");
                instance.Remove();
                System.IO.Directory.CreateDirectory("Docs/Validation");
                System.IO.File.WriteAllText("Docs/Validation/WorldSpawnChecks.txt","PASS: resource catalog, natural forest selection, generated terrain NavMesh, region discovery retry, actual creature instantiation, positions, no duplicates, defeated persistence.");
                Debug.Log("WORLD_SPAWN_PASS");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
