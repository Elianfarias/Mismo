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
    // Tajo sangrante's mark: drop symbol and axe-head glow. Only fills fields that are still empty.
    [MenuItem("Mismo/Combate/Buffs/Crear marca de sangrado")]
    public static void InstallBleedMark()
    {
        foreach(string folder in new[]{Meshes,Materials})ProjectAssetOrganizer.EnsureFolder(folder);
        var profile=AssetDatabase.LoadAssetAtPath<BuffPresentation>(ProfilePath);
        if(profile==null)throw new InvalidOperationException("Buff presentation missing: "+ProfilePath);
        string meshPath=Meshes+"/BleedDrop.asset";
        var drop=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(drop==null){drop=Drop();AssetDatabase.CreateAsset(drop,meshPath);}
        string glowPath=Materials+"/BleedMarkBladeGlow.mat";
        var glow=AssetDatabase.LoadAssetAtPath<Material>(glowPath);
        if(glow==null)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/SwordBladeGlow.shader");
            if(shader==null)throw new InvalidOperationException("Blade glow shader missing");
            glow=new Material(shader);glow.SetColor("_Color",new Color(.77f,.12f,.18f,.55f));AssetDatabase.CreateAsset(glow,glowPath);
        }
        if(profile.bleedMarkSymbol==null)profile.bleedMarkSymbol=drop;
        if(profile.bladeGlowMaterial==null)profile.bladeGlowMaterial=glow;
        EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
    }
    // Modo Berserker: axe embers, ground ring and screen edge. Only creates what is missing and fills empty fields.
    [MenuItem("Mismo/Combate/Buffs/Crear efectos del Berserker")]
    public static void InstallBerserk()
    {
        const string Prefabs="Assets/Art/Prefabs/Combat/Buffs";
        foreach(string folder in new[]{Meshes,Materials,Icons,Prefabs})ProjectAssetOrganizer.EnsureFolder(folder);
        var profile=AssetDatabase.LoadAssetAtPath<BuffPresentation>(ProfilePath);
        if(profile==null)throw new InvalidOperationException("Buff presentation missing: "+ProfilePath);
        var emberMaterial=MaterialAt(Materials+"/BerserkEmbers.mat","Assets/Art/Shaders/CombatParticles.shader",Color.white);
        string prefabPath=Prefabs+"/BerserkEmbers.prefab";
        // One emitter the size of an axe head; an older body-and-crown version is rebuilt in place, keeping its GUID.
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(existing==null||existing.GetComponentsInChildren<ParticleSystem>().Length!=1)
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root=new GameObject("BerserkEmbers");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try
            {
                Embers(root,"Embers",emberMaterial,Vector3.zero,new Vector3(.16f,.22f,.16f),20,120,new Vector2(.4f,.8f),new Vector2(.5f,1),new Vector2(.025f,.05f));
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
        }
        string ringPath=Meshes+"/FuryRing.asset";
        var ring=AssetDatabase.LoadAssetAtPath<Mesh>(ringPath);
        if(ring==null){ring=Ring();AssetDatabase.CreateAsset(ring,ringPath);}
        string vignettePath=Icons+"/FuryVignette.png";
        if(!File.Exists(vignettePath))
        {
            const int size=128;var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float d=new Vector2((x+.5f)/size*2-1,(y+.5f)/size*2-1).magnitude;
                pixels[y*size+x]=new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1.25f,d)));
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels(pixels);texture.Apply();
            File.WriteAllBytes(vignettePath,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(vignettePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(vignettePath);
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;
            importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        if(profile.furyEmbers==null)profile.furyEmbers=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(profile.furyRing==null)profile.furyRing=ring;
        if(profile.furyVignette==null)profile.furyVignette=AssetDatabase.LoadAssetAtPath<Texture2D>(vignettePath);
        EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
    }
    static Material MaterialAt(string path,string shaderPath,Color tint)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null)return material;
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if(shader==null)throw new InvalidOperationException("Missing shader "+shaderPath);
        material=new Material(shader);material.SetColor("_Color",tint);AssetDatabase.CreateAsset(material,path);return material;
    }
    // Voxel embers that rise in world space, yellow to orange to red; BerserkVisual drives their rate and speed.
    static void Embers(GameObject parent,string name,Material material,Vector3 position,Vector3 box,float rate,int max,Vector2 lifetime,Vector2 rise,Vector2 size)
    {
        var child=new GameObject(name);child.transform.SetParent(parent.transform,false);child.transform.localPosition=position;
        var system=child.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=system.main;
        main.loop=true;main.playOnAwake=false;main.duration=2;main.maxParticles=max;
        main.startLifetime=new ParticleSystem.MinMaxCurve(lifetime.x,lifetime.y);
        main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(size.x,size.y);main.startColor=Color.white;
        main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
        main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
        var emission=system.emission;emission.rateOverTime=rate;
        var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=box;
        var velocity=system.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
        velocity.x=new ParticleSystem.MinMaxCurve(-.08f,.08f);velocity.y=new ParticleSystem.MinMaxCurve(rise.x,rise.y);velocity.z=new ParticleSystem.MinMaxCurve(-.08f,.08f);
        var noise=system.noise;noise.enabled=true;noise.strength=.15f;noise.frequency=1.2f;noise.scrollSpeed=.4f;
        var color=system.colorOverLifetime;color.enabled=true;
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.82f,.23f),0),new GradientColorKey(new Color(1,.48f,.1f),.35f),new GradientColorKey(new Color(1,.23f,.18f),.65f),new GradientColorKey(new Color(.54f,.06f,.06f),1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.1f),new GradientAlphaKey(1,.6f),new GradientAlphaKey(0,1)});
        color.color=gradient;
        var sizeOverLifetime=system.sizeOverLifetime;sizeOverLifetime.enabled=true;
        sizeOverLifetime.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(1,.4f)));
        var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
        renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=cube.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(cube);
    }
    // Flat full ring of radius 1 on the ground plane (XZ); BerserkVisual scales it.
    static Mesh Ring()
    {
        var s=new Shape();
        for(int segment=0;segment<64;segment++)
        {
            float a=segment*360f/64*Mathf.Deg2Rad,b=(segment+1)*360f/64*Mathf.Deg2Rad;
            var p=new Vector2(Mathf.Sin(a),Mathf.Cos(a));var q=new Vector2(Mathf.Sin(b),Mathf.Cos(b));
            s.Polygon(new[]{p,p*.94f,q*.94f,q});
        }
        var mesh=s.Build("Berserker ground ring");var points=mesh.vertices;
        for(int i=0;i<points.Length;i++)points[i]=new Vector3(points[i].x,0,points[i].y);
        mesh.vertices=points;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static Mesh Drop()
    {
        // Tip on top, round bulb below; the fan from the tip stays inside the outline.
        var points=new List<Vector2>{new Vector2(0,.42f)};
        for(float angle=30;angle>=-210;angle-=15)points.Add(new Vector2(0,-.12f)+new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*.26f);
        var s=new Shape();s.Polygon(points.ToArray());
        return s.Build("Bleed mark drop symbol");
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
