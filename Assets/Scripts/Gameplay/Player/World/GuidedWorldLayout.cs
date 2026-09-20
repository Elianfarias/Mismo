using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    /// <summary>Concept-map topology: meadow SW, desert SE, ice NW, mountains NE.</summary>
    public sealed class GuidedWorldLayout
    {
        readonly int seed;
        readonly float sx,sz;
        public GuidedWorldLayout(ExplorationWorldSettings settings)
        {
            seed=settings.seed;
            sx=Mathf.Clamp(settings.worldLength,4096,16384)/4096f;
            sz=Mathf.Clamp(settings.worldHalfWidth,768,3072)/768f;
        }
        float Noise(float x,float z,int stream)=>Mathf.PerlinNoise(x/240+(seed&4095)*.173f+stream*17.7f,z/240+stream*31.3f);
        float Signed(int stream)=>(uint)ExplorationTerrain.Hash(seed,0,0,stream)/(float)uint.MaxValue*2-1;
        public Rect Bounds=>Rect.MinMaxRect(-900*sx,-750*sz,2500*sx,2150*sz);
        public float NorthBorder(float x)=>sz*(650+(Noise(x/sx,0,850)-.5f)*90);
        public float MeadowBorder(float z)=>sx*(800+(Noise(0,z/sz,851)-.5f)*100);
        public float MountainBorder(float z)=>sx*(1450+(Noise(0,z/sz,852)-.5f)*100);
        public float DesertPassX=>sx*(1100+Signed(853)*45);
        public float MountainPassZ=>sz*(1350+Signed(854)*45);
        public Vector2 DesertPass=>new Vector2(DesertPassX,NorthBorder(DesertPassX));
        public Vector2 MountainPass=>new Vector2(MountainBorder(MountainPassZ),MountainPassZ);
        public float InlandDistance(float x,float z)
        {
            float nx=(x/sx-800)/1550,nz=(z/sz-700)/1300;
            float radius=Mathf.Sqrt(nx*nx+nz*nz);
            float coast=(1-radius)*Mathf.Min(1550*sx,1300*sz);
            return coast+(Noise(x/sx,z/sz,855)-.5f)*100;
        }
        public int Stage(float x,float z)=>z<NorthBorder(x)?(x<MeadowBorder(z)?0:1):(x<MountainBorder(z)?2:3);
        public Vector2 ObjectiveTarget(int stage)
        {
            Vector2 target=stage==0?new Vector2(460,220):stage==1?new Vector2(1500,180):stage==2?new Vector2(620,1150):new Vector2(1870,1490);
            target+=new Vector2(Signed(860+stage*2)*45,Signed(861+stage*2)*45);
            return new Vector2(target.x*sx,target.y*sz);
        }
        public Vector2[] Route(Vector2 start,Vector2[] goals)=>new[]{
            start+Vector2.up*80,start+new Vector2(128,80),goals[0],
            new Vector2(MeadowBorder(180*sz)+180*sx,180*sz),goals[1],
            DesertPass+Vector2.down*200*sz,DesertPass+Vector2.up*200*sz,goals[2],
            MountainPass+Vector2.left*180*sx,MountainPass+Vector2.right*180*sx,goals[3]};
        public float[] RouteHeights=>new float[]{12,12,14,18,22,35,106,112,118,158,174};
        public float BarrierDistance(float x,float z)
        {
            float horizontal=Mathf.Abs(x-DesertPassX)>64*sx?Mathf.Abs(z-NorthBorder(x)):float.MaxValue;
            float vertical=z>=NorthBorder(x)-32&&Mathf.Abs(z-MountainPassZ)>64*sz?Mathf.Abs(x-MountainBorder(z)):float.MaxValue;
            return Mathf.Min(horizontal,vertical);
        }
        public bool Blocked(float x,float z)=>BarrierDistance(x,z)<20;
        public float ApplyBarriers(float x,float z,float height)
        {
            if(Mathf.Abs(x-DesertPassX)>64*sx)
            {
                float d=Mathf.Abs(z-NorthBorder(x));
                height=Mathf.Max(height,116*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,40,d))));
            }
            if(z>=NorthBorder(x)-32&&Mathf.Abs(z-MountainPassZ)>64*sz)
            {
                float d=Mathf.Abs(x-MountainBorder(z));
                height=Mathf.Max(height,240*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,48,d))));
            }
            return height;
        }
        public float NaturalHeight(float x,float z)
        {
            float macro=Noise(x/sx,z/sz,870),detail=Noise(x/sx*4,z/sz*4,871);
            float meadow=8+macro*10+detail*2;
            float desert=12+macro*16+Mathf.Sin(x*.025f+detail*4)*2;
            float ice=102+macro*15+detail*2;
            float mountains=142+Mathf.Pow(macro,2)*160+detail*12;
            float south=Mathf.Lerp(meadow,desert,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-70,70,x-MeadowBorder(z))));
            float north=Mathf.Lerp(ice,mountains,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-40,40,x-MountainBorder(z))));
            return Mathf.Lerp(south,north,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-4,4,z-NorthBorder(x))));
        }
        public bool IsForest(float x,float z)=>Noise(x/sx,z/sz,813)>.59f;
        public Color GroundColor(float x,float z)
        {
            // The south-facing escarpment is exposed ice, even below the biome boundary.
            if(Mathf.Abs(x-DesertPassX)>64*sx&&Mathf.Abs(z-NorthBorder(x))<52)return new Color(.48f,.73f,.86f);
            Color meadow=IsForest(x,z)?new Color(.24f,.43f,.18f):new Color(.43f,.63f,.24f);
            Color south=Color.Lerp(meadow,new Color(.82f,.65f,.37f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(-70,70,x-MeadowBorder(z))));
            Color north=Color.Lerp(new Color(.72f,.86f,.91f),new Color(.49f,.54f,.60f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(-40,40,x-MountainBorder(z))));
            return Color.Lerp(south,north,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-35,12,z-NorthBorder(x))));
        }
    }
}
