using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Player.World
{
    public sealed class ExplorationContent
    {
        readonly ExplorationWorldSettings settings;
        readonly ExplorationTerrain terrain;
        readonly HashSet<string> defeated=new HashSet<string>();
        readonly HashSet<Vector2Int> populated=new HashSet<Vector2Int>();
        public ExplorationContent(ExplorationWorldSettings s,ExplorationTerrain t){settings=s;terrain=t;}
        public void Unload(Vector2Int chunk)=>populated.Remove(chunk);
        public IEnumerable<WorldSite> Encounters(Vector2Int chunk)
        {
            foreach(var site in Sites(chunk))yield return site;
            if(settings.content==null)yield break;
            int spacing=Mathf.Max(32,settings.content.roamingSpacing);
            int cx=Mathf.FloorToInt(chunk.x*32f/spacing),cz=Mathf.FloorToInt(chunk.y*32f/spacing);
            var cell=new Vector2Int(cx,cz);
            int hash=ExplorationTerrain.Hash(settings.seed,cx,cz,410);
            var rng=new System.Random(hash);
            if(rng.NextDouble()>settings.content.roamingChance)yield break;
            float x=(cx+.3f+(float)rng.NextDouble()*.4f)*spacing,z=(cz+.3f+(float)rng.NextDouble()*.4f)*spacing;
            var p=new Vector3(x,terrain.Height(x,z),z);
            if(ExplorationChunks.Coordinate(p)!=chunk||terrain.Reserved(x,z,6))yield break;
            int siteSpacing=terrain.SiteSpacing;
            var siteCell=new Vector2Int(Mathf.FloorToInt(x/siteSpacing),Mathf.FloorToInt(z/siteSpacing));
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                var nearby=terrain.Site(siteCell+new Vector2Int(dx,dz));
                float distance=Vector2.Distance(new Vector2(x,z),new Vector2(nearby.position.x,nearby.position.z));
                if(distance<nearby.radius+(nearby.kind==WorldSiteKind.Village?30:12))yield break;
            }
            yield return new WorldSite{cell=cell,position=p,kind=WorldSiteKind.Clearing,radius=6,roaming=true};
        }
        public IEnumerable<WorldSite> Sites(Vector2Int chunk)
        {
            int spacing=terrain.SiteSpacing;
            int x=Mathf.FloorToInt(chunk.x*32f/spacing),z=Mathf.FloorToInt(chunk.y*32f/spacing);
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                var site=terrain.Site(new Vector2Int(x+dx,z+dz));
                if(ExplorationChunks.Coordinate(site.position)==chunk&&terrain.IsExterior(site.position.x,site.position.z))yield return site;
            }
        }
        public void Decorate(Vector2Int chunk,Transform root,Material material)
        {
            Scatter(chunk,root);
            settings.content?.groveStyle?.DecorateLedges(terrain,chunk,root,settings.seed);
            foreach(var site in Sites(chunk))
            {
                if(site.structure!=null)
                {
                    var p=site.position;p.y=terrain.Height(p.x,p.z)+.05f;
                    var structure=Object.Instantiate(site.structure.prefab,p,Quaternion.identity,root);
                    structure.name=site.structure.id+" "+site.cell;
                    // Reuse the site's persistent reward identity, including previously consumed springs.
                    structure.ConfigureWorld("world-v1:"+settings.seed+":"+site.cell.x+":"+site.cell.y+":secret");
                    continue;
                }
                if(site.kind==WorldSiteKind.Village)
                {
                    var village=settings.content!=null?settings.content.Asset(WorldAssetKind.Village,terrain.Biome(site.position.x,site.position.z),ExplorationTerrain.Hash(settings.seed,site.cell.x,site.cell.y,710)):null;
                    if(village!=null)
                    {
                        var p=site.position;p.y=terrain.Height(p.x,p.z)+settings.content.villageGroundOffset*Mathf.Max(1,settings.content.villageSizeMultiplier);
                        var settlement=Object.Instantiate(village.prefab,p,Quaternion.identity,root);
                        settlement.transform.localScale*=Mathf.Max(1,settings.content.villageSizeMultiplier);
                        settlement.name="Village "+site.cell.x+", "+site.cell.y;
                        Quests.VillageNpcSpawner.Populate(settlement,terrain,site.cell==Vector2Int.zero&&!settings.preserveAuthoredCenter);
                        continue;
                    }
                    for(int i=0;i<4;i++)Place(root,site,WorldAssetKind.House,new Vector3(i%2==0?-15:15,0,i<2?-14:14),i,material);
                    Place(root,site,WorldAssetKind.Blacksmith,new Vector3(0,0,19),9,material);
                }
                else if(site.kind!=WorldSiteKind.Clearing)
                    Place(root,site,WorldAssetKind.Ruin,new Vector3(0,0,site.radius-3),0,material);
                if(site.kind==WorldSiteKind.Secret)
                {
                    var grove=settings.content!=null?settings.content.groveStyle:null;
                    if(grove!=null&&grove.fireflies!=null&&GroveWorldStyle.Supports(terrain.Biome(site.position.x,site.position.z)))
                        Object.Instantiate(grove.fireflies,new Vector3(site.position.x,terrain.Height(site.position.x,site.position.z)+.8f,site.position.z),Quaternion.identity,root);
                    var water=new GameObject("Spring");water.transform.SetParent(root,false);water.transform.position=site.position+Vector3.down*.25f;
                    var surface=water.AddComponent<WorldWaterSurface>();var waterGeometry=new VoxelRegionGeometry();
                    waterGeometry.Quad(new Vector3(-6,0,-6),new Vector3(-6,0,6),new Vector3(6,0,6),new Vector3(6,0,-6),surface.color);
                    var waterMesh=water.AddComponent<GeneratedWorldMesh>();waterMesh.Value=waterGeometry.Mesh("Spring water");
                    water.AddComponent<MeshFilter>().sharedMesh=waterMesh.Value;water.AddComponent<MeshRenderer>().sharedMaterial=grove!=null&&grove.waterMaterial!=null?grove.waterMaterial:material;
                    var pickup=GameObject.CreatePrimitive(PrimitiveType.Cube);pickup.name="Hidden healing crystal";pickup.transform.SetParent(root,false);
                    pickup.transform.position=new Vector3(site.position.x,terrain.Height(site.position.x,site.position.z)+1,site.position.z);
                    pickup.transform.localScale=Vector3.one*.7f;pickup.GetComponent<Collider>().isTrigger=true;
                    var mesh=new VoxelRegionGeometry();mesh.Box(Vector3.zero,Vector3.one,new Color(.35f,.95f,.85f));
                    var owned=pickup.AddComponent<GeneratedWorldMesh>();owned.Value=mesh.Mesh("Healing crystal");pickup.GetComponent<MeshFilter>().sharedMesh=owned.Value;
                    pickup.GetComponent<Renderer>().sharedMaterial=material;pickup.AddComponent<SecretRewardPickup>().ConfigureWorld("world-v1:"+settings.seed+":"+site.cell.x+":"+site.cell.y+":secret");
                }
            }
        }
        public void Scatter(Vector2Int chunk,Transform root)
        {
            var catalog=settings.content;if(catalog==null)return;
            var kinds=new[]{WorldAssetKind.Grass,WorldAssetKind.Bush,WorldAssetKind.Flower,WorldAssetKind.Rock,WorldAssetKind.Deadwood};
            for(int k=0;k<kinds.Length;k++)
            {
                int spacing=k==0?4:8;
                for(int z=spacing/2;z<32;z+=spacing)for(int x=spacing/2;x<32;x+=spacing)
                {
                    int seed=ExplorationTerrain.Hash(settings.seed,chunk.x*32+x,chunk.y*32+z,600+k);
                    var rng=new System.Random(seed);double densityRoll=rng.NextDouble();
                    float px=chunk.x*32+x+((float)rng.NextDouble()-.5f)*2,pz=chunk.y*32+z+((float)rng.NextDouble()-.5f)*2;
                    if(terrain.Reserved(px,pz,k==0?0:2))continue;
                    var biome=terrain.Biome(px,pz);
                    float patches=catalog.groveStyle!=null&&GroveWorldStyle.Supports(biome)?catalog.groveStyle.PatchDensity(kinds[k],px,pz):1;
                    if(densityRoll>=Mathf.Clamp01(catalog.Density(kinds[k],biome)*patches))continue;
                    var asset=catalog.Asset(kinds[k],biome,ExplorationTerrain.Hash(seed,x,z,650));if(asset==null)continue;
                    // Generic meadow flowers/grass should not carpet the new desert or glacier.
                    if(catalog.BiomeContent(biome)==null&&terrain.Plan!=null&&terrain.Plan.Stage(px,pz)>0&&kinds[k]!=WorldAssetKind.Rock&&(asset.biomes==null||asset.biomes.Length==0))continue;
                    float yaw=Rotation(asset,rng);
                    float scale=Mathf.Lerp(Mathf.Max(.1f,asset.scaleRange.x),Mathf.Max(.1f,asset.scaleRange.y),(float)rng.NextDouble());
                    if(!TryPlaceNature(terrain,asset,new Vector2(px,pz),scale,yaw,out float y))continue;
                    if(kinds[k]==WorldAssetKind.Deadwood&&!TrySupport(terrain,new Vector2(px,pz),asset.footprint*scale,yaw,out y))continue;
                    var go=GatheringDistribution.PlaceAsset(asset,"gather-detail-v1:"+settings.seed+":"+chunk.x+":"+chunk.y+":"+k+":"+x+":"+z,new Vector3(px,y+.01f,pz),Quaternion.Euler(0,yaw,0),root);
                    go.transform.localScale*=scale;
                }
            }
        }
        public static float Rotation(WorldAssetEntry asset,System.Random random) =>
            asset.rotations!=null&&asset.rotations.Length>0?asset.rotations[random.Next(asset.rotations.Length)]:(float)random.NextDouble()*360;

        public static bool TryPlaceNature(ExplorationTerrain terrain,WorldAssetEntry asset,Vector2 position,float scale,float yaw,out float height)
        {
            height=terrain.Height(position.x,position.y);
            var half=asset.footprint*scale*.5f;
            if(terrain.Reserved(position.x,position.y,Mathf.Max(half.x,half.y)))return false;
            float angle=yaw*Mathf.Deg2Rad,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            float low=height,high=height;
            for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
            {
                float px=position.x+c*x*half.x-s*z*half.y,pz=position.y+s*x*half.x+c*z*half.y;
                if(terrain.Plan!=null&&!terrain.Plan.Contains(px,pz))return false;
                float y=terrain.Height(px,pz);low=Mathf.Min(low,y);high=Mathf.Max(high,y);
            }
            float radius=Mathf.Max(.25f,Mathf.Min(half.x,half.y));
            if(Mathf.Atan2(high-low,radius*2)*Mathf.Rad2Deg>asset.maxSlope)return false;
            // Base-centered models rest at the lowest supporting sample instead of floating above terraces.
            height=low;
            return true;
        }
        public static bool TrySupport(ExplorationTerrain terrain,Vector2 position,Vector2 footprint,float yaw,out float height)
        {
            // Check every voxel column beneath the rotated footprint. A fallen log
            // must sit on one terrace, not bridge a step or intersect its upper side.
            float angle=yaw*Mathf.Deg2Rad,c=Mathf.Abs(Mathf.Cos(angle)),s=Mathf.Abs(Mathf.Sin(angle));
            var half=new Vector2(c*footprint.x+s*footprint.y,s*footprint.x+c*footprint.y)*.5f;
            float low=float.MaxValue; height=float.MinValue;
            for(int z=Mathf.FloorToInt(position.y-half.y);z<=Mathf.FloorToInt(position.y+half.y);z++)
            for(int x=Mathf.FloorToInt(position.x-half.x);x<=Mathf.FloorToInt(position.x+half.x);x++)
            {float y=terrain.Height(x+.5f,z+.5f);low=Mathf.Min(low,y);height=Mathf.Max(height,y);}
            return height-low<.05f;
        }
        void Place(Transform root,WorldSite site,WorldAssetKind kind,Vector3 offset,int slot,Material material)
        {
            var p=site.position+offset;p.y=terrain.Height(p.x,p.z)+.08f;
            if(terrain.PathDistance(p.x,p.z)<8)return;
            int seed=ExplorationTerrain.Hash(settings.seed,site.cell.x,site.cell.y,100+slot);
            var asset=settings.content!=null?settings.content.Asset(kind,terrain.Biome(p.x,p.z),seed):null;
            if(asset!=null)
            {
                if(terrain.PathDistance(p.x,p.z)<8+Mathf.Max(asset.footprint.x,asset.footprint.y)*.5f)return;
                if(asset.footprint.x>site.radius||asset.footprint.y>site.radius)return;
                float spread=0;
                foreach(float dx in new[]{-asset.footprint.x*.5f,asset.footprint.x*.5f})foreach(float dz in new[]{-asset.footprint.y*.5f,asset.footprint.y*.5f})
                    spread=Mathf.Max(spread,Mathf.Abs(terrain.Height(p.x+dx,p.z+dz)-p.y));
                if(Mathf.Atan2(spread,Mathf.Max(.5f,Mathf.Min(asset.footprint.x,asset.footprint.y)*.5f))*Mathf.Rad2Deg>asset.maxSlope)return;
                float yaw=asset.rotations!=null&&asset.rotations.Length>0?asset.rotations[new System.Random(seed).Next(asset.rotations.Length)]:0;
                Object.Instantiate(asset.prefab,p,Quaternion.Euler(0,yaw,0),root);return;
            }
            // Ruins stay empty until an authored camp/ruin prefab is assigned.
            // Do not substitute the old grey block arch when the catalog has none.
            if(kind==WorldAssetKind.Ruin)return;
            // Visible provisional composition remains replaceable through the catalog.
            var g=new VoxelRegionGeometry();bool house=kind==WorldAssetKind.House||kind==WorldAssetKind.Blacksmith;
            if(house){g.Box(new Vector3(0,2,0),new Vector3(7,4,6),new Color(.58f,.43f,.26f));g.Box(new Vector3(0,4.4f,0),new Vector3(8,1,7),new Color(.32f,.26f,.23f));}
            else {g.Box(new Vector3(-3,3,0),new Vector3(2,6,2),Color.gray);g.Box(new Vector3(3,4,0),new Vector3(2,8,2),Color.gray);g.Box(new Vector3(0,7,0),new Vector3(8,1,2),Color.gray);}
            var go=new GameObject(kind+" (provisional)");go.transform.SetParent(root,false);go.transform.position=p;
            var owner=go.AddComponent<GeneratedWorldMesh>();owner.Value=g.Mesh(kind.ToString());go.AddComponent<MeshFilter>().sharedMesh=owner.Value;
            go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<MeshCollider>().sharedMesh=owner.Value;
        }
        public void Populate(Vector2Int chunk,Transform root,PlayerInventory inventory)
        {
            if(settings.content==null||inventory==null||!inventory.IsReady||populated.Contains(chunk))return;
            foreach(var structure in root.GetComponentsInChildren<Structures.StructureInstance>())
                structure.ActivateContent(inventory,settings,terrain.RegionId(structure.transform.position.x,structure.transform.position.z));
            foreach(var site in Encounters(chunk))
            {
                string region=terrain.RegionId(site.position.x,site.position.z);
                // Preloading is not discovery; wait until the player actually enters this region.
                if(inventory.RegionMinimum(region)==0&&terrain.IsExterior(site.position.x,site.position.z))return;
                int seed=ExplorationTerrain.Hash(settings.seed,site.cell.x,site.cell.y,200);
                if(site.kind==WorldSiteKind.Village||site.kind==WorldSiteKind.Secret)continue;
                var rng=new System.Random(seed);
                if(!site.roaming&&site.kind==WorldSiteKind.Clearing&&rng.NextDouble()>settings.content.clearingChance)continue;
                // Separate rolls: using the density seed here restricted low-density
                // clearings to the first catalog entries, excluding forest creatures.
                int selectionSeed=ExplorationTerrain.Hash(settings.seed,site.cell.x,site.cell.y,site.roaming?411:201);
                var entry=settings.content.Encounter(site.kind,terrain.Biome(site.position.x,site.position.z),Mathf.Max(1,inventory.RegionMinimum(region)),site.position.y,selectionSeed);
                if(entry==null)continue;
                if(Vector3.Distance(inventory.transform.position,site.position)<entry.minimumPlayerDistance)return;
                int count=site.kind==WorldSiteKind.BossArena?1:rng.Next(Mathf.Clamp(entry.minimumCount,1,5),Mathf.Clamp(Mathf.Max(entry.minimumCount,entry.maximumCount),1,5)+1);
                var positions=new Vector3[count];
                for(int i=0;i<count;i++)
                {
                    float angle=i*Mathf.PI*2/count;Vector3 p=site.position+new Vector3(Mathf.Cos(angle)*4,0,Mathf.Sin(angle)*4);
                    p.y=terrain.Height(p.x,p.z)+.08f;
                    if(!NavMesh.SamplePosition(p,out var hit,1.5f,NavMesh.AllAreas))return;
                    positions[i]=hit.position;
                }
                for(int i=0;i<count;i++)
                {
                    // Slot identity excludes prefab and catalog contents; an art replacement cannot resurrect a kill.
                    string id=(site.roaming?"world-roaming-v1:":"world-v1:")+settings.seed+":"+site.cell.x+":"+site.cell.y+":"+i;
                    bool elite=entry.elitePrefab!=null&&(entry.guaranteedElite&&i==0||rng.NextDouble()<entry.eliteChance);
                    if(defeated.Contains(id)||inventory.IsWorldEnemyDefeated(id)||root.Find("Encounter "+id)!=null)continue;
                    var holder=new GameObject("Encounter "+id);holder.transform.SetParent(root,false);holder.SetActive(false);
                    var actor=Object.Instantiate(elite?entry.elitePrefab:entry.prefab,positions[i],Quaternion.identity,holder.transform);
                    var identity=actor.AddComponent<WorldEnemyIdentity>();identity.Configure(id,region,entry.id+(elite?" ÉLITE":""),inventory,settings);
                    var health=actor.GetComponent<Mismo.Gameplay.Combat.Health>();
                    if(health!=null)health.Died+=_=>defeated.Add(id);
                    holder.SetActive(true);
                }
            }
            populated.Add(chunk);
        }
    }
}
