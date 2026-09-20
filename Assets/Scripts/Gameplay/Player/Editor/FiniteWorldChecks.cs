using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class FiniteWorldChecks
    {
        static int checks;
        static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException(message);checks++;}

        [MenuItem("Mismo/World/Verify finite generation")]
        public static void Run()
        {
            checks=0;
            var template=Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings");
            Check(template!=null,"World settings exist");
            var settings=Object.Instantiate(template);
            try
            {
                settings.preserveAuthoredCenter=false;settings.generationVersion=2;
                for(int seed=1;seed<=24;seed++)
                {
                    settings.seed=seed*7319;
                    var terrain=new ExplorationTerrain(settings);var plan=terrain.Plan;
                    CheckGuidedTopology(terrain);
                    var start=terrain.Site(Vector2Int.zero);
                    Check(start.kind==WorldSiteKind.Village&&plan.Stage(start.position.x,start.position.z)==0,"Safe start in first stage");
                    var arrival=start.position+settings.content.VillageArrivalOffset;
                    Check(plan.Contains(arrival.x,arrival.z,80),"Arrival inside continent");
                    Check(Mathf.Abs(terrain.Height(arrival.x,arrival.z)-start.position.y)<=.25f,"Arrival on village apron");
                    for(int stage=0;stage<4;stage++)
                    {
                        var site=terrain.Site(plan.ObjectiveCell(stage));
                        Check(site.kind==WorldSiteKind.BossArena&&plan.Stage(site.position.x,site.position.z)==stage,"Exactly positioned regional objective, seed "+settings.seed+", stage "+stage);
                        Check(plan.Contains(site.position.x,site.position.z,80),"Objective has land clearance");
                        Check(Mathf.Abs(terrain.Height(site.position.x,site.position.z)-site.position.y)<=.25f,"Objective level with ground");
                        Check(settings.content.Encounter(WorldSiteKind.BossArena,terrain.Biome(site.position.x,site.position.z),plan.MinimumLevel(site.position.x,site.position.z),site.position.y,seed)!=null,"Existing catalog supplies regional guardian");
                    }
                    int previousStage=0;
                    for(int segment=0;segment<plan.RoutePointCount-1;segment++)
                    {
                        Vector2 a=plan.RoutePoint(segment),b=plan.RoutePoint(segment+1);
                        int steps=Mathf.CeilToInt(Vector2.Distance(a,b));float last=terrain.Height(a.x,a.y);
                        for(int i=0;i<=steps;i++)
                        {
                            var p=Vector2.Lerp(a,b,i/(float)steps);int stage=plan.Stage(p.x,p.y);
                            Check(plan.Contains(p.x,p.y,80),"Route remains inland");
                            Check(stage>=previousStage&&stage<=previousStage+1,"Route follows ordered stages");previousStage=stage;
                            float height=terrain.Height(p.x,p.y);
                            Check(Mathf.Abs(height-last)<=.5f,"Route step traversable, seed "+settings.seed+", point "+p+", height "+last+" -> "+height);last=height;
                            Check(terrain.Reserved(p.x,p.y,2),"Route reserved from scattering");
                            Check(plan.Walkable(p.x,p.y),"Guided route passes only through open crossings");
                        }
                    }
                    Check(previousStage==3,"Route reaches final stage");
                    Check(!plan.ContainsChunk(new Vector2Int(10000,10000)),"Streaming has finite bounds");
                    Check(terrain.Biome(plan.MinX-200,0)==WorldBiome.Ocean&&terrain.Reserved(plan.MinX-200,0),"Ocean excludes content");
                    var from=new Vector3(start.position.x,12,start.position.z);
                    foreach(var end in new[]{new Vector3(-10000,12,0),new Vector3(10000,12,0),new Vector3(from.x,12,10000),new Vector3(from.x,12,-10000)})
                    {
                        var limited=plan.ConstrainMovement(from,end);
                        Check(plan.Contains(limited.x,limited.z,23.99f),"Movement and dash cannot cross coastline");
                    }
                    var restored=Object.Instantiate(template);
                    try
                    {
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(settings),restored);
                        var same=new ExplorationTerrain(restored);
                        for(int i=0;i<30;i++)
                        {
                            float x=i*117,z=(i%7-3)*87;
                            Check(terrain.Height(x,z)==same.Height(x,z)&&terrain.Biome(x,z)==same.Biome(x,z),"Saved settings reconstruct identical world");
                        }
                    }
                    finally{Object.DestroyImmediate(restored);}
                }
                settings.seed=7319;
                var field=new ExplorationTerrain(settings);
                var meshA=ExplorationChunks.BuildTerrain(field,new Vector2Int(42,1));
                var meshB=ExplorationChunks.BuildTerrain(new ExplorationTerrain(settings),new Vector2Int(42,1));
                Check(meshA.vertices.SequenceEqual(meshB.vertices)&&meshA.colors.SequenceEqual(meshB.colors)&&meshA.triangles.SequenceEqual(meshB.triangles),"Chunk regeneration deterministic");
                Object.DestroyImmediate(meshA);Object.DestroyImmediate(meshB);
                CheckNavigation(field);
                ExportPreview(field);
                CheckSaveCompatibility(settings);
                var other=Object.Instantiate(settings);other.seed=14638;
                Check(new ExplorationTerrain(other).Height(1600,100)!=field.Height(1600,100),"Different seeds change terrain");
                Object.DestroyImmediate(other);
                settings.generationVersion=0;
                Check(new ExplorationTerrain(settings).Plan==null,"Original procedural saves retain old generator");
                settings.generationVersion=2;settings.preserveAuthoredCenter=true;
                Check(new ExplorationTerrain(settings).Plan==null,"Authored centre retains old generator");
                // No writes to player profiles or scenes. Invoke the project-wide layout check too.
                var layout=Type.GetType("ProjectOrganizationChecks, Assembly-CSharp-Editor",true);
                layout.GetMethod("Run").Invoke(null,null);
                Directory.CreateDirectory("output/finite-world");
                File.WriteAllText("output/finite-world/checks.txt","PASS: "+checks+" assertions; 24 seeds; ordered stages, inland connected route, reachable objectives, movement bounds, deterministic terrain and saves, legacy generators, real NavMesh paths, project organization.\n");
                Debug.Log("FINITE_WORLD_PASS "+checks);
            }
            finally{Object.DestroyImmediate(settings);}
        }
        static void CheckSaveCompatibility(ExplorationWorldSettings settings)
        {
            var current=typeof(WorldSession).GetProperty("Current");var previous=WorldSession.Current;
            string json=JsonUtility.ToJson(settings);
            try
            {
                current.SetValue(null,new WorldSaveData{seed=settings.seed,legacy=false,settingsJson=json});
                var restored=WorldSession.Settings(settings);
                Check(restored.UsesFiniteWorld,"Continuing new world keeps finite version");Object.DestroyImmediate(restored);
                current.SetValue(null,new WorldSaveData{seed=settings.seed,legacy=false,settingsJson=json.Replace("\"generationVersion\":2,","")});
                restored=WorldSession.Settings(settings);
                Check(restored.generationVersion==0&&new ExplorationTerrain(restored).Plan==null,"Save without generator version ignores newer template default");Object.DestroyImmediate(restored);
                current.SetValue(null,new WorldSaveData{seed=settings.seed,legacy=false,settingsJson=json.Replace("\"generationVersion\":2,","\"generationVersion\":1,")});
                restored=WorldSession.Settings(settings);
                Check(restored.generationVersion==1&&new ExplorationTerrain(restored).Plan.Layout==null,"Version 1 saves retain their original finite layout");Object.DestroyImmediate(restored);
                var actor=new GameObject("Finite level check");
                try
                {
                    var inventory=actor.AddComponent<Equipment.Inventory.PlayerInventory>();
                    var profile=new Equipment.Inventory.InventoryProfile();profile.progression.level=100;
                    profile.regions.Add(new Equipment.Inventory.DiscoveredRegion{id="planned",minimumLevel=16});
                    typeof(Equipment.Inventory.PlayerInventory).GetField("profile",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(inventory,profile);
                    var identity=actor.AddComponent<WorldEnemyIdentity>();identity.Configure("test","planned","test",inventory,settings);
                    Check(identity.Level==16,"Finite encounter does not scale up when player outlevels region");
                }
                finally{Object.DestroyImmediate(actor);}
            }
            finally{current.SetValue(null,previous);}
        }
        static void CheckGuidedTopology(ExplorationTerrain terrain)
        {
            var plan=terrain.Plan;var layout=plan.Layout;
            var meadow=plan.SitePosition(plan.ObjectiveCell(0));var desert=plan.SitePosition(plan.ObjectiveCell(1));
            var ice=plan.SitePosition(plan.ObjectiveCell(2));var mountains=plan.SitePosition(plan.ObjectiveCell(3));
            Check(desert.x>meadow.x&&ice.y>meadow.y&&ice.x<desert.x&&mountains.x>ice.x&&mountains.y>desert.y,"Concept-map quadrants and northward turn");
            float border=layout.NorthBorder(meadow.x);
            Check(plan.Stage(meadow.x,border-50)==0&&plan.Stage(meadow.x,border+50)==2,"Ice directly neighbours meadow to the north");
            Check(terrain.Height(meadow.x,border+50)-terrain.Height(meadow.x,border-50)>60,"Glacier visibly rises above meadow");
            for(float x=plan.Bounds.xMin;x<plan.Bounds.xMax;x+=24)
            {
                float z=layout.NorthBorder(x);
                if(!plan.Contains(x,z,90)||Mathf.Abs(x-layout.DesertPassX)<90)continue;
                var from=new Vector3(x,400,z-45);var to=new Vector3(x,400,z+45);
                Check(plan.ConstrainMovement(from,to).z<z,"North escarpment blocks even airborne dashes outside desert pass");
                Check(plan.ConstrainMovement(to,from).z>z,"Escarpment cannot be bypassed in reverse");
            }
            foreach(var gate in new[]{layout.DesertPass,layout.MountainPass})
            {
                var axis=gate==layout.DesertPass?Vector3.forward:Vector3.right;
                var center=new Vector3(gate.x,100,gate.y);var from=center-axis*50;var to=center+axis*50;
                Check(Vector3.Distance(plan.ConstrainMovement(from,to),to)<.01f,"Designated pass accepts traversal");
            }
            float forbiddenZ=layout.MountainPassZ-240,xBorder=layout.MountainBorder(forbiddenZ);
            var west=new Vector3(xBorder-45,400,forbiddenZ);var east=new Vector3(xBorder+45,400,forbiddenZ);
            Check(plan.ConstrainMovement(west,east).x<xBorder,"Mountain ridge blocks shortcuts away from its pass");
        }
        static void CheckNavigation(ExplorationTerrain field)
        {
            for(int segment=1;segment<field.Plan.RoutePointCount-1;segment++)
            {
                Vector2 a=field.Plan.RoutePoint(segment),b=field.Plan.RoutePoint(segment+1),middle=(a+b)*.5f,direction=(b-a).normalized;
                var cell=ExplorationChunks.Coordinate(new Vector3(middle.x,0,middle.y));
                var meshes=new List<Mesh>();var sources=new List<NavMeshBuildSource>();
                NavMeshData data=null;NavMeshDataInstance instance=default;
                try
                {
                    for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
                    {
                        var mesh=ExplorationChunks.BuildTerrain(field,cell+new Vector2Int(x,z));meshes.Add(mesh);
                        sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,sourceObject=mesh,transform=Matrix4x4.identity,area=0});
                    }
                    data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(new Vector3(middle.x,160,middle.y),new Vector3(110,500,110)),Vector3.zero,Quaternion.identity);
                    Check(data!=null,"Route builds navigation");instance=NavMesh.AddNavMeshData(data);
                    var start=middle-direction*20;var end=middle+direction*20;
                    Check(NavMesh.SamplePosition(new Vector3(start.x,field.Height(start.x,start.y),start.y),out var startHit,2,NavMesh.AllAreas),"Route start on navigation");
                    Check(NavMesh.SamplePosition(new Vector3(end.x,field.Height(end.x,end.y),end.y),out var endHit,2,NavMesh.AllAreas),"Route end on navigation");
                    var path=new NavMeshPath();
                    Check(NavMesh.CalculatePath(startHit.position,endHit.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Continuous navigation across generated chunks");
                }
                finally{if(instance.valid)instance.Remove();if(data!=null)Object.DestroyImmediate(data);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);}
            }
        }
        static void ExportPreview(ExplorationTerrain field)
        {
            const int width=1000,height=850;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            var bounds=field.Plan.Bounds;
            try
            {
                var colors=new Color[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    float px=Mathf.Lerp(bounds.xMin,bounds.xMax,x/(float)(width-1)),pz=Mathf.Lerp(bounds.yMin,bounds.yMax,y/(float)(height-1));
                    float elevation=field.Height(px,pz);var color=field.Top(px,pz,elevation);
                    if(elevation<0)color=new Color(.10f,.28f,.37f);
                    else color*=Mathf.Lerp(.82f,1.18f,Mathf.Clamp01((elevation-field.Height(px+4,pz+4)+4)/8));
                    if(field.Plan.Contains(px,pz)&&field.PathDistance(px,pz)<7)color=new Color(1,.79f,.27f);
                    for(int i=0;i<4;i++)if(Vector2.Distance(new Vector2(px,pz),field.Plan.SitePosition(field.Plan.ObjectiveCell(i)))<18)color=new Color(1,.35f,.2f);
                    color.a=1;colors[y*width+x]=color;
                }
                texture.SetPixels(colors);texture.Apply();Directory.CreateDirectory("output/finite-world");
                File.WriteAllBytes("output/finite-world/guided-seed-7319.png",texture.EncodeToPNG());
            }
            finally{Object.DestroyImmediate(texture);}
        }
        public static void RunBatch()
        {
            try{Run();EditorApplication.Exit(0);}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BuildBatch()
        {
            try
            {
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                    scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                    locationPathName="output/finite-world/build/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Finite-world build: "+report.summary.result);
                Debug.Log("FINITE_WORLD_BUILD_PASS");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
