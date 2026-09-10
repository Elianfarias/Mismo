using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mismo.Gameplay.Player.World
{
    // Shared deterministic geometry for editor generation and runtime exploration.
    public class VoxelRegionGeometry
    {
        private readonly List<Vector3> vertices=new List<Vector3>();
        private readonly List<int> triangles=new List<int>();
        private readonly List<Color> colors=new List<Color>();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {
            int i=vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for(int j=0;j<4;j++) colors.Add(color);
            triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);
            triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);
        }
        public void Box(Vector3 center,Vector3 size,Color color)
        {
            Vector3 a=center-size*.5f,b=center+size*.5f;
            Quad(new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),b,new Vector3(b.x,b.y,a.z),color);
            Quad(a,new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(a.x,a.y,b.z),color);
            Quad(new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),b,new Vector3(b.x,a.y,b.z),color);
            Quad(new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z),a,color);
            Quad(new Vector3(b.x,a.y,b.z),b,new Vector3(a.x,b.y,b.z),new Vector3(a.x,a.y,b.z),color);
            Quad(a,new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,a.y,a.z),color);
        }
        public Mesh Mesh(string name)
        {
            var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);
            mesh.RecalculateNormals();mesh.RecalculateBounds(); return mesh;
        }
    }

    public class VoxelRegionHeightfield
    {
        public static readonly Vector3[] Sites={new Vector3(-50,4,-70),new Vector3(-30,6,-30),new Vector3(12,8,5),new Vector3(-8,10,50),new Vector3(40,12,80),new Vector3(66,9,8),new Vector3(40,12,100)};
        private static readonly float[] Radii={19,11,14,13,16,10,10};
        public static readonly int[,] Links={{0,1},{1,2},{2,3},{3,4},{2,5},{5,3},{4,6}};
        private readonly int seed,size; private readonly float relief,stepHeight;
        public VoxelRegionHeightfield(int seed,int size,float relief,float stepHeight) { this.seed=seed;this.size=size;this.relief=relief;this.stepHeight=stepHeight; }
        private float Noise(float x,float z,float scale)
        {
            float offset=(seed&65535)*.137f;
            return Mathf.PerlinNoise(x*scale+offset,z*scale+offset*.731f);
        }
        public virtual float Height(float x,float z)
        {
            float y=3+relief*(.45f*Noise(x,z,.014f)+.25f*Noise(x,z,.038f));
            float radius=Mathf.Max(Mathf.Abs(x),Mathf.Abs(z));
            float mountain=Mathf.SmoothStep(0,1,Mathf.InverseLerp(size*.34f,size*.49f,radius));
            y+=mountain*(12+24*Noise(x,z,.023f));
            for(int i=0;i<Sites.Length;i++)
            {
                var site=Sites[i]; float distance=Vector2.Distance(new Vector2(x,z),new Vector2(site.x,site.z));
                float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(Radii[i],Radii[i]+20,distance));
                y=Mathf.Lerp(y,site.y,weight);
            }
            float nearest=float.MaxValue,target=y;
            for(int i=0;i<Links.GetLength(0);i++)
            {
                Vector3 a=Sites[Links[i,0]],b=Sites[Links[i,1]];
                float t=Segment(x,z,a,b,out float distance);
                if(distance<nearest) { nearest=distance;target=Mathf.Lerp(a.y,b.y,t); }
            }
            y=Mathf.Lerp(y,target,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,11,nearest)));
            return Mathf.Round(y/stepHeight)*stepHeight;
        }
        public virtual float PathDistance(float x,float z)
        {
            float nearest=float.MaxValue;
            for(int i=0;i<Links.GetLength(0);i++)
            { Segment(x,z,Sites[Links[i,0]],Sites[Links[i,1]],out float d); nearest=Mathf.Min(nearest,d); }
            return nearest;
        }
        public virtual bool Reserved(float x,float z,float margin=0)
        {
            if(PathDistance(x,z)<6+margin)return true;
            for(int i=0;i<Sites.Length;i++)
                if(Vector2.Distance(new Vector2(x,z),new Vector2(Sites[i].x,Sites[i].z))<Radii[i]+margin)return true;
            return false;
        }
        public virtual Color Top(float x,float z,float height)
        {
            float variation=Noise(x,z,.7f);
            Color color=Color.Lerp(new Color(.28f,.51f,.16f),new Color(.43f,.65f,.23f),variation);
            if(PathDistance(x,z)<2.6f || Vector2.Distance(new Vector2(x,z),new Vector2(-50,-70))<10)
                color=Color.Lerp(new Color(.63f,.52f,.32f),new Color(.74f,.63f,.40f),variation);
            if(height>24)color=Color.Lerp(new Color(.40f,.46f,.43f),new Color(.59f,.62f,.54f),variation);
            if(height>37)color=Color.Lerp(color,new Color(.84f,.88f,.81f),.75f);
            return color;
        }
        private static float Segment(float x,float z,Vector3 a,Vector3 b,out float distance)
        {
            Vector2 p=new Vector2(x,z),start=new Vector2(a.x,a.z),delta=new Vector2(b.x-a.x,b.z-a.z);
            float t=Mathf.Clamp01(Vector2.Dot(p-start,delta)/delta.sqrMagnitude);
            distance=Vector2.Distance(p,start+delta*t);return t;
        }
    }
}
