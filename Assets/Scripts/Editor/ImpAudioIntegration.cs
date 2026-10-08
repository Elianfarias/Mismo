using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ImpAudioIntegration
{
    public const string SettingsPath = "Assets/Data/Enemies/ForestCreatures/Imp.asset";
    public const string PrefabPath = "Assets/Art/Prefabs/Enemies/ForestCreatures/Imp.prefab";
    public const string Output = "output/imp-audio";
    const string Voice = "Assets/Art/Audio/Enemies/Imp/Concept/Imp_Balanced_";
    const string Fire = "Assets/Art/Audio/Enemies/Imp/Fire/Imp_Fire_";
    static readonly List<string> Report = new List<string>();

    static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Report.Add("PASS " + label);
    }

    [MenuItem("Mismo/Audio/Integrar voz B y fuego del Imp")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var settings = AssetDatabase.LoadAssetAtPath<GoblinSettings>(SettingsPath);
        if (settings == null || settings.attacks.Length != 2 || settings.attacks[0].kind != CreatureAttackKind.Melee ||
            settings.attacks[1].kind != CreatureAttackKind.Projectile)
            throw new InvalidOperationException("Expected the existing Imp melee and fireball actions.");
        string before = NonAudioJson(settings);
        string backup = ".validation/imp-audio/integration-before/Imp.asset";
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if (!File.Exists(backup)) File.Copy(SettingsPath, backup);
        AudioClip Load(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path) ??
            throw new InvalidOperationException("Missing Imp audio: " + path);
        // Resolve everything before changing the configuration.
        var prepare = Load(Voice + "Prepare.wav");
        var attack = Load(Voice + "Attack.wav");
        var hit = Load(Voice + "Hit.wav");
        var charge = Load(Fire + "Prepare.wav");
        var launch = Load(Fire + "Launch.wav");
        var impact = Load(Fire + "Impact.wav");
        var melee = settings.attacks[0];
        melee.preparationSfx = prepare; melee.preparationSfxVolume = .8f;
        melee.executionSfx = attack; melee.executionSfxVolume = .8f;
        var spell = settings.attacks[1];
        spell.preparationSfx = charge; spell.preparationSfxVolume = .78f;
        spell.executionSfx = launch; spell.executionSfxVolume = .68f;
        spell.projectileImpactSfx = impact; spell.projectileImpactSfxVolume = .65f;
        settings.hitSfx = new[] { hit }; settings.hitSfxVolume = .75f;
        if (before != NonAudioJson(settings)) throw new InvalidOperationException("Unexpected non-audio configuration change.");
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
    }

    static string NonAudioJson(EnemySettings settings)
    {
        var copy = Object.Instantiate(settings);
        try
        {
            copy.hitSfx = Array.Empty<AudioClip>(); copy.hitSfxVolume = 0;
            foreach (var attack in copy.attacks)
            {
                attack.preparationSfx = attack.executionSfx = attack.projectileImpactSfx = null;
                attack.preparationSfxVolume = attack.executionSfxVolume = attack.projectileImpactSfxVolume = 0;
            }
            return EditorJsonUtility.ToJson(copy);
        }
        finally { Object.DestroyImmediate(copy); }
    }

    [MenuItem("Mismo/Audio/Verificar integración del Imp")]
    public static void Verify()
    {
        Report.Clear(); Directory.CreateDirectory(Output);
        try
        {
            var settings = AssetDatabase.LoadAssetAtPath<GoblinSettings>(SettingsPath);
            CheckSettings(settings);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(prefab != null && prefab.GetComponent<ImpController>().Settings == settings,
                "Actual Imp prefab uses the configured settings");
            Check(AssetDatabase.GetDependencies(PrefabPath, true).Contains(Fire + "Impact.wav"),
                "Explosion is a dependency of the actual Imp prefab");
            try { ProjectOrganizationChecks.Run(); File.WriteAllText(Output + "/organization.txt", "PASS\n"); }
            catch (InvalidOperationException error)
            {
                File.WriteAllText(Output + "/organization.txt", "FAIL\n" + error.Message);
                Report.Add("WARN Existing global organization issues; see organization.txt.");
            }
            BuildContent();
            File.WriteAllLines(Output + "/integration-checks.txt", Report);
        }
        catch (Exception error)
        {
            File.WriteAllLines(Output + "/integration-checks.txt", Report.Concat(new[] { "FAIL " + error }));
            throw;
        }
    }

    public static void RunBatch()
    {
        try
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the menus in an interactive editor.");
            Configure(); Verify(); ImpAudioPlayChecks.RunBatch();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static void CheckSettings(EnemySettings settings)
    {
        Check(settings != null && settings.attacks.Length == 2, "Both Imp actions load");
        var melee = settings.attacks[0]; var spell = settings.attacks[1];
        Check(melee.preparationSfx != null && melee.preparationSfx.name == "Imp_Balanced_Prepare" &&
            melee.executionSfx != null && melee.executionSfx.name == "Imp_Balanced_Attack", "Approved B preparation and melee attack assigned");
        Check(settings.hitSfx.Length == 1 && settings.hitSfx[0] != null && settings.hitSfx[0].name == "Imp_Balanced_Hit",
            "Approved B hurt voice assigned");
        Check(spell.preparationSfx != null && spell.preparationSfx.name == "Imp_Fire_Prepare" &&
            spell.executionSfx != null && spell.executionSfx.name == "Imp_Fire_Launch" &&
            spell.projectileImpactSfx != null && spell.projectileImpactSfx.name == "Imp_Fire_Impact", "Three fire stages assigned");
        Check(melee.preparationSfx.length <= melee.windup && spell.preparationSfx.length <= spell.windup,
            "Both preparation clips fit their existing telegraph durations");
        Check(spell.kind == CreatureAttackKind.Projectile && spell.projectileVisual != null &&
            spell.projectileImpactVfx != null && spell.groundPoolPrefab != null && spell.spawnGroundPool,
            "Fireball visual, explosion and burning ground retained");
        var clips = settings.hitSfx.Concat(new[] { melee.preparationSfx, melee.executionSfx,
            spell.preparationSfx, spell.executionSfx, spell.projectileImpactSfx }).ToArray();
        Check(clips.Distinct().Count() == 6, "Six distinct runtime audio clips");
        foreach (var clip in clips)
        {
            clip.LoadAudioData(); var samples = new float[clip.samples * clip.channels];
            Check(clip.channels == 1 && clip.frequency == 48000 && clip.GetData(samples, 0) &&
                samples.Any(v => Mathf.Abs(v) > .05f) && samples.All(v => Mathf.Abs(v) < .9f), "Readable mono 48 kHz PCM without clipping: " + clip.name);
        }
    }

    static void BuildContent()
    {
        const string folder = ".validation/imp-audio/content-build";
        Directory.CreateDirectory(folder);
        var manifest = BuildPipeline.BuildAssetBundles(folder, new[] { new AssetBundleBuild {
            assetBundleName = "imp-audio", assetNames = new[] { PrefabPath }
        } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        Check(manifest != null, "Windows Imp content bundle builds");
        var bundle = AssetBundle.LoadFromFile(folder + "/imp-audio");
        Check(bundle != null, "Windows Imp content bundle reloads");
        try
        {
            var prefab = bundle.LoadAsset<GameObject>(PrefabPath);
            CheckSettings(prefab.GetComponent<ImpController>().Settings);
            Check(true, "Built Imp prefab retains and decodes all six audio dependencies");
        }
        finally { bundle.Unload(true); }
    }
}
