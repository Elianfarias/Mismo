using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Core;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

public static class VillageNpcChecks
{
    public static void InspectScale()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Run in isolated project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var lines=new List<string>();
        foreach(var path in new[]{"Assets/Art/Prefabs/Player/Player.prefab","Assets/Art/Prefabs/Quests/Village/mara.prefab"})
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));var renderers=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            lines.Add(path+" visible bounds="+bounds);
            foreach(var r in renderers.OrderByDescending(r=>r.bounds.max.y).Take(12))lines.Add(r.name+" y="+r.bounds.min.y+".."+r.bounds.max.y);
            foreach(Transform child in go.transform)lines.Add("child "+child.name+" scale="+child.lossyScale);
            Object.DestroyImmediate(go);
        }
        foreach(var path in Directory.GetFiles(VillageNpcIntegration.Source,"*.fbx",SearchOption.AllDirectories))
        {
            string asset=path.Replace('\\','/');var importer=(ModelImporter)AssetImporter.GetAtPath(asset);
            lines.Add(Path.GetRelativePath(VillageNpcIntegration.Source,asset)+" clips="+AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().Count()+" source takes="+importer.defaultClipAnimations.Length+" importAnimation="+importer.importAnimation);
        }
        Directory.CreateDirectory("output/village-npcs");File.WriteAllLines("output/village-npcs/ScaleInspection.txt",lines);Debug.Log("NPC_SCALE_INSPECT_OK");
    }
    public static void Build(){Run();QuestIntegration.Build();}
    public static void Run()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Run in isolated project");
        Directory.CreateDirectory("output/village-npcs");
        try
        {
            ProjectOrganizationChecks.Run();
            var catalog=QuestCatalog.Load();var population=catalog.villageNpcs;
            Require(!catalog.journalContacts&&population!=null&&population.residents.Length==5,"Physical contacts configured");
            var notes=new List<string>();
            foreach(var resident in population.residents)
            {
                Require(resident.prefab!=null&&resident.prefab.GetComponent<QuestGiver>()?.npc!=null,"NPC prefab and dialogue linked");
                var skin=resident.prefab.GetComponentInChildren<SkinnedMeshRenderer>();
                Require(skin!=null&&skin.bones.Length>10&&skin.sharedMesh.vertexCount>1000,"Voxel mesh keeps skeleton");
                Require(skin.sharedMaterials.All(m=>m!=null&&m.shader!=null),"NPC material dependencies valid");
            }
            Require(Directory.GetFiles(VillageNpcIntegration.Prefabs,"*.prefab").Length==14,"14 unique voxelized models");
            foreach(int seed in new[]{7319,12,654321})CheckWorld(seed,false,notes);
            CheckWorld(7319,true,notes);
            CheckWorld(7319,false,notes,2);
            File.WriteAllLines("output/village-npcs/WorldChecks.txt",new[]{"PASS: assets, 3 procedural seeds, legacy and finite worlds, navigation with conversation line of sight, scale, save migration, duplicate prevention and unload."}.Concat(notes));
            Debug.Log("NPC_WORLD_CHECKS_OK");
        }
        catch(Exception e){File.WriteAllText("output/village-npcs/WorldChecks.txt","FAIL: "+e);throw;}
    }
    static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
    static void CheckWorld(int seed,bool legacy,List<string> notes,int generation=0)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var settings=Object.Instantiate(ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.seed=seed;settings.preserveAuthoredCenter=legacy;settings.generationVersion=generation;
        var terrain=new ExplorationTerrain(settings);var origin=legacy?new Vector3(-50,terrain.Height(-50,-70),-70):terrain.Site(Vector2Int.zero).position;
        var center=ExplorationChunks.Coordinate(origin);var root=new GameObject("NPC validation world");var material=ProjectAssets.Load<Material>("TerrainSurface");
        var meshes=new List<Mesh>();
        for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++)
        {
            var tile=new GameObject("Terrain");tile.transform.SetParent(root.transform);var mesh=ExplorationChunks.BuildTerrain(terrain,center+new Vector2Int(x,z));meshes.Add(mesh);
            tile.AddComponent<MeshFilter>().sharedMesh=mesh;tile.AddComponent<MeshRenderer>().sharedMaterial=material;tile.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        GameObject town;
        if(legacy)
        {
            var village=settings.content.Asset(WorldAssetKind.Village,terrain.Biome(origin.x,origin.z),seed);
            town=Object.Instantiate(village.prefab,origin+Vector3.up*settings.content.villageGroundOffset*settings.content.villageSizeMultiplier,Quaternion.identity,root.transform);town.transform.localScale*=settings.content.villageSizeMultiplier;
            VillageNpcSpawner.Populate(town,terrain,true);
        }
        else
        {
            new ExplorationContent(settings,terrain).Decorate(center,root.transform,material);
            town=root.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("Village ")).gameObject;
        }
        var npcs=town.GetComponentsInChildren<QuestGiver>();Require(npcs.Length==5,"Five residents spawned for seed "+seed+" legacy="+legacy);
        VillageNpcSpawner.Populate(town,terrain,true);Require(town.GetComponentsInChildren<QuestGiver>().Length==5,"Duplicate population prevented");
        Physics.SyncTransforms();
        var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(root.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
        var nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(origin,new Vector3(180,100,180)),Vector3.zero,Quaternion.identity);var navInstance=NavMesh.AddNavMeshData(nav);
        try
        {
            var arrival=origin+settings.content.VillageArrivalOffset;arrival.y=terrain.Height(arrival.x,arrival.z)+.3f;
            var saved=new WorldSaveData{villageLayoutRevision=1,x=origin.x,y=origin.y,z=origin.z};WorldSession.MigrateVillageLayout(saved,settings);
            Require(saved.villageLayoutRevision==WorldSession.CurrentVillageLayoutRevision&&Vector3.Distance(new Vector3(saved.x,saved.y,saved.z),arrival)<.01f,"Previous village layout migrates to clear entrance");
            saved.x=origin.x;saved.y=origin.y;saved.z=origin.z;WorldSession.MigrateVillageLayout(saved,settings);Require(saved.x==origin.x&&saved.z==origin.z,"Migration does not relocate twice");
            var distant=new WorldSaveData{villageLayoutRevision=1,x=9999,y=123,z=9999};WorldSession.MigrateVillageLayout(distant,settings);Require(distant.x==9999&&distant.y==123&&distant.z==9999,"Distant exploration position preserved");
            Require(NavMesh.SamplePosition(arrival,out var entry,3,NavMesh.AllAreas),"Walkable entrance");
            foreach(var npc in npcs)
            {
                var p=npc.transform.position;Require(Mathf.Abs(p.y-terrain.Height(p.x,p.z))<.03f,"NPC feet on terrain: "+npc.name);
                Require(Mathf.Abs(npc.transform.lossyScale.y-QuestCatalog.Load().villageNpcs.residentScale)<.01f,"Configured NPC scale independent of village multiplier");
                var capsule=npc.GetComponent<CapsuleCollider>();Require(capsule.bounds.size.y>2.2f&&capsule.bounds.size.y<2.4f,"Resident body matches the enlarged character scale");
                bool reached=false;int sampled=0,visible=0;var blockers=new HashSet<string>();
                foreach(float approachRadius in new[]{1.25f,2f,2.6f})for(int i=0;i<24;i++)
                {
                    float angle=i*Mathf.PI/12;var approach=p+new Vector3(Mathf.Sin(angle)*approachRadius,.1f,Mathf.Cos(angle)*approachRadius);
                    if(!NavMesh.SamplePosition(approach,out var target,.8f,NavMesh.AllAreas)||Vector3.Distance(p,target.position)>npc.interactionRange)continue;
                    sampled++;
                    var eye=target.position+Vector3.up*1.2f;var destination=npc.transform.TransformPoint(Vector3.up*1.2f);var delta=destination-eye;
                    var obstruction=Physics.RaycastAll(eye,delta,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Where(hit=>!hit.transform.IsChildOf(npc.transform)).ToArray();
                    foreach(var hit in obstruction)blockers.Add(hit.transform.name+" at "+hit.point);
                    if(obstruction.Length>0)continue;
                    visible++;
                    var path=new NavMeshPath();if(NavMesh.CalculatePath(entry.position,target.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete){reached=true;break;}
                }
                if(!reached)
                {
                    Render(origin,npcs);var diagnostic=new List<string>{"sampled="+sampled+" visible="+visible+" blockers="+string.Join("; ",blockers.Take(12)),"entry="+entry.position};
                    for(int z=-10;z<=10;z+=2)for(int x=-10;x<=10;x+=2)
                    {
                        var spot=town.transform.TransformPoint(new Vector3(x,0,z));spot.y=terrain.Height(spot.x,spot.z);
                        var hits=Physics.RaycastAll(spot+Vector3.up*10,Vector3.down,11,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).ToArray();
                        var route=new NavMeshPath();bool found=NavMesh.SamplePosition(spot,out var sampledSpot,1,NavMesh.AllAreas);bool connected=found&&NavMesh.CalculatePath(entry.position,sampledSpot.position,NavMesh.AllAreas,route)&&route.status==NavMeshPathStatus.PathComplete;
                        diagnostic.Add(x+","+z+" connected="+connected+" navY="+(found?sampledSpot.position.y-spot.y:-99)+" surfaces="+string.Join(";",hits.Take(4).Select(h=>h.collider.name+":"+(h.point.y-spot.y))));
                    }
                    File.WriteAllLines("output/village-npcs/NavigationDiagnostic.txt",diagnostic);
                }
                Require(reached,"Entrance cannot reach "+npc.name+" at "+p);
                notes.Add("Seed "+seed+" legacy="+legacy+" generation="+generation+": "+npc.npc.displayName+" reachable at "+(p-origin));
            }
            if(seed==7319&&!legacy)Render(origin,npcs);
        }
        finally{navInstance.Remove();Object.DestroyImmediate(nav);Object.DestroyImmediate(root);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);Object.DestroyImmediate(settings);}
        Require(!QuestInteractable.Active.OfType<QuestGiver>().Any(),"Unloaded NPCs leave no registered interactions");
    }
    static void Render(Vector3 origin,QuestGiver[] npcs)
    {
        var cycle=new GameObject("Lighting").AddComponent<DayNightCycle>();cycle.Initialize(ProjectAssets.Load<DayNightSettings>("DayNightSettings"));cycle.SetHour(10);RenderSettings.fog=false;
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.farClipPlane=500;
        camera.transform.position=origin+new Vector3(30,48,-40);camera.transform.LookAt(origin);Capture(camera,"Town");
        foreach(var npc in npcs)
        {
            var p=npc.transform.position;camera.transform.position=p+npc.transform.forward*4+Vector3.up*1.7f;
            camera.transform.LookAt(p+Vector3.up*.95f);Capture(camera,npc.npc.id);
        }
        Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(cycle.gameObject);
    }
    static void Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(1100,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var pixels=new Texture2D(1100,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1100,800),0,0);pixels.Apply();File.WriteAllBytes("output/village-npcs/"+name+".png",pixels.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);
    }
}
