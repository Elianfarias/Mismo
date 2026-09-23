using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// VOX scene/palette specification: https://github.com/ephtracy/voxel-model
// Reads original VOX cells; reduces the runtime grid and generates distance LODs.
public static class MountainLabsTreeConversion
{
    const string Source = "Assets/Art/Source/Nature/MountainLabs/VoxelTreePack/";
    const string Meshes = "Assets/Art/Meshes/Nature/MountainLabs/";
    const string Materials = "Assets/Art/Materials/Nature/MountainLabs/";
    const string Textures = "Assets/Art/Textures/Nature/MountainLabs/";
    const string Prefabs = "Assets/Art/Prefabs/World/Nature/MountainLabs/";
    const string ScenePath = "Assets/Scenes/Previews/MountainLabsTrees.unity";
    const string Output = "output/mountainlabs-trees";
    const int TriangleBudget = 25000;
    static readonly List<string> report = new List<string>();

    sealed class Model { public Vector3Int size; public byte[] cells; }
    sealed class Node
    {
        public int id, layer = -1, model = -1;
        public bool hidden;
        public int[] children = Array.Empty<int>();
        public Matrix4x4 transform = Matrix4x4.identity;
    }
    sealed class VoxScene
    {
        public readonly List<Model> models = new List<Model>();
        public readonly Dictionary<int, Node> nodes = new Dictionary<int, Node>();
        public readonly HashSet<int> hiddenLayers = new HashSet<int>();
        public readonly Color32[] palette = new Color32[256];
        public bool hasPalette;
        public int instances;
        public int overlaps;
    }
    sealed class Grid
    {
        public Vector3Int size;
        public byte[] cells;
        public Vector3 anchor;
        public Color32[] palette;
        public int occupied;
        public byte Get(int x, int y, int z) => x < 0 || y < 0 || z < 0 || x >= size.x || y >= size.y || z >= size.z ? (byte)0 : cells[x + size.x * (y + size.y * z)];
        public byte Get(Vector3Int p) => Get(p.x,p.y,p.z);
    }
    static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > 1024 * 1024 || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Invalid VOX string length");
        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }
    static Dictionary<string,string> Dict(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > 4096) throw new InvalidDataException("Invalid VOX dictionary");
        var result = new Dictionary<string,string>();
        for (int i=0;i<count;i++) result.Add(ReadString(reader),ReadString(reader));
        return result;
    }
    static bool Hidden(Dictionary<string,string> attributes) => attributes.TryGetValue("_hidden",out var value) && value == "1";
    static Matrix4x4 Transform(Dictionary<string,string> frame)
    {
        var matrix = Matrix4x4.identity;
        if (frame.TryGetValue("_r",out var rotation))
        {
            int r = int.Parse(rotation,CultureInfo.InvariantCulture), a = r & 3, b = (r >> 2) & 3;
            if (a > 2 || b > 2 || a == b) throw new InvalidDataException("Invalid VOX rotation");
            matrix = Matrix4x4.zero; matrix[3,3] = 1;
            matrix[0,a] = (r & 16) == 0 ? 1 : -1;
            matrix[1,b] = (r & 32) == 0 ? 1 : -1;
            matrix[2,3-a-b] = (r & 64) == 0 ? 1 : -1;
        }
        if (frame.TryGetValue("_t",out var translation))
        {
            var values = translation.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != 3) throw new InvalidDataException("Invalid VOX translation");
            for (int axis=0;axis<3;axis++) matrix[axis,3] = int.Parse(values[axis],CultureInfo.InvariantCulture);
        }
        return matrix;
    }
    static VoxScene Read(string path)
    {
        var scene = new VoxScene();
        using (var reader = new BinaryReader(File.OpenRead(path)))
        {
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "VOX ") throw new InvalidDataException("Not a VOX file");
            int version=reader.ReadInt32();
            if (version != 150 && version != 200) throw new InvalidDataException("Unsupported VOX version " + version);
            Vector3Int size = Vector3Int.zero;
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                string tag=Encoding.ASCII.GetString(reader.ReadBytes(4)); int length=reader.ReadInt32(), children=reader.ReadInt32();
                long end=reader.BaseStream.Position+length;
                if(length<0 || children<0 || end+children>reader.BaseStream.Length)throw new InvalidDataException("Invalid VOX chunk bounds");
                if(tag=="MAIN") { if(length!=0)throw new InvalidDataException("Invalid MAIN"); continue; }
                if(children!=0)throw new InvalidDataException("Unsupported nested VOX chunk " + tag);
                switch(tag)
                {
                    case "SIZE":
                        size=new Vector3Int(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32());
                        if(size.x<=0||size.y<=0||size.z<=0||size.x>256||size.y>256||size.z>256)throw new InvalidDataException("Invalid model dimensions");
                        break;
                    case "XYZI":
                        int count=reader.ReadInt32();
                        if(count<0 || count>(long)size.x*size.y*size.z || length!=4+count*4)throw new InvalidDataException("Invalid voxel count");
                        var cells=reader.ReadBytes(count*4);
                        for(int i=0;i<cells.Length;i+=4)
                            if(cells[i]>=size.x||cells[i+1]>=size.y||cells[i+2]>=size.z||cells[i+3]==0)throw new InvalidDataException("Voxel outside model or palette");
                        scene.models.Add(new Model{size=size,cells=cells}); break;
                    case "RGBA":
                        if(length!=1024)throw new InvalidDataException("Invalid palette");
                        for(int i=1;i<=256;i++) { var c=new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte()); if(i<256)scene.palette[i]=c; }
                        scene.hasPalette=true; break;
                    case "nTRN":
                    {
                        int id=reader.ReadInt32(); var attributes=Dict(reader); int child=reader.ReadInt32(); reader.ReadInt32(); int layer=reader.ReadInt32();
                        if(reader.ReadInt32()!=1)throw new InvalidDataException("Animated VOX scenes are not supported by this static converter");
                        var frame=Dict(reader);
                        scene.nodes.Add(id,new Node{id=id,children=new[]{child},layer=layer,hidden=Hidden(attributes)||Hidden(frame),transform=Transform(frame)}); break;
                    }
                    case "nGRP":
                    {
                        int id=reader.ReadInt32();var attributes=Dict(reader);int countNodes=reader.ReadInt32();
                        if(countNodes<0||countNodes>4096)throw new InvalidDataException("Invalid group");
                        var ids=new int[countNodes];for(int i=0;i<ids.Length;i++)ids[i]=reader.ReadInt32();
                        scene.nodes.Add(id,new Node{id=id,children=ids,hidden=Hidden(attributes)});break;
                    }
                    case "nSHP":
                    {
                        int id=reader.ReadInt32();var attributes=Dict(reader);
                        if(reader.ReadInt32()!=1)throw new InvalidDataException("Animated shapes are not supported");
                        int model=reader.ReadInt32();Dict(reader);
                        scene.nodes.Add(id,new Node{id=id,model=model,hidden=Hidden(attributes)});break;
                    }
                    case "LAYR":
                    {
                        int id=reader.ReadInt32();if(Hidden(Dict(reader)))scene.hiddenLayers.Add(id);reader.ReadInt32();break;
                    }
                    case "IMAP": throw new InvalidDataException("Palette remapping is not supported; export a standard palette first");
                }
                if(reader.BaseStream.Position>end)throw new InvalidDataException("VOX chunk overrun: " + tag);
                reader.BaseStream.Position=end;
            }
        }
        if(!scene.hasPalette||scene.models.Count==0)throw new InvalidDataException("Missing models or palette");
        return scene;
    }
    static Grid Flatten(VoxScene scene)
    {
        var occupied=new Dictionary<Vector3Int,byte>();var visiting=new HashSet<int>();
        void AddModel(int index,Matrix4x4 matrix)
        {
            if(index<0||index>=scene.models.Count)throw new InvalidDataException("Invalid model reference");
            scene.instances++;var model=scene.models[index];var pivot=(Vector3)model.size*.5f;
            for(int i=0;i<model.cells.Length;i+=4)
            {
                var center=new Vector3(model.cells[i]+.5f,model.cells[i+1]+.5f,model.cells[i+2]+.5f)-pivot;
                var p=matrix.MultiplyPoint3x4(center);
                // VOX Z-up to Unity Y-up, applied before meshing so winding stays correct.
                var cell=new Vector3Int(Mathf.FloorToInt(p.x),Mathf.FloorToInt(p.z),Mathf.FloorToInt(p.y));
                if(occupied.ContainsKey(cell))scene.overlaps++;
                occupied[cell]=model.cells[i+3];
            }
        }
        void Visit(int id,Matrix4x4 parent)
        {
            if(!scene.nodes.TryGetValue(id,out var node)||!visiting.Add(id))throw new InvalidDataException("Broken/cyclic VOX scene graph");
            if(!node.hidden&&!scene.hiddenLayers.Contains(node.layer))
            {
                var world=parent*node.transform;
                if(node.model>=0)AddModel(node.model,world);
                foreach(int child in node.children)Visit(child,world);
            }
            visiting.Remove(id);
        }
        if(scene.nodes.Count==0)
        {
            if(scene.models.Count!=1)throw new InvalidDataException("Multiple models require scene transforms");
            AddModel(0,Matrix4x4.identity);
        }
        else
        {
            var children=new HashSet<int>(scene.nodes.Values.SelectMany(n=>n.children));
            var roots=scene.nodes.Keys.Where(id=>!children.Contains(id)).OrderBy(id=>id).ToArray();
            if(roots.Length==0)throw new InvalidDataException("Missing scene root");
            foreach(int id in roots)Visit(id,Matrix4x4.identity);
        }
        if(occupied.Count==0)throw new InvalidDataException("Empty visible scene");
        var min=occupied.Keys.First();var max=min;
        foreach(var p in occupied.Keys){min=Vector3Int.Min(min,p);max=Vector3Int.Max(max,p);}
        var size=max-min+Vector3Int.one;
        long volume=(long)size.x*size.y*size.z;
        if(volume>64000000)throw new InvalidDataException("Model too large for dense meshing");
        var grid=new Grid{size=size,cells=new byte[(int)volume],palette=scene.palette,occupied=occupied.Count};
        // Centre the pivot on the trunk's base rather than the often asymmetric crown.
        double sumX=0,sumZ=0;int baseCount=0;
        foreach(var pair in occupied)
        {
            var p=pair.Key-min;grid.cells[p.x+size.x*(p.y+size.y*p.z)]=pair.Value;
            if(p.y<Mathf.Max(1,Mathf.CeilToInt(size.y*.04f))){sumX+=p.x+.5;sumZ+=p.z+.5;baseCount++;}
        }
        grid.anchor=new Vector3((float)(sumX/baseCount),0,(float)(sumZ/baseCount));
        return grid;
    }
    static Mesh MakeMesh(Grid grid,float height,out long exposed,out long mergedArea)
    {
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        float scale=height/grid.size.y;exposed=0;mergedArea=0;
        for(int d=0;d<3;d++)
        {
            int u=(d+1)%3,v=(d+2)%3, width=grid.size[u],rows=grid.size[v];
            var mask=new int[width*rows];var step=Vector3Int.zero;step[d]=1;
            for(int slice=-1;slice<grid.size[d];slice++)
            {
                for(int j=0;j<rows;j++)for(int i=0;i<width;i++)
                {
                    var p=Vector3Int.zero;p[d]=slice;p[u]=i;p[v]=j;
                    byte a=grid.Get(p),b=grid.Get(p+step);
                    int face=a!=0&&b==0?a:a==0&&b!=0?-b:0;
                    mask[i+j*width]=face;if(face!=0)exposed++;
                }
                for(int j=0;j<rows;j++)for(int i=0;i<width;)
                {
                    int color=mask[i+j*width];if(color==0){i++;continue;}
                    int w=1;while(i+w<width&&mask[i+w+j*width]==color)w++;
                    int h=1;bool extend=true;
                    while(j+h<rows&&extend)
                    {
                        for(int k=0;k<w;k++)if(mask[i+k+(j+h)*width]!=color){extend=false;break;}
                        if(extend)h++;
                    }
                    var origin=Vector3.zero;origin[d]=slice+1;origin[u]=i;origin[v]=j;
                    var du=Vector3.zero;du[u]=w;var dv=Vector3.zero;dv[v]=h;
                    int first=vertices.Count;
                    vertices.Add((origin-grid.anchor)*scale);vertices.Add((origin+du-grid.anchor)*scale);
                    vertices.Add((origin+du+dv-grid.anchor)*scale);vertices.Add((origin+dv-grid.anchor)*scale);
                    var normal=Vector3.zero;normal[d]=color>0?1:-1;
                    for(int k=0;k<4;k++){normals.Add(normal);uv.Add(new Vector2((Math.Abs(color)+.5f)/256,.5f));}
                    if(color>0)triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
                    else triangles.AddRange(new[]{first,first+2,first+1,first,first+3,first+2});
                    for(int y=0;y<h;y++)for(int x=0;x<w;x++)mask[i+x+(j+y)*width]=0;
                    mergedArea+=(long)w*h;i+=w;
                }
            }
        }
        if(exposed!=mergedArea)throw new InvalidOperationException("Surface area changed during meshing");
        var mesh=new Mesh{indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        // This verifies winding after coordinate conversion, not only triangle counts.
        for(int i=0;i<triangles.Count;i+=3)
        {
            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
            if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normals[a])<=0)throw new InvalidOperationException("Inverted/degenerate face");
        }
        return mesh;
    }
    static Grid Reduce(Grid source,int factor)
    {
        if(factor==1)return source;
        var size=new Vector3Int((source.size.x+factor-1)/factor,(source.size.y+factor-1)/factor,(source.size.z+factor-1)/factor);
        var result=new Grid{size=size,cells=new byte[size.x*size.y*size.z],palette=source.palette,anchor=source.anchor/factor};
        var counts=new int[256];
        for(int z=0;z<size.z;z++)for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)
        {
            Array.Clear(counts,0,counts.Length);int best=0;byte color=0;
            for(int dz=0;dz<factor;dz++)for(int dy=0;dy<factor;dy++)for(int dx=0;dx<factor;dx++)
            {
                byte c=source.Get(x*factor+dx,y*factor+dy,z*factor+dz);if(c==0)continue;
                int count=++counts[c];if(count>best){best=count;color=c;}
            }
            result.cells[x+size.x*(y+size.y*z)]=color;if(color!=0)result.occupied++;
        }
        return result;
    }
    static Mesh SaveMesh(Mesh mesh,string name)
    {
        mesh.name=name;string path=Meshes+name+".asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
    }
    static void Folder(string path)
    {
        path=path.TrimEnd('/');if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static GameObject Convert(string source,string name,float height,float trunkRadius,float trunkHeight)
    {
        var scene=Read(Source+source+".vox");var originalGrid=Flatten(scene);
        Grid grid=null;Mesh mesh=null;long exposed=0,mergedArea=0;
        int factor=originalGrid.occupied>100000?2:1;
        for(;factor<=8;factor++)
        {
            grid=Reduce(originalGrid,factor);mesh=MakeMesh(grid,height,out exposed,out mergedArea);
            if(mesh.triangles.Length/3<=TriangleBudget)break;
            Object.DestroyImmediate(mesh);mesh=null;
        }
        if(mesh==null)throw new InvalidOperationException("Unable to meet the per-tree triangle budget");
        var savedMesh=SaveMesh(mesh,name);
        var lod1=SaveMesh(MakeMesh(Reduce(originalGrid,factor*2),height,out _,out _),name+"_LOD1");
        var lod2=SaveMesh(MakeMesh(Reduce(originalGrid,factor*4),height,out _,out _),name+"_LOD2");
        var palette=new Texture2D(256,1,TextureFormat.RGBA32,false);palette.SetPixels32(grid.palette);palette.Apply();
        string palettePath=Textures+name+"Palette.png";File.WriteAllBytes(palettePath,palette.EncodeToPNG());Object.DestroyImmediate(palette);AssetDatabase.ImportAsset(palettePath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(palettePath);importer.sRGBTexture=true;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
        importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
        var material=AssetDatabase.LoadAssetAtPath<Material>(Materials+name+".mat");
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Materials+name+".mat");}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath));material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_Smoothness",0);material.SetFloat("_Metallic",0);material.enableInstancing=true;EditorUtility.SetDirty(material);
        var root=new GameObject(name);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh=savedMesh;root.AddComponent<MeshRenderer>().sharedMaterial=material;
            var lodRenderers=new Renderer[3];lodRenderers[0]=root.GetComponent<MeshRenderer>();
            foreach(var level in new[]{1,2})
            {
                var child=new GameObject("LOD"+level);child.transform.SetParent(root.transform,false);
                child.AddComponent<MeshFilter>().sharedMesh=level==1?lod1:lod2;lodRenderers[level]=child.AddComponent<MeshRenderer>();lodRenderers[level].sharedMaterial=material;
            }
            var lods=root.AddComponent<LODGroup>();lods.SetLODs(new[]{new LOD(.25f,new[]{lodRenderers[0]}),new LOD(.08f,new[]{lodRenderers[1]}),new LOD(.015f,new[]{lodRenderers[2]})});lods.RecalculateBounds();
            var collider=root.AddComponent<CapsuleCollider>();collider.radius=trunkRadius;collider.height=trunkHeight;collider.center=Vector3.up*trunkHeight*.5f;
            root.AddComponent<ClimbableTree>();root.AddComponent<VegetationMotionBinding>().kind=WorldAssetKind.Tree;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+name+".prefab");
            if(Mathf.Abs(savedMesh.bounds.min.y)>.001f||Mathf.Abs(savedMesh.bounds.size.y-height)>.001f)throw new InvalidOperationException("Incorrect grounded bounds/height");
            if(prefab.GetComponent<MeshCollider>()!=null)throw new InvalidOperationException("Canopy must not block navigation");
            report.Add(name+": models="+scene.models.Count+", instances="+scene.instances+", original occupied="+originalGrid.occupied+", overlaps merged="+scene.overlaps+", game reduction="+factor+", game grid="+grid.size+", exposed unit faces="+exposed+", merged quads="+(savedMesh.vertexCount/4)+", triangles LOD0/1/2="+savedMesh.triangles.Length/3+"/"+lod1.triangles.Length/3+"/"+lod2.triangles.Length/3+", bounds(m)="+savedMesh.bounds.size.ToString("F2")+", pivot="+grid.anchor);
            return prefab;
        }
        finally{Object.DestroyImmediate(root);}
    }
    static void MeshingChecks()
    {
        var test=new Grid{size=new Vector3Int(2,2,2),cells=Enumerable.Repeat((byte)1,8).ToArray(),anchor=Vector3.zero};
        var cube=MakeMesh(test,2,out long surface,out long area);
        if(cube.vertexCount!=24||surface!=24||area!=24)throw new InvalidOperationException("Solid cube meshing regression");Object.DestroyImmediate(cube);
        test.cells[0]=2;var colored=MakeMesh(test,2,out surface,out area);
        if(surface!=24||colored.uv.Select(p=>p.x).Distinct().Count()!=2)throw new InvalidOperationException("Palette boundary regression");Object.DestroyImmediate(colored);
        var m=Transform(new Dictionary<string,string>{{"_r","33"},{"_t","10 20 30"}});
        if(m.MultiplyPoint3x4(new Vector3(1,2,3))!=new Vector3(12,19,33))throw new InvalidOperationException("VOX rotation regression");
        report.Add("Meshing checks passed: internal-face removal, exact surface area, color boundaries, flat outward normals, scene rotation/translation.");
    }
    [MenuItem("Mismo/World/MountainLabs/Convertir arbol y pino de muestra")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Salir de Play Mode antes de convertir");
        report.Clear();Directory.CreateDirectory(Output);
        foreach(string folder in new[]{Meshes,Materials,Textures,Prefabs,"Assets/Scenes/Previews"})Folder(folder);
        MeshingChecks();
        var live=Convert("Live Tree","MountainLabs_LiveTree",8,.38f,3.4f);
        var pine=Convert("Fluffy Pine","MountainLabs_FluffyPine",6,.2f,2.4f);
        AssetDatabase.SaveAssets();
        File.WriteAllLines(Output+"/conversion.txt",report);
        CreatePreview(live,pine);
        File.WriteAllLines(Output+"/conversion.txt",report);
        try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","Passed");}
        catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.Message);Debug.LogWarning("Conversion completed; existing project organization issues listed in "+Output+"/organization.txt");}
        Debug.Log("MOUNTAINLABS_CONVERSION_OK\n"+string.Join("\n",report));
    }
    static void CreatePreview(GameObject live,GameObject pine)
    {
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        // Batch validation starts with an empty untitled scene. Never discard a user's scene.
        bool replaceEmptyBatch=Application.isBatchMode&&string.IsNullOrEmpty(previous.path)&&!previous.isDirty;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,replaceEmptyBatch?NewSceneMode.Single:NewSceneMode.Additive);
        try
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var a=(GameObject)PrefabUtility.InstantiatePrefab(live,scene);a.transform.position=new Vector3(-5,0,0);
            var b=(GameObject)PrefabUtility.InstantiatePrefab(pine,scene);b.transform.position=new Vector3(5,0,0);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Preview ground";ground.transform.position=new Vector3(0,-.15f,0);ground.transform.localScale=new Vector3(32,.3f,22);
            string floorPath=Materials+"PreviewGround.mat";var floor=AssetDatabase.LoadAssetAtPath<Material>(floorPath);
            if(floor==null){floor=new Material(Shader.Find("Universal Render Pipeline/Lit"));floor.SetColor("_BaseColor",new Color(.23f,.29f,.20f));floor.SetFloat("_Smoothness",0);AssetDatabase.CreateAsset(floor,floorPath);}
            floor.SetTexture("_BaseMap",Texture2D.whiteTexture);EditorUtility.SetDirty(floor);
            ground.GetComponent<Renderer>().sharedMaterial=floor;
            var scale=GameObject.CreatePrimitive(PrimitiveType.Capsule);scale.name="Character scale - 1.8 metres";scale.transform.position=new Vector3(0,.9f,-2);scale.transform.localScale=new Vector3(.6f,.9f,.6f);
            string figurePath=Materials+"PreviewCharacter.mat";var figure=AssetDatabase.LoadAssetAtPath<Material>(figurePath);
            if(figure==null){figure=new Material(Shader.Find("Universal Render Pipeline/Lit"));figure.SetColor("_BaseColor",new Color(.85f,.7f,.48f));figure.SetFloat("_Smoothness",0);AssetDatabase.CreateAsset(figure,figurePath);}
            figure.SetTexture("_BaseMap",Texture2D.whiteTexture);EditorUtility.SetDirty(figure);
            scale.GetComponent<Renderer>().sharedMaterial=figure;
            var sun=new GameObject("Preview sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.7f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);RenderSettings.sun=sun;
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.65f,.75f);RenderSettings.ambientEquatorColor=new Color(.35f,.39f,.32f);RenderSettings.ambientGroundColor=new Color(.18f,.16f,.13f);
            var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.43f,.58f,.69f);camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.fieldOfView=42;
            camera.scene=scene;
            camera.transform.position=new Vector3(16,11,-26);camera.transform.LookAt(new Vector3(0,3.3f,0));
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
            Capture(camera,"comparison");
            camera.transform.position=new Vector3(-5,5,-14);camera.transform.LookAt(a.transform.position+Vector3.up*3.8f);Capture(camera,"live-tree");
            camera.transform.position=new Vector3(5,3.5f,-11);camera.transform.LookAt(b.transform.position+Vector3.up*2.8f);Capture(camera,"fluffy-pine");
            // Wind test uses the real prefab + existing shader, never changes saved materials.
            var catalog=AssetDatabase.LoadAssetAtPath<WorldContentCatalog>("Assets/Data/World/WorldContentCatalog.asset");
            var host=new GameObject("Temporary wind validation");var wind=host.AddComponent<VegetationMotionWorld>();wind.Initialize(catalog,scale.transform);
            a.GetComponent<VegetationMotionBinding>().Apply();b.GetComponent<VegetationMotionBinding>().Apply();wind.Tick(1.3f);
            if(a.GetComponent<Renderer>().sharedMaterial.shader!=catalog.vegetationMotion.shader||b.GetComponent<Renderer>().sharedMaterial.shader!=catalog.vegetationMotion.shader)throw new InvalidOperationException("Wind material integration failed");
            var block=new MaterialPropertyBlock();a.GetComponent<Renderer>().GetPropertyBlock(block);
            if(block.GetVector("_VegetationResponse").x!=0)throw new InvalidOperationException("Tree must not bend from player proximity");
            camera.transform.position=new Vector3(16,11,-26);camera.transform.LookAt(new Vector3(0,3.3f,0));Capture(camera,"comparison-wind");
            var errors=ShaderUtil.GetShaderMessages(catalog.vegetationMotion.shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            if(errors.Length>0)throw new InvalidOperationException(string.Join(";",errors.Select(e=>e.message)));
            Object.DestroyImmediate(host);
            report.Add("Preview saved; prefab/material references, grounded pivots, requested heights, trunk-only colliders, original palette, wind binding and GPU shader checked. WorldContentCatalog and biome profiles untouched.");
        }
        finally{if(previous.IsValid()&&previous.isLoaded){UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}}
    }
    static void Capture(Camera camera,string name)
    {
        // Immediate editor renders have no frame update to refresh SRP material buffers.
        bool batching=GraphicsSettings.useScriptableRenderPipelineBatching;
        GraphicsSettings.useScriptableRenderPipelineBatching=false;
        foreach(var root in camera.scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            var material=renderer.sharedMaterial;if(material==null)continue;
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap",material.GetTexture("_BaseMap")??Texture2D.whiteTexture);
            block.SetColor("_BaseColor",material.GetColor("_BaseColor"));renderer.SetPropertyBlock(block);
        }
        var target=new RenderTexture(1440,1000,24);var previous=RenderTexture.active;var texture=new Texture2D(1440,1000,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1440,1000),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);GraphicsSettings.useScriptableRenderPipelineBatching=batching;}
    }
    public static void RunBatch()
    {
        try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
