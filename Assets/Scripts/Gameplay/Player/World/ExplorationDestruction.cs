using System.Linq;
using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    public sealed partial class ExplorationChunks
    {
        public float SurfaceHeight(Vector3 p)=>field.Height(Mathf.Floor(p.x)+.5f,Mathf.Floor(p.z)+.5f);
        public bool CanDragonImpact(Vector3 center,float radius)
        {
            var arc=field?.DragonArc;
            if(arc==null || WorldSession.Current?.seed!=Settings.seed || !new WorldImpactRecord(center,radius,.2f).Valid)return false;
            bool Near(Vector3 p,float clearance)=>Vector2.Distance(new Vector2(center.x,center.z),new Vector2(p.x,p.z))<radius+clearance;
            if(Near(arc.Altar,7) || Near(arc.King,7) || Near(arc.Lookout,7))return false;
            foreach(var beacon in arc.Beacons)if(Near(beacon,10))return false;
            if(field.Introduction?.Reserved(center.x,center.z,radius+3)==true)return false;
            int x=Mathf.FloorToInt(center.x/field.SiteSpacing),z=Mathf.FloorToInt(center.z/field.SiteSpacing);
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                var site=field.Site(new Vector2Int(x+dx,z+dz));
                if((site.kind==WorldSiteKind.Village || site.structure!=null) && Near(site.position,site.radius*1.42f+4))return false;
            }
            return true;
        }
        public bool TryDragonImpact(Vector3 center,float radius,float depth)
        {
            if(!CanDragonImpact(center,radius) || !new WorldImpactRecord(center,radius,depth).Valid)return false;
            var impact=new WorldImpactRecord(center,radius,depth);
            if(!WorldSession.SaveDragonImpact(impact))return false;
            field.SetImpacts(WorldSession.Current.dragonImpacts);
            // Include neighbour samples at chunk boundaries to rebuild both sides of each seam.
            foreach(var pair in chunks)
            {
                var id=pair.Key;
                if(center.x+radius<id.x*32-1 || center.x-radius>id.x*32+33 || center.z+radius<id.y*32-1 || center.z-radius>id.y*32+33)continue;
                var filter=pair.Value.GetComponent<MeshFilter>();var old=filter.sharedMesh;
                var mesh=BuildTerrain(field,id);filter.sharedMesh=mesh;
                var collider=pair.Value.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;
                Destroy(old);
            }
            foreach(var prop in WorldDestructible.Loaded.ToArray())
                if(prop!=null && impact.Contains(prop.transform.position))prop.Break(center,true);
            Physics.SyncTransforms();RefreshResourceNavigation();return true;
        }
        public bool CanDragonDestroyProp(WorldDestructible prop)=>prop!=null&&WorldDestructible.Allowed(prop.Kind)&&prop.gameObject.activeInHierarchy&&CanDragonImpact(prop.transform.position,2);
        public bool CanDragonFellTree(WorldDestructible tree)=>tree!=null&&tree.IsTree&&CanDragonDestroyProp(tree);
        public bool TryDragonFellTrees(System.Collections.Generic.IReadOnlyList<WorldDestructible> trees,Vector3 origin)
        {
            if(trees==null)return false;
            foreach(var tree in trees)if(!CanDragonFellTree(tree))return false;
            return TryDragonDestroyProps(trees,origin);
        }
        public bool TryDragonDestroyProps(System.Collections.Generic.IReadOnlyList<WorldDestructible> props,Vector3 origin)
        {
            if(props==null||props.Count==0)return false;
            foreach(var prop in props)if(!CanDragonDestroyProp(prop))return false;
            if(!WorldSession.SaveDragonDestroyedProps(props))return false;
            foreach(var prop in props)prop.Break(origin,true);
            // Contact never changes the heightfield or rebuilds terrain meshes.
            Physics.SyncTransforms();RefreshResourceNavigation();return true;
        }
        void DecorateDragonArena(Vector2Int chunk,Transform root)
        {
            var arc=field.DragonArc;if(arc==null)return;
            // Sparse deterministic cover gives the dive targets without obstructing the ritual or centre.
            for(int i=0;i<18;i++)
            {
                float angle=(i*137.5f+23)*Mathf.Deg2Rad;
                float r=Mathf.Lerp(16,arc.Definition.arenaRadius-12,(i%3)/2f);
                var p=arc.Arena+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*r;
                if(Coordinate(p)!=chunk || Vector3.Distance(p,arc.Altar)<12)continue;
                p.y=field.UndamagedHeight(p.x,p.z);
                bool tree=i%3==0;
                var asset=Settings.content.Asset(tree?WorldAssetKind.Tree:WorldAssetKind.Rock,field.Biome(p.x,p.z),ExplorationTerrain.Hash(Settings.seed,i,0,977));
                if(asset==null)continue;
                var placed=GatheringDistribution.PlaceAsset(asset,"dragon-arena:"+Settings.seed+":"+i,p,Quaternion.Euler(0,i*73,0),root);
                placed.transform.localScale*=tree?.75f:.85f;
            }
        }
    }
}
