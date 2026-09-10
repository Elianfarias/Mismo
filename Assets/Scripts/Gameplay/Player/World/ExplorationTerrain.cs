using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    public struct WorldSite
    {
        public Vector2Int cell;
        public Vector3 position;
        public WorldSiteKind kind;
        public float radius;
        public bool roaming;
    }
    // Pure global queries: loading order and presentation catalogs never affect geography.
    public sealed class ExplorationTerrain : VoxelRegionHeightfield
    {
        readonly ExplorationWorldSettings settings;
        readonly System.Collections.Generic.Dictionary<Vector2Int,WorldSite> sites=new System.Collections.Generic.Dictionary<Vector2Int,WorldSite>();
        public ExplorationTerrain(ExplorationWorldSettings s):base(s.seed,s.authoredSize,s.relief,s.stepHeight){settings=s;}
        public static int Hash(int seed,int x,int z,int stream)
        {unchecked{uint h=(uint)seed^(uint)x*73856093u^(uint)z*19349663u^(uint)stream*83492791u;h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;return (int)(h^(h>>16));}}
        static float Unit(int hash)=>(uint)hash/(float)uint.MaxValue;
        float Noise(float x,float z,float scale,int stream)=>Mathf.PerlinNoise(x/Mathf.Max(1,scale)+(settings.seed&4095)*.173f+stream*17.7f,z/Mathf.Max(1,scale)+stream*31.3f);
        public Vector2Int Region(float x,float z)=>new Vector2Int(Mathf.FloorToInt(x/Mathf.Max(128,settings.regionSize)),Mathf.FloorToInt(z/Mathf.Max(128,settings.regionSize)));
        public string RegionId(float x,float z){var r=Region(x,z);return "world-v1:"+settings.seed+":"+r.x+":"+r.y;}
        public WorldBiome Biome(float x,float z){float n=Noise(x,z,Mathf.Max(128,settings.regionSize),5);return n<.43f?WorldBiome.Meadow:n<.64f?WorldBiome.Forest:WorldBiome.Highlands;}
        TerrainBiome Profile(WorldBiome b)
        {if(settings.biomes!=null)foreach(var p in settings.biomes)if(p!=null&&p.kind==b)return p;return new TerrainBiome{kind=b};}
        public float TreeDensity(float x,float z)=>Profile(Biome(x,z)).trees;
        float Natural(float x,float z)
        {
            // Blend regional properties continuously to avoid biome boundary cliffs.
            float n=Noise(x,z,Mathf.Max(128,settings.regionSize),5);
            var a=Profile(n<.53f?WorldBiome.Meadow:WorldBiome.Forest);
            var b=Profile(n<.53f?WorldBiome.Forest:WorldBiome.Highlands);
            float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(n<.53f?.33f:.53f,n<.53f?.53f:.75f,n));
            float y=Mathf.Lerp(a.baseHeight,b.baseHeight,t)+Mathf.Lerp(a.heightVariation,b.heightVariation,t)*Noise(x,z,settings.macroScale,1);
            y+=(Noise(x,z,settings.detailScale,2)-.5f)*2*Mathf.Lerp(a.terrainRoughness,b.terrainRoughness,t);
            int spacing=Mathf.Max(128,settings.mountainSpacing),cx=Mathf.FloorToInt(x/spacing),cz=Mathf.FloorToInt(z/spacing);
            float radius=Mathf.Clamp(settings.mountainRadius,16,spacing*.45f);
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                int gx=cx+dx,gz=cz+dz;if(Unit(Hash(settings.seed,gx,gz,10))>=settings.mountainChance)continue;
                float mx=(gx+.3f+.4f*Unit(Hash(settings.seed,gx,gz,11)))*spacing,mz=(gz+.3f+.4f*Unit(Hash(settings.seed,gx,gz,12)))*spacing;
                float d=Vector2.Distance(new Vector2(x,z),new Vector2(mx,mz))/radius;
                if(d<1)y+=settings.mountainHeight*Mathf.Pow(1-d*d,2);
            }
            return y;
        }
        public float VillageHalfExtent=>26*Mathf.Max(1,settings.content!=null?settings.content.villageSizeMultiplier:2);
        // One candidate in the interior of each large region keeps settlements rare
        // and separated, independent of chunk loading order. Origin remains a safe start.
        bool IsVillageCell(Vector2Int cell)
        {
            if(!settings.preserveAuthoredCenter&&cell==Vector2Int.zero)return true;
            int span=Mathf.Max(4,settings.villageRegionCells),margin=Mathf.Max(1,span/4);
            var region=new Vector2Int(Mathf.FloorToInt(cell.x/(float)span),Mathf.FloorToInt(cell.y/(float)span));
            if(region==Vector2Int.zero)return false;
            if(Unit(Hash(settings.seed,region.x,region.y,720))>=settings.villageRegionChance)return false;
            int inner=span-2*margin;
            int x=region.x*span+margin+(int)((uint)Hash(settings.seed,region.x,region.y,721)%(uint)inner);
            int z=region.y*span+margin+(int)((uint)Hash(settings.seed,region.x,region.y,722)%(uint)inner);
            if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(z))<4)return false;
            return cell.x==x&&cell.y==z;
        }
        public WorldSite Site(Vector2Int cell)
        {
            if(sites.TryGetValue(cell,out var cached))return cached;
            int spacing=Mathf.Max(64,settings.siteSpacing);int h=Hash(settings.seed,cell.x,cell.y,20);
            float x=(cell.x+.5f)*spacing+(Unit(Hash(h,0,0,21))-.5f)*24,z=(cell.y+.5f)*spacing+(Unit(Hash(h,0,0,22))-.5f)*24;
            float roll=Unit(h);var kind=roll<.12f?WorldSiteKind.Clearing:roll<.18f?WorldSiteKind.BossArena:roll<.32f?WorldSiteKind.Ruin:roll<.40f?WorldSiteKind.Secret:WorldSiteKind.Clearing;
            if(IsVillageCell(cell))kind=WorldSiteKind.Village;
            var result=new WorldSite{cell=cell,position=new Vector3(x,Natural(x,z),z),kind=kind,radius=kind==WorldSiteKind.Village?VillageHalfExtent:kind==WorldSiteKind.BossArena?20:12};
            if(sites.Count>=4096)sites.Clear();sites[cell]=result;return result;
        }
        public bool IsExterior(float x,float z)=>!settings.preserveAuthoredCenter||Mathf.Max(Mathf.Abs(x),Mathf.Abs(z))>settings.authoredSize*.5f+Mathf.Max(64,settings.transitionWidth);
        float Pass(float x,float z,out float distance,out bool reserved)
        {
            float y=Natural(x,z);distance=float.MaxValue;reserved=false;
            int spacing=Mathf.Max(64,settings.siteSpacing),cx=Mathf.FloorToInt(x/spacing),cz=Mathf.FloorToInt(z/spacing);
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                var s=Site(new Vector2Int(cx+dx,cz+dz));
                if(!IsExterior(s.position.x,s.position.z))continue;
                float d=Vector2.Distance(new Vector2(x,z),new Vector2(s.position.x,s.position.z));
                y=Mathf.Lerp(y,s.position.y,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(s.radius,s.radius+16,d)));
                reserved|=d<s.radius+3;
                if(s.kind==WorldSiteKind.Secret)y-=1.3f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,8,d)));
                for(int axis=0;axis<2;axis++)
                {
                    var other=Site(s.cell+(axis==0?Vector2Int.right:Vector2Int.up));
                    if(s.kind!=WorldSiteKind.Village&&other.kind!=WorldSiteKind.Village||!IsExterior(other.position.x,other.position.z))continue;
                    Vector2 start=new Vector2(s.position.x,s.position.z),delta=new Vector2(other.position.x,other.position.z)-start;
                    float t=Mathf.Clamp01(Vector2.Dot(new Vector2(x,z)-start,delta)/delta.sqrMagnitude);
                    float road=Vector2.Distance(new Vector2(x,z),start+delta*t);distance=Mathf.Min(distance,road);
                    y=Mathf.Lerp(y,Mathf.Lerp(s.position.y,other.position.y,t),1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,10,road)));
                }
            }
            // Roads and round clearings must never lift terrain through the square town floor.
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                var village=Site(new Vector2Int(cx+dx,cz+dz));
                if(village.kind!=WorldSiteKind.Village||!IsExterior(village.position.x,village.position.z))continue;
                float edge=Mathf.Max(Mathf.Abs(x-village.position.x),Mathf.Abs(z-village.position.z));
                y=Mathf.Lerp(y,village.position.y,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(VillageHalfExtent,VillageHalfExtent+16,edge)));
                reserved|=edge<VillageHalfExtent+3;
            }
            reserved|=distance<7;return y;
        }
        public override float Height(float x,float z)
        {
            if(!settings.preserveAuthoredCenter)
            {float stepSize=Mathf.Max(.05f,settings.stepHeight);return Mathf.Round(Pass(x,z,out _,out _)/stepSize)*stepSize;}
            float edge=Mathf.Max(Mathf.Abs(x),Mathf.Abs(z))-settings.authoredSize*.5f;
            // Keep the saved centre and its first neighbouring samples byte-for-byte compatible.
            if(edge<=1)
            {
                float townEdge=Mathf.Max(Mathf.Abs(x+50),Mathf.Abs(z+70));
                float flat=Mathf.Lerp(base.Height(x,z),4,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(VillageHalfExtent,VillageHalfExtent+16,townEdge)));
                float townStep=Mathf.Max(.05f,settings.stepHeight);return Mathf.Round(flat/townStep)*townStep;
            }
            float value=Pass(x,z,out _,out _);
            if(edge<Mathf.Max(64,settings.transitionWidth))value=Mathf.Lerp(base.Height(x,z),value,Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,Mathf.Max(64,settings.transitionWidth),edge)));
            float legacyTownEdge=Mathf.Max(Mathf.Abs(x+50),Mathf.Abs(z+70));
            value=Mathf.Lerp(value,4,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(VillageHalfExtent,VillageHalfExtent+16,legacyTownEdge)));
            float step=Mathf.Max(.05f,settings.stepHeight);return Mathf.Round(value/step)*step;
        }
        public override bool Reserved(float x,float z,float margin=0)
        {
            if(settings.content!=null)
            {
                int arrivalSpacing=Mathf.Max(64,settings.siteSpacing);
                int ax=Mathf.FloorToInt(x/arrivalSpacing),az=Mathf.FloorToInt(z/arrivalSpacing);
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    var town=Site(new Vector2Int(ax+dx,az+dz));if(town.kind!=WorldSiteKind.Village)continue;
                    var arrival=town.position+settings.content.VillageArrivalOffset;
                    if(Vector2.Distance(new Vector2(x,z),new Vector2(arrival.x,arrival.z))<15+margin)return true;
                }
            }
            if(settings.preserveAuthoredCenter&&Mathf.Abs(x+50)<VillageHalfExtent+4+margin&&Mathf.Abs(z+70)<VillageHalfExtent+4+margin)return true;
            int spacing=Mathf.Max(64,settings.siteSpacing),cx=Mathf.FloorToInt(x/spacing),cz=Mathf.FloorToInt(z/spacing);
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {var site=Site(new Vector2Int(cx+dx,cz+dz));if(site.kind==WorldSiteKind.Village&&IsExterior(site.position.x,site.position.z)&&Mathf.Abs(x-site.position.x)<VillageHalfExtent+4+margin&&Mathf.Abs(z-site.position.z)<VillageHalfExtent+4+margin)return true;}
            if(!IsExterior(x,z))return base.Reserved(x,z,margin);Pass(x,z,out float d,out bool reserved);return reserved||d<7+margin;
        }
        public override float PathDistance(float x,float z)
        {if(!IsExterior(x,z))return base.PathDistance(x,z);Pass(x,z,out float d,out _);return d;}
        public override Color Top(float x,float z,float height)
        {
            if(!IsExterior(x,z))return base.Top(x,z,height);
            Color color=Biome(x,z)==WorldBiome.Forest?new Color(.24f,.43f,.18f):Biome(x,z)==WorldBiome.Highlands?new Color(.46f,.51f,.29f):new Color(.43f,.63f,.24f);
            if(PathDistance(x,z)<2.7f)color=new Color(.64f,.53f,.35f);
            if(height>40)color=new Color(.5f,.53f,.46f);
            return color*Mathf.Lerp(.94f,1.06f,Noise(x,z,2,4));
        }
    }
}
