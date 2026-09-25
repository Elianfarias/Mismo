using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.World.Structures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class StructureWorkshopChecks
{
    static readonly List<string> report=new List<string>();
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);report.Add("PASS "+message);}
    public static void Run()
    {
        if(!Application.isBatchMode||!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Run checks in an isolated batch validation project.");
        report.Clear();var d=AssetDatabase.LoadAssetAtPath<StructureDefinition>(StructureWorkshopSetup.DefRoot+"Cave_Moss.asset");
        var copy=Object.Instantiate(d);
        for(int seed=1;seed<=20;seed++){copy.layoutSeed=seed;copy.GenerateLayout();Require(copy.ValidateLayout()==null,"valid connected layout seed "+seed);}
        copy.layoutSeed=194;copy.GenerateLayout();string a=JsonUtility.ToJson(copy);copy.GenerateLayout();Require(a==JsonUtility.ToJson(copy),"layout deterministic");
        copy.connections.Clear();Require(copy.ValidateLayout()!=null,"disconnected graph rejected");Object.DestroyImmediate(copy);
        foreach(string name in new[]{"Cave_Moss","Cave_Crystal","Ruin_Grove","Temple_Grove"})CheckPrefab(name);
        WorldChecks(true);WorldChecks(false);File.WriteAllLines(StructureWorkshopSetup.Output+"/checks.txt",report);
    }
    static void CheckPrefab(string name)
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var definition=AssetDatabase.LoadAssetAtPath<StructureDefinition>(StructureWorkshopSetup.DefRoot+name+".asset");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(StructureBaker.PrefabRoot+name+".prefab");
        var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var instance=root.GetComponent<StructureInstance>();
        if(definition.style==StructureStyle.Temple)
        {
            Physics.SyncTransforms();var branch=definition.rooms.First(r=>r.role==StructureRoomRole.Branch);
            Require(Physics.Raycast(new Vector3(branch.center.x,1.5f,branch.center.y),Vector3.left,out _,branch.radius+2,~0,QueryTriggerInteraction.Ignore),"Temple lateral room has a closed outer wall");
        }
        var ground=new GameObject("Landscape");var landscape=StructureExterior.Ground(definition);ground.AddComponent<MeshFilter>().sharedMesh=landscape;ground.AddComponent<MeshCollider>().sharedMesh=landscape;ground.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/TerrainSurface.mat");
        Physics.SyncTransforms();
        foreach(var c in definition.connections)
        {
            var p=definition.rooms[c.from].center;var q=definition.rooms[c.to].center;var from=new Vector3(p.x,0,p.y);var to=new Vector3(q.x,0,q.y);var delta=to-from;
            Require(!Physics.CapsuleCast(from+Vector3.up*.6f,from+Vector3.up*1.7f,.38f,delta.normalized,out _,delta.magnitude,~0,QueryTriggerInteraction.Ignore),name+" clear corridor "+c.from+"-"+c.to);
        }
        foreach(var room in definition.rooms)
        {
            var p=new Vector3(room.center.x,1,room.center.y);
            Require(Physics.Raycast(p,Vector3.down,out var hit,2,~0,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y)<.12f,name+" floor "+room.name);
            if(definition.style==StructureStyle.Cave||definition.style==StructureStyle.Temple)Require(Physics.Raycast(p,Vector3.up,out _,12,~0,QueryTriggerInteraction.Ignore),name+" closed roof "+room.name);
        }
        var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(null,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
        var data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(Vector3.up*8,Vector3.one*100),Vector3.zero,Quaternion.identity);var handle=NavMesh.AddNavMeshData(data);
        try
        {
            var path=new NavMeshPath();Require(NavMesh.SamplePosition(instance.entrance,out var start,2,NavMesh.AllAreas)&&NavMesh.SamplePosition(instance.finalRoom,out var end,2,NavMesh.AllAreas)&&NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,name+" navigation entrance to final");
            Require(path.corners.Length>1,name+" nonempty navigation route");
        }
        finally{handle.Remove();Object.DestroyImmediate(data);}
        Require(instance.InteriorWeight(instance.finalRoom+Vector3.up)> .9f||definition.style!=StructureStyle.Cave,name+" final room ambient zone");
        Require(instance.InteriorWeight(new Vector3(0,30,0))==0,name+" exterior roof keeps daylight");
        Capture(root,definition,ground);
        Object.DestroyImmediate(root);Object.DestroyImmediate(ground);Object.DestroyImmediate(landscape);
    }
    static void WorldChecks(bool finite)
    {
        var template=AssetDatabase.LoadAssetAtPath<ExplorationWorldSettings>("Assets/Data/World/ExplorationWorldSettings.asset");var settings=Object.Instantiate(template);settings.preserveAuthoredCenter=!finite;settings.generationVersion=finite?2:0;settings.seed=7319;
        try
        {
            var terrain=new ExplorationTerrain(settings);var other=new ExplorationTerrain(settings);int count=0;
            if(finite)
            {
                Require(terrain.IceStructure.HasValue,"dedicated ice escarpment cave exists");
                Require(terrain.WoodlandStructure.HasValue,"woodland temple landmark exists");
                var ice=terrain.IceStructure.Value;Require(ice.cell==other.IceStructure.Value.cell,"ice landmark placement deterministic");
                float border=terrain.Plan.Layout.NorthBorder(ice.position.x);Require(!terrain.Plan.Walkable(ice.position.x,border),"ice boundary still blocks progression");
                Require(ice.position.z+ice.structure.prefab.finalRoom.z<border-40,"ice final room stays on accessible side");
            }
            var lines=new List<string>();
            for(int z=-18;z<40&&count<4;z++)for(int x=-18;x<40&&count<4;x++)
            {
                var site=terrain.Site(new Vector2Int(x,z));if(site.structure==null)continue;
                var same=other.Site(site.cell);Require(same.structure.id==site.structure.id&&same.position==site.position,"deterministic world structure "+site.cell);
                float y=terrain.Height(site.position.x,site.position.z);
                if(site.structure.prefab.style==StructureStyle.Temple)
                {Require(terrain.Height(site.position.x+23,site.position.z)<y-1,"temple basin carved below water");Require(terrain.Reserved(site.position.x+23,site.position.z),"temple water excludes vegetation");}
                foreach(var room in site.structure.prefab.rooms)
                {var p=site.position+room.center;Require(Mathf.Abs(terrain.Height(p.x,p.z)-y)<.26f,"flat foundation "+site.cell);Require(terrain.Reserved(p.x,p.z),"vegetation reservation "+site.cell);}
                lines.Add((finite?"finite":"authored centre")+" | "+site.structure.id+" | seed 7319 | "+site.position+" | radius "+site.radius);count++;
                if(count>=4)break;
            }
            Require(count>0,"structures present in "+(finite?"finite":"legacy")+" world");File.WriteAllLines(StructureWorkshopSetup.Output+"/world-locations-"+(finite?"finite":"legacy")+".txt",lines);
            if(finite){CaptureWorld(terrain,terrain.IceStructure.Value,"IceEscarpment-world");CaptureWorld(terrain,terrain.WoodlandStructure.Value,"Temple_Grove-world");}
        }
        finally{Object.DestroyImmediate(settings);}
    }
    static void Capture(GameObject root,StructureDefinition d,GameObject ground)
    {
        var sun=new GameObject("Capture sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(45,-28,0);
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.32f,.37f,.40f);RenderSettings.ambientEquatorColor=new Color(.22f,.25f,.28f);RenderSettings.ambientGroundColor=new Color(.1f,.12f,.14f);RenderSettings.fog=false;
        var camera=new GameObject("Capture camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.27f,.32f);camera.farClipPlane=180;camera.nearClipPlane=.08f;camera.fieldOfView=65;
        camera.transform.position=d.style==StructureStyle.Temple?new Vector3(49,38,-56):new Vector3(42,28,-46);camera.transform.LookAt(new Vector3(0,d.style==StructureStyle.Temple?9:4,0));Render(camera,d.name+"-exterior");
        var room=d.rooms[d.EntranceIndex];camera.transform.position=new Vector3(room.center.x,2.1f,room.center.y-1);
        var next=d.rooms[1];camera.transform.LookAt(new Vector3(next.center.x,2.2f,next.center.y));
        if(d.style==StructureStyle.Cave){RenderSettings.ambientSkyColor*=.15f;RenderSettings.ambientEquatorColor*=.15f;RenderSettings.ambientGroundColor*=.15f;}
        Render(camera,d.name+"-interior");
        var final=d.rooms[d.FinalIndex];camera.transform.position=new Vector3(final.center.x-3,2.3f,final.center.y-4);camera.transform.LookAt(new Vector3(final.center.x,2,final.center.y+2));Render(camera,d.name+"-final");
        Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(sun.gameObject);
    }
    static void CaptureWorld(ExplorationTerrain field,WorldSite site,string filename)
    {
        var root=new GameObject("World composition capture");var meshes=new List<Mesh>();
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/TerrainSurface.mat");var center=ExplorationChunks.Coordinate(site.position);
        try
        {
            for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++)
            {var mesh=ExplorationChunks.BuildTerrain(field,center+new Vector2Int(x,z));meshes.Add(mesh);var go=new GameObject("Terrain");go.transform.SetParent(root.transform);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;}
            var p=site.position;p.y=field.Height(p.x,p.z)+.05f;
            Object.Instantiate(site.structure.prefab,p,Quaternion.identity,root.transform);
            var sun=new GameObject("Sun").AddComponent<Light>();sun.transform.SetParent(root.transform);sun.type=LightType.Directional;sun.intensity=1.3f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(43,-28,0);
            RenderSettings.ambientSkyColor=new Color(.32f,.37f,.40f);RenderSettings.ambientEquatorColor=new Color(.22f,.25f,.28f);RenderSettings.ambientGroundColor=new Color(.1f,.12f,.14f);
            var camera=new GameObject("World camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.34f,.46f,.52f);camera.farClipPlane=340;camera.nearClipPlane=.12f;camera.fieldOfView=65;
            camera.transform.position=p+new Vector3(57,43,-65);camera.transform.LookAt(p+new Vector3(0,12,4));Render(camera,filename);
        }
        finally{Object.DestroyImmediate(root);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);}
    }
    public static void Render(Camera camera,string name)
    {
        bool batching=GraphicsSettings.useScriptableRenderPipelineBatching;GraphicsSettings.useScriptableRenderPipelineBatching=false;
        foreach(var r in Object.FindObjectsByType<MeshRenderer>())if(r.sharedMaterial!=null&&r.sharedMaterial.HasProperty("_BaseMap"))
        {var b=new MaterialPropertyBlock();b.SetTexture("_BaseMap",r.sharedMaterial.GetTexture("_BaseMap")??Texture2D.whiteTexture);b.SetColor("_BaseColor",r.sharedMaterial.GetColor("_BaseColor"));r.SetPropertyBlock(b);}
        var target=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(StructureWorkshopSetup.Output+"/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);GraphicsSettings.useScriptableRenderPipelineBatching=batching;}
    }
    public static void BuildBatch()
    {
        try
        {
            Directory.CreateDirectory(StructureWorkshopSetup.Output+"/windows-build");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=StructureWorkshopSetup.Output+"/windows-build/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText(StructureWorkshopSetup.Output+"/build.txt",result.summary.result+" errors="+result.summary.totalErrors);EditorApplication.Exit(result.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:1);
        }
        catch(Exception e){File.WriteAllText(StructureWorkshopSetup.Output+"/build.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void CheckBatch()
    {try{Run();Debug.Log("STRUCTURE_CHECKS_OK");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
