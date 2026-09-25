using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Editor-only asset authoring. Never registers content in the world or changes build scenes.
public static class CaveAssetKit
{
    const string MeshRoot="Assets/Art/Meshes/Environment/CaveKit/";
    const string MatRoot="Assets/Art/Materials/Environment/CaveKit/";
    const string TexRoot="Assets/Art/Textures/Environment/CaveKit/";
    const string PrefabRoot="Assets/Art/Prefabs/World/CaveKit/";
    const string SourceRoot="Assets/Art/Source/Environment/CaveKit/";
    const string Output="output/cave-kit";
    const string ScenePath="Assets/Scenes/Previews/CaveKit.unity";
    static Material material,backdrop;
    static Color32[] palette;
    static Scene authoringScene;
    static readonly List<string> report=new List<string>();
    static readonly List<GameObject> prefabs=new List<GameObject>();
    static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static void Folder(string path)
    {
        path=path.TrimEnd('/');if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static T Save<T>(T value,string path) where T:Object
    {
        var existing=AssetDatabase.LoadAssetAtPath<T>(path);
        if(existing==null){AssetDatabase.CreateAsset(value,path);return value;}
        EditorUtility.CopySerialized(value,existing);Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing;
    }
    static Texture2D Texture(Color[] colors,string name)
    {
        var t=new Texture2D(64,1,TextureFormat.RGBA32,false);t.SetPixels(colors);t.Apply();
        string path=TexRoot+name+".png";File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void Materials()
    {
        var colors=Enumerable.Repeat(Color.black,64).ToArray();
        string[] hex={"000000","434C52","566169","69757A","303B44","41505B","53646C","49362A","64503A","81664A","3C5625","597837","80964A","B1B89B","D0D7B5","869A88","198F91","35C0BC","7DE7DC","207F8B","35ADB7","7ED8D8"};
        for(int i=0;i<hex.Length;i++)ColorUtility.TryParseHtmlString("#"+hex[i],out colors[i]);
        palette=colors.Select(c=>(Color32)c).ToArray();var albedo=Texture(colors,"CavePalette");
        var emission=Enumerable.Repeat(Color.black,64).ToArray();for(int i=16;i<=21;i++)emission[i]=colors[i];
        var emissionMap=Texture(emission,"CaveEmission");
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit shader is required");
        var mat=new Material(shader){name="CavePalette"};mat.SetTexture("_BaseMap",albedo);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.13f);mat.SetFloat("_Metallic",0);mat.enableInstancing=true;
        mat.SetTexture("_EmissionMap",emissionMap);mat.SetColor("_EmissionColor",Color.white*.85f);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;mat.EnableKeyword("_EMISSION");
        material=Save(mat,MatRoot+"CavePalette.mat");
        mat=new Material(material){name="CaveWet"};mat.SetFloat("_Smoothness",.62f);mat.SetColor("_BaseColor",new Color(.64f,.70f,.72f));Save(mat,MatRoot+"CaveWet.mat");
        mat=new Material(shader){name="PreviewBackdrop"};mat.SetColor("_BaseColor",new Color(.21f,.24f,.26f));mat.SetFloat("_Smoothness",0);backdrop=Save(mat,MatRoot+"PreviewBackdrop.mat");
    }
    [Serializable] sealed class SourceInfo
    {
        public float voxelSizeMetres;
        public Vector3 minimumUnityMetres;
        public string axisMapping="VOX X,Y,Z = Unity X,Z,Y";
        public string pivot;
    }
    static void Export(string name,EnchantedGroveVoxels g,bool hanging)
    {
        var low=g.Cells.Keys.Aggregate(Vector3Int.Min);var high=g.Cells.Keys.Aggregate(Vector3Int.Max);var size=high-low+Vector3Int.one;
        if(Mathf.Max(size.x,Mathf.Max(size.y,size.z))>256)throw new InvalidOperationException("VOX extent >256: "+name);
        using(var stream=new MemoryStream())using(var chunks=new BinaryWriter(stream))
        {
            void Header(string id,int length){chunks.Write(System.Text.Encoding.ASCII.GetBytes(id));chunks.Write(length);chunks.Write(0);}
            Header("SIZE",12);chunks.Write(size.x);chunks.Write(size.z);chunks.Write(size.y);
            Header("XYZI",4+g.Cells.Count*4);chunks.Write(g.Cells.Count);
            foreach(var c in g.Cells.OrderBy(c=>c.Key.x).ThenBy(c=>c.Key.y).ThenBy(c=>c.Key.z))
            {var p=c.Key-low;chunks.Write((byte)p.x);chunks.Write((byte)p.z);chunks.Write((byte)p.y);chunks.Write(c.Value);}
            Header("RGBA",1024);for(int i=1;i<=256;i++){Color32 c=i<palette.Length?palette[i]:new Color32(0,0,0,255);chunks.Write(c.r);chunks.Write(c.g);chunks.Write(c.b);chunks.Write((byte)255);}
            chunks.Flush();using(var file=new BinaryWriter(File.Create(SourceRoot+name+".vox")))
            {file.Write(System.Text.Encoding.ASCII.GetBytes("VOX "));file.Write(150);file.Write(System.Text.Encoding.ASCII.GetBytes("MAIN"));file.Write(0);file.Write((int)stream.Length);file.Write(stream.ToArray());}
        }
        File.WriteAllText(SourceRoot+name+".json",JsonUtility.ToJson(new SourceInfo{voxelSizeMetres=g.Step,minimumUnityMetres=(Vector3)low*g.Step,pivot=hanging?"Attachment at local origin":"Ground at Y=0"},true));
    }
    static MeshRenderer MeshPart(GameObject parent,string name,EnchantedGroveVoxels voxels,string filename)
    {
        var go=new GameObject(name);go.transform.SetParent(parent.transform,false);
        var mesh=voxels.Mesh(filename);
        if(mesh.triangles.Length/3>65000)throw new InvalidOperationException("Triangle budget exceeded: "+filename);
        mesh=Save(mesh,MeshRoot+filename+".asset");go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;
    }
    static void Build(CaveAssetShapes.Piece piece)
    {
        Export(piece.Name,piece.Body,piece.Hanging);
        var root=new GameObject(piece.Name);
        SceneManager.MoveGameObjectToScene(root,authoringScene);
        try
        {
            var body=MeshPart(root,"Body",piece.Body,piece.Name);
            var mesh=body.GetComponent<MeshFilter>().sharedMesh;
            var high=new List<Renderer>{body};var low=new List<Renderer>();
            bool useLOD=piece.Solid||mesh.triangles.Length>18000;
            if(useLOD)low.Add(MeshPart(root,"Body_LOD1",piece.Body.Reduced(2),piece.Name+"_LOD1"));
            if(piece.Moss!=null)
            {
                Export(piece.Name+"_Moss",piece.Moss,piece.Hanging);
                high.Add(MeshPart(root,"Moss_Optional",piece.Moss,piece.Name+"_Moss"));
                if(useLOD)low.Add(MeshPart(root,"Moss_Optional_LOD1",piece.Moss.Reduced(2),piece.Name+"_Moss_LOD1"));
            }
            var group=root.AddComponent<LODGroup>();
            group.SetLODs(useLOD?new[]{new LOD(.15f,high.ToArray()),new LOD(.015f,low.ToArray())}:new[]{new LOD(.008f,high.ToArray())});group.RecalculateBounds();
            if(piece.Solid)
            {
                // Static-only surface collider preserves the open arch. Never use a convex hull for an entrance.
                var collider=root.AddComponent<MeshCollider>();collider.sharedMesh=useLOD?low[0].GetComponent<MeshFilter>().sharedMesh:mesh;collider.convex=false;
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabRoot+piece.Name+".prefab");prefabs.Add(prefab);
            int triangles=high.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
            report.Add(piece.Name+" | LOD0 triangles="+triangles+" | body metres="+mesh.bounds.size.ToString("F2")+" | voxel="+piece.Body.Step.ToString("F3",Inv)+" | pivot="+(piece.Hanging?"attachment":"ground")+" | collider="+piece.Solid);
        }
        finally{Object.DestroyImmediate(root);}
    }
    static Bounds Bounds(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<MeshRenderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
    }
    static void Capture(Camera camera,string name,int width=1000,int height=850)
    {
        bool batching=GraphicsSettings.useScriptableRenderPipelineBatching;GraphicsSettings.useScriptableRenderPipelineBatching=false;
        foreach(var root in camera.scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            var mat=renderer.sharedMaterial;if(mat==null||!mat.HasProperty("_BaseMap"))continue;
            var block=new MaterialPropertyBlock();block.SetTexture("_BaseMap",mat.GetTexture("_BaseMap")??Texture2D.whiteTexture);block.SetColor("_BaseColor",mat.GetColor("_BaseColor"));renderer.SetPropertyBlock(block);
        }
        var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);GraphicsSettings.useScriptableRenderPipelineBatching=batching;}
    }
    static void Preview()
    {
        var previous=SceneManager.GetActiveScene();
        bool emptyBatch=Application.isBatchMode&&string.IsNullOrEmpty(previous.path);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,emptyBatch?NewSceneMode.Single:NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        try
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.30f,.34f,.39f);RenderSettings.ambientEquatorColor=new Color(.19f,.23f,.25f);RenderSettings.ambientGroundColor=new Color(.10f,.12f,.14f);RenderSettings.fog=false;RenderSettings.skybox=null;
            var light=new GameObject("Studio key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.color=new Color(1,.91f,.80f);light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);
            var fill=new GameObject("Studio fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.5f;fill.color=new Color(.65f,.80f,1);fill.transform.rotation=Quaternion.Euler(25,145,0);
            var camera=new GameObject("Kit camera").AddComponent<Camera>();camera.scene=scene;camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.19f);camera.nearClipPlane=.05f;camera.farClipPlane=180;camera.orthographic=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            foreach(var prefab in prefabs)
            {
                var item=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);item.GetComponent<LODGroup>().ForceLOD(0);var bounds=Bounds(item);
                float extent=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
                camera.orthographicSize=extent*.76f;camera.transform.position=bounds.center+V(1,.70f,-1.45f).normalized*(extent*3+2);camera.transform.LookAt(bounds.center);
                Capture(camera,prefab.name);
                Object.DestroyImmediate(item);
            }
            // Persist a labelled gallery at physical scale. No gameplay world is loaded.
            int index=0;
            foreach(var prefab in prefabs)
            {
                int x=index%5,z=index/5;var item=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var bounds=Bounds(item);
                item.transform.position=V((x-2)*10,-bounds.min.y,z*9);
                var text=new GameObject(prefab.name+" label").AddComponent<TextMesh>();text.text=prefab.name.Replace('_',' ');text.fontSize=48;text.characterSize=.075f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.80f,.91f,.92f);text.transform.position=V((x-2)*10,.05f,z*9-2.1f);text.transform.rotation=Quaternion.Euler(90,0,0);index++;
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Gallery floor";floor.transform.position=V(0,-.10f,22);floor.transform.localScale=V(7,1,7);floor.GetComponent<Renderer>().sharedMaterial=backdrop;
            camera.orthographicSize=37;camera.transform.position=V(23,50,-30);camera.transform.LookAt(V(0,0,22));
            EditorSceneManager.SaveScene(scene,ScenePath);Capture(camera,"gallery",1600,1300);
            report.Add("29 individual Unity renders + physical-scale gallery saved; no automatic lights in asset prefabs.");
        }
        finally
        {
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(SceneManager.sceneCount==1)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            else EditorSceneManager.CloseScene(scene,true);
        }
    }
    static void Validate()
    {
        if(prefabs.Count!=29)throw new InvalidOperationException("Expected 29 assets");
        foreach(var p in prefabs)
        {
            foreach(var f in p.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh==null||f.sharedMesh.vertexCount==0)throw new InvalidOperationException("Missing mesh: "+p.name);
            foreach(var r in p.GetComponentsInChildren<Renderer>())if(r.sharedMaterial==null||r.sharedMaterial.shader==null)throw new InvalidOperationException("Missing material: "+p.name);
            if(p.GetComponentInChildren<Light>()!=null)throw new InvalidOperationException("Prefab should not spawn lights: "+p.name);
        }
        var arch=(GameObject)PrefabUtility.InstantiatePrefab(prefabs.Single(p=>p.name=="R05_Arco_entrada"),authoringScene);
        try
        {
            Physics.SyncTransforms();var collider=arch.GetComponent<MeshCollider>();
            if(collider.Raycast(new Ray(V(0,1.1f,-5),Vector3.forward),out _,10))throw new InvalidOperationException("Arch opening is blocked");
            if(!collider.Raycast(new Ray(V(2.9f,1.1f,-5),Vector3.forward),out _,10))throw new InvalidOperationException("Arch pier has no collision");
        }
        finally{Object.DestroyImmediate(arch);}
        report.Add("PASS: all mesh/material references, triangle budgets, greedy mesh winding/area and open arch collider.");
    }
    static void Index()
    {
        string html="<!doctype html><html lang='es'><meta charset='utf-8'><title>Mismo · Kit de cuevas</title><style>body{background:#151c23;color:#e0e7ec;font:16px system-ui;margin:40px}h1{font-size:32px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:20px}figure{margin:0;background:#202c36;border-radius:12px;overflow:hidden}img{width:100%;display:block}figcaption{padding:16px}a{color:inherit}</style><h1>Mismo · Kit de cuevas · 29 assets 3D</h1><p>Capturas reales de Unity. Modelos a escala en metros, fuentes VOX editables, musgo opcional. El brillo no añade luces automáticamente.</p><main>";
        foreach(var p in prefabs)html+="<figure><a href='"+p.name+".png'><img loading='lazy' src='"+p.name+".png'></a><figcaption>"+p.name.Replace('_',' ')+"</figcaption></figure>";
        File.WriteAllText(Output+"/index.html",html+"</main></html>");
    }
    [MenuItem("Mismo/Modelos/Cuevas/Generar o regenerar kit de assets")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Salir de Play antes de generar");
        if(!Application.isBatchMode&&string.IsNullOrEmpty(SceneManager.GetActiveScene().path))throw new InvalidOperationException("Guardar la escena sin nombre antes de abrir la exposición aditiva.");
        // Regeneration explicitly replaces this generated kit, preserving existing GUIDs.
        prefabs.Clear();report.Clear();Directory.CreateDirectory(Output);
        foreach(string path in new[]{MeshRoot,MatRoot,TexRoot,PrefabRoot,SourceRoot,"Assets/Scenes/Previews"})Folder(path);
        authoringScene=EditorSceneManager.NewPreviewScene();
        try
        {
            Materials();foreach(var piece in CaveAssetShapes.Build())Build(piece);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);AssetDatabase.SaveAssets();Validate();
        }
        finally{EditorSceneManager.ClosePreviewScene(authoringScene);}
        File.WriteAllLines(Output+"/assets.txt",report);
        Preview();Index();AssetDatabase.SaveAssets();
        try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","PROJECT_LAYOUT_CHECKS_OK");report.Add("PROJECT_LAYOUT_CHECKS_OK");}
        catch(Exception e)
        {
            File.WriteAllText(Output+"/organization.txt",e.Message);
            // Report unrelated project imports without discarding a successfully validated kit.
            if(e.Message.Contains("/CaveKit/"))throw;
            report.Add("Project organization has existing issues outside CaveKit; see organization.txt.");
            Debug.LogWarning("Existing project organization issues outside CaveKit: "+Output+"/organization.txt");
        }
        File.WriteAllLines(Output+"/assets.txt",report);Debug.Log("CAVE_KIT_OK: 29 assets");
    }
    public static void RunBatch(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
