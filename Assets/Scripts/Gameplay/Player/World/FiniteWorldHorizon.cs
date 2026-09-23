using UnityEngine;
using UnityEngine.Rendering;

namespace Mismo.Gameplay.Player.World
{
    /// <summary>Cheap distant terrain; streamed chunks mask it out without hiding other regions.</summary>
    public sealed class FiniteWorldHorizon : MonoBehaviour
    {
        Texture2D coverage;
        Material material;
        Mesh mesh;
        int originX,originZ;
        float viewDistance;
        UnityEngine.Camera viewCamera;
        float previousFar;
        public static Mesh BuildMesh(ExplorationTerrain field)
        {
            var bounds=field.Plan.Bounds;
            int x0=Mathf.FloorToInt(bounds.xMin/32),z0=Mathf.FloorToInt(bounds.yMin/32);
            int width=Mathf.CeilToInt(bounds.xMax/32)-x0,height=Mathf.CeilToInt(bounds.yMax/32)-z0;
            var elevations=new float[width+1,height+1];
            for(int z=0;z<=height;z++)for(int x=0;x<=width;x++)elevations[x,z]=Mathf.Max(0,field.Height((x0+x)*32,(z0+z)*32))-.15f;
            var geometry=new VoxelRegionGeometry();
            for(int z=0;z<height;z++)for(int x=0;x<width;x++)
            {
                float px=(x0+x)*32,pz=(z0+z)*32,cy=field.Height(px+16,pz+16);
                var color=cy<0?new Color(.12f,.35f,.46f,0):field.Top(px+16,pz+16,cy);
                if(cy<0&&field.GroveStyle!=null){color=field.GroveStyle.distantWater.linear;color.a=0;}
                geometry.Quad(new Vector3(px,elevations[x,z],pz),new Vector3(px,elevations[x,z+1],pz+32),
                    new Vector3(px+32,elevations[x+1,z+1],pz+32),new Vector3(px+32,elevations[x+1,z],pz),color);
            }
            return geometry.Mesh("Finite distant landscape");
        }
        public void Initialize(ExplorationTerrain field,Material terrainMaterial)
        {
            var bounds=field.Plan.Bounds;
            originX=Mathf.FloorToInt(bounds.xMin/32);originZ=Mathf.FloorToInt(bounds.yMin/32);
            int width=Mathf.CeilToInt(bounds.xMax/32)-originX,height=Mathf.CeilToInt(bounds.yMax/32)-originZ;
            coverage=new Texture2D(width,height,TextureFormat.RGBA32,false,true){name="Loaded terrain coverage",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            coverage.SetPixels(new Color[width*height]);coverage.Apply(false,false);
            material=new Material(terrainMaterial){name="Distant biome terrain"};
            material.SetFloat("_DistantTerrain",1);
            material.SetTexture("_TerrainCoverage",coverage);
            material.SetVector("_CoverageGrid",new Vector4(originX,originZ,width,height));
            mesh=BuildMesh(field);gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            viewDistance=bounds.size.magnitude+500;
            UpdateView();
        }
        public void SetChunkLoaded(Vector2Int chunk,bool loaded)
        {
            int x=chunk.x-originX,z=chunk.y-originZ;
            if(coverage==null||x<0||z<0||x>=coverage.width||z>=coverage.height)return;
            coverage.SetPixel(x,z,loaded?Color.white:Color.black);coverage.Apply(false,false);
        }
        public void UpdateView()
        {
            if(viewCamera==null){viewCamera=UnityEngine.Camera.main;if(viewCamera!=null)previousFar=viewCamera.farClipPlane;}
            if(viewCamera!=null)viewCamera.farClipPlane=Mathf.Max(previousFar,viewDistance);
            if(RenderSettings.fog)
            {
                RenderSettings.fogStartDistance=viewDistance*.45f;
                RenderSettings.fogEndDistance=viewDistance;
            }
        }
        void OnDestroy()
        {
            if(viewCamera!=null)viewCamera.farClipPlane=previousFar;
            Dispose(coverage);Dispose(material);Dispose(mesh);
        }
        static void Dispose(Object value)
        {if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
