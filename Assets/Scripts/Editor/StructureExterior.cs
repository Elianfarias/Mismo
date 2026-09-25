using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.World.Structures;
using UnityEditor;
using UnityEngine;

// Architecture and landscape share the world's stone and water materials.
public static class StructureExterior
{
    static float Peak(Vector2 p,Vector2 center,Vector2 size,float height)
    {var q=p-center;float r=Mathf.Max(Mathf.Abs(q.x)/size.x,Mathf.Abs(q.y)/size.y);return height*Mathf.Pow(Mathf.Max(0,1-r),.28f);}
    public static float RockHeight(StructureDefinition d,Vector2 p)
    {
        float r=d.Radius,scale=r/24;
        float n=Mathf.PerlinNoise(p.x*.22f+(d.layoutSeed&255),p.y*.14f+41);
        p+=new Vector2((n-.5f)*7,Mathf.Sin(p.x*.28f)*2.7f);
        float h=Mathf.Max(Peak(p,new Vector2(-7,-2)*scale,new Vector2(17,21)*scale,d.mountainHeight),Peak(p,new Vector2(8,8)*scale,new Vector2(15,17)*scale,d.mountainHeight*1.25f));
        h=Mathf.Max(h,Peak(p,new Vector2(-3,13)*scale,new Vector2(18,13)*scale,d.mountainHeight*1.12f));
        return h*(.83f+n*.3f)+(Mathf.PerlinNoise(p.x*.6f+16,p.y*.6f+59)-.5f)*2.5f;
    }
    public static float TempleHeight(Vector2 p)
    {
        float x=Mathf.Abs(p.x),z=Mathf.Abs(p.y);float h=0;
        if(x<15&&z<18)h=7;
        if(x<12&&z<16)h=13;
        if(x<9&&z<14)h=20;
        if(x<7&&z<10)h=27;
        return h;
    }
    internal static void TempleDetails(EnchantedGroveVoxels g,StructureDefinition d)
    {
        // Five cornices, paired buttresses and broken crown stones give a readable silhouette.
        foreach(var tier in new[]{new Vector3(16,1,19),new Vector3(15.5f,6.5f,18.5f),new Vector3(12.5f,12.5f,16.5f),new Vector3(9.5f,19.5f,14.5f),new Vector3(7.5f,26.5f,10.5f)})
        {
            float x=tier.x,z=tier.z,y=tier.y;
            g.Box(new Vector3(-x,y,-z),new Vector3(x,y+.5f,-z+.75f),56);
            g.Box(new Vector3(-x,y,z-.75f),new Vector3(x,y+.5f,z),56);
            g.Box(new Vector3(-x,y,-z),new Vector3(-x+.75f,y+.5f,z),56);
            g.Box(new Vector3(x-.75f,y,-z),new Vector3(x,y+.5f,z),56);
        }
        foreach(float x in new[]{-11.5f,-6f,6f,11.5f})
        {
            float h=Mathf.Abs(x)<8?12:7;
            g.Box(new Vector3(x-1,-.5f,-19.2f),new Vector3(x+1,.7f,-16.8f),56);
            g.Box(new Vector3(x-.6f,.7f,-18.5f),new Vector3(x+.6f,h,-17),3);
            g.Box(new Vector3(x-1,h,-19),new Vector3(x+1,h+.7f,-16.5f),56);
        }
        for(int i=0;i<7;i++)
        {
            float z=-12+i*4;
            foreach(float sign in new[]{-1f,1f})g.Box(new Vector3(sign*14.5f-.6f,0,z-.7f),new Vector3(sign*14.5f+.6f,7.5f,z+.7f),56);
        }
        for(int i=0;i<5;i++)
        {
            float x=-6+i*3;
            g.Box(new Vector3(x-.7f,27,-10),new Vector3(x+.7f,28+(i%2)*.5f,-8.5f),56);
            g.Box(new Vector3(x-.8f,27,8),new Vector3(x+.8f,28.5f,10),56);
        }
        foreach(var tier in new[]{new Vector3(12,8,16),new Vector3(9,15,14),new Vector3(7,22,10)})
        {
            for(float x=-tier.x+3;x<tier.x-1;x+=3)
            {
                g.Box(new Vector3(x-1,tier.y-.3f,-tier.z-.4f),new Vector3(x+1,tier.y,-tier.z+.7f),56);
                g.Box(new Vector3(x-1,tier.y+3.2f,-tier.z-.4f),new Vector3(x+1,tier.y+3.6f,-tier.z+.7f),56);
                foreach(float side in new[]{-1f,1f})g.Box(new Vector3(x+side*.95f-.2f,tier.y,-tier.z-.35f),new Vector3(x+side*.95f+.2f,tier.y+3.2f,-tier.z+.5f),56);
                var lo=Vector3Int.FloorToInt(new Vector3(x-.7f,tier.y,-tier.z-.1f)/g.Step);var hi=Vector3Int.CeilToInt(new Vector3(x+.7f,tier.y+3.2f,-tier.z+1)/g.Step);
                for(int cz=lo.z;cz<hi.z;cz++)for(int cy=lo.y;cy<hi.y;cy++)for(int cx=lo.x;cx<hi.x;cx++)g.Cells.Remove(new Vector3Int(cx,cy,cz));
            }
            foreach(float side in new[]{-1f,1f})for(float z=-tier.z+3;z<tier.z-1;z+=4)
            {float x=side*tier.x;g.Box(new Vector3(x-.3f,tier.y,z-.35f),new Vector3(x+.3f,tier.y+3.8f,z+.35f),56);}
        }
        // Moss grows on shelves and the roof, without covering the interior floor.
        for(int x=-14;x<15;x++)for(int z=-17;z<18;z++)
        {
            float top=TempleHeight(new Vector2(x+.5f,z+.5f));
            if(top>0&&Mathf.PerlinNoise(x*.29f+18,z*.25f+39)>.52f)g.Box(new Vector3(x,top,z),new Vector3(x+1,top+.25f,z+1),55);
        }
        // A continuous deck across the pond; rail gaps at both ends remain open.
        g.Box(new Vector3(-2.4f,-.7f,-d.Radius-1),new Vector3(2.4f,0,-17),56);
        foreach(float x in new[]{-2.8f,2.8f})for(float z=-d.Radius;z<-20;z+=3)
        {g.Box(new Vector3(x-.35f,-1.5f,z-.4f),new Vector3(x+.35f,1.3f,z+.4f),56);g.Box(new Vector3(x-.5f,1.3f,z-.55f),new Vector3(x+.5f,1.6f,z+.55f),55);}
    }
    public static void ColorStone(Mesh mesh,StructureDefinition d)
    {
        if(!d.worldStoneSurface)return;
        var v=mesh.vertices;var uv=mesh.uv;var colors=new Color[v.Length];
        for(int i=0;i<v.Length;i+=4)
        {
            Vector3 center=(v[i]+v[Mathf.Min(i+2,v.Length-1)])*.5f;
            int palette=uv.Length==v.Length?Mathf.FloorToInt(uv[i].x*64):1;
            Color c=d.stoneStyle!=null?d.stoneStyle.Stone(center.x,center.z,center.y):new Color(.30f,.34f,.32f,0);
            if(palette==55||palette>=10&&palette<=12)c=new Color(.24f,.36f,.13f).linear;
            if(palette==56)c*=1.15f;
            if(d.style==StructureStyle.Temple&&palette!=55)c=Color.Lerp(c,new Color(.29f,.24f,.16f),.17f);
            c.a=0;for(int k=i;k<Mathf.Min(i+4,v.Length);k++)colors[k]=c;
        }
        mesh.colors=colors;
    }
    public static Mesh Water(StructureDefinition d)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();float r=d.moatRadius;
        for(int z=-(int)r;z<r;z++)for(int x=-(int)r;x<r;x++)
        {
            if(!StructureDefinition.IsMoat(new Vector2(x+.5f,z+.5f),r))continue;int first=v.Count;
            foreach(var p in new[]{new Vector3(x,-.35f,z),new Vector3(x,-.35f,z+1),new Vector3(x+1,-.35f,z+1),new Vector3(x+1,-.35f,z)}){v.Add(p);uv.Add(new Vector2(p.x/(r*2)+.5f,p.z/(r*2)+.5f));}
            indices.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
        }
        var mesh=new Mesh{name=d.name+"_Water"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    public static Mesh Ground(StructureDefinition d)
    {
        var g=new VoxelRegionGeometry();
        float Height(int x,int z)=>d.FoundationOffset(new Vector2(x+.5f,z+.5f))-.08f;
        for(int z=-45;z<45;z++)for(int x=-45;x<45;x++)
        {
            float y=Height(x,z);var c=d.stoneStyle!=null?d.stoneStyle.Ground(WorldBiome.Forest,x,z,50):new Color(.14f,.24f,.09f,0);
            g.Quad(new Vector3(x,y,z),new Vector3(x,y,z+1),new Vector3(x+1,y,z+1),new Vector3(x+1,y,z),c);
            float low=Height(x+1,z);if(y>low)g.Quad(new Vector3(x+1,low,z),new Vector3(x+1,y,z),new Vector3(x+1,y,z+1),new Vector3(x+1,low,z+1),c);
            low=Height(x-1,z);if(y>low)g.Quad(new Vector3(x,low,z+1),new Vector3(x,y,z+1),new Vector3(x,y,z),new Vector3(x,low,z),c);
            low=Height(x,z+1);if(y>low)g.Quad(new Vector3(x+1,low,z+1),new Vector3(x+1,y,z+1),new Vector3(x,y,z+1),new Vector3(x,low,z+1),c);
            low=Height(x,z-1);if(y>low)g.Quad(new Vector3(x,low,z),new Vector3(x,y,z),new Vector3(x+1,y,z),new Vector3(x+1,low,z),c);
        }
        return g.Mesh(d.name+"_PreviewGround");
    }
}
