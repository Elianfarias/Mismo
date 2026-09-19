using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    public static class GatheringDistribution
    {
        public static void WrapTree(GameObject tree,string id)
        {
            var settings=Mismo.Core.ProjectAssets.Load<GatheringSettings>("GatheringSettings");if(settings==null||settings.tree==null)return;
            var node=Place(settings.tree,id,tree.transform.position,tree.transform.parent);
            node.transform.rotation=tree.transform.rotation;
            tree.transform.SetParent(node.transform,true);tree.transform.localPosition=Vector3.zero;tree.transform.localRotation=Quaternion.identity;
            tree.SetActive(false);node.UseWorldPrefab(tree);
        }
        public static GameObject PlaceAsset(WorldAssetEntry asset,string id,Vector3 point,Quaternion rotation,Transform parent)
        {
            if(asset.decorativeOnly)return Object.Instantiate(asset.prefab,point,rotation,parent);
            var settings=Mismo.Core.ProjectAssets.Load<GatheringSettings>("GatheringSettings");
            var definition=asset.gatheringNode;
            if(definition==null&&settings!=null)
            {
                if(asset.kind==WorldAssetKind.Tree||asset.kind==WorldAssetKind.Deadwood)definition=settings.tree;
                else if(asset.kind==WorldAssetKind.Rock)definition=settings.stone;
                else if(asset.kind==WorldAssetKind.Flower||asset.kind==WorldAssetKind.Bush)definition=settings.herb;
            }
            if(definition==null)return Object.Instantiate(asset.prefab,point,rotation,parent);
            var node=Place(definition,id,point,parent);node.UseWorldPrefab(asset.prefab);node.transform.rotation=rotation;
            return node.gameObject;
        }
        public static GatheringNode Place(ResourceNodeDefinition definition,string id,Vector3 point,Transform parent)
        {
            if(definition==null)return null;
            var go=new GameObject(definition.displayName);go.transform.SetParent(parent,true);go.transform.position=point;
            var node=go.AddComponent<GatheringNode>();node.Configure(definition,id);return node;
        }
        public static bool Clear(Vector3 point)
        {
            foreach(var obstacle in Physics.OverlapCapsule(point+Vector3.up*.65f,point+Vector3.up*1.7f,.55f,~0,QueryTriggerInteraction.Ignore))
                if(obstacle.GetComponentInParent<Mismo.Gameplay.Combat.Health>()==null&&obstacle.GetComponentInParent<GatheringNode>()==null)return false;
            return true;
        }
        public static void Decorate(ExplorationWorldSettings world,ExplorationTerrain terrain,Vector2Int chunk,Transform root)
        {
            var settings=Mismo.Core.ProjectAssets.Load<GatheringSettings>("GatheringSettings");if(settings==null)return;
            var rng=new System.Random(ExplorationTerrain.Hash(world.seed,chunk.x,chunk.y,910+settings.generationVersion));
            var placed=new System.Collections.Generic.List<Vector3>();
            Physics.SyncTransforms();
            for(int slot=0;slot<settings.groupsPerChunk;slot++)
            {
                if(rng.NextDouble()>settings.groupChance)continue;
                float x=chunk.x*32+4+(float)rng.NextDouble()*24,z=chunk.y*32+4+(float)rng.NextDouble()*24;
                if(terrain.Reserved(x,z,4)||!ExplorationContent.TrySupport(terrain,new Vector2(x,z),Vector2.one*2.2f,0,out float y))continue;
                var point=new Vector3(x,y,z);if(!Clear(point)||placed.Exists(p=>Vector3.Distance(p,point)<settings.minimumSeparation))continue;
                var biome=terrain.Biome(x,z);
                var definition=biome==WorldBiome.Forest?settings.tree:biome==WorldBiome.Highlands?settings.stone:settings.herb;
                // Deterministic minority resources keep each biome useful without uniform coverage.
                if(rng.NextDouble()<.25)definition=slot%2==0?settings.stone:settings.herb;
                if(definition==settings.stone)definition=settings.Mineral(ExplorationTerrain.Hash(world.seed,chunk.x,chunk.y,930+slot));
                Place(definition,"gather-v"+settings.generationVersion+":"+world.seed+":"+chunk.x+":"+chunk.y+":"+slot,point,root);placed.Add(point);
            }
        }
    }
    public sealed class GatheringArrival:MonoBehaviour
    {
        Vector3 arrival;GatheringSettings settings;
        GameObject root;bool stationPlaced;readonly bool[] resources=new bool[3];float retryAt;
        void Start()
        {
            settings=Mismo.Core.ProjectAssets.Load<GatheringSettings>("GatheringSettings");var world=WorldSession.Current;
            arrival=world!=null?new Vector3(world.spawnX,world.spawnY,world.spawnZ):transform.position;
            root=new GameObject("Arrival gathering and workbench");
        }
        void Update()
        {
            if(settings==null||Time.unscaledTime<retryAt||Vector3.Distance(transform.position,arrival)>100)return;retryAt=Time.unscaledTime+2;
            Physics.SyncTransforms();
            if(!stationPlaced&&TryPosition(3,out var bench))
            {
                var go=settings.workbenchPrefab!=null?Instantiate(settings.workbenchPrefab,bench,Quaternion.identity,root.transform):new GameObject("Workbench");
                go.transform.position=bench;go.transform.SetParent(root.transform,true);
                var station=go.GetComponent<CraftingStation>()??go.AddComponent<CraftingStation>();station.recipes=settings.recipes;stationPlaced=true;
            }
            var types=new[]{settings.herb,settings.tree,settings.stone};
            for(int i=0;i<3;i++)if(!resources[i]&&TryPosition(settings.starterRadius+i*4,out var point))
            {
                resources[i]=GatheringDistribution.Place(types[i],"gather-starter-v"+settings.generationVersion+":"+i,point,root.transform)!=null;
            }
        }
        bool TryPosition(float radius,out Vector3 point)
        {
            var streaming=FindFirstObjectByType<ExplorationChunks>();
            var terrain=streaming!=null&&streaming.Settings!=null?new ExplorationTerrain(streaming.Settings):null;
            for(int attempt=0;attempt<64;attempt++)
            {
                float angle=attempt*137.5f*Mathf.Deg2Rad;
                var candidate=arrival+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(radius+attempt/16*3);
                if(!Physics.Raycast(candidate+Vector3.up*25,Vector3.down,out var hit,60,~0,QueryTriggerInteraction.Ignore)||hit.normal.y<.95f)continue;
                if(terrain!=null&&(terrain.PathDistance(candidate.x,candidate.z)<3||Mathf.Abs(hit.point.y-terrain.Height(candidate.x,candidate.z))>.3f||!ExplorationContent.TrySupport(terrain,new Vector2(candidate.x,candidate.z),Vector2.one*2.2f,0,out _)))continue;
                if(!GatheringDistribution.Clear(hit.point))continue;
                bool close=false;foreach(var node in GatheringNode.Loaded)if(node!=null&&Vector3.Distance(node.transform.position,hit.point)<settings.minimumSeparation){close=true;break;}
                if(close)continue;point=hit.point;return true;
            }
            point=default;return false;
        }
        void OnDestroy(){if(root!=null)Destroy(root);}
    }
}
