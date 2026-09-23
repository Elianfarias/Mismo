using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    [CreateAssetMenu(menuName="Mismo/World/Grove appearance")]
    public sealed class GroveWorldStyle : ScriptableObject
    {
        public Color meadowLow=new Color(.424f,.506f,.329f),meadowHigh=new Color(.525f,.573f,.369f);
        public Color forestLow=new Color(.314f,.412f,.294f),forestHigh=new Color(.424f,.506f,.329f);
        public Color stoneLow=new Color(.467f,.494f,.475f),stoneHigh=new Color(.698f,.706f,.627f);
        public Color path=new Color(.66f,.60f,.46f);
        public GameObject shortVines,longVines,fireflies;
        public Material waterMaterial;
        public Color distantWater=new Color(.08f,.32f,.33f);
        [Range(0,4)] public int vinesPerChunk=2;
        public static bool Supports(WorldBiome biome)=>biome==WorldBiome.Meadow||biome==WorldBiome.Forest||biome==WorldBiome.Highlands;
        public Color Ground(WorldBiome biome,float x,float z,float road)
        {
            // Quantised broad patches: stable global coordinates, no per-chunk seams.
            float n=Mathf.PerlinNoise(Mathf.Floor(x/2)*.19f+71,Mathf.Floor(z/2)*.19f+137);
            float t=Mathf.Floor(n*4)/3;
            Color tint=biome==WorldBiome.Forest?Color.Lerp(forestLow,forestHigh,t):Color.Lerp(meadowLow,meadowHigh,t);
            tint=Color.Lerp(tint,path,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f,5.5f,road)));
            // The kit's palette texture is sRGB; vertex colors arrive at the shader unconverted.
            tint=tint.linear;tint.a=0;return tint;
        }
        public Color Stone(float x,float z,float height)
        {
            float n=Mathf.PerlinNoise(Mathf.Floor(x+z)*.21f+19,Mathf.Floor(height/.75f)*.31f+53);
            Color color=Color.Lerp(stoneLow,stoneHigh,Mathf.Floor(n*4)/3).linear;color.a=0;return color;
        }
        public float PatchDensity(WorldAssetKind kind,float x,float z)
        {
            if(kind!=WorldAssetKind.Grass&&kind!=WorldAssetKind.Bush&&kind!=WorldAssetKind.Flower)return 1;
            float n=Mathf.PerlinNoise(x/15+83,z/15+129);
            return Mathf.Lerp(.3f,1.45f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.7f,n)));
        }
        public void DecorateLedges(ExplorationTerrain terrain,Vector2Int chunk,Transform root,int seed)
        {
            int count=0;
            for(int z=3;z<32&&count<vinesPerChunk;z+=6)for(int x=3;x<32&&count<vinesPerChunk;x+=6)
            {
                float px=chunk.x*32+x+.5f,pz=chunk.y*32+z+.5f;
                if(!Supports(terrain.Biome(px,pz))||terrain.Reserved(px,pz,3)||terrain.Height(px,pz)<=0)continue;
                int hash=ExplorationTerrain.Hash(seed,(int)px,(int)pz,1801);if((uint)hash%4!=0)continue;
                foreach(var direction in new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left})
                {
                    float high=terrain.Height(px,pz),low=terrain.Height(px+direction.x,pz+direction.z),drop=high-low;
                    if(drop<1.5f)continue;
                    // Check full vine width against the same cliff, avoiding unsupported corners.
                    var tangent=new Vector3(-direction.z,0,direction.x);bool supported=true;
                    for(int s=-1;s<=1;s+=2)
                    {
                        float sx=px+tangent.x*s*.7f,sz=pz+tangent.z*s*.7f;
                        if(Mathf.Abs(terrain.Height(sx,sz)-high)>.3f||high-terrain.Height(sx+direction.x,sz+direction.z)<1.5f)supported=false;
                    }
                    if(!supported)continue;
                    var prefab=drop>=2.8f?longVines:shortVines;if(prefab==null)break;
                    var go=Instantiate(prefab,new Vector3(px,high-.08f,pz)+direction*.68f,Quaternion.LookRotation(-direction),root);
                    go.name="Grove ledge vines";float length=drop>=2.8f?2.52f:1.47f;
                    go.transform.localScale=new Vector3(.85f,Mathf.Min(1,(drop-.2f)/length),.85f);count++;break;
                }
            }
        }
    }
}
