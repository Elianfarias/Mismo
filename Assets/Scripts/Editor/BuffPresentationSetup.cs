using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Core;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BuffPresentationSetup
{
    public const string Output="output/buffs";
    public const string ProfilePath="Assets/Data/Combat/Buffs/BuffPresentation.asset";
    const string Meshes="Assets/Art/Meshes/Combat/Buffs",Icons="Assets/Art/UI/Combat/Buffs",Materials="Assets/Art/Materials/Combat/Buffs";
    public static void Batch(){Install();BuffPresentationChecks.Run();}
    [MenuItem("Mismo/Combate/Buffs/Crear presentación inicial")]
    public static void Install()
    {
        Directory.CreateDirectory(Output);
        foreach(string folder in new[]{Meshes,Icons,Materials,"Assets/Data/Combat/Buffs"})ProjectAssetOrganizer.EnsureFolder(folder);
        var material=AssetDatabase.LoadAssetAtPath<Material>(Materials+"/BuffSymbols.mat");
        if(material==null)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/BuffSymbol.shader");
            if(shader==null)throw new InvalidOperationException("Buff shader missing");
            material=new Material(shader);AssetDatabase.CreateAsset(material,Materials+"/BuffSymbols.mat");
        }
        var profile=AssetDatabase.LoadAssetAtPath<BuffPresentation>(ProfilePath);
        if(profile==null)
        {
            profile=ScriptableObject.CreateInstance<BuffPresentation>();profile.material=material;
            profile.styles=new BuffPresentation.Style[4];
            var colors=new[]{new Color(.58f,.94f,1),new Color(1,.24f,.29f),new Color(.25f,.49f,1),new Color(.74f,.38f,1)};
            for(int i=0;i<4;i++)
            {
                var kind=(BuffKind)i;var mesh=Symbol(kind);var path=Meshes+"/"+kind+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else Object.DestroyImmediate(mesh);
                profile.styles[i]=new BuffPresentation.Style{kind=kind,color=colors[i],symbol=saved,icon=Icon(saved,kind),size=i==0?.56f:.27f};
            }
            var arcPath=Meshes+"/AnkleArc.asset";
            profile.footArc=AssetDatabase.LoadAssetAtPath<Mesh>(arcPath);
            if(profile.footArc==null){profile.footArc=Arc();AssetDatabase.CreateAsset(profile.footArc,arcPath);}
            AssetDatabase.CreateAsset(profile,ProfilePath);
        }
        var catalog=AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        var entry=catalog.entries.FirstOrDefault(e=>e.key==BuffPresentation.CatalogKey);
        if(entry==null)
        {
            catalog.entries=catalog.entries.Concat(new[]{new RuntimeAssetCatalog.Entry{key=BuffPresentation.CatalogKey,assets=new Object[]{profile}}}).ToArray();
            catalog.Invalidate();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        }
        else if(!entry.assets.Contains(profile))throw new InvalidOperationException("Combat/Buffs already points to another profile");
        AssetDatabase.SaveAssets();
    }
    sealed class Shape
    {
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();readonly List<Color> colors=new List<Color>();
        public void Polygon(Vector2[] points,float alpha=1,float rotation=0)
        {
            int start=vertices.Count;var q=Quaternion.Euler(0,0,rotation);
            foreach(var p in points){vertices.Add(q*new Vector3(p.x,p.y,0));colors.Add(new Color(1,1,1,alpha));}
            for(int i=1;i<points.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
        }
        public void Line(Vector2 a,Vector2 b,float width,float alpha=1)
        {var d=(b-a).normalized;var side=new Vector2(-d.y,d.x)*width/2;Polygon(new[]{a-side,a+side,b+side,b-side},alpha);}
        public Mesh Build(string name)
        {var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
    }
    static Mesh Symbol(BuffKind kind)
    {
        var s=new Shape();
        if(kind==BuffKind.Damage)
        {
            foreach(float angle in new[]{-42f,42f})
            {
                s.Polygon(new[]{new Vector2(-.055f,-.12f),new Vector2(.055f,-.12f),new Vector2(.055f,.30f),new Vector2(0,.43f),new Vector2(-.055f,.30f)},1,angle);
                s.Polygon(new[]{new Vector2(-.18f,-.18f),new Vector2(.18f,-.18f),new Vector2(.18f,-.105f),new Vector2(-.18f,-.105f)},1,angle);
                s.Polygon(new[]{new Vector2(-.038f,-.37f),new Vector2(.038f,-.37f),new Vector2(.038f,-.18f),new Vector2(-.038f,-.18f)},1,angle);
            }
        }
        else if(kind==BuffKind.Speed)
        {
            for(int i=0;i<2;i++){float y=i*.3f-.25f;s.Line(new Vector2(-.3f,y),new Vector2(0,y+.25f),.075f);s.Line(new Vector2(0,y+.25f),new Vector2(.3f,y),.075f);}
        }
        else
        {
            var points=kind==BuffKind.Defense?new[]{new Vector2(-.32f,.32f),new Vector2(0,.43f),new Vector2(.32f,.32f),new Vector2(.27f,-.12f),new Vector2(0,-.43f),new Vector2(-.27f,-.12f)}
                :new[]{new Vector2(-.3f,.25f),new Vector2(0,.44f),new Vector2(.3f,.25f),new Vector2(.3f,-.25f),new Vector2(0,-.44f),new Vector2(-.3f,-.25f)};
            s.Polygon(points,kind==BuffKind.Shield?.11f:.065f);
            for(int i=0;i<points.Length;i++)s.Line(points[i],points[(i+1)%points.Length],kind==BuffKind.Shield?.028f:.055f);
            s.Line(new Vector2(0,-.20f),new Vector2(0,.24f),kind==BuffKind.Shield?.015f:.045f,kind==BuffKind.Shield?.45f:1);
            if(kind==BuffKind.Defense)s.Line(new Vector2(-.13f,.10f),new Vector2(.13f,.10f),.045f);
        }
        return s.Build(kind+" buff symbol");
    }
    static Mesh Arc()
    {
        var s=new Shape();
        for(int part=0;part<3;part++)for(int segment=0;segment<8;segment++)
        {
            float a=(part*120+segment*5)*Mathf.Deg2Rad,b=a+5*Mathf.Deg2Rad;
            s.Polygon(new[]{new Vector2(Mathf.Sin(a),Mathf.Cos(a)),new Vector2(Mathf.Sin(a),Mathf.Cos(a))*.978f,new Vector2(Mathf.Sin(b),Mathf.Cos(b))*.978f,new Vector2(Mathf.Sin(b),Mathf.Cos(b))});
        }
        var mesh=s.Build("Subtle ankle arcs");var points=mesh.vertices;
        for(int i=0;i<points.Length;i++)points[i]=new Vector3(points[i].x,0,points[i].y);
        mesh.vertices=points;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static Texture2D Icon(Mesh mesh,BuffKind kind)
    {
        string path=Icons+"/"+kind+".png";
        if(!File.Exists(path))
        {
            const int size=64;var pixels=new Color[size*size];var vertices=mesh.vertices;var triangles=mesh.triangles;var colors=mesh.colors;
            float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var p=new Vector2((x+.5f)/size-.5f,(y+.5f)/size-.5f);float alpha=0;
                for(int t=0;t<triangles.Length;t+=3)
                {
                    int i=triangles[t];Vector2 a=vertices[i],b=vertices[triangles[t+1]],c=vertices[triangles[t+2]];
                    float d=Cross(b-a,c-a),u=Cross(p-a,c-a)/d,v=Cross(b-a,p-a)/d;
                    if(u>=0&&v>=0&&u+v<=1)alpha=Mathf.Max(alpha,colors[i].a);
                }
                pixels[y*size+x]=new Color(1,1,1,alpha);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
