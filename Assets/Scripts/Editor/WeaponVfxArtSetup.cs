using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Creates only absent assets/bindings. Existing authored effects and socket transforms are preserved.
public static class WeaponVfxArtSetup
{
    public const string Prefabs = "Assets/Art/Prefabs/Weapons/AbilityVfx";
    const string Materials = "Assets/Art/Materials/Weapons/AbilityVfx";
    const string Meshes = "Assets/Art/Meshes/Weapons/AbilityVfx";
    public const string Output = "output/weapon-vfx";
    static readonly string[] Effects = { "BowCharge", "BowRelease", "BladeCharge", "BladeRelease", "ParryHalo", "MasteryPowerFlare" };
    static readonly string[] Abilities = { "Bow/BowShot", "Bow/BowPower", "Sword/SwordLunge", "Sword/SwordParry", "Skills/GuardBreaker" };
    static readonly Color Azure = new Color(.12f,.7f,1.25f,1), Amber = new Color(1.2f,.63f,.12f,1), Violet = new Color(.95f,.3f,1.25f,1);
    static Material glow, solid;
    static Mesh ring, shard;

    [InitializeOnLoadMethod] static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        const string path = "Temp/WeaponVfxArt.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer || !File.Exists(path)) return;
        string command;
        try { command = File.ReadAllText(path).Trim(); File.Delete(path); } catch (IOException) { return; }
        Directory.CreateDirectory(Output);
        try
        {
            if (command == "create") Create();
            else if (command == "inspect") Inspect();
            else if (command == "preview") RenderPreviews();
            else if (command == "tune") { TuneInitialArt(); RenderPreviews(); }
            else if (command == "build") BuildCheck();
            File.WriteAllText(Output + "/last-command.txt", command + " OK " + DateTime.Now);
        }
        catch (Exception e) { File.WriteAllText(Output + "/last-command.txt", command + " FAIL\n" + e); Debug.LogException(e); }
    }

    static T Load<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(path);
    static WeaponDefinition Weapon(string name) => Load<WeaponDefinition>("Assets/Data/Weapons/" + name + ".asset");
    static AbilityDefinition Ability(string name) => Load<AbilityDefinition>("Assets/Data/Weapons/" + name + ".asset");
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    [MenuItem("Mismo/Armas/VFX/Crear e integrar efectos iniciales")]
    public static void Create()
    {
        Directory.CreateDirectory(Output); Folder(Prefabs); Folder(Materials); Folder(Meshes);
        var shader = Load<Shader>("Assets/Art/Shaders/WeaponEnergy.shader");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Weapon Energy shader missing or invalid.");
        glow = Material("EnergyGlow", shader, 1); solid = Material("EnergyFacets", shader, 0);
        ring = MakeRing(); shard = MakeShard();
        Prefab("BowCharge", root => Charge(root, Azure, .2f));
        Prefab("BowRelease", root => Release(root, Azure, .44f, 8));
        Prefab("BladeCharge", root => Charge(root, Amber, .22f));
        Prefab("BladeRelease", root => Release(root, Amber, .52f, 10));
        Prefab("ParryHalo", root =>
        {
            Ring(root, "Guard ring", Amber, true, .65f, .7f);
            var second = Ring(root, "Crossed guard ring", new Color(1,.88f,.48f,.6f), true, .48f, .65f);
            second.transform.localRotation = Quaternion.Euler(65,0,25);
            Core(root, Amber, true, .2f, .3f);
        });
        Prefab("MasteryPowerFlare", root => Release(root, Violet, .38f, 5));
        AddSockets(Weapon("Sword/Sword")); AddSockets(Weapon("Sword/GuardianSword")); AddSockets(Weapon("Bow/Bow"));
        Bind(Ability("Bow/BowShot"), "BowCharge", WeaponVfxPhase.Preparation, "bowstring", 1, 1.6f, anchor:WeaponVfxAnchor.AboveHead, faceCamera:true);
        Bind(Ability("Bow/BowShot"), "BowRelease", WeaponVfxPhase.Execution, "bowstring", .45f, anchor:WeaponVfxAnchor.AboveHead, faceCamera:true);
        Bind(Ability("Bow/BowPower"), "BowCharge", WeaponVfxPhase.Preparation, "bowstring", 1, 1.3f, anchor:WeaponVfxAnchor.AboveHead, faceCamera:true);
        Bind(Ability("Bow/BowPower"), "BowRelease", WeaponVfxPhase.Execution, "bowstring", .45f, anchor:WeaponVfxAnchor.AboveHead, faceCamera:true);
        Bind(Ability("Sword/SwordLunge"), "BladeRelease", WeaponVfxPhase.Execution, "blade", .4f);
        Bind(Ability("Skills/GuardBreaker"), "BladeCharge", WeaponVfxPhase.Preparation, "blade");
        Bind(Ability("Skills/GuardBreaker"), "BladeRelease", WeaponVfxPhase.Execution, "blade", .4f);
        Bind(Ability("Sword/SwordParry"), "ParryHalo", WeaponVfxPhase.Active, "blade");
        foreach (var name in new[]{"Bow/BowPower","Sword/SwordLunge","Skills/GuardBreaker"})
            Bind(Ability(name), "MasteryPowerFlare", WeaponVfxPhase.Execution, name.StartsWith("Bow/")?"bowstring":"blade", .42f, modifier:"power",
                anchor:name.StartsWith("Bow/")?WeaponVfxAnchor.AboveHead:WeaponVfxAnchor.Weapon, faceCamera:name.StartsWith("Bow/"));
        Validate(); Inspect();
    }

    static Material Material(string name, Shader shader, float soft)
    {
        string path = Materials + "/" + name + ".mat"; var material = Load<Material>(path);
        if (material != null) return material;
        material = new Material(shader) { name = name }; material.SetFloat("_Soft",soft); material.color = Color.white;
        AssetDatabase.CreateAsset(material,path); return material;
    }

    static Mesh MakeRing()
    {
        string path = Meshes + "/FacetedRing.asset"; var mesh = Load<Mesh>(path); if (mesh != null) return mesh;
        const int segments = 16;
        var vertices = new Vector3[segments*2]; var triangles = new int[segments*6];
        for (int i=0;i<segments;i++)
        {
            float angle = i*Mathf.PI*2/segments; var direction = new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
            vertices[2*i]=direction*.5f; vertices[2*i+1]=direction*.445f;
            int a=i*2,b=(i*2+2)%(segments*2), t=i*6;
            triangles[t]=a; triangles[t+1]=b; triangles[t+2]=a+1; triangles[t+3]=a+1; triangles[t+4]=b; triangles[t+5]=b+1;
        }
        mesh=new Mesh{name="FacetedRing",vertices=vertices,triangles=triangles};mesh.RecalculateBounds();mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static Mesh MakeShard()
    {
        string path=Meshes+"/EnergyShard.asset";var mesh=Load<Mesh>(path);if(mesh!=null)return mesh;
        mesh=new Mesh{name="EnergyShard",vertices=new[]{Vector3.up*.8f,Vector3.right*.3f,Vector3.forward*.3f,Vector3.left*.3f,Vector3.back*.3f,Vector3.down*.8f},
            triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1}};
        mesh.RecalculateBounds();mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static void Prefab(string name,Action<GameObject> fill)
    {
        string path=Prefabs+"/"+name+".prefab";if(Load<GameObject>(path)!=null)return;
        var root=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};
        try { fill(root);root.hideFlags=HideFlags.None;PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { Object.DestroyImmediate(root); }
    }
    static ParticleSystem System(GameObject root,string name,Color color,bool loop,float lifetime,float size,Mesh mesh=null)
    {
        var child=new GameObject(name);child.transform.SetParent(root.transform,false);
        var system=child.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        system.useAutoRandomSeed=false;system.randomSeed=127;
        var main=system.main;main.duration=loop?.6f:1;main.loop=loop;main.prewarm=loop;main.playOnAwake=false;
        main.startLifetime=lifetime;main.startSpeed=0;main.startSize=size;main.startColor=color;main.maxParticles=48;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        var emission=system.emission;emission.rateOverTime=0;
        var shape=system.shape;shape.enabled=false;
        var fade=system.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.7f,.55f),new GradientAlphaKey(0,1)});fade.color=gradient;
        var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mesh!=null?solid:glow;
        renderer.renderMode=mesh!=null?ParticleSystemRenderMode.Mesh:ParticleSystemRenderMode.Billboard;
        if(mesh!=null){renderer.mesh=mesh;renderer.alignment=ParticleSystemRenderSpace.Local;}
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
        return system;
    }
    static void Burst(ParticleSystem system,int count){var emission=system.emission;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});}
    static ParticleSystem Core(GameObject root,Color color,bool loop,float size,float opacity=1)
    {
        color.a*=opacity;var system=System(root,"Soft core",color,loop,loop?.25f:.13f,size);
        var emission=system.emission;emission.rateOverTime=loop?8:0;Burst(system,1);
        var scale=system.sizeOverLifetime;scale.enabled=true;scale.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.7f,1,1.15f));return system;
    }
    static ParticleSystem Ring(GameObject root,string name,Color color,bool loop,float size,float lifetime)
    {
        var system=System(root,name,color,loop,lifetime,size,ring);Burst(system,1);
        var emission=system.emission;emission.rateOverTime=loop?2:0;
        var scale=system.sizeOverLifetime;scale.enabled=true;scale.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,loop?.65f:.2f,1,loop?1.1f:1.8f));
        var rotate=system.rotationOverLifetime;rotate.enabled=true;rotate.z=.65f;
        system.GetComponent<ParticleSystemRenderer>().alignment=ParticleSystemRenderSpace.View;
        return system;
    }
    static void Charge(GameObject root,Color color,float size)
    {
        Core(root,color,true,size,.45f);Ring(root,"Gathering ring",color,true,size*1.8f,.65f);
        var motes=System(root,"Inward facets",color,true,.55f,.055f,shard);
        var emission=motes.emission;emission.rateOverTime=16;
        var shape=motes.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=size*1.05f;shape.radiusThickness=.1f;
        var main=motes.main;main.startSpeed=-size*1.3f;main.startSize=new ParticleSystem.MinMaxCurve(.025f,.055f);main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
    }
    static void Release(GameObject root,Color color,float size,int count)
    {
        Core(root,color,false,size,.8f);Ring(root,"Release wave",color,false,size,.32f);
        var sparks=System(root,"Facet sparks",color,false,.3f,.04f,shard);Burst(sparks,count);
        var shape=sparks.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.02f;
        var main=sparks.main;main.startSpeed=new ParticleSystem.MinMaxCurve(.4f,.9f);main.startLifetime=new ParticleSystem.MinMaxCurve(.18f,.32f);main.startSize=new ParticleSystem.MinMaxCurve(.025f,.055f);
        var velocity=sparks.velocityOverLifetime;velocity.enabled=true;velocity.speedModifier=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.15f));
    }

    static Bounds LocalBounds(GameObject root)
    {
        Bounds result=default;bool found=false;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.sharedMesh==null)continue;var b=filter.sharedMesh.bounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {var p=root.transform.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));if(!found){result=new Bounds(p,Vector3.zero);found=true;}else result.Encapsulate(p);}
        }
        if(!found)throw new Exception("No mesh bounds in "+root.name);return result;
    }
    static void AddSockets(WeaponDefinition weapon)
    {
        string path=AssetDatabase.GetAssetPath(weapon.visualPrefab);var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var bounds=LocalBounds(root);bool changed=false;
            if(weapon.isBow)changed|=Socket(root,"bowstring",bounds.center,Quaternion.identity);
            else
            {
                var basePoint=weapon.poseProfile.trailBase;var tip=weapon.poseProfile.trailTip;
                // Shared poses can have been authored for a different sword. Fit to this model's bounds.
                tip=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
                basePoint=new Vector3(bounds.center.x,Mathf.Lerp(bounds.min.y,bounds.max.y,.3f),bounds.center.z);
                var rotation=Quaternion.LookRotation((tip-basePoint).normalized,Vector3.forward);
                changed|=Socket(root,"tip",tip,rotation);changed|=Socket(root,"blade",Vector3.Lerp(basePoint,tip,.64f),rotation);
            }
            if(changed)PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static bool Socket(GameObject root,string id,Vector3 position,Quaternion rotation)
    {
        if(root.GetComponentsInChildren<WeaponVfxSocket>(true).Any(s=>s.socketId==id))return false;
        var go=new GameObject("VFX · "+id);go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localRotation=rotation;go.AddComponent<WeaponVfxSocket>().socketId=id;return true;
    }
    static void Bind(AbilityDefinition ability,string effect,WeaponVfxPhase phase,string socket,float lifetime=1,float chargeScale=1,string modifier=null,
        WeaponVfxAnchor anchor=WeaponVfxAnchor.Weapon,bool faceCamera=false)
    {
        var prefab=Load<GameObject>(Prefabs+"/"+effect+".prefab");
        var target=modifier==null?null:ability.FindModifier(modifier);if(modifier!=null&&target==null)throw new Exception("Modifier missing: "+ability.name+"/"+modifier);
        var existing=target==null?ability.weaponVfx:target.weaponVfx;
        if(existing!=null&&existing.Any(v=>v!=null&&v.prefab==prefab&&v.phase==phase))return;
        var list=(existing??Array.Empty<WeaponVfxDefinition>()).ToList();
        list.Add(new WeaponVfxDefinition{prefab=prefab,phase=phase,socketId=socket,lifetime=lifetime,fullChargeScale=chargeScale,anchor=anchor,faceCamera=faceCamera});
        if(target==null)ability.weaponVfx=list.ToArray();else target.weaponVfx=list.ToArray();
        EditorUtility.SetDirty(ability);AssetDatabase.SaveAssetIfDirty(ability);
    }
    static void Inspect()
    {
        var lines=new List<string>();
        foreach(var name in new[]{"Sword/Sword","Sword/GuardianSword","Bow/Bow"})
        {
            var weapon=Weapon(name);var root=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(weapon.visualPrefab));
            try{lines.Add(name+" scale="+root.transform.localScale+" bounds="+LocalBounds(root));foreach(var socket in root.GetComponentsInChildren<WeaponVfxSocket>())lines.Add("  "+socket.socketId+" "+socket.transform.localPosition);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        File.WriteAllLines(Output+"/model-sockets.txt",lines);
    }
    static void Validate()
    {
        var lines=new List<string>();
        foreach(var name in Effects)
        {
            var prefab=Load<GameObject>(Prefabs+"/"+name+".prefab");if(prefab==null)throw new Exception(name);
            var systems=prefab.GetComponentsInChildren<ParticleSystem>(true);if(systems.Length<2)throw new Exception("Missing particle layers "+name);
            foreach(var system in systems){var renderer=system.GetComponent<ParticleSystemRenderer>();if(renderer.sharedMaterial==null||renderer.sharedMaterial.shader==null||ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))throw new Exception("Broken material "+name);}
            lines.Add("PASS "+name+": particle layers and serialized mesh/material/shader dependencies");
        }
        foreach(var name in Abilities)
        {
            var ability=Ability(name);if(ability.weaponVfx.Length==0||ability.weaponVfx.Any(v=>v.prefab==null))throw new Exception("VFX binding "+name);
            var model=Weapon(name.StartsWith("Bow/")?"Bow/Bow":"Sword/Sword").visualPrefab;
            foreach(var entry in ability.weaponVfx)
                if(entry.anchor==WeaponVfxAnchor.Weapon&&!string.IsNullOrEmpty(entry.socketId)&&!model.GetComponentsInChildren<WeaponVfxSocket>(true).Any(s=>s.socketId==entry.socketId))
                    throw new Exception("Missing socket "+name);
            lines.Add("PASS "+name+": effects bound to character anchors or existing weapon sockets");
        }
        File.WriteAllLines(Output+"/asset-checks.txt",lines);
        try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization-checks.txt","PASS");}
        catch(Exception e){File.WriteAllText(Output+"/organization-checks.txt",e.ToString());}
    }

    public static void RenderPreviews()
    {
        Directory.CreateDirectory(Output);
        foreach(var entry in new[]{("Bow/Bow","BowCharge","bowstring",.7f),("Bow/Bow","BowRelease","bowstring",.12f),("Sword/Sword","BladeRelease","blade",.1f),("Sword/Sword","ParryHalo","blade",.3f),("Sword/GuardianSword","MasteryPowerFlare","blade",.1f)})
            Render(entry.Item1,entry.Item2,entry.Item3,entry.Item4);
    }
    // Explicit art pass used during initial authoring; the normal creation command never overwrites art.
    static void TuneInitialArt()
    {
        foreach(var name in Effects)
        {
            string path=Prefabs+"/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var system in root.GetComponentsInChildren<ParticleSystem>())
                {
                    var renderer=system.GetComponent<ParticleSystemRenderer>();
                    if(renderer.mesh!=null&&renderer.mesh.name=="FacetedRing")renderer.alignment=ParticleSystemRenderSpace.View;
                    var main=system.main;
                    if(name=="BladeRelease"&&(system.name=="Soft core"||system.name=="Release wave"))main.startSize=.52f;
                    if(name=="BladeRelease"&&system.name=="Facet sparks")Burst(system,10);
                    if(name=="MasteryPowerFlare"&&(system.name=="Soft core"||system.name=="Release wave"))main.startSize=.38f;
                    if(name=="ParryHalo")main.startSize=system.name=="Guard ring"?.65f:system.name=="Crossed guard ring"?.48f:.2f;
                    if(name=="BladeCharge"&&system.name=="Soft core")main.startSize=.22f;
                    if(name=="BladeCharge"&&system.name=="Gathering ring")main.startSize=.396f;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
    static void Render(string weaponName,string effectName,string socketId,float time)
    {
        var preview=new PreviewRenderUtility();GameObject model=null;
        try
        {
            model=Object.Instantiate(Weapon(weaponName).visualPrefab);preview.AddSingleGO(model);
            model.transform.localScale=Vector3.one*Weapon(weaponName).poseProfile.equipped.scale;
            var socket=WeaponVfxSocket.Resolve(model.transform,socketId);
            var effect=Object.Instantiate(Load<GameObject>(Prefabs+"/"+effectName+".prefab"),socket,false);
            foreach(var system in effect.GetComponentsInChildren<ParticleSystem>()){system.Simulate(time,false,true,true);system.Pause(false);}
            var bounds=model.GetComponentsInChildren<MeshRenderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            var camera=preview.camera;camera.orthographic=true;camera.orthographicSize=Mathf.Max(.75f,bounds.extents.magnitude*1.2f);
            camera.backgroundColor=new Color(.022f,.032f,.05f);camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.01f;camera.farClipPlane=30;
            // Bow geometry lies in the YZ plane; sword faces the Z axis.
            var direction=weaponName.StartsWith("Bow/")?new Vector3(4,.3f,1.2f):new Vector3(.7f,1.7f,-4);
            camera.transform.position=bounds.center+direction.normalized*6;camera.transform.LookAt(bounds.center);
            preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(35,30,0);preview.lights[1].intensity=.8f;
            preview.ambientColor=new Color(.45f,.45f,.5f);
            preview.BeginPreview(new Rect(0,0,800,800),GUIStyle.none);preview.Render(true);var texture=preview.EndPreview();
            var old=RenderTexture.active;var rt=RenderTexture.GetTemporary(800,800,0);var image=new Texture2D(800,800,TextureFormat.RGB24,false);
            try{Graphics.Blit(texture,rt);RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,800,800),0,0);image.Apply();File.WriteAllBytes(Output+"/"+effectName+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
        finally{preview.Cleanup();if(model!=null)Object.DestroyImmediate(model);}
    }

    [MenuItem("Mismo/Armas/VFX/Verificar contenido compilado")]
    public static void BuildCheck()
    {
        Directory.CreateDirectory(Output);Directory.CreateDirectory(".validation/WeaponVfxContent");
        var scripts=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings{target=BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone},".validation/WeaponVfxPlayerScripts");
        if(scripts.assemblies==null||!scripts.assemblies.Any(p=>p.EndsWith("Mismo.Gameplay.Player.dll")))throw new Exception("Player compilation failed");
        File.WriteAllText(Output+"/player-compilation.txt","PASS Windows runtime scripts without UNITY_EDITOR");
        string[] paths=Abilities.Select(n=>"Assets/Data/Weapons/"+n+".asset").Concat(new[]{"Assets/Data/Weapons/Sword/Sword.asset","Assets/Data/Weapons/Sword/GuardianSword.asset","Assets/Data/Weapons/Bow/Bow.asset"}).ToArray();
        var manifest=BuildPipeline.BuildAssetBundles(".validation/WeaponVfxContent",new[]{new AssetBundleBuild{assetBundleName="weapon-vfx",assetNames=paths}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if(manifest==null)throw new Exception("Content build failed");
        var bundle=AssetBundle.LoadFromFile(".validation/WeaponVfxContent/weapon-vfx");if(bundle==null)throw new Exception("Cannot open built content");
        var lines=new List<string>();
        try
        {
            foreach(var path in paths.Where(p=>Abilities.Any(a=>p.EndsWith(a+".asset"))))
            {
                var ability=bundle.LoadAsset<AbilityDefinition>(path);if(ability==null)throw new Exception("Missing built ability "+path);
                var bindings=ability.weaponVfx.Concat(ability.masteryModifiers.SelectMany(m=>m.weaponVfx??Array.Empty<WeaponVfxDefinition>()));
                foreach(var binding in bindings)
                {
                    if(binding.prefab==null)throw new Exception("Missing built VFX prefab "+path);
                    foreach(var renderer in binding.prefab.GetComponentsInChildren<ParticleSystemRenderer>())
                        if(renderer.sharedMaterial==null||renderer.sharedMaterial.shader==null||renderer.renderMode==ParticleSystemRenderMode.Mesh&&renderer.mesh==null)throw new Exception("Missing built VFX dependency "+path);
                }
                lines.Add("PASS Windows bundle: "+ability.name+" -> VFX prefabs -> meshes/materials/shader");
            }
            File.WriteAllLines(Output+"/content-build.txt",lines);
        }
        finally{bundle.Unload(true);}
    }
}
