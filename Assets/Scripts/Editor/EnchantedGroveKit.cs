using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class EnchantedGroveKit
{
    const string MeshRoot="Assets/Art/Meshes/Environment/EnchantedGrove/";
    const string MaterialRoot="Assets/Art/Materials/Environment/EnchantedGrove/";
    const string TextureRoot="Assets/Art/Textures/Environment/EnchantedGrove/";
    const string PrefabRoot="Assets/Art/Prefabs/World/EnchantedGrove/";
    const string SourceRoot="Assets/Art/Source/Environment/EnchantedGrove/";
    const string PreviewPath="Assets/Scenes/Previews/EnchantedGrove.unity";
    const string ProfilePath="Assets/Data/Rendering/EnchantedGrove/PreviewLighting.asset";
    const string Output="output/enchanted-grove";
    static readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
    static readonly List<string> report=new List<string>();
    static Material paletteMaterial,waterMaterial;
    static Color32[] sourcePalette;
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
    static void Palette()
    {
        string[] hex={"000000","456247","67815A","92A573","B69A50","CFB66F","E1CF91","A56170","C8848F","E6A9AC","777E79","949A8D","B2B4A0","483F35","635141","876849","B4A481","D4C6A0","E6D8B2","854B43","A86550","BC8060","F3D595","5DA99D","255F65","F3E6C3","A89ABD","FFD489","637D72","50694B","6C8154","86925E"};
        var colors=Enumerable.Repeat(Color.black,64).ToArray();for(int i=0;i<hex.Length;i++){ColorUtility.TryParseHtmlString("#"+hex[i],out colors[i]);}
        sourcePalette=colors.Select(c=>(Color32)c).ToArray();
        Texture2D Make(Color[] pixels,string name)
        {
            var t=new Texture2D(64,1,TextureFormat.RGBA32,false);t.SetPixels(pixels);t.Apply();string path=TextureRoot+name+".png";File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;importer.sRGBTexture=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        var albedo=Make(colors,"GrovePalette");var emission=Enumerable.Repeat(Color.black,64).ToArray();emission[27]=colors[27];var emissionMap=Make(emission,"GroveEmission");
        paletteMaterial=AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot+"GrovePalette.mat");
        if(paletteMaterial==null){paletteMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(paletteMaterial,MaterialRoot+"GrovePalette.mat");}
        paletteMaterial.SetTexture("_BaseMap",albedo);paletteMaterial.SetColor("_BaseColor",Color.white);paletteMaterial.SetFloat("_Smoothness",.12f);paletteMaterial.SetFloat("_Metallic",0);
        paletteMaterial.SetTexture("_EmissionMap",emissionMap);paletteMaterial.SetColor("_EmissionColor",Color.white*1.5f);paletteMaterial.EnableKeyword("_EMISSION");paletteMaterial.enableInstancing=true;EditorUtility.SetDirty(paletteMaterial);
        waterMaterial=AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot+"PondWater.mat");
        if(waterMaterial==null){waterMaterial=new Material(Shader.Find("Mismo/Enchanted Grove Water"));AssetDatabase.CreateAsset(waterMaterial,MaterialRoot+"PondWater.mat");}
    }
    static GameObject Asset(string name,EnchantedGroveVoxels voxels,int budget=12000,WorldAssetKind? vegetation=null,bool lod=false,Action<GameObject> colliders=null)
    {
        ExportVox(name,voxels);
        var meshes=new List<Mesh>();int factor=1;
        var mesh=voxels.Mesh(name);
        while(mesh.triangles.Length/3>budget&&factor<4){Object.DestroyImmediate(mesh);factor++;mesh=voxels.Reduced(factor).Mesh(name);}
        if(mesh.triangles.Length/3>budget)throw new InvalidOperationException(name+" exceeds budget");
        meshes.Add(Save(mesh,MeshRoot+name+".asset"));
        if(lod)for(int i=1;i<=2;i++)meshes.Add(Save(voxels.Reduced(factor*(1<<i)).Mesh(name+"_LOD"+i),MeshRoot+name+"_LOD"+i+".asset"));
        var root=new GameObject(name);
        try
        {
            var renderers=new List<Renderer>();
            for(int i=0;i<meshes.Count;i++)
            {
                GameObject part=i==0?root:new GameObject("LOD"+i);if(i>0)part.transform.SetParent(root.transform,false);
                part.AddComponent<MeshFilter>().sharedMesh=meshes[i];var r=part.AddComponent<MeshRenderer>();r.sharedMaterial=paletteMaterial;renderers.Add(r);
            }
            if(lod){var group=root.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.18f,new[]{renderers[0]}),new LOD(.07f,new[]{renderers[1]}),new LOD(.018f,new[]{renderers[2]})});group.RecalculateBounds();}
            else if(vegetation==WorldAssetKind.Grass||vegetation==WorldAssetKind.Flower||name=="Mushrooms")
            {var group=root.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.02f,renderers.ToArray())});group.RecalculateBounds();}
            if(vegetation.HasValue)root.AddComponent<VegetationMotionBinding>().kind=vegetation.Value;
            colliders?.Invoke(root);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabRoot+name+".prefab");prefabs.Add(name,prefab);
            report.Add(name+" | triangles "+string.Join(" / ",meshes.Select(m=>m.triangles.Length/3))+" | vertices "+meshes[0].vertexCount+" | bounds metres "+meshes[0].bounds.size.ToString("F2")+" | voxel step "+(voxels.Step*factor).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));return prefab;
        }
        finally{Object.DestroyImmediate(root);}
    }
    [Serializable] sealed class SourceInfo { public float voxelSizeMetres; public Vector3 minimumUnityMetres; public string axisMapping="VOX X,Y,Z = Unity X,Z,Y"; }
    static void ExportVox(string name,EnchantedGroveVoxels voxels)
    {
        var low=voxels.Cells.Keys.Aggregate(Vector3Int.Min);var high=voxels.Cells.Keys.Aggregate(Vector3Int.Max);var size=high-low+Vector3Int.one;
        if(Mathf.Max(size.x,Mathf.Max(size.y,size.z))>256)throw new InvalidOperationException("VOX source exceeds 256 cells: "+name);
        using(var stream=new MemoryStream())using(var chunks=new BinaryWriter(stream))
        {
            void Header(string tag,int length){chunks.Write(System.Text.Encoding.ASCII.GetBytes(tag));chunks.Write(length);chunks.Write(0);}
            Header("SIZE",12);chunks.Write(size.x);chunks.Write(size.z);chunks.Write(size.y);
            Header("XYZI",4+voxels.Cells.Count*4);chunks.Write(voxels.Cells.Count);
            foreach(var cell in voxels.Cells.OrderBy(c=>c.Key.x).ThenBy(c=>c.Key.y).ThenBy(c=>c.Key.z))
            {
                var p=cell.Key-low;chunks.Write((byte)p.x);chunks.Write((byte)p.z);chunks.Write((byte)p.y);chunks.Write(cell.Value);
            }
            Header("RGBA",1024);
            for(int i=1;i<=256;i++){var color=i<sourcePalette.Length?sourcePalette[i]:new Color32(0,0,0,255);chunks.Write(color.r);chunks.Write(color.g);chunks.Write(color.b);chunks.Write((byte)255);}
            chunks.Flush();
            using(var file=new BinaryWriter(File.Create(SourceRoot+name+".vox"))){file.Write(System.Text.Encoding.ASCII.GetBytes("VOX "));file.Write(150);file.Write(System.Text.Encoding.ASCII.GetBytes("MAIN"));file.Write(0);file.Write((int)stream.Length);file.Write(stream.ToArray());}
        }
        File.WriteAllText(SourceRoot+name+".json",JsonUtility.ToJson(new SourceInfo{voxelSizeMetres=voxels.Step,minimumUnityMetres=(Vector3)low*voxels.Step},true));
    }
    static void BoxCollider(GameObject root,Vector3 center,Vector3 size)
    {
        var c=root.AddComponent<BoxCollider>();c.center=center;c.size=size;
    }
    static void TreeCollider(GameObject root)
    {
        var c=root.AddComponent<CapsuleCollider>();c.radius=.3f;c.height=2.8f;c.center=Vector3.up*1.4f;root.AddComponent<ClimbableTree>();
    }
    static void BuildAssets()
    {
        Asset("Tree_Sage",EnchantedGroveShapes.Tree(18,1),12000,WorldAssetKind.Tree,true,TreeCollider);
        Asset("Tree_Blossom",EnchantedGroveShapes.Tree(31,7),12000,WorldAssetKind.Tree,true,TreeCollider);
        Asset("Tree_Amber",EnchantedGroveShapes.Tree(62,4),12000,WorldAssetKind.Tree,true,TreeCollider);
        Asset("Tree_Cypress",EnchantedGroveShapes.Tree(14,1,true),9000,WorldAssetKind.Tree,true,TreeCollider);
        Asset("Shrub_Sage",EnchantedGroveShapes.Shrub(1,41),4500,WorldAssetKind.Bush,true);
        Asset("Shrub_Hydrangea",EnchantedGroveShapes.Shrub(7,26),4500,WorldAssetKind.Bush,true);
        Asset("Fern",EnchantedGroveShapes.Fern(),2200,WorldAssetKind.Grass);
        Asset("Grass_Tuft",EnchantedGroveShapes.Flowers(3,false,true),1800,WorldAssetKind.Grass);
        Asset("Flowers_Ivory",EnchantedGroveShapes.Flowers(25),2400,WorldAssetKind.Flower);
        Asset("Flowers_Coral",EnchantedGroveShapes.Flowers(9),2400,WorldAssetKind.Flower);
        Asset("Flowers_Lavender",EnchantedGroveShapes.Flowers(26),2600,WorldAssetKind.Flower);
        Asset("Reeds",EnchantedGroveShapes.Flowers(3,true),2400,WorldAssetKind.Grass);
        Asset("Lily_Pads",EnchantedGroveShapes.Lilies(),1500);
        Asset("Mushrooms",EnchantedGroveShapes.Mushrooms(),1400);
        Asset("Rock_Moss_Large",EnchantedGroveShapes.Boulder(1.25f,78),5000,null,true,g=>BoxCollider(g,V(0,.55f,0),V(2,.95f,1.7f)));
        Asset("Rock_Moss_Small",EnchantedGroveShapes.Boulder(.65f,34),2500,null,false,g=>BoxCollider(g,V(0,.28f,0),V(1,.5f,.8f)));
        Asset("Cliff_Moss",EnchantedGroveShapes.Cliff(),8000,null,true,g=>BoxCollider(g,V(0,2.35f,0),V(3.9f,4.7f,1.1f)));
        Asset("Vines_Long",EnchantedGroveShapes.Vines(true),5000);
        Asset("Vines_Short",EnchantedGroveShapes.Vines(false),3500);
        Asset("Ruin_Arch",EnchantedGroveShapes.Arch(),5000,null,false,g=>{BoxCollider(g,V(-1.75f,1.3f,0),V(.85f,2.6f,.9f));BoxCollider(g,V(1.75f,1.3f,0),V(.85f,2.6f,.9f));BoxCollider(g,V(0,3.6f,0),V(3.1f,.8f,.9f));});
        Asset("Ruin_Pillar",EnchantedGroveShapes.Pillar(),1800,null,false,g=>BoxCollider(g,V(0,1.2f,0),V(.8f,2.4f,.8f)));
        Asset("Ruin_Wall",EnchantedGroveShapes.Wall(),2200,null,false,g=>BoxCollider(g,V(0,.75f,0),V(3.1f,1.5f,.7f)));
        Asset("Stone_Steps",EnchantedGroveShapes.Steps(),1000,null,false,g=>{for(int i=0;i<5;i++)BoxCollider(g,V(0,(i+1)*.1f,-.8f+i*.42f),V(2,(i+1)*.2f,.4f));});
        Asset("Path_Stones",EnchantedGroveShapes.Path(),1500);
        Asset("Lantern_Post",EnchantedGroveShapes.Lantern(true),1600,null,false,g=>BoxCollider(g,V(0,.85f,0),V(.25f,1.7f,.25f)));
        Asset("Lantern_Ground",EnchantedGroveShapes.Lantern(false),1500);
        Asset("Footbridge",EnchantedGroveShapes.Bridge(),4000,null,false,g=>{for(int i=0;i<13;i++){float z=-2+i*.32f,y=.15f+Mathf.Sin(i/12f*Mathf.PI)*.38f;BoxCollider(g,V(0,y+.075f,z+.14f),V(1.6f,.15f,.28f));}});
        Asset("Cottage_Exterior",EnchantedGroveShapes.Cottage(),16000,null,false,g=>BoxCollider(g,V(0,1.6f,0),V(4.3f,3.2f,3.9f)));
        Asset("Diorama_Terrain",EnchantedGroveShapes.Island(),18000,null,false,g=>{var c=g.AddComponent<MeshCollider>();c.sharedMesh=g.GetComponent<MeshFilter>().sharedMesh;});
        Water();Fireflies();
    }
    static void Water()
    {
        // Stepped shoreline mesh; UV radial distance drives the shallow water tint.
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
        for(int z=-12;z<12;z++)for(int x=-16;x<16;x++)
        {
            float px=(x+.5f)*.25f,pz=(z+.5f)*.25f;if(px*px/15+pz*pz/9>1)continue;
            int start=vertices.Count;
            foreach(var p in new[]{V(x*.25f,0,z*.25f),V(x*.25f,0,(z+1)*.25f),V((x+1)*.25f,0,(z+1)*.25f),V((x+1)*.25f,0,z*.25f)}){vertices.Add(p);normals.Add(Vector3.up);uv.Add(new Vector2(p.x/7.8f+.5f,p.z/6+.5f));}
            indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        var mesh=new Mesh{name="Pond_Water"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh=Save(mesh,MeshRoot+"Pond_Water.asset");
        var go=new GameObject("Pond_Water");go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=waterMaterial;
        prefabs.Add(go.name,PrefabUtility.SaveAsPrefabAsset(go,PrefabRoot+go.name+".prefab"));report.Add(go.name+" | triangles "+indices.Count/3+" | decorative opaque surface, no water gameplay");Object.DestroyImmediate(go);
    }
    static void Fireflies()
    {
        var vox=new EnchantedGroveVoxels(.025f);vox.Box(V(-.025f,-.025f,-.025f),V(.025f,.025f,.025f),27);var mesh=Save(vox.Mesh("Firefly"),MeshRoot+"Firefly.asset");
        var go=new GameObject("Fireflies");var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=system.main;main.duration=8;main.loop=true;main.startLifetime=7;main.startSpeed=.045f;main.startSize=1;main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        var emission=system.emission;emission.rateOverTime=3;var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=V(4,1.8f,4);
        var noise=system.noise;noise.enabled=true;noise.strength=.18f;noise.frequency=.3f;noise.scrollSpeed=.12f;
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh;renderer.sharedMaterial=paletteMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
        prefabs.Add(go.name,PrefabUtility.SaveAsPrefabAsset(go,PrefabRoot+go.name+".prefab"));report.Add("Fireflies | 12 triangles per particle | maximum 24 particles | no realtime lights");Object.DestroyImmediate(go);
    }
    static GameObject Place(string name,Vector3 position,float yaw=0,float scale=1)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[name]);go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;return go;
    }
    static void Lighting()
    {
        var sun=new GameObject("Warm evening sun").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.95f,.86f);sun.intensity=2.1f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.75f;sun.transform.rotation=Quaternion.Euler(43,-35,0);RenderSettings.sun=sun;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.54f,.65f,.7f);RenderSettings.ambientEquatorColor=new Color(.45f,.5f,.43f);RenderSettings.ambientGroundColor=new Color(.26f,.24f,.23f);RenderSettings.ambientIntensity=1;
        RenderSettings.fog=false;
        var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.24f,.29f,.3f));RenderSettings.ambientProbe=ambient;
        var environment=new GameObject("Standalone preview environment").AddComponent<Mismo.Presentation.Environment.GrovePreviewEnvironment>();
        var serialized=new SerializedObject(environment);serialized.FindProperty("vegetationShader").objectReferenceValue=Shader.Find("Mismo/Vegetation Motion");serialized.ApplyModifiedPropertiesWithoutUndo();
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,ProfilePath);}
        T Add<T>() where T:VolumeComponent { if(profile.TryGet<T>(out var component))return component;component=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(component,profile);return component; }
        var bloom=Add<Bloom>();bloom.intensity.Override(.18f);bloom.threshold.Override(1.05f);bloom.scatter.Override(.65f);
        var color=Add<ColorAdjustments>();color.postExposure.Override(.25f);color.contrast.Override(7);color.saturation.Override(-5);
        var tone=Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
        EditorUtility.SetDirty(profile);foreach(var component in profile.components)EditorUtility.SetDirty(component);
        var volume=new GameObject("Grove preview grading").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
    }
    static Camera Camera(Scene scene,Vector3 position,Vector3 target,bool orthographic)
    {
        var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.tag="MainCamera";camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.72f,.6f,.57f);camera.nearClipPlane=.1f;camera.farClipPlane=160;camera.fieldOfView=50;camera.orthographic=orthographic;camera.orthographicSize=13.8f;camera.allowHDR=true;
        camera.transform.position=position;camera.transform.LookAt(target);var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;return camera;
    }
    static void Preview()
    {
        var previous=SceneManager.GetActiveScene();bool emptyBatch=Application.isBatchMode&&string.IsNullOrEmpty(previous.path)&&!previous.isDirty;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,emptyBatch?NewSceneMode.Single:NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);Lighting();
            Place("Diorama_Terrain",Vector3.zero);Place("Pond_Water",V(-2,-.16f,-1));
            for(int i=0;i<4;i++)Place("Cliff_Moss",V(-6.1f+i*4,-4.75f,-7.9f),0,.98f);
            for(int i=0;i<3;i++)Place("Cliff_Moss",V(-9.3f,-4.75f,-4.5f+i*4),90,.98f);
            Place("Cottage_Exterior",V(3.6f,0,3.5f),-12,.9f);
            Place("Tree_Sage",V(-7.1f,0,4.5f),28,1.03f);Place("Tree_Blossom",V(7,0,-3.4f),-42,.88f);Place("Tree_Amber",V(-6.6f,0,-4.9f),16,.8f);
            Place("Tree_Cypress",V(7.5f,0,5.8f),0,.83f);Place("Tree_Cypress",V(.2f,0,6.4f),38,.8f);
            Place("Ruin_Arch",V(-2.5f,0,5.5f),-16);Place("Vines_Short",V(-3.5f,3.8f,5.0f),0,.9f);
            Place("Ruin_Pillar",V(-5.25f,0,3.4f),12);Place("Ruin_Wall",V(5.1f,0,-6),-14);
            Place("Stone_Steps",V(-1,0,-6.8f),0);Place("Footbridge",V(.1f,-.05f,-1.35f),0,.8f);
            for(int i=0;i<4;i++)Place("Path_Stones",V(1.7f+i*.48f,.015f,.2f+i*.58f),25+i*12,.7f);
            Place("Lantern_Post",V(3.1f,0,.6f),-10);Place("Lantern_Post",V(-4.7f,0,2.1f),40,.9f);Place("Lantern_Ground",V(-.4f,0,-5.6f));
            Place("Lily_Pads",V(-3.6f,-.13f,-.2f),13);Place("Lily_Pads",V(-2,-.13f,-2.4f),60,.72f);
            Place("Reeds",V(-5.3f,-.12f,-1.2f),26);Place("Reeds",V(-1.9f,-.1f,1.7f),16,.8f);
            var random=new System.Random(514);
            for(int i=0;i<34;i++)
            {
                float angle=i*2.39996f;float radius=5.0f+(float)random.NextDouble()*2.5f;float x=Mathf.Cos(angle)*radius,z=Mathf.Sin(angle)*radius*.82f;
                if(x>1.1f&&z>1)continue;
                string name=i%5==0?"Shrub_Hydrangea":i%5==1?"Shrub_Sage":i%5==2?"Flowers_Lavender":i%5==3?"Fern":"Flowers_Ivory";
                Place(name,V(x,0,z),i*51,.65f+(float)random.NextDouble()*.5f);
                if(i%4==0)Place("Rock_Moss_Small",V(x+.5f,0,z+.3f),i*37,.7f);
            }
            Place("Rock_Moss_Large",V(6.6f,0,.2f),-35);Place("Rock_Moss_Large",V(-7.4f,0,0),38,.8f);
            Place("Mushrooms",V(6.3f,0,-4),15);Place("Flowers_Coral",V(5.6f,0,1.7f),5);
            // Draping plants on the cliff provide a vertical layer visible from the camera.
            for(int i=0;i<11;i++)
            {
                float x=-7.5f+i*1.48f;Place(i%3==0?"Vines_Short":"Vines_Long",V(x,-.12f,-8.95f),0,.8f+(i%3)*.12f);
                if(i%2==0)Place("Shrub_Sage",V(x,-.03f,-7.1f),i*30,.55f);
            }
            for(int i=0;i<6;i++)Place("Vines_Long",V(-10.25f,-.15f,-4.8f+i*1.7f),90,.85f);
            Place("Fireflies",V(-2,.7f,-1));
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Preview backdrop";ground.transform.position=V(0,-5.05f,0);ground.transform.localScale=V(20,1,20);
            var backdrop=AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot+"PreviewBackdrop.mat");
            if(backdrop==null){backdrop=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(backdrop,MaterialRoot+"PreviewBackdrop.mat");}
            backdrop.SetColor("_BaseColor",new Color(.64f,.48f,.43f));backdrop.SetTexture("_BaseMap",Texture2D.whiteTexture);backdrop.SetFloat("_Smoothness",0);EditorUtility.SetDirty(backdrop);ground.GetComponent<Renderer>().sharedMaterial=backdrop;
            var camera=Camera(scene,V(22,19,-27),V(0,.6f,0),true);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,PreviewPath);
            Capture(camera,"diorama",1600,1300);
            camera.orthographic=false;camera.transform.position=V(4.5f,2.8f,-9.8f);camera.transform.LookAt(V(-1.2f,1.5f,2.5f));Capture(camera,"player-view",1600,1000);
            var shader=Shader.Find("Mismo/Enchanted Grove Water");var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            if(errors.Length>0)throw new InvalidOperationException(string.Join(";",errors.Select(e=>e.message)));
            // Contact sheets are temporary; the saved scene remains the composed diorama.
            foreach(var root in scene.GetRootGameObjects())if(root.GetComponent<Light>()==null&&root.GetComponent<Volume>()==null&&root.GetComponent<Mismo.Presentation.Environment.GrovePreviewEnvironment>()==null&&root!=camera.gameObject)Object.DestroyImmediate(root);
            var names=prefabs.Keys.Where(n=>n!="Diorama_Terrain"&&n!="Pond_Water"&&n!="Fireflies").ToArray();
            for(int page=0;page<3;page++)
            {
                var items=new List<GameObject>();
                for(int i=page*10;i<Mathf.Min(names.Length,(page+1)*10);i++)
                {
                    int index=i-page*10;var item=Place(names[i],V((index%5-2)*4,0,(index/5)*5));var bounds=item.GetComponent<MeshFilter>().sharedMesh.bounds;
                    float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));item.transform.localScale=Vector3.one*Mathf.Min(1,3.2f/size);
                    if(item.GetComponent<LODGroup>()!=null)item.GetComponent<LODGroup>().ForceLOD(0);
                    if(bounds.max.y<=.1f)item.transform.position+=Vector3.up*2.7f;
                    items.Add(item);
                }
                camera.orthographic=true;camera.orthographicSize=11.2f;camera.transform.position=V(12,17,-23);camera.transform.LookAt(V(0,1.3f,2.5f));Capture(camera,"kit-"+(page+1),1600,1000);
                foreach(var item in items)Object.DestroyImmediate(item);
            }
            report.Add("Preview and three contact sheets rendered. Water shader has no compile errors. No world catalog or build scene list modified.");
        }
        finally
        {
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            // In batch this may be the last scene, so create an empty replacement first.
            if(SceneManager.sceneCount==1)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            EditorSceneManager.CloseScene(scene,true);
        }
    }
    static void Capture(Camera camera,string name,int width,int height)
    {
        bool batching=GraphicsSettings.useScriptableRenderPipelineBatching;GraphicsSettings.useScriptableRenderPipelineBatching=false;
        // Explicit per-renderer values avoid stale material buffers in immediate batch renders.
        foreach(var root in camera.scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            var material=renderer.sharedMaterial;if(material==null||!material.HasProperty("_BaseMap"))continue;
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);block.SetTexture("_BaseMap",material.GetTexture("_BaseMap")??Texture2D.whiteTexture);block.SetColor("_BaseColor",material.GetColor("_BaseColor"));renderer.SetPropertyBlock(block);
        }
        var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);GraphicsSettings.useScriptableRenderPipelineBatching=batching;}
    }
    static void Validate()
    {
        var cube=new EnchantedGroveVoxels(.1f);cube.Box(Vector3.zero,Vector3.one,1);var mesh=cube.Mesh("check");if(mesh.triangles.Length!=36)throw new InvalidOperationException("Solid cube did not merge to 12 triangles");Object.DestroyImmediate(mesh);
        foreach(var pair in prefabs)
        {
            foreach(var filter in pair.Value.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh==null||filter.sharedMesh.vertexCount==0)throw new InvalidOperationException("Missing mesh "+pair.Key);
            foreach(var renderer in pair.Value.GetComponentsInChildren<Renderer>())if(renderer.sharedMaterial==null||renderer.sharedMaterial.shader==null)throw new InvalidOperationException("Missing material "+pair.Key);
            if(pair.Value.GetComponent<VegetationMotionBinding>()!=null&&pair.Value.GetComponent<MeshCollider>()!=null)throw new InvalidOperationException("Vegetation must not use mesh colliders");
        }
        // Exercise the existing wind system for all LOD renderers without changing saved prefabs.
        var host=new GameObject("Temporary grove wind validation");var world=host.AddComponent<VegetationMotionWorld>();world.Initialize(AssetDatabase.LoadAssetAtPath<WorldContentCatalog>("Assets/Data/World/WorldContentCatalog.asset"),null);
        try
        {
            foreach(var prefab in prefabs.Values.Where(p=>p.GetComponent<VegetationMotionBinding>()!=null))
            {
                var go=Object.Instantiate(prefab);
                try{go.GetComponent<VegetationMotionBinding>().Apply();foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>())if(renderer.sharedMaterial.shader!=world.Settings.shader)throw new InvalidOperationException("Wind failed: "+prefab.name);}
                finally{Object.DestroyImmediate(go);}
            }
        }
        finally{Object.DestroyImmediate(host);}
        report.Add("Checks passed: greedy meshing area and winding, triangle limits, prefab mesh/material references, vegetation wind integration across every LOD.");
    }
    [MenuItem("Mismo/World/Enchanted Grove/Generar kit y escena de muestra")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first");
        report.Clear();prefabs.Clear();Directory.CreateDirectory(Output);
        foreach(string path in new[]{MeshRoot,MaterialRoot,TextureRoot,PrefabRoot,SourceRoot,"Assets/Scenes/Previews","Assets/Data/Rendering/EnchantedGrove"})Folder(path);
        Palette();BuildAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);Validate();AssetDatabase.SaveAssets();File.WriteAllLines(Output+"/assets.txt",report);
        Preview();File.WriteAllLines(Output+"/assets.txt",report);
        try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","Passed");}catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.Message);Debug.LogWarning("Existing organization issues reported in "+Output+"/organization.txt");}
        Debug.Log("ENCHANTED_GROVE_OK: "+prefabs.Count+" prefabs\n"+string.Join("\n",report));
    }
    public static void RunBatch(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
