using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Core;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

public static class VillageRoutineChecks
{
    static bool buildStreets;
    [InitializeOnLoadMethod] static void Register(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
    [MenuItem("Mismo/NPCs/Verificar pueblo y actualizar senderos")]
    public static void UpdateStreets(){buildStreets=true;Run();}
    static void Poll()
    {
        string request=Path.GetFullPath(Application.dataPath+"/../Temp/VillageRoutineChecks.request");
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
        buildStreets=File.ReadAllText(request).Trim()=="streets";
        File.Delete(request);
        try { Run(); } catch(Exception e){File.WriteAllText(VillageHumanoidUpgrade.Output+"/navigation.txt",e.ToString());Debug.LogException(e);}
    }
    public static void Run()
    {
        var report=new List<string>();
        foreach(int seed in new[]{7319,12,654321})Check(seed,report);
        File.WriteAllLines(VillageHumanoidUpgrade.Output+"/navigation.txt",new[]{"PASS: complete entrance/workplace/walking routes, duplicate prevention, world scale, feet and animation."}.Concat(report));
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Check(int seed,List<string> report)
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var meshes=new List<Mesh>();NavMeshData data=null;NavMeshDataInstance nav=default;
        var settings=Object.Instantiate(ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.seed=seed;settings.preserveAuthoredCenter=false;
        var terrain=new ExplorationTerrain(settings);var origin=terrain.Site(Vector2Int.zero).position;
        var center=ExplorationChunks.Coordinate(origin);
        var root=new GameObject("Village route check");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++)
            {
                var tile=new GameObject("Terrain");tile.transform.SetParent(root.transform);
                var mesh=ExplorationChunks.BuildTerrain(terrain,center+new Vector2Int(x,z));meshes.Add(mesh);
                tile.AddComponent<MeshFilter>().sharedMesh=mesh;tile.AddComponent<MeshRenderer>().sharedMaterial=ProjectAssets.Load<Material>("TerrainSurface");tile.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            var town=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/Villages/MedievalVillage.prefab"),root.transform);
            town.transform.position=new Vector3(origin.x,terrain.Height(origin.x,origin.z)+settings.content.villageGroundOffset*settings.content.villageSizeMultiplier,origin.z);
            town.transform.localScale*=settings.content.villageSizeMultiplier;
            VillageNpcSpawner.Populate(town,terrain,true);VillageNpcSpawner.Populate(town,terrain,true);
            var population=QuestCatalog.Load().villageNpcs;var residents=town.GetComponentsInChildren<VillageNpcRoutine>();
            Require(residents.Length==population.residents.Length,"Duplicate residents");
            var sources=new List<NavMeshBuildSource>();
            // Preview-scene colliders are outside the default physics scene used by CollectSources.
            foreach(var collider in root.GetComponentsInChildren<MeshCollider>())
                sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,sourceObject=collider.sharedMesh,transform=collider.transform.localToWorldMatrix,area=0});
            data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(origin,new Vector3(220,200,220)),Vector3.zero,Quaternion.identity);nav=NavMesh.AddNavMeshData(data);
            var entry=origin+settings.content.VillageArrivalOffset;entry.y=terrain.Height(entry.x,entry.z);
            Require(NavMesh.SamplePosition(entry,out var entrance,3,NavMesh.AllAreas),"Entrance missing: "+entry+" sources="+sources.Count);
            var streets=new List<Vector3[]>();
            if(seed==7319)
            {
                var grid=new List<string>();
                for(int z=-12;z<=14;z+=2)for(int x=-12;x<=12;x+=2)
                {
                    var spot=town.transform.TransformPoint(new Vector3(x,0,z));spot.y=terrain.Height(spot.x,spot.z);
                    var path=new NavMeshPath();
                    if(NavMesh.SamplePosition(spot,out var hit,.8f,NavMesh.AllAreas)&&Mathf.Abs(hit.position.y-spot.y)<.5f&&NavMesh.CalculatePath(entrance.position,hit.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)grid.Add(x+","+z);
                }
                File.WriteAllLines(VillageHumanoidUpgrade.Output+"/walkable-local-points.txt",grid);
            }
            for(int i=0;i<residents.Length;i++)
            {
                var npc=residents[i];var p=npc.transform.position;
                Require(npc.animator!=null&&npc.animator.isHuman,"Resident Animator missing");
                npc.animator.Rebind();npc.animator.Play("Idle");npc.animator.Update(0);
                Require(Mathf.Abs(p.y-terrain.Height(p.x,p.z))<.04f,"Feet placement "+npc.name);
                var body=npc.GetComponent<CapsuleCollider>();Require(Mathf.Abs(body.bounds.size.y-2.8f)<.02f,"Adult height");
                Require(NavMesh.SamplePosition(p,out var home,2,NavMesh.AllAreas),"Home off navigation: "+npc.name);
                foreach(var local in population.residents[i].route.Prepend(population.residents[i].localPosition))
                {
                    var destination=town.transform.TransformPoint(local);destination.y=terrain.Height(destination.x,destination.z);
                    Require(NavMesh.SamplePosition(destination,out var target,1.5f,NavMesh.AllAreas),"Stop off navigation "+npc.name+" "+local);
                    var path=new NavMeshPath();
                    Require(NavMesh.CalculatePath(entrance.position,target.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Unreachable stop "+npc.name+" "+local);
                    streets.Add(path.corners);
                    Require(NavMesh.CalculatePath(home.position,target.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Disconnected home "+npc.name);
                }
                report.Add("seed="+seed+" "+npc.name+": 2.8m adult, all stops reachable");
            }
            if(seed==7319)
            {
                if(buildStreets)BuildStreets(town,streets);
                File.WriteAllLines(VillageHumanoidUpgrade.Output+"/visual-bounds.txt",residents.SelectMany(n=>n.GetComponentsInChildren<Renderer>().Select(r=>n.name+" scene="+n.gameObject.scene.name+" root="+n.transform.position+" renderer="+r.bounds+" enabled="+r.enabled)));
                Capture(root,town,residents,scene);
            }
        }
        finally{nav.Remove();if(data!=null)Object.DestroyImmediate(data);EditorSceneManager.ClosePreviewScene(scene);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);Object.DestroyImmediate(settings);}
    }
    static void BuildStreets(GameObject town,List<Vector3[]> routes)
    {
        const float cell=.4f;
        var cells=new HashSet<Vector2Int>();
        foreach(var route in routes)for(int i=1;i<route.Length;i++)
        {
            var a=town.transform.InverseTransformPoint(route[i-1]);var b=town.transform.InverseTransformPoint(route[i]);
            int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/.15f);
            for(int j=0;j<=steps;j++)
            {
                var p=Vector3.Lerp(a,b,j/(float)Mathf.Max(1,steps));
                if(Mathf.Abs(p.x)>20||Mathf.Abs(p.z)>20)continue;
                var c=new Vector2Int(Mathf.RoundToInt(p.x/cell),Mathf.RoundToInt(p.z/cell));
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)cells.Add(c+new Vector2Int(x,z));
            }
        }
        var vertices=new List<Vector3>();var triangles=new List<int>();
        float y=-ProjectAssets.Load<WorldContentCatalog>("WorldContentCatalog").villageGroundOffset+.012f;
        foreach(var c in cells.OrderBy(c=>c.x).ThenBy(c=>c.y))
        {
            var local=new Vector3(c.x*cell,y,c.y*cell);var world=town.transform.TransformPoint(local);
            if(!NavMesh.SamplePosition(world,out var hit,.18f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-world.y)>.25f)continue;
            int v=vertices.Count;float half=cell*.46f;
            vertices.Add(local+new Vector3(-half,0,-half));vertices.Add(local+new Vector3(-half,0,half));vertices.Add(local+new Vector3(half,0,half));vertices.Add(local+new Vector3(half,0,-half));
            triangles.AddRange(new[]{v,v+1,v+2,v,v+2,v+3});
        }
        const string meshFolder="Assets/Art/Meshes/World";
        if(!AssetDatabase.IsValidFolder(meshFolder))AssetDatabase.CreateFolder("Assets/Art/Meshes","World");
        const string meshPath=meshFolder+"/VillageStreets.asset";
        const string previousMesh="Assets/Art/Meshes/Voxelized/NPC/Craftpix/VillageStreets.asset";
        if(AssetDatabase.LoadAssetAtPath<Mesh>(meshPath)==null&&AssetDatabase.LoadAssetAtPath<Mesh>(previousMesh)!=null)
            Require(AssetDatabase.MoveAsset(previousMesh,meshPath)=="","Cannot organize street mesh");
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,meshPath);}else mesh.Clear(false);
        mesh.name="VillageStreets";mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
        mesh.SetColors(vertices.Select((v,i)=>new Color(.48f,.44f,.36f)*Mathf.Lerp(.92f,1.08f,Mathf.Repeat(Mathf.Sin(Mathf.Floor(i/4f)*17.17f)*43758.54f,1))).ToList());
        mesh.RecalculateNormals();mesh.RecalculateBounds();var meshBounds=mesh.bounds;meshBounds.Expand(new Vector3(0,.1f,0));mesh.bounds=meshBounds;mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);
        const string materialPath="Assets/Art/Materials/MedievalVillage/StreetStone.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.38f,.35f,.29f);material.SetFloat("_Smoothness",0);AssetDatabase.CreateAsset(material,materialPath);}
        // The project's voxel shader supports both the scene pipeline and editor previews.
        material.shader=Shader.Find("Mismo/Voxel Landscape");material.color=Color.white;EditorUtility.SetDirty(material);
        const string path="Assets/Art/Prefabs/World/Villages/MedievalVillage.prefab";
        var prefab=PrefabUtility.LoadPrefabContents(path);
        try {AttachStreets(prefab,mesh,material);PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        AttachStreets(town,mesh,material);AssetDatabase.SaveAssets();
        var sample=town.transform.TransformPoint(vertices[0]);
        File.WriteAllText(VillageHumanoidUpgrade.Output+"/streets.txt",vertices.Count/4+" paving stones along verified entrance/workplace routes. No additional colliders.\nSample world="+sample+" renderer="+town.transform.Find("Village streets").GetComponent<Renderer>().bounds);
    }
    static void AttachStreets(GameObject town,Mesh mesh,Material material)
    {
        var old=town.transform.Find("Village streets");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var go=new GameObject("Village streets");go.transform.SetParent(town.transform,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        var serialized=new SerializedObject(renderer);var smallMesh=serialized.FindProperty("m_SmallMeshCulling");if(smallMesh!=null){smallMesh.boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();}
    }
    static void Capture(GameObject root,GameObject town,VillageNpcRoutine[] residents,UnityEngine.SceneManagement.Scene scene)
    {
        var light=new GameObject("Sun").AddComponent<Light>();light.transform.SetParent(root.transform);light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(45,-30,0);
        var camera=new GameObject("Preview").AddComponent<Camera>();camera.transform.SetParent(root.transform);camera.scene=scene;camera.farClipPlane=500;camera.backgroundColor=new Color(.18f,.22f,.28f);camera.clearFlags=CameraClearFlags.Color;
        bool fog=RenderSettings.fog;RenderSettings.fog=false;
        camera.transform.position=town.transform.position+new Vector3(68,100,-92);camera.transform.LookAt(town.transform.position);Capture(camera,"Town");
        var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"),root.transform);
        foreach(var b in player.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
        var position=residents[0].transform.position;player.transform.position=position+Vector3.right*2.8f;
        foreach(var a in player.GetComponentsInChildren<Animator>()){a.Rebind();a.Update(0);}
        camera.transform.position=position+new Vector3(1.4f,2.7f,9);camera.transform.LookAt(position+new Vector3(1.4f,1.4f,0));Capture(camera,"Player-and-resident");
        RenderSettings.fog=fog;
    }
    static void Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(1200,900,24);var previous=RenderTexture.active;var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(VillageHumanoidUpgrade.Output+"/"+name+".png",pixels.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}
    }
}
