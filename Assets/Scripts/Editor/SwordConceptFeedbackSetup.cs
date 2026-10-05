using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class SwordConceptFeedbackSetup
{
    const string Prefabs="Assets/Art/Prefabs/Weapons/AbilityVfx/";
    const string Materials="Assets/Art/Materials/Weapons/AbilityVfx/";
    const string Output="output/sword-concept-feedback";
    const string Lunge="Assets/Data/Weapons/Sword/SwordLunge.asset";
    const string Parry="Assets/Data/Weapons/Sword/SwordParry.asset";
    const string Feedback="Assets/Data/Combat/Feedback/SwordFeedback.asset";
    const string Whoosh="Assets/Art/Audio/Weapons/Sword/Sword_Parry_Whoosh.wav";
    static Mesh cube;
    public static void RunAndPlayBatch(){Run();ParryRegressionPlayChecks.RunBatch();}
    [MenuItem("Mismo/Armas/Aplicar concepto Estocada y Parry")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);Directory.CreateDirectory(Materials);Directory.CreateDirectory(Prefabs);
        try
        {
            foreach(string path in new[]{Lunge,Parry,Feedback})
            {
                string backup=Output+"/before-"+Path.GetFileName(path);
                if(!File.Exists(backup))File.Copy(path,backup);
            }
            var comboBefore=File.ReadAllBytes("Assets/Data/Weapons/Sword/SwordCombo.asset");
            var soundBefore=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Lunge).executionSfx;
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube=Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(primitive);
            string meshPath="Assets/Art/Meshes/Weapons/SwordVfxCube.asset";Directory.CreateDirectory(Path.GetDirectoryName(meshPath));
            var existingCube=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(existingCube==null)AssetDatabase.CreateAsset(cube,meshPath);else{Object.DestroyImmediate(cube);cube=existingCube;}
            var particle=Material("SwordConceptParticles",AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/CombatParticles.shader"));
            var glow=Material("SwordLungeGlow",AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/SwordBladeGlow.shader"));
            var sparks=Material("SwordParrySparks",AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/CombatImpact.shader"));
            MakeWhoosh();
            var lunge=new GameObject("SwordLungeTurquoise");
            var component=lunge.AddComponent<SwordLungeVfx>();component.bladeMaterial=glow;component.ribbonMaterial=particle;
            component.fragments=Particles(lunge,"Cubic wake",particle,0,.18f,.035f,0);
            var emission=component.fragments.emission;emission.enabled=false;
            Save(lunge,"SwordLungeTurquoise");
            var contact=new GameObject("SwordParryContact");
            var ps=Particles(contact,"Blade contact cubes",sparks,40,.48f,.16f,4f);
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.06f;
            var main=ps.main;main.gravityModifier=.35f;main.startSpeed=new ParticleSystem.MinMaxCurve(2.4f,5.5f);
            main.startLifetime=new ParticleSystem.MinMaxCurve(.32f,.48f);main.startSize=new ParticleSystem.MinMaxCurve(.12f,.20f);
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            Save(contact,"SwordParryContact");
            string lungeText=File.ReadAllText(Lunge);
            lungeText=ReplaceVfx(lungeText,"SwordLungeTurquoise",1,1,"",.38f);
            Write(Lunge,lungeText);
            string parryText=File.ReadAllText(Parry);
            parryText=Regex.Replace(parryText,@"(?m)^  executionSfx:[^\r\n]*","  executionSfx: "+Reference(AssetDatabase.LoadAssetAtPath<AudioClip>(Whoosh)));
            parryText=Regex.Replace(parryText,@"(?m)^  executionSfxVolume:[^\r\n]*","  executionSfxVolume: 0.5");
            parryText=Regex.Replace(parryText,@"(?ms)^  weaponVfx:.*?(?=^  pose:)","  weaponVfx: []\n");
            Write(Parry,parryText);
            string profile=File.ReadAllText(Feedback);int offset=profile.IndexOf("  parry:",StringComparison.Ordinal);
            string parryCue=profile.Substring(offset);
            parryCue=Regex.Replace(parryCue,@"(?m)^    prefab:[^\r\n]*","    prefab: "+Reference(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"SwordParryContact.prefab")));
            parryCue=Regex.Replace(parryCue,@"(?m)^    tint:[^\r\n]*","    tint: {r: 1, g: 0.94, b: 0.8, a: 1}");
            parryCue=Regex.Replace(parryCue,@"(?m)^    useDefenderBlade:[^\r\n]*\r?\n","");
            parryCue=Regex.Replace(parryCue,@"(?m)^(    tint:[^\r\n]*)","$1\n    useDefenderBlade: 1");
            parryCue=Regex.Replace(parryCue,@"(?m)^    maximumLifetime:[^\r\n]*","    maximumLifetime: 1");
            Write(Feedback,profile.Substring(0,offset)+parryCue);
            AssetDatabase.SaveAssets();
            var a=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Lunge);var b=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Parry);
            var feedback=AssetDatabase.LoadAssetAtPath<CombatFeedbackProfile>(Feedback);
            if(!comboBefore.SequenceEqual(File.ReadAllBytes("Assets/Data/Weapons/Sword/SwordCombo.asset"))||a.executionSfx!=soundBefore)
                throw new Exception("Combo o sonido de Estocada alterados.");
            if(a.weaponVfx.Length!=1||a.weaponVfx[0].anchor!=WeaponVfxAnchor.Character||a.weaponVfx[0].prefab.GetComponent<SwordLungeVfx>()==null)
                throw new Exception("Estocada no quedó conectada.");
            if(b.executionSfx==null||b.executionSfx==feedback.parry.sound||b.executionSfx.length>.3f||b.weaponVfx.Length!=0)
                throw new Exception("Parry no separa intento de contacto.");
            ParryRegressionPlayChecks.CheckImportedContent();
            try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","PASS");}
            catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.ToString());}
            File.WriteAllText(Output+"/integration.txt","PASS: Combo intacto, sonido Estocada intacto, VFX y audio Parry conectados; importación y regresión de datos correctas.\n");
            Directory.CreateDirectory(".validation/SwordConceptContent");
            var manifest=BuildPipeline.BuildAssetBundles(".validation/SwordConceptContent",new[]{new AssetBundleBuild{assetBundleName="sword-concept",assetNames=new[]{Lunge,Parry,Feedback}}},BuildAssetBundleOptions.ChunkBasedCompression,EditorUserBuildSettings.activeBuildTarget);
            if(manifest==null)throw new Exception("Build de contenido falló.");
            var bundle=AssetBundle.LoadFromFile(".validation/SwordConceptContent/sword-concept");
            if(bundle==null)throw new Exception("No se pudo recargar la build.");
            try
            {
                var built=bundle.LoadAsset<AbilityDefinition>(Lunge);var builtParry=bundle.LoadAsset<AbilityDefinition>(Parry);
                if(built.weaponVfx[0].prefab.GetComponent<SwordLungeVfx>().bladeMaterial.shader==null||builtParry.executionSfx==null||bundle.LoadAsset<CombatFeedbackProfile>(Feedback).parry.prefab==null)
                    throw new Exception("Dependencias ausentes en build.");
            }
            finally{bundle.Unload(true);}
            File.WriteAllText(Output+"/build.txt","PASS: build y recarga de efectos, shader y audio.\n");
        }
        catch(Exception e){File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
    }
    static Material Material(string name,Shader shader)
    {
        if(shader==null)throw new Exception("Shader ausente: "+name);
        string path=Materials+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(value==null){value=new Material(shader){name=name};AssetDatabase.CreateAsset(value,path);}
        return value;
    }
    static ParticleSystem Particles(GameObject root,string name,Material material,int count,float life,float size,float speed)
    {
        var child=new GameObject(name);child.transform.SetParent(root.transform,false);var ps=child.AddComponent<ParticleSystem>();
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=.3f;main.startLifetime=life;main.startSpeed=speed;main.startSize=size;
        main.maxParticles=80;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startColor=Color.white;
        var emission=ps.emission;emission.rateOverTime=0;if(count>0)emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
        var shape=ps.shape;shape.enabled=false;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=cube;renderer.sharedMaterial=material;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return ps;
    }
    static void Save(GameObject go,string name){try{PrefabUtility.SaveAsPrefabAsset(go,Prefabs+name+".prefab");}finally{Object.DestroyImmediate(go);}}
    static string Reference(Object asset){if(asset==null)throw new Exception("Referencia nula");AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out string guid,out long id);return "{fileID: "+id+", guid: "+guid+", type: 3}";}
    static string ReplaceVfx(string text,string prefab,int phase,int anchor,string socket,float lifetime)
    {
        string block="  weaponVfx:\n  - prefab: "+Reference(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+prefab+".prefab"))+"\n    phase: "+phase+"\n    hand: 0\n    anchor: "+anchor+"\n    socketId: "+socket+"\n    localOffset: {x: 0, y: 0, z: 0}\n    localRotation: {x: 0, y: 0, z: 0}\n    localScale: {x: 1, y: 1, z: 1}\n    fullChargeScale: 1\n    lifetime: "+lifetime.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n";
        return Regex.Replace(text,@"(?ms)^  weaponVfx:.*?(?=^  pose:)",block);
    }
    static void Write(string path,string text)
    {
        if(File.ReadAllText(path)==text)return;
        AssetDatabase.ReleaseCachedFileHandles();
        // Atomic content replacement avoids truncating a Windows memory-mapped asset; path and .meta stay intact.
        string pending="Temp/SwordConcept-"+Path.GetFileName(path);
        File.WriteAllText(pending,text);File.Replace(pending,path,null);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
    }
    static void MakeWhoosh()
    {
        // Short filtered air transient; no tonal oscillators or metallic ringing.
        const int rate=44100;int count=(int)(rate*.16f);var random=new System.Random(7163);float low=0,slow=0;
        using(var file=new BinaryWriter(File.Create(Whoosh)))
        {
            file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+count*2);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(rate);file.Write(rate*2);file.Write((short)2);file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(count*2);
            for(int i=0;i<count;i++){float t=i/(float)count;float noise=(float)random.NextDouble()*2-1;low+=.24f*(noise-low);slow+=.035f*(noise-slow);float envelope=Mathf.Pow(Mathf.Sin(Mathf.PI*t),1.7f);file.Write((short)(Mathf.Clamp((low-slow)*envelope*1.9f,-1,1)*32767));}
        }
        AssetDatabase.ImportAsset(Whoosh,ImportAssetOptions.ForceSynchronousImport);
    }
}
