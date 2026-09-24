using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// Authoring grid in metres. Only exposed surfaces reach the saved runtime mesh.
internal sealed class EnchantedGroveVoxels
{
    internal readonly Dictionary<Vector3Int, byte> Cells = new Dictionary<Vector3Int, byte>();
    internal readonly float Step;
    internal EnchantedGroveVoxels(float step = .1f) { Step = step; }
    internal static float Noise(float x, float y, float z) => Mathf.PerlinNoise(x + y * .43f + 71, z - y * .61f + 137);
    internal void Box(Vector3 min, Vector3 max, byte color)
    {
        var lo = Vector3Int.FloorToInt(min / Step + Vector3.one * .001f);
        var hi = Vector3Int.CeilToInt(max / Step - Vector3.one * .001f);
        for (int z = lo.z; z < hi.z; z++) for (int y = lo.y; y < hi.y; y++) for (int x = lo.x; x < hi.x; x++) Cells[new Vector3Int(x,y,z)] = color;
    }
    internal void Blob(Vector3 center, Vector3 radius, byte color, bool variation = true, float roughness = .1f)
    {
        var lo = Vector3Int.FloorToInt((center - radius * 1.12f) / Step);
        var hi = Vector3Int.CeilToInt((center + radius * 1.12f) / Step);
        for (int z = lo.z; z <= hi.z; z++) for (int y = lo.y; y <= hi.y; y++) for (int x = lo.x; x <= hi.x; x++)
        {
            var p = (new Vector3(x,y,z) + Vector3.one * .5f) * Step;
            var d = p - center;
            float q = d.x*d.x/(radius.x*radius.x) + d.y*d.y/(radius.y*radius.y) + d.z*d.z/(radius.z*radius.z);
            float n = Noise(p.x * 2.8f, p.y * 2.8f, p.z * 2.8f);
            if (q <= 1 + (n - .5f) * roughness * 2)
                Cells[new Vector3Int(x,y,z)] = (byte)(color + (variation ? Mathf.Clamp(Mathf.FloorToInt(Noise(p.x * 1.7f,p.y * 1.7f,p.z * 1.7f) * 3),0,2) : 0));
        }
    }
    internal void Branch(Vector3 from, Vector3 to, float radiusA, float radiusB, byte color)
    {
        int steps = Mathf.CeilToInt(Vector3.Distance(from,to) / (Step * .6f));
        for (int i=0;i<=steps;i++) { float t = (float)i/Mathf.Max(1,steps); float r = Mathf.Lerp(radiusA,radiusB,t); Blob(Vector3.Lerp(from,to,t),Vector3.one*r,color,false,0); }
    }
    internal EnchantedGroveVoxels Reduced(int factor)
    {
        if (factor == 1) return this;
        var output = new EnchantedGroveVoxels(Step * factor);
        var groups = new Dictionary<Vector3Int, Dictionary<byte,int>>();
        foreach (var cell in Cells)
        {
            var p = Vector3Int.FloorToInt((Vector3)cell.Key / factor);
            if (!groups.TryGetValue(p,out var counts)) groups.Add(p,counts = new Dictionary<byte,int>());
            counts.TryGetValue(cell.Value,out int count); counts[cell.Value] = count + 1;
        }
        foreach (var pair in groups) output.Cells[pair.Key] = pair.Value.OrderByDescending(c=>c.Value).ThenBy(c=>c.Key).First().Key;
        return output;
    }
    internal Mesh Mesh(string name,int maxQuadSize=int.MaxValue)
    {
        if (Cells.Count == 0) throw new InvalidOperationException("Empty voxel asset: " + name);
        var low = Cells.Keys.Aggregate(Vector3Int.Min); var high = Cells.Keys.Aggregate(Vector3Int.Max);
        var size = high-low+Vector3Int.one;
        var data = new byte[size.x*size.y*size.z];
        foreach (var cell in Cells) { var p=cell.Key-low;data[p.x+size.x*(p.y+size.y*p.z)] = cell.Value; }
        byte Get(Vector3Int p) => p.x<0||p.y<0||p.z<0||p.x>=size.x||p.y>=size.y||p.z>=size.z ? (byte)0 : data[p.x+size.x*(p.y+size.y*p.z)];
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        long area=0,surface=0;
        for(int d=0;d<3;d++)
        {
            int u=(d+1)%3,v=(d+2)%3,width=size[u],rows=size[v];var mask=new int[width*rows];var step=Vector3Int.zero;step[d]=1;
            for(int slice=-1;slice<size[d];slice++)
            {
                for(int j=0;j<rows;j++)for(int i=0;i<width;i++)
                {
                    var p=Vector3Int.zero;p[d]=slice;p[u]=i;p[v]=j;byte a=Get(p),b=Get(p+step);
                    int face=a!=0&&b==0?a:a==0&&b!=0?-b:0;mask[i+j*width]=face;if(face!=0)surface++;
                }
                for(int j=0;j<rows;j++)for(int i=0;i<width;)
                {
                    int color=mask[i+j*width];if(color==0){i++;continue;}
                    int w=1;while(w<maxQuadSize&&i+w<width&&mask[i+w+j*width]==color)w++;
                    int h=1;bool extend=true;
                    while(h<maxQuadSize&&j+h<rows&&extend){for(int k=0;k<w;k++)if(mask[i+k+(j+h)*width]!=color){extend=false;break;}if(extend)h++;}
                    Vector3 origin=low;origin[d]+=slice+1;origin[u]+=i;origin[v]+=j;
                    var du=Vector3.zero;du[u]=w;var dv=Vector3.zero;dv[v]=h;int first=vertices.Count;
                    vertices.Add(origin*Step);vertices.Add((origin+du)*Step);vertices.Add((origin+du+dv)*Step);vertices.Add((origin+dv)*Step);
                    var normal=Vector3.zero;normal[d]=color>0?1:-1;
                    for(int k=0;k<4;k++){normals.Add(normal);uv.Add(new Vector2((Math.Abs(color)+.5f)/64,.5f));}
                    if(color>0)triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});else triangles.AddRange(new[]{first,first+2,first+1,first,first+3,first+2});
                    for(int y=0;y<h;y++)for(int x=0;x<w;x++)mask[i+x+(j+y)*width]=0;
                    area+=(long)w*h;i+=w;
                }
            }
        }
        if(area!=surface)throw new InvalidOperationException("Greedy surface mismatch: "+name);
        for(int i=0;i<triangles.Count;i+=3)
        {
            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
            if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normals[a])<=0)throw new InvalidOperationException("Invalid winding: "+name);
        }
        var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
    }
}
