using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.World
{
    public interface IStaticWorldNavigation { void StopNavigation(); }
    /// <summary>Keeps the authored centre, streams deterministic terrain outside it.</summary>
    public sealed class ExplorationChunks : MonoBehaviour
    {
        public const int ChunkSize=32;
        public ExplorationWorldSettings Settings { get; private set; }
        public int LoadedCount=>chunks.Count;
        public void RefreshResourceNavigation()=>dirty=true;
        readonly Dictionary<Vector2Int,GameObject> chunks=new Dictionary<Vector2Int,GameObject>();
        readonly Dictionary<Vector2Int,GameObject> authoredEncounters=new Dictionary<Vector2Int,GameObject>();
        Transform player,geometry;
        Material material;
        Mesh[] trees;
        ExplorationTerrain field;
        ExplorationContent content;
        Equipment.Inventory.PlayerInventory inventory;
        float nextDiscovery;
        NavMeshData navigation;
        NavMeshDataInstance navInstance;
        AsyncOperation navOperation;
        IStaticWorldNavigation previousNavigation;
        bool dirty,navigationReady;
        float nextNav;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register(){SceneManager.sceneLoaded-=OnSceneLoaded;SceneManager.sceneLoaded+=OnSceneLoaded;}
        static void OnSceneLoaded(Scene scene,LoadSceneMode mode)
        {
            var settings=Resources.Load<ExplorationWorldSettings>("ExplorationWorldSettings");
            if(settings==null||!settings.streamingEnabled)return;
            var region=scene.GetRootGameObjects().FirstOrDefault(g=>g.name.StartsWith("Voxel Region"));
            if(region==null||region.GetComponent<ExplorationChunks>()!=null)return;
            if(!region.name.Contains("seed "+settings.seed+" "))return;
            var player=Object.FindAnyObjectByType<PlayerController>();
            if(player==null)return;
            region.AddComponent<ExplorationChunks>().Initialize(WorldSession.Settings(settings),player.transform);
        }
        public static Vector2Int Coordinate(Vector3 position)=>new Vector2Int(Mathf.FloorToInt(position.x/ChunkSize),Mathf.FloorToInt(position.z/ChunkSize));
        public bool IsAuthored(Vector2Int id)
        {if(!Settings.preserveAuthoredCenter)return false;int half=Settings.authoredSize/2;return id.x*32>=-half&&id.x*32<half&&id.y*32>=-half&&id.y*32<half;}
        public void Initialize(ExplorationWorldSettings settings,Transform target)
        {
            Settings=settings;player=target;field=new ExplorationTerrain(settings);
            var music = target.GetComponent<Presentation.PlayerMusic>() ?? target.gameObject.AddComponent<Presentation.PlayerMusic>();
            music.InitializeWorld(field);
            var map=target.GetComponent<Presentation.WorldMapPanel>();if(map==null)map=target.gameObject.AddComponent<Presentation.WorldMapPanel>();map.Initialize(settings);
            content=new ExplorationContent(settings,field);inventory=target.GetComponent<Equipment.Inventory.PlayerInventory>();
            // sceneLoaded runs before PlayerController.Start initializes the inventory.
            if(WorldSession.Current!=null)
            {
                inventory=inventory??target.gameObject.AddComponent<Equipment.Inventory.PlayerInventory>();
                inventory.Initialize(Resources.Load<Equipment.Inventory.ItemCatalog>("ItemCatalog"));
            }
            if(target.GetComponent<Presentation.WorldSurfaceFeedback>()==null)target.gameObject.AddComponent<Presentation.WorldSurfaceFeedback>();
            geometry=transform.Find("RegionGeometry");
            if(geometry==null){enabled=false;return;}
            material=geometry.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.StartsWith("Terrain_")).sharedMaterial;
            var surface=Resources.Load<Material>("TerrainSurface");
            if(surface!=null)
            {
                material=surface;
                foreach(var renderer in geometry.GetComponentsInChildren<MeshRenderer>())if(renderer.name.StartsWith("Terrain_"))renderer.sharedMaterial=surface;
            }
            var forest=geometry.Find("GeneratedGeometry/Generated Forest");
            trees=forest!=null?forest.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).Distinct().ToArray():new Mesh[0];
            if(forest!=null)foreach(var collider in forest.GetComponentsInChildren<BoxCollider>())
                if(collider.GetComponent<ClimbableTree>()==null)collider.gameObject.AddComponent<ClimbableTree>();
            if(settings.preserveAuthoredCenter&&forest!=null&&settings.content!=null)
                foreach(var old in forest.Cast<Transform>().ToArray())
                {
                    var p=old.position;int seed=ExplorationTerrain.Hash(settings.seed,(int)p.x,(int)p.z,300);
                    var asset=settings.content.Asset(WorldAssetKind.Tree,field.Biome(p.x,p.z),seed);if(asset==null)continue;
                    var replacement=GatheringDistribution.PlaceAsset(asset,"gather-authored-tree-v1:"+settings.seed+":"+old.GetSiblingIndex(),p,old.rotation,forest);
                    replacement.transform.localScale*=Mathf.Lerp(Mathf.Max(.1f,asset.scaleRange.x),Mathf.Max(.1f,asset.scaleRange.y),(float)new System.Random(seed).NextDouble());
                    old.gameObject.SetActive(false);
                }
            var authoredTrees=forest!=null?forest.Cast<Transform>().ToArray():new Transform[0];
            foreach(var existing in authoredTrees)
                if(existing.gameObject.activeSelf&&existing.GetComponent<GatheringNode>()==null)
                    GatheringDistribution.WrapTree(existing.gameObject,"gather-authored-tree-v1:"+settings.seed+":"+System.Array.IndexOf(authoredTrees,existing));
            var horizon=transform.Find("Distant landscape");if(horizon!=null)horizon.gameObject.SetActive(false);
            if(settings.preserveAuthoredCenter&&settings.content!=null)
            {
                var village=settings.content.Asset(WorldAssetKind.Village,field.Biome(-50,-70),settings.seed);
                var previousVillage=geometry.Find("AuthoredGeometry/Village");
                if(village!=null&&previousVillage!=null)
                {
                    previousVillage.gameObject.SetActive(false);
                    foreach(var tile in geometry.GetComponentsInChildren<MeshFilter>())
                    {
                        var parts=tile.name.Split('_');
                        if(parts.Length!=3||parts[0]!="Terrain"||!int.TryParse(parts[1],out int tx)||!int.TryParse(parts[2],out int tz))continue;
                        float pad=field.VillageHalfExtent+16;
                        if(tx+32 < -50-pad || tx > -50+pad || tz+32 < -70-pad || tz > -70+pad)continue;
                        var mesh=BuildTerrain(field,new Vector2Int(tx/32,tz/32));
                        var owned=tile.GetComponent<GeneratedWorldMesh>();if(owned==null)owned=tile.gameObject.AddComponent<GeneratedWorldMesh>();owned.Value=mesh;tile.sharedMesh=mesh;
                        var collider=tile.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=mesh;
                    }
                    var settlement=Object.Instantiate(village.prefab,new Vector3(-50,field.Height(-50,-70)+settings.content.villageGroundOffset*Mathf.Max(1,settings.content.villageSizeMultiplier),-70),Quaternion.identity,geometry);
                    settlement.transform.localScale*=Mathf.Max(1,settings.content.villageSizeMultiplier);settlement.name="Medieval starting village";
                    if(forest!=null)foreach(Transform tree in forest)
                        if(Mathf.Abs(tree.position.x+50)<field.VillageHalfExtent+4&&Mathf.Abs(tree.position.z+70)<field.VillageHalfExtent+4)tree.gameObject.SetActive(false);
                }
            }
            var boundary=transform.Find("Safety boundary");if(boundary!=null)boundary.gameObject.SetActive(false);
            previousNavigation=GetComponents<MonoBehaviour>().OfType<IStaticWorldNavigation>().FirstOrDefault();
            if(!settings.preserveAuthoredCenter)
            {
                previousNavigation?.StopNavigation();
                foreach(Transform child in transform)child.gameObject.SetActive(false);
            }
            navigation=new NavMeshData(0);navInstance=NavMesh.AddNavMeshData(navigation);dirty=true;
            if(WorldSession.Current!=null)
            {
                var saved=WorldSession.Current;var position=new Vector3(saved.x,saved.y,saved.z);
                var coordinate=Coordinate(position);
                // Materialize the landing neighbourhood before allowing the motor to update.
                for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
                {var id=coordinate+new Vector2Int(x,z);if(!IsAuthored(id))CreateChunk(id);}
                Physics.SyncTransforms();
                var supports=Physics.RaycastAll(position+Vector3.up*2,Vector3.down,6,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.normal.y>.5f&&h.collider.GetComponentInParent<Mismo.Gameplay.Combat.Health>()==null).OrderBy(h=>h.distance).ToArray();
                if(supports.Length>0)
                    position.y=Mathf.Max(position.y,supports[0].point.y+.1f);
                else position.y=field.Height(position.x,position.z)+.3f;
                target.GetComponent<Movement.PlayerMotor>().ResetPosition(position);
                target.rotation=Quaternion.Euler(0,saved.yaw,0);
                if(target.GetComponent<WorldCheckpoint>()==null)target.gameObject.AddComponent<WorldCheckpoint>();
            }
            var lighting=GetComponent<DayNightCycle>();if(lighting==null)lighting=gameObject.AddComponent<DayNightCycle>();lighting.Initialize(Resources.Load<DayNightSettings>("DayNightSettings"));
        }
        void Update()
        {
            if(Settings==null||player==null)return;
            if(Settings.content!=null)Settings.content.ApplyAtmosphere(Settings.loadRadius);
            if(inventory==null)inventory=player.GetComponent<Equipment.Inventory.PlayerInventory>();
            var center=Coordinate(player.position);int radius=Mathf.Clamp(Settings.loadRadius,2,5);
            if(inventory!=null&&inventory.IsReady&&field.IsExterior(player.position.x,player.position.z)&&Time.unscaledTime>=nextDiscovery)
            {inventory.TryDiscoverRegion(field.RegionId(player.position.x,player.position.z),Settings.regionalLevelIncrement);nextDiscovery=Time.unscaledTime+2;}
            Vector2Int? nearest=null;int best=int.MaxValue;
            for(int z=-radius;z<=radius;z++)for(int x=-radius;x<=radius;x++)
            {var id=center+new Vector2Int(x,z);if(IsAuthored(id)||chunks.ContainsKey(id))continue;int d=x*x+z*z;if(d<best){best=d;nearest=id;}}
            if(nearest.HasValue){CreateChunk(nearest.Value);dirty=true;}
            if(navOperation==null||navOperation.isDone)
            {
                if(navOperation!=null){navOperation=null;navigationReady=true;previousNavigation?.StopNavigation();}
                foreach(var id in chunks.Keys.Where(id=>Mathf.Abs(id.x-center.x)>radius+1||Mathf.Abs(id.y-center.y)>radius+1).ToArray())
                {
                    var chunk=chunks[id];if(chunk.GetComponentsInChildren<WorldEnemyIdentity>().Any(e=>e.PendingReward))continue;content.Unload(id);var mesh=chunk.GetComponent<MeshFilter>().sharedMesh;chunk.SetActive(false);Destroy(chunk);Destroy(mesh);chunks.Remove(id);dirty=true;
                }
                // Moving keeps adding chunks. Populate from completed navigation instead of
                // waiting for the entire streaming window to stop changing.
                if(navigationReady)
                {
                    foreach(var pair in chunks)content.Populate(pair.Key,pair.Value.transform,inventory);
                    // Direct scene Play and legacy saves retain authored terrain. They
                    // still need the new roaming encounters, without replacing that terrain.
                    for(int z=-radius;z<=radius;z++)for(int x=-radius;x<=radius;x++)
                    {
                        var id=center+new Vector2Int(x,z);if(!IsAuthored(id))continue;
                        if(!authoredEncounters.TryGetValue(id,out var holder))
                        {holder=new GameObject("Roaming encounters "+id);holder.transform.SetParent(transform,false);authoredEncounters.Add(id,holder);content.Scatter(id,holder.transform);dirty=true;}
                        content.Populate(id,holder.transform,inventory);
                    }
                    foreach(var id in authoredEncounters.Keys.Where(id=>Mathf.Abs(id.x-center.x)>radius+1||Mathf.Abs(id.y-center.y)>radius+1).ToArray())
                    {
                        var holder=authoredEncounters[id];if(holder.GetComponentsInChildren<WorldEnemyIdentity>().Any(e=>e.PendingReward))continue;
                        holder.SetActive(false);Destroy(holder);authoredEncounters.Remove(id);content.Unload(id);dirty=true;
                    }
                }
                if(dirty&&Time.time>=nextNav)
                {
                    var sources=new List<NavMeshBuildSource>();
                    if(Settings.preserveAuthoredCenter)NavMeshBuilder.CollectSources(geometry,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
                    foreach(var chunk in chunks.Values.Concat(authoredEncounters.Values))
                    {var extra=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(chunk.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,chunk.GetComponentsInChildren<WorldEnemyIdentity>().Select(e=>new NavMeshBuildMarkup{root=e.transform,ignoreFromBuild=true}).ToList(),extra);sources.AddRange(extra);}
                    var bounds=new Bounds(new Vector3(player.position.x,30,player.position.z),new Vector3((radius+2)*64,140,(radius+2)*64));
                    if(Settings.preserveAuthoredCenter)bounds.Encapsulate(new Bounds(new Vector3(0,30,0),new Vector3(Settings.authoredSize,140,Settings.authoredSize)));
                    navOperation=NavMeshBuilder.UpdateNavMeshDataAsync(navigation,NavMesh.GetSettingsByID(0),sources,bounds);dirty=false;nextNav=Time.time+1;
                }
            }
        }
        public GameObject CreateChunk(Vector2Int id)
        {
            if(chunks.TryGetValue(id,out var existing))return existing;
            var root=new GameObject("Chunk "+id.x+", "+id.y);root.transform.SetParent(transform,false);
            var mesh=BuildTerrain(field,id);root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=material;root.AddComponent<MeshCollider>().sharedMesh=mesh;
            var random=new System.Random(unchecked(Settings.seed^id.x*73856093^id.y*19349663));
            if(trees.Length>0||Settings.content!=null)for(int z=4;z<32;z+=8)for(int x=4;x<32;x+=8)
            {
                float px=id.x*32+x+(float)random.NextDouble()*2,pz=id.y*32+z+(float)random.NextDouble()*2;
                if(random.NextDouble()>Settings.treeDensity*field.TreeDensity(px,pz)||field.Reserved(px,pz,2))continue;
                float y=field.Height(Mathf.Floor(px)+.5f,Mathf.Floor(pz)+.5f);
                if(Mathf.Abs(y-field.Height(px+2,pz+2))>1)continue;
                var asset=Settings.content!=null?Settings.content.Asset(WorldAssetKind.Tree,field.Biome(px,pz),ExplorationTerrain.Hash(Settings.seed,(int)px,(int)pz,300)):null;
                if(asset!=null){var instance=GatheringDistribution.PlaceAsset(asset,"gather-tree-v1:"+Settings.seed+":"+id.x+":"+id.y+":"+x+":"+z,new Vector3(px,y,pz),Quaternion.Euler(0,random.Next(4)*90,0),root.transform);instance.transform.localScale*=Mathf.Lerp(Mathf.Max(.1f,asset.scaleRange.x),Mathf.Max(.1f,asset.scaleRange.y),(float)random.NextDouble());continue;}
                if(trees.Length==0)continue;
                var tree=new GameObject("Oak");tree.transform.SetParent(root.transform,false);tree.transform.position=new Vector3(px,y,pz);
                tree.transform.localScale=Vector3.one*Mathf.Lerp(Settings.treeScale.x,Settings.treeScale.y,(float)random.NextDouble());
                tree.AddComponent<MeshFilter>().sharedMesh=trees[random.Next(trees.Length)];tree.AddComponent<MeshRenderer>().sharedMaterial=material;
                var trunk=tree.AddComponent<BoxCollider>();trunk.center=new Vector3(0,3,0);trunk.size=new Vector3(.9f,6,.9f);tree.AddComponent<ClimbableTree>();
                GatheringDistribution.WrapTree(tree,"gather-tree-v1:"+Settings.seed+":"+id.x+":"+id.y+":"+x+":"+z);
            }
            content.Decorate(id,root.transform,material);GatheringDistribution.Decorate(Settings,field,id,root.transform);chunks.Add(id,root);return root;
        }
        public static Mesh BuildTerrain(VoxelRegionHeightfield field,Vector2Int id)
        {
            var mesh=new VoxelRegionGeometry();int sx=id.x*32,sz=id.y*32;
            var heights=new float[34,34];
            for(int z=-1;z<=32;z++)for(int x=-1;x<=32;x++)heights[x+1,z+1]=field.Height(sx+x+.5f,sz+z+.5f);
            for(int z=sz;z<sz+32;z++)for(int x=sx;x<sx+32;x++)
            {
                float y=heights[x-sx+1,z-sz+1];Color side=new Color(.32f,.43f,.17f);
                mesh.Quad(new Vector3(x,y,z),new Vector3(x,y,z+1),new Vector3(x+1,y,z+1),new Vector3(x+1,y,z),field.Top(x,z,y));
                float low=heights[x-sx+2,z-sz+1];if(y>low)mesh.Quad(new Vector3(x+1,low,z),new Vector3(x+1,y,z),new Vector3(x+1,y,z+1),new Vector3(x+1,low,z+1),side);
                low=heights[x-sx,z-sz+1];if(y>low)mesh.Quad(new Vector3(x,low,z+1),new Vector3(x,y,z+1),new Vector3(x,y,z),new Vector3(x,low,z),side);
                low=heights[x-sx+1,z-sz+2];if(y>low)mesh.Quad(new Vector3(x+1,low,z+1),new Vector3(x+1,y,z+1),new Vector3(x,y,z+1),new Vector3(x,low,z+1),side);
                low=heights[x-sx+1,z-sz];if(y>low)mesh.Quad(new Vector3(x,low,z),new Vector3(x,y,z),new Vector3(x+1,y,z),new Vector3(x+1,low,z),side);
            }
            return mesh.Mesh("Exploration terrain "+id);
        }
        void OnDestroy()
        {
            if(WorldSession.Current!=null&&Settings!=null)Destroy(Settings);
            if(navigation!=null)NavMeshBuilder.Cancel(navigation);
            if(navInstance.valid)navInstance.Remove();if(navigation!=null)Destroy(navigation);
            foreach(var chunk in chunks.Values)if(chunk!=null)Destroy(chunk.GetComponent<MeshFilter>().sharedMesh);
        }
    }
}
