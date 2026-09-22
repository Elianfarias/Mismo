using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Mismo.Gameplay.Player.World;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class VillageSizeChecks
    {
        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var notes=new System.Collections.Generic.List<string>();
                var settings=Object.Instantiate(Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.preserveAuthoredCenter=false;
                int total=0;
                foreach(int seed in new[]{7319,12,654321})
                {
                    settings.seed=seed;var f=new ExplorationTerrain(settings);var towns=new System.Collections.Generic.List<Vector3>();
                    for(int z=-32;z<=32;z++)for(int x=-32;x<=32;x++){var site=f.Site(new Vector2Int(x,z));if(site.kind==WorldSiteKind.Village)towns.Add(site.position);}
                    if(towns.Count>85||towns.Count<10)throw new Exception("Unexpected village density: "+towns.Count);
                    float nearest=float.MaxValue;for(int i=0;i<towns.Count;i++)for(int j=i+1;j<towns.Count;j++)nearest=Mathf.Min(nearest,Vector2.Distance(new Vector2(towns[i].x,towns[i].z),new Vector2(towns[j].x,towns[j].z)));
                    if(nearest<450)throw new Exception("Villages too close: "+nearest);
                    if(f.Site(Vector2Int.zero).kind!=WorldSiteKind.Village)throw new Exception("Missing starting village");
                    notes.Add("Seed "+seed+": "+towns.Count+" villages / 4225 sites; nearest pair "+nearest.ToString("F0")+"m");total+=towns.Count;
                }
                settings.preserveAuthoredCenter=true;var legacyField=new ExplorationTerrain(settings);
                for(int z=-50;z<=50;z+=2)for(int x=-48;x<=48;x+=2)if(Mathf.Abs(legacyField.Height(-50+x,-70+z)-4)>.001f)throw new Exception("Legacy footprint not flat");
                if(Mathf.Abs(legacyField.Height(-50,-129)-legacyField.Height(-50,-129.1f))>.5f)throw new Exception("Legacy boundary seam");
                settings.preserveAuthoredCenter=false;
                var field=new ExplorationTerrain(settings);var origin=field.Site(Vector2Int.zero);float ground=field.Height(origin.position.x,origin.position.z);
                for(int z=-50;z<=50;z+=2)for(int x=-48;x<=48;x+=2)if(Mathf.Abs(field.Height(origin.position.x+x,origin.position.z+z)-ground)>.001f)throw new Exception("Unlevel town footprint");
                var oldSave=new WorldSaveData{x=origin.position.x,y=ground+.3f,z=origin.position.z};WorldSession.MigrateVillageLayout(oldSave,settings);
                var arrival=origin.position+settings.content.VillageArrivalOffset;arrival.y=field.Height(arrival.x,arrival.z)+.3f;
                if(Vector3.Distance(new Vector3(oldSave.x,oldSave.y,oldSave.z),arrival)>.01f||oldSave.villageLayoutRevision!=WorldSession.CurrentVillageLayoutRevision)throw new Exception("Old save relocation");
                var far=new WorldSaveData{x=9999,y=12,z=9999};WorldSession.MigrateVillageLayout(far,settings);if(far.x!=9999||far.y!=12||far.z!=9999)throw new Exception("Distant save moved");
                var root=new GameObject("World");var center=ExplorationChunks.Coordinate(origin.position);var material=Mismo.Core.ProjectAssets.Load<Material>("TerrainSurface");
                for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++)
                {var go=new GameObject("Terrain");go.transform.SetParent(root.transform);var mesh=ExplorationChunks.BuildTerrain(field,center+new Vector2Int(x,z));go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<MeshCollider>().sharedMesh=mesh;}
                new ExplorationContent(settings,field).Decorate(center,root.transform,material);
                var town=root.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("Village "));int foundations=0;
                foreach(var filter in town.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;var vertices=mesh.vertices;var materials=filter.GetComponent<Renderer>().sharedMaterials;
                    for(int i=0;i<materials.Length;i++)
                    {
                        if(!materials[i].name.StartsWith("Wall_")&&!materials[i].name.StartsWith("House_"))continue;
                        var indices=mesh.GetTriangles(i);if(indices.Length==0)continue;
                        float minY=indices.Min(index=>filter.transform.TransformPoint(vertices[index]).y);
                        if(Mathf.Abs(minY-ground)>.04f)throw new Exception("Floating/buried structure: "+materials[i].name+" "+(minY-ground));foundations++;
                    }
                }
                if(foundations<5)throw new Exception("No structural bases tested");
                Physics.SyncTransforms();var pivot=arrival+Vector3.up*1.3f;var rotation=Quaternion.Euler(25,settings.content.villageSpawnYaw,0);
                if(Physics.SphereCast(pivot,.25f,rotation*Vector3.back,out _,9,~0,QueryTriggerInteraction.Ignore))throw new Exception("Arrival camera blocked");
                var sources=new System.Collections.Generic.List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(root.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new System.Collections.Generic.List<NavMeshBuildMarkup>(),sources);
                var nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(origin.position,new Vector3(180,100,180)),Vector3.zero,Quaternion.identity);var instance=NavMesh.AddNavMeshData(nav);
                if(!NavMesh.SamplePosition(arrival,out var entry,3,NavMesh.AllAreas)||!NavMesh.SamplePosition(new Vector3(origin.position.x,ground,origin.position.z),out var plaza,3,NavMesh.AllAreas))throw new Exception("Missing walkable entrance/plaza");
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(entry.position,plaza.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Entrance cannot reach plaza");
                var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));player.transform.position=arrival;player.transform.rotation=Quaternion.Euler(0,settings.content.villageSpawnYaw,0);
                var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";camera.transform.position=origin.position+new Vector3(80,45,105);camera.transform.LookAt(origin.position+Vector3.up*5);camera.farClipPlane=500;
                var cycle=new GameObject("Lighting").AddComponent<DayNightCycle>();cycle.Initialize(Mismo.Core.ProjectAssets.Load<DayNightSettings>("DayNightSettings"));cycle.SetHour(10);RenderSettings.fog=false;
                Directory.CreateDirectory("Docs/Validation/VillageSize");var rt=new RenderTexture(1260,850,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1260,850,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1260,850),0,0);pixels.Apply();File.WriteAllBytes("Docs/Validation/VillageSize/Town.png",pixels.EncodeToPNG());
                notes.Add("PASS: village scale "+settings.content.villageSizeMultiplier+", "+foundations+" structural bases touch ground, level full footprint, old-save migration, distant position preserved, 9m camera clearance, entrance-to-plaza navigation.");File.WriteAllLines("Docs/Validation/VillageSize/Checks.txt",notes);
                instance.Remove();Debug.Log("VILLAGE_SIZE_PASS "+total);EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void Inspect()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var town=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/Villages/MedievalVillage.prefab"));
                var lines=new System.Collections.Generic.List<string>();
                foreach(var filter in town.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;var vertices=mesh.vertices;var materials=filter.GetComponent<Renderer>().sharedMaterials;
                    for(int i=0;i<materials.Length;i++)
                    {
                        string name=materials[i].name;var bounds=new Bounds();bool first=true;
                        foreach(int index in mesh.GetTriangles(i)){var p=filter.transform.TransformPoint(vertices[index]);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                        if(!first)lines.Add(name+" min="+bounds.min.ToString("F3")+" max="+bounds.max.ToString("F3"));
                    }
                }
                File.WriteAllLines("Docs/Validation/VillageMeshBounds.txt",lines);Debug.Log("VILLAGE_BOUNDS_PASS");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

