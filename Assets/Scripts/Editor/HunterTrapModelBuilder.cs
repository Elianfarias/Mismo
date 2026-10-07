using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Deterministic voxel authoring. Only exposed faces are saved; the runtime shares four meshes.</summary>
public static class HunterTrapModelBuilder
{
    public const string Output = "output/hunter-trap";
    public const string PrefabPath = "Assets/Art/Prefabs/Combat/HunterTrap/HunterTrap.prefab";
    public const string AbilityPath = "Assets/Data/Weapons/Bow/HunterTrap.asset";
    const string MeshRoot = "Assets/Art/Meshes/Combat/HunterTrap";
    const string MaterialPath = "Assets/Art/Materials/Combat/HunterTrap/ForgedSteel.mat";
    const string PalettePath = "Assets/Art/Textures/Combat/HunterTrap/SteelPalette.png";
    const float Step = .0125f;
    static readonly List<string> report = new List<string>();

    public static void Batch()
    {
        try { CreateAndIntegrate(); HunterTrapChecks.Run(); Preview(); }
        catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/FAILED.txt", e.ToString()); Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }
    [MenuItem("Mismo/Armas/Trampa de cazador/Crear modelo e integrar")]
    public static void CreateAndIntegrate()
    {
        Directory.CreateDirectory(Output); report.Clear();
        foreach (var path in new[] { MeshRoot, Path.GetDirectoryName(PrefabPath), Path.GetDirectoryName(MaterialPath), Path.GetDirectoryName(PalettePath) })
            ProjectAssetOrganizer.EnsureFolder(path.Replace('\\', '/'));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            var material = SteelMaterial();
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("HunterTrap"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var baseGrid = Base();
                Piece(root, "Base and chain", baseGrid, material, Vector3.zero);
                var front = Jaw(1); var back = Jaw(-1);
                CheckConnected(front, "Front jaw"); CheckConnected(back, "Back jaw");
                var frontTransform = Piece(root, "Front jaw", front, material, new Vector3(0, .075f, .0125f));
                var backTransform = Piece(root, "Back jaw", back, material, new Vector3(0, .075f, -.0125f));
                var plate = Piece(root, "Pressure plate", Plate(), material, new Vector3(0, .055f, 0));
                var visual = root.AddComponent<HunterTrapVisual>();
                visual.frontJaw = frontTransform; visual.backJaw = backTransform; visual.pressurePlate = plate;
                root.AddComponent<HunterTrap>();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                report.Add("Four shared rigid meshes, one material, no visual colliders or runtime mesh generation.");
                report.Add("Voxel pitch 0.0125 m; jaws form two connected semicircles across two common hinge blocks.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        else report.Add("Existing prefab and authored meshes preserved.");
        var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AbilityPath);
        var action = ability.actions.OfType<TrapAction>().Single();
        if (action.prefab == null) { action.prefab = prefab; EditorUtility.SetDirty(ability); AssetDatabase.SaveAssetIfDirty(ability); }
        else if (action.prefab != prefab) throw new InvalidOperationException("La habilidad ya tiene otro prefab asignado; se conserva.");
        report.Add("Serialized reference: HunterTrap ability -> TrapAction.prefab -> model, meshes and palette.");
        File.WriteAllLines(Output + "/model.txt", report);
    }
    static Transform Piece(GameObject root, string name, EnchantedGroveVoxels grid, Material material, Vector3 pivot)
    {
        string path = MeshRoot + "/" + name.Replace(" ", "") + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) throw new IOException("Existing authored mesh is preserved: " + path);
        var mesh = grid.Mesh(name); AssetDatabase.CreateAsset(mesh, path);
        var go = new GameObject(name); go.transform.SetParent(root.transform, false); go.transform.localPosition = pivot;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        report.Add(name + ": " + grid.Cells.Count + " voxels, " + mesh.triangles.Length / 3 + " triangles.");
        return go.transform;
    }
    static void Fill(EnchantedGroveVoxels grid, Vector3 min, Vector3 max, Func<Vector3, byte> sample)
    {
        var lo = Vector3Int.FloorToInt(min / Step); var hi = Vector3Int.CeilToInt(max / Step);
        for (int z = lo.z; z < hi.z; z++) for (int y = lo.y; y < hi.y; y++) for (int x = lo.x; x < hi.x; x++)
        {
            var cell = new Vector3Int(x,y,z); var point = ((Vector3)cell + Vector3.one * .5f) * Step;
            byte color = sample(point); if (color != 0) grid.Cells[cell] = color;
        }
    }
    static byte Shade(Vector3 p, int palette)
    {
        var cell = Vector3Int.FloorToInt(p / Step);
        uint hash = unchecked((uint)(cell.x * 73856093 ^ cell.y * 19349663 ^ cell.z * 83492791));
        return (byte)(palette + hash % 3);
    }
    static EnchantedGroveVoxels Jaw(int side)
    {
        var grid = new EnchantedGroveVoxels(Step);
        Fill(grid, new Vector3(-.475f,0,side > 0 ? 0 : -.475f), new Vector3(.475f,.16f,side > 0 ? .475f : 0), p => {
            float radius = new Vector2(p.x,p.z).magnitude;
            if (radius < .405f || radius > .4625f) return 0;
            float angle = Mathf.Atan2(Mathf.Abs(p.z),p.x) * Mathf.Rad2Deg;
            float top = .05f;
            // Stagger opposing teeth so their tips interleave when the jaws close.
            for (int i=0;i<9;i++)
            {
                float center = (side > 0 ? 12 : 21) + i * 18;
                float tooth = Mathf.Max(0, 1 - Mathf.Abs(angle - center) / 6.8f);
                top = Mathf.Max(top, .05f + .10f * tooth);
            }
            if (p.y > top) return 0;
            bool edge = p.y > top - Step * 1.2f || radius < .418f;
            return Shade(p,edge ? 5 : 1);
        });
        return grid;
    }
    static EnchantedGroveVoxels Base()
    {
        var grid = new EnchantedGroveVoxels(Step);
        Fill(grid,new Vector3(-.53f,0,-.115f),new Vector3(.53f,.14f,.115f),p=> {
            bool bar = Mathf.Abs(p.z)<.0375f && p.y<.05f;
            bool feet = Mathf.Abs(p.x)>.4f && Mathf.Abs(p.x)<.525f && Mathf.Abs(p.z)<.105f && p.y<.025f;
            bool hinge = Mathf.Abs(p.x)>.425f && Mathf.Abs(p.x)<.5f && Mathf.Abs(p.z)<.077f && p.y<.125f;
            // Bright end caps on the common axle visually connect both half-ring ends.
            bool pin = Mathf.Abs(p.x)>.495f && new Vector2(p.y-.075f,p.z).magnitude<.03f;
            if (!(bar || feet || hinge || pin)) return 0;
            return Shade(p,pin ? 9 : hinge ? 1 : 13);
        });
        // Closed chain links alternate their plane and overlap at their tips, as one static cosmetic mesh.
        for(int i=0;i<4;i++)
        {
            var center=new Vector3(.535f+i*.078f,.04f+(i%2)*.0125f,.025f+i*.014f);
            int link=i;
            Fill(grid,center-new Vector3(.073f,.06f,.065f),center+new Vector3(.073f,.06f,.065f),p=>{
                var q=p-center;
                float shortAxis=link%2==0?q.z:q.y;
                float thickness=link%2==0?Mathf.Abs(q.y):Mathf.Abs(q.z);
                float ellipse=Mathf.Sqrt(q.x*q.x/(.057f*.057f)+shortAxis*shortAxis/(.032f*.032f));
                return thickness<.013f && ellipse>.68f && ellipse<1.22f ? Shade(p,13) : (byte)0;
            });
        }
        return grid;
    }
    static EnchantedGroveVoxels Plate()
    {
        var grid = new EnchantedGroveVoxels(Step);
        Fill(grid,new Vector3(-.1375f,0,-.1375f),new Vector3(.1375f,.026f,.1375f),p=>{
            float r=new Vector2(p.x,p.z).magnitude;
            return r<.131f?Shade(p,r>.112f?5:9):(byte)0;
        });
        return grid;
    }
    static void CheckConnected(EnchantedGroveVoxels grid, string name)
    {
        var visited = new HashSet<Vector3Int>(); var pending = new Queue<Vector3Int>();
        var first = grid.Cells.Keys.First(); visited.Add(first); pending.Enqueue(first);
        var directions = new[] { Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,Vector3Int.forward,Vector3Int.back };
        while(pending.Count>0) { var cell=pending.Dequeue(); foreach(var d in directions) if(grid.Cells.ContainsKey(cell+d)&&visited.Add(cell+d))pending.Enqueue(cell+d); }
        if(visited.Count!=grid.Cells.Count)throw new Exception(name+" has disconnected voxels");
        report.Add("PASS " + name + ": every tooth and band voxel is connected; no gaps in the semicircle.");
    }
    static Material SteelMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath); if (material != null) return material;
        if (!File.Exists(PalettePath))
        {
            var palette = new Texture2D(64,1,TextureFormat.RGB24,false);
            var colors = Enumerable.Repeat(Color.magenta,64).ToArray();
            Color[] bases = { new Color(.24f,.26f,.28f), new Color(.55f,.58f,.60f), new Color(.37f,.39f,.41f), new Color(.20f,.22f,.24f) };
            int[] indices = {1,5,9,13};
            for(int i=0;i<indices.Length;i++)for(int j=0;j<3;j++)colors[indices[i]+j]=bases[i]*(.94f+j*.06f);
            palette.SetPixels(colors); palette.Apply(); File.WriteAllBytes(PalettePath,palette.EncodeToPNG()); Object.DestroyImmediate(palette);
            AssetDatabase.ImportAsset(PalettePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(PalettePath);
            importer.mipmapEnabled=false; importer.filterMode=FilterMode.Point; importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ForgedSteel" };
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath));
        material.SetFloat("_Metallic",.45f); material.SetFloat("_Smoothness",.3f);
        AssetDatabase.CreateAsset(material,MaterialPath); return material;
    }
    [MenuItem("Mismo/Armas/Trampa de cazador/Renderizar abierta y cerrada")]
    public static void Preview()
    {
        Directory.CreateDirectory(Output);
        var preview=new PreviewRenderUtility();
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));preview.AddSingleGO(root);
        try
        {
            var camera=preview.camera;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.fieldOfView=31;
            camera.backgroundColor=new Color(.095f,.11f,.12f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.position=new Vector3(1.15f,1.45f,1.7f);camera.transform.LookAt(new Vector3(.1f,.07f,0));
            preview.lights[0].intensity=2.1f;preview.lights[0].transform.rotation=Quaternion.Euler(40,200,0);
            preview.lights[1].intensity=1.2f;preview.lights[1].transform.rotation=Quaternion.Euler(55,-40,0);
            preview.ambientColor=new Color(.55f,.6f,.65f);
            Render(preview,"trap-open.png");
            root.GetComponent<HunterTrapVisual>().SetClosure(1);Render(preview,"trap-closed.png");
        }
        finally {preview.Cleanup();}
    }
    static void Render(PreviewRenderUtility preview,string file)
    {
        preview.BeginPreview(new Rect(0,0,1200,1000),GUIStyle.none);preview.Render(true);var rendered=preview.EndPreview();
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1200,1000,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var image=new Texture2D(1200,1000,TextureFormat.RGB24,false);
        try {Graphics.Blit(rendered,rt);RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1200,1000),0,0);image.Apply();File.WriteAllBytes(Output+"/"+file,image.EncodeToPNG());}
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
}
