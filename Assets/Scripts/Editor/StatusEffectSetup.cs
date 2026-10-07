using System;
using System.IO;
using System.Linq;
using Mismo.Core;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class StatusEffectSetup
{
    public const string Output = "output/status-effects";
    public const string ProfilePath = "Assets/Data/Combat/StatusEffects/StatusEffectPresentation.asset";
    public const string PrefabPath = "Assets/Art/Prefabs/Combat/StatusEffects/PoisonAura.prefab";
    const string Materials = "Assets/Art/Materials/Combat/StatusEffects";
    [InitializeOnLoadMethod] static void Register() => EditorApplication.update += Poll;
    static void Poll()
    {
        const string request = "Temp/StatusEffects.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer || !File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim(); File.Delete(request);
        try { if (command == "play") StatusEffectPlayChecks.Run(); else if (command == "build") StatusEffectChecks.Build(); else { Install(); StatusEffectChecks.Run(); StatusEffectChecks.Preview(); } }
        catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/FAILED.txt", e.ToString()); Debug.LogException(e); }
    }
    public static void Batch()
    {
        if (File.Exists(Output + "/FAILED.txt")) File.Delete(Output + "/FAILED.txt");
        try { Install(); StatusEffectChecks.Run(); StatusEffectChecks.Preview(); Debug.Log("STATUS_EFFECT_CHECKS_OK"); }
        catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/FAILED.txt", e.ToString()); Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }
    [MenuItem("Mismo/Combate/Estados/Configurar veneno y colores")]
    public static void Install()
    {
        Directory.CreateDirectory(Output);
        ProjectAssetOrganizer.EnsureFolder(Materials);
        ProjectAssetOrganizer.EnsureFolder(Path.GetDirectoryName(ProfilePath).Replace('\\','/'));
        ProjectAssetOrganizer.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\','/'));
        var overlay = MaterialAt(Materials + "/PoisonOverlay.mat", "Assets/Art/Shaders/StatusPoisonOverlay.shader", new Color(.38f, 1.1f, .04f, 1));
        var smoke = MaterialAt(Materials + "/PoisonMist.mat", "Assets/Art/Shaders/StatusPoisonMist.shader", new Color(.32f, .7f, .055f, 1));
        var violet = MaterialAt(Materials + "/PoisonUndertone.mat", "Assets/Art/Shaders/StatusPoisonMist.shader", new Color(.23f, .075f, .28f, 1));
        var motes = MaterialAt(Materials + "/PoisonMotes.mat", "Assets/Art/Shaders/CombatParticles.shader", new Color(.6f, 1.3f, .12f, 1));
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("PoisonAura"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                MakeParticles(root, "Toxic mist", smoke, false, 13, 32, .42f, .68f, .38f);
                MakeParticles(root, "Violet undertone", violet, false, 3, 10, .4f, .6f, .22f);
                MakeParticles(root, "Rising voxel motes", motes, true, 13, 36, .025f, .045f, .88f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        var profile = AssetDatabase.LoadAssetAtPath<StatusEffectPresentation>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<StatusEffectPresentation>();
            profile.poisonOverlay = overlay;
            profile.poisonParticles = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        var catalog = AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        var entry = catalog.entries.FirstOrDefault(e => e.key == StatusEffectPresentation.CatalogKey);
        if (entry == null)
        {
            catalog.entries = catalog.entries.Concat(new[] { new RuntimeAssetCatalog.Entry {
                key = StatusEffectPresentation.CatalogKey, assets = new Object[] { profile } } }).ToArray();
            catalog.Invalidate(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
        }
        else if (!entry.assets.Contains(profile)) throw new InvalidOperationException("Status presentation catalog key is already owned by another asset.");
        File.WriteAllText(Output + "/setup.txt", "PASS: own procedural URP shaders, three particle layers, serialized profile in runtime catalog. Existing settings preserved.");
    }
    static Material MaterialAt(string path, string shaderPath, Color tint)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null) throw new Exception("Missing shader " + shaderPath);
        material = new Material(shader); material.SetColor("_Color", tint);
        AssetDatabase.CreateAsset(material, path); return material;
    }
    static void MakeParticles(GameObject parent, string name, Material material, bool cubes, float rate, int max, float minSize, float maxSize, float alpha)
    {
        var child = new GameObject(name); child.transform.SetParent(parent.transform, false);
        var system = child.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true; main.playOnAwake = false; main.duration = 2; main.maxParticles = max;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.06f, .16f);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new Color(1, 1, 1, alpha);
        main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.useUnscaledTime = false; main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        var emission = system.emission; emission.rateOverTime = rate;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(.7f, 1.35f, .7f); shape.position = Vector3.down * .05f;
        var velocity = system.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = cubes ? .33f : .2f; velocity.orbitalY = cubes ? .25f : .55f;
        var noise = system.noise; noise.enabled = true; noise.strength = .1f; noise.frequency = .8f; noise.scrollSpeed = .3f;
        var color = system.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(.7f, .65f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var size = system.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .55f), new Keyframe(.35f, 1), new Keyframe(1, cubes ? .3f : 1.3f)));
        var renderer = system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        if (cubes)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(cube);
        }
        else renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }
}
