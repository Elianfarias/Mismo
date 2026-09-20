using System.Collections;
using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public sealed partial class WorldMapPanel
    {
        struct MapSample { public float height,path; public WorldBiome biome; }
        readonly Dictionary<Vector2,MapSample> mapSamples=new Dictionary<Vector2,MapSample>();
        MapSample SampleMap(float x,float z)
        {
            var key=new Vector2(x,z);
            if(mapSamples.TryGetValue(key,out var sample))return sample;
            sample=new MapSample{height=Mathf.Floor(terrain.Height(x,z)*2)*.5f,path=terrain.PathDistance(x,z),biome=terrain.Biome(x,z)};
            if(terrain.Plan!=null)sample.height=Mathf.Max(FiniteWorldPlan.SeaLevel,sample.height);
            mapSamples.Add(key,sample);return sample;
        }
        IEnumerator Build()
        {
            // Cache land beyond the visible sheet so the camera can move without rebuilding it.
            float viewExtent=TerrainExtent,extent=viewExtent+Mathf.Max(32,viewExtent*.5f);
            bool open=IsOpen;int resolution=open?224:128;
            if(mapSamples.Count>65536)mapSamples.Clear();
            int discoveryRevision=exploration.Revision;
            var discovery=new MapExploration(exploration.Snapshot());
            float step=Mathf.Max(1,Mathf.Pow(2,Mathf.Ceil(Mathf.Log(extent*2/resolution,2))));
            var center=ViewCenter;float x0=Mathf.Floor((center.x-extent)/step)*step,z0=Mathf.Floor((center.y-extent)/step)*step;
            int count=Mathf.CeilToInt(extent*2/step);
            // Always yield before completion so StartCoroutine cannot leave a completed handle assigned.
            yield return null;
            var heights=new float[count+1,count+1];var known=new bool[count+1,count+1];
            var geometry=new VoxelRegionGeometry();var budget=System.Diagnostics.Stopwatch.StartNew();
            float floor=8;
            for(int z=0;z<=count;z++)
            {
                for(int x=0;x<=count;x++)
                {
                    float px=x0+x*step,pz=z0+z*step;known[x,z]=discovery.Contains(px,pz);
                    heights[x,z]=known[x,z]?SampleMap(px,pz).height:8;floor=Mathf.Min(floor,heights[x,z]);
                }
                if(budget.ElapsedMilliseconds>=3){yield return null;budget.Restart();}
            }
            floor-=Mathf.Max(2,extent*.008f);
            for(int z=0;z<count;z++)
            {
                for(int x=0;x<count;x++)
                {
                    float px=x0+x*step,pz=z0+z*step,y=heights[x,z];bool discovered=known[x,z];
                    Color color=new Color(.10f,.14f,.18f);
                    if(discovered)
                    {
                        var sample=SampleMap(px,pz);var biome=sample.biome;
                        color=biome==WorldBiome.Forest?new Color(.30f,.61f,.25f):biome==WorldBiome.Highlands?new Color(.48f,.65f,.31f):new Color(.52f,.76f,.29f);
                        color=Color.Lerp(color,new Color(.72f,.75f,.68f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(38,48,y)));
                        if(sample.path<Mathf.Max(2.7f,Mathf.Min(step*.4f,8)))color=new Color(.88f,.75f,.45f);
                        if(terrain.Plan!=null){color=terrain.Top(px,pz,y);color.a=1;}
                        float hollow=Mathf.Max(heights[x+1,z],heights[x,z+1])-y;
                        color*=Mathf.Lerp(1,.76f,Mathf.Clamp01(hollow/4));color.a=1;
                    }
                    geometry.Quad(new Vector3(px,y,pz),new Vector3(px,y,pz+step),new Vector3(px+step,y,pz+step),new Vector3(px+step,y,pz),color);
                    float other=heights[x+1,z];
                    if(Mathf.Abs(other-y)>.01f)geometry.Quad(new Vector3(px+step,other,pz),new Vector3(px+step,y,pz),new Vector3(px+step,y,pz+step),new Vector3(px+step,other,pz+step),color);
                    other=heights[x,z+1];
                    if(Mathf.Abs(other-y)>.01f)geometry.Quad(new Vector3(px,other,pz+step),new Vector3(px,y,pz+step),new Vector3(px+step,y,pz+step),new Vector3(px+step,other,pz+step),color);
                    var edge=discovered?new Color(.35f,.28f,.19f):new Color(.075f,.10f,.13f);
                    if(x==0)geometry.Quad(new Vector3(px,floor,pz),new Vector3(px,y,pz),new Vector3(px,y,pz+step),new Vector3(px,floor,pz+step),edge);
                    if(z==0)geometry.Quad(new Vector3(px,floor,pz),new Vector3(px+step,floor,pz),new Vector3(px+step,y,pz),new Vector3(px,y,pz),edge);
                    if(x==count-1)geometry.Quad(new Vector3(px+step,floor,pz),new Vector3(px+step,y,pz),new Vector3(px+step,y,pz+step),new Vector3(px+step,floor,pz+step),edge);
                    if(z==count-1)geometry.Quad(new Vector3(px,floor,pz+step),new Vector3(px,y,pz+step),new Vector3(px+step,y,pz+step),new Vector3(px+step,floor,pz+step),edge);
                }
                if(budget.ElapsedMilliseconds>=3){yield return null;budget.Restart();}
            }
            // Vegetation positions are anchored to the world's 8 m grid, independently of terrain LOD.
            float treeStep=Mathf.Max(8,step*2);
            if(step<=32)
            for(float pz=Mathf.Ceil(z0/treeStep)*treeStep;pz<z0+count*step;pz+=treeStep)
            {
                for(float px=Mathf.Ceil(x0/treeStep)*treeStep;px<x0+count*step;px+=treeStep)
                {
                    if(!discovery.Contains(px,pz))continue;
                    uint hash=(uint)ExplorationTerrain.Hash(settings.seed,Mathf.FloorToInt(px),Mathf.FloorToInt(pz),301);
                    if(hash/(float)uint.MaxValue>=settings.treeDensity*terrain.TreeDensity(px,pz)||terrain.Reserved(px,pz,3))continue;
                    float y=Mathf.Floor(terrain.Height(px,pz)*2)*.5f;if(Mathf.Abs(y-terrain.Height(px+2,pz+2))>1.5f)continue;
                    float size=Mathf.Lerp(4.5f,6.5f,(hash%127)/126f);
                    var trunk=new Color(.40f,.26f,.12f);var leaf=Color.Lerp(new Color(.10f,.34f,.20f),new Color(.24f,.57f,.18f),(hash%31)/30f);
                    geometry.Box(new Vector3(px,y+2,pz),new Vector3(.8f,4,.8f),trunk);
                    if(open&&step<=4)
                    {
                        // Stepped, rounded crowns remain recognizable in the closest map view.
                        for(int tier=0;tier<5;tier++)
                        {
                            float width=size*(tier==0?.65f:tier==1?.9f:tier==2?1:tier==3?.78f:.45f);
                            var tint=leaf*Mathf.Lerp(.88f,1.17f,tier/4f);tint.a=1;
                            geometry.Box(new Vector3(px,y+3+tier*.8f,pz),new Vector3(width,.8f,width*.65f),tint);
                            geometry.Box(new Vector3(px,y+3+tier*.8f,pz),new Vector3(width*.65f,.8f,width),tint);
                        }
                    }
                    else
                    {
                        geometry.Box(new Vector3(px,y+3.5f,pz),new Vector3(size,1.5f,size),leaf);
                        geometry.Box(new Vector3(px,y+5,pz),new Vector3(size*.65f,1.5f,size*.65f),leaf);
                    }
                }
                if(budget.ElapsedMilliseconds>=3){yield return null;budget.Restart();}
            }
            var nextSites=new List<WorldSite>();int spacing=terrain.SiteSpacing;
            int radius=Mathf.CeilToInt(extent/spacing)+1;var cell=new Vector2Int(Mathf.FloorToInt(center.x/spacing),Mathf.FloorToInt(center.y/spacing));
            for(int z=-radius;z<=radius;z++)
            {
                for(int x=-radius;x<=radius;x++)
                {

                    // Unknown regions do not expose points of interest through labels.
                    var site=terrain.Site(cell+new Vector2Int(x,z));
                    if(site.kind==WorldSiteKind.Village&&terrain.IsExterior(site.position.x,site.position.z)&&discovery.Contains(site.position.x,site.position.z))nextSites.Add(site);
                }
                if(budget.ElapsedMilliseconds>=3){yield return null;budget.Restart();}
            }
            if(settings.preserveAuthoredCenter&&discovery.Contains(VoxelRegionHeightfield.Sites[0].x,VoxelRegionHeightfield.Sites[0].z))
                nextSites.Add(new WorldSite{kind=WorldSiteKind.Village,position=VoxelRegionHeightfield.Sites[0],cell=new Vector2Int(int.MinValue,0)});
            // A wheel gesture may have requested a different extent while this build was running.
            // Keep the previous complete view instead of publishing an obsolete intermediate zoom.
            // Preserve the sheet's world-space edges throughout a right-drag gesture.
            while(dragging&&dragButton==1)yield return null;
            if(open!=IsOpen||!Mathf.Approximately(viewExtent,TerrainExtent)){builder=null;dirty=true;yield break;}
            var previous=mesh;mesh=geometry.Mesh("Explored map relief");surface.GetComponent<MeshFilter>().sharedMesh=mesh;if(previous!=null)Destroy(previous);
            sites.Clear();sites.AddRange(nextSites);builtCenter=center;builtZoom=viewExtent;builtOpen=open;
            displayedExploration=discovery;builtDiscoveryRevision=discoveryRevision;
            builtFootprint=new Rect(x0,z0,count*step,count*step);builder=null;
            // Do not retain rebuild requests already satisfied by this snapshot.
            dirty=NeedsBuild();
        }
    }
}



