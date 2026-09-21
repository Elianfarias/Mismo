using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    /// <summary>Versioned seed geography, independent of chunk order and discovery.</summary>
    public sealed class FiniteWorldPlan
    {
        public const float SeaLevel=0;
        readonly int seed,spacing;
        readonly float length,halfWidth;
        readonly float[] borders=new float[3];
        readonly Vector2[] route=new Vector2[6];
        readonly Vector2Int[] objectives=new Vector2Int[4];
        readonly float[] routeHeights={12,12,14,20,28,58};
        public GuidedWorldLayout Layout {get;}
        public int Version {get;}
        public int RoutePointCount=>route.Length;
        public float MinX=>Layout!=null?Layout.Bounds.xMin:-512;
        public float MaxX=>Layout!=null?Layout.Bounds.xMax:MinX+length;
        public Rect Bounds=>Layout!=null?Layout.Bounds:Rect.MinMaxRect(MinX-192,-halfWidth-384,MaxX+192,halfWidth+384);
        public Vector2 Start=>SitePosition(Vector2Int.zero);
        public FiniteWorldPlan(ExplorationWorldSettings settings)
        {
            seed=settings.seed;spacing=Mathf.Clamp(settings.siteSpacing,64,128);
            length=Mathf.Clamp(settings.worldLength,4096,16384);
            halfWidth=Mathf.Clamp(settings.worldHalfWidth,512,4096);
            Version=settings.generationVersion;
            if(Version==2)
            {
                Layout=new GuidedWorldLayout(settings);
                var goals=new Vector2[4];
                for(int i=0;i<4;i++)
                {
                    var target=Layout.ObjectiveTarget(i);
                    objectives[i]=new Vector2Int(Mathf.FloorToInt(target.x/spacing),Mathf.FloorToInt(target.y/spacing));
                    goals[i]=SitePosition(objectives[i]);
                }
                route=Layout.Route(Start,goals);routeHeights=Layout.RouteHeights;return;
            }
            for(int i=0;i<3;i++)borders[i]=MinX+length*(i+1)/4+Signed(i,801)*length*.018f;
            route[0]=Start+Vector2.up*80;
            route[1]=Start+new Vector2(128,80);
            for(int i=0;i<4;i++)
            {
                float left=i==0?MinX:borders[i-1],right=i==3?MaxX:borders[i];
                float x=Mathf.Lerp(left,right,.68f)+Signed(i,802)*40;
                if(i==0)x=Mathf.Max(x,Start.x+256);
                float z=Center(x)+Signed(i,803)*halfWidth*.20f;
                objectives[i]=new Vector2Int(Mathf.FloorToInt(x/spacing),Mathf.FloorToInt(z/spacing));
                route[i+2]=SitePosition(objectives[i]);
            }
        }
        float Signed(int cell,int stream)=>(uint)ExplorationTerrain.Hash(seed,cell,0,stream)/(float)uint.MaxValue*2-1;
        float Noise(float x,float z,float scale,int stream)=>Mathf.PerlinNoise(x/scale+(seed&4095)*.173f+stream*17.7f,z/scale+stream*31.3f);
        public int SiteSpacing=>spacing;
        public Vector2 SitePosition(Vector2Int cell)
        {
            int h=ExplorationTerrain.Hash(seed,cell.x,cell.y,20);
            float x=(cell.x+.5f)*spacing+((uint)ExplorationTerrain.Hash(h,0,0,21)/(float)uint.MaxValue-.5f)*24;
            float z=(cell.y+.5f)*spacing+((uint)ExplorationTerrain.Hash(h,0,0,22)/(float)uint.MaxValue-.5f)*24;
            return new Vector2(x,z);
        }
        float Center(float x)=>(Noise(x,0,700,810)-.5f)*halfWidth*.45f;
        public float InlandDistance(float x,float z)
        {
            if(Layout!=null)return Layout.InlandDistance(x,z);
            float t=Mathf.Clamp01((x-MinX)/length);
            float width=halfWidth*(.45f+.55f*Mathf.Sin(t*Mathf.PI))+(Noise(x,z,230,811)-.5f)*90;
            width*=Mathf.Sqrt(Mathf.Clamp01(Mathf.Min(x-MinX,MaxX-x)/256));
            return Mathf.Min(x-MinX,MaxX-x,width-Mathf.Abs(z-Center(x)));
        }
        public bool Contains(float x,float z,float margin=0)=>InlandDistance(x,z)>=margin;
        public bool ContainsChunk(Vector2Int chunk)=>Bounds.Overlaps(new Rect(chunk.x*32,chunk.y*32,32,32));
        public bool Walkable(float x,float z)=>Contains(x,z,24)&&(Layout==null||!Layout.Blocked(x,z));
        public float BarrierDistance(float x,float z)=>Layout!=null?Layout.BarrierDistance(x,z):float.MaxValue;
        public float ApplyBarriers(float x,float z,float height)=>Layout!=null?Layout.ApplyBarriers(x,z,height):height;
        public Vector3 ConstrainMovement(Vector3 from,Vector3 to)
        {
            // Sample the whole displacement, including skill dashes, so a bay cannot be skipped.
            int steps=Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(new Vector2(from.x,from.z),new Vector2(to.x,to.z))/4),1,4096);
            Vector3 last=from;
            for(int i=1;i<=steps;i++)
            {
                var point=Vector3.Lerp(from,to,i/(float)steps);
                if(Walkable(point.x,point.z)){last=point;continue;}
                var outside=point;
                for(int j=0;j<10;j++)
                {
                    var middle=(last+outside)*.5f;
                    if(Walkable(middle.x,middle.z))last=middle;else outside=middle;
                }
                last.y=to.y;return last;
            }
            return to;
        }
        // Border displacement depends only on Z: regions never fold over one another.
        float ProgressX(float x,float z)=>x+(Noise(0,z,480,812)-.5f)*120;
        public int Stage(float x,float z)
        {
            if(Layout!=null)return Layout.Stage(x,z);
            float p=ProgressX(x,z);return p<borders[0]?0:p<borders[1]?1:p<borders[2]?2:3;
        }
        public int MinimumLevel(float x,float z)=>1+Stage(x,z)*15;
        public WorldBiome Biome(float x,float z)
        {
            if(!Contains(x,z))return WorldBiome.Ocean;
            int stage=Stage(x,z);
            if(stage==0)return (Layout!=null?Layout.IsForest(x,z):Noise(x,z,150,813)>.59f)?WorldBiome.Forest:WorldBiome.Meadow;
            return stage==1?WorldBiome.Desert:stage==2?WorldBiome.Ice:WorldBiome.Mountains;
        }
        public Vector2Int ObjectiveCell(int stage)=>objectives[Mathf.Clamp(stage,0,3)];
        public Vector2 RoutePoint(int index)=>route[Mathf.Clamp(index,0,route.Length-1)];
        public int ObjectiveStage(Vector2Int cell)
        {for(int i=0;i<4;i++)if(objectives[i]==cell)return i;return -1;}
        public float RouteDistance(float x,float z,out float height)
        {
            var p=new Vector2(x,z);float best=float.MaxValue;height=12;
            for(int i=0;i<route.Length-1;i++)
            {
                var delta=route[i+1]-route[i];float t=Mathf.Clamp01(Vector2.Dot(p-route[i],delta)/delta.sqrMagnitude);
                float d=Vector2.Distance(p,route[i]+delta*t);
                if(d>=best)continue;best=d;height=Mathf.Lerp(routeHeights[i],routeHeights[i+1],t);
            }
            return best;
        }
        float RegionalHeight(int stage,float x,float z)
        {
            float macro=Noise(x,z,220,820),detail=Noise(x,z,48,821);
            switch(stage)
            {
                case 0:return 8+macro*10+detail*2;
                case 1:return 10+macro*15+Mathf.Sin(x*.025f+detail*4)*2;
                case 2:return 20+macro*9+detail;
                default:return 30+Mathf.Pow(macro,2)*65+detail*8;
            }
        }
        public float NaturalHeight(float x,float z)
        {
            if(Layout!=null)return Layout.NaturalHeight(x,z);
            float h=RegionalHeight(0,x,z),p=ProgressX(x,z);
            for(int i=0;i<3;i++)h=Mathf.Lerp(h,RegionalHeight(i+1,x,z),Mathf.SmoothStep(0,1,Mathf.InverseLerp(borders[i]-80,borders[i]+80,p)));
            return h;
        }
        public float ApplyRoute(float x,float z,float height,out float distance)
        {
            distance=RouteDistance(x,z,out float roadHeight);
            // Wide, obstacle-free shoulders. Heights at objectives match the route exactly.
            float influence=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,24,distance));
            height=Mathf.Lerp(height,roadHeight,influence);
            for(int stage=0;stage<4;stage++)
            {
                int i=Layout==null?stage+2:stage==0?2:stage==1?4:stage==2?7:10;
                height=Mathf.Lerp(height,routeHeights[i],1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(24,40,Vector2.Distance(new Vector2(x,z),route[i]))));
            }
            return height;
        }
        public float ApplyCoast(float x,float z,float height)=>Mathf.Lerp(-8,height,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-28,64,InlandDistance(x,z))));
        public Color GroundColor(float x,float z)
        {
            if(!Contains(x,z))return new Color(.10f,.31f,.42f);
            if(Layout!=null)return Layout.GroundColor(x,z);
            Color color=Noise(x,z,150,813)>.59f?new Color(.24f,.43f,.18f):new Color(.43f,.63f,.24f);
            float p=ProgressX(x,z);
            color=Color.Lerp(color,new Color(.82f,.65f,.37f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(borders[0]-80,borders[0]+80,p)));
            color=Color.Lerp(color,new Color(.72f,.86f,.91f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(borders[1]-80,borders[1]+80,p)));
            return Color.Lerp(color,new Color(.49f,.54f,.60f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(borders[2]-80,borders[2]+80,p)));
        }
    }
}
