using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GoblinAudioChecks
{
    const string SettingsPath = "Assets/Data/Enemies/BaseGoblin.asset";
    const string Output = "output/goblin-audio";
    static readonly List<string> Report = new List<string>();
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Report.Add("PASS " + message);
    }

    static object Call(object target, string method, params object[] arguments) =>
        (target is EnemyController ? typeof(EnemyController) : target.GetType())
        .GetMethod(method, Private).Invoke(target, arguments);

    static void AllowHit(EnemyController enemy) => typeof(EnemyController)
        .GetField("lastHitSoundAt", Private).SetValue(enemy, float.NegativeInfinity);

    [MenuItem("Mismo/Audio/Verificar sonidos del goblin")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Ejecutar fuera de Play Mode.");
        Report.Clear();
        Directory.CreateDirectory(Output);
        try
        {
            var settings = AssetDatabase.LoadAssetAtPath<GoblinSettings>(SettingsPath);
            CheckSettings(settings);
            foreach (var path in new[] { "Assets/Art/Prefabs/Enemies/Goblin.prefab", "Assets/Art/Prefabs/Enemies/GoblinElite.prefab" })
                Check(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<EnemyController>().Settings == settings,
                    Path.GetFileName(path) + " inherits the configured sounds");
            CheckEvents(settings);
            Mismo.Gameplay.Player.Editor.CombatFeedbackChecks.RunBatch();
            Check(true, "Existing sword/goblin combat regression checks");
            try
            {
                ProjectOrganizationChecks.Run();
                File.WriteAllText(Output + "/organization.txt", "PASS");
                Check(true, "ProjectOrganizationChecks.Run");
            }
            catch (InvalidOperationException exception)
            {
                File.WriteAllText(Output + "/organization.txt", "FAIL\n" + exception.Message);
                Report.Add("WARN Global asset organization has issues; see organization.txt. Content build is checked independently.");
            }
            BuildContent();
            File.WriteAllLines(Output + "/checks.txt", Report);
            Debug.Log("GOBLIN_AUDIO_CHECKS_OK\n" + string.Join("\n", Report));
        }
        catch (Exception exception)
        {
            File.WriteAllLines(Output + "/checks.txt", Report.Concat(new[] { "FAIL " + exception }));
            throw;
        }
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    static void CheckSettings(GoblinSettings settings)
    {
        Check(settings != null, "Goblin settings load");
        Check(settings.attacks.Length == 0 && settings.slash.damage == 12 && settings.charge.damage == 20,
            "Original attack selection and damage preserved");
        Check(settings.hitSfx.Length == 3 && settings.hitSfxVolume > 0, "Three configured hit takes");
        var clips = settings.hitSfx.Concat(new[] { settings.slash.preparationSfx, settings.slash.executionSfx,
            settings.charge.preparationSfx, settings.charge.executionSfx }).ToArray();
        Check(clips.All(c => c != null) && clips.Distinct().Count() == 7, "Seven distinct serialized clips");
        foreach (var clip in clips)
        {
            Check(clip.channels == 1 && clip.frequency == 48000 && clip.length > .1f && clip.length < .5f,
                "Short mono 48 kHz clip: " + clip.name);
            clip.LoadAudioData();
            var samples = new float[clip.samples];
            Check(clip.GetData(samples, 0) && samples.Any(v => Mathf.Abs(v) > .05f) && samples.All(v => Mathf.Abs(v) < .9f),
                "Non-silent PCM, no clipping: " + clip.name);
        }
        Check(settings.slash.preparationSfx.length <= settings.slash.windup &&
            settings.charge.preparationSfx.length <= settings.charge.windup, "Preparations fit existing telegraph timing");
    }

    static void CheckEvents(GoblinSettings original)
    {
        var settings = Object.Instantiate(original);
        var actor = new GameObject("Goblin audio check");
        var source = new GameObject("Goblin audio attacker");
        var heard = new List<AudioClip>();
        var volumes = new List<float>();
        void Listen(AudioClip clip, float volume) { heard.Add(clip); volumes.Add(volume); }
        AudioEvents.OnPlayAbilitySFX += Listen;
        try
        {
            var health = actor.AddComponent<Health>();
            health.ConfigureMaximum(1000); health.Revive();
            var receiver = actor.AddComponent<DamageReceiver>();
            Call(receiver, "Awake");
            var state = actor.GetComponent<CombatState>(); Call(state, "Awake");
            var enemy = actor.AddComponent<GoblinController>();
            enemy.Configure(settings, null); Call(enemy, "Awake"); Call(enemy, "OnEnable");
            foreach (var action in new[] { settings.slash, settings.charge })
            {
                Call(enemy, "PlayPreparationSound", action);
                Check(heard.Last() == action.preparationSfx && Mathf.Approximately(volumes.Last(), action.preparationSfxVolume),
                    action.label + " preparation uses the SFX volume channel");
                Call(enemy, "PlayExecutionSound", action);
                Check(heard.Last() == action.executionSfx && Mathf.Approximately(volumes.Last(), action.executionSfxVolume),
                    action.label + " execution uses the SFX volume channel");
            }
            heard.Clear();
            DamageInfo Hit(StatusEffectType tick = StatusEffectType.None, float amount = 1) =>
                new DamageInfo(amount, source, Vector3.up, Vector3.back, AttackIdentity.Next(), 0, statusEffect: tick);
            for (int i = 0; i < 4; i++)
            {
                AllowHit(enemy); receiver.Resolve(Hit());
                Check(heard.Count == i + 1 && heard.Last() == settings.hitSfx[i % 3], "Confirmed hit cycles take " + i);
            }
            receiver.Resolve(Hit());
            Check(heard.Count == 4, "Simultaneous hits do not stack vocal reactions");
            AllowHit(enemy);
            foreach (var tick in new[] { StatusEffectType.Poison, StatusEffectType.Bleed, StatusEffectType.Burn }) receiver.Resolve(Hit(tick));
            Check(heard.Count == 4, "Status ticks stay silent");
            var defense = actor.GetComponent<DefenseWindow>();
            defense.OpenGuard(1); receiver.Resolve(Hit()); defense.CloseGuard();
            defense.OpenDodge(1); receiver.Resolve(Hit()); defense.CloseDodge();
            defense.OpenParry(1); receiver.Resolve(Hit()); defense.CloseParry();
            Check(heard.Count == 4, "Block, dodge and parry have no hurt voice");
            CombatTimeFeedback.CancelForPause();
            var contact = Hit(); receiver.Resolve(contact); int count = heard.Count;
            AllowHit(enemy); receiver.Resolve(contact);
            Check(heard.Count == count, "Repeated collision for one attack has no duplicate voice");
            settings.hitSfxVolume = 0; AllowHit(enemy); receiver.Resolve(Hit());
            Check(heard.Count == count, "Zero hit volume is silent"); settings.hitSfxVolume = original.hitSfxVolume;
            settings.hitSfx = new AudioClip[] { null, original.hitSfx[0] };
            AllowHit(enemy); receiver.Resolve(Hit());
            Check(heard.Count == ++count && heard.Last() == original.hitSfx[0], "Empty variant slots are skipped");
            settings.hitSfx = Array.Empty<AudioClip>(); AllowHit(enemy); receiver.Resolve(Hit());
            Check(heard.Count == count, "Unconfigured enemies stay silent");
            settings.hitSfx = original.hitSfx; AllowHit(enemy); receiver.Resolve(Hit(amount: 10000));
            Check(health.IsDead && heard.Count == count + 1, "Lethal confirmed hit retains its voice");
            AllowHit(enemy); receiver.Resolve(Hit());
            Check(heard.Count == count + 1, "Dead enemies cannot emit another hurt reaction");
            Call(enemy, "OnDisable");
        }
        finally
        {
            AudioEvents.OnPlayAbilitySFX -= Listen;
            CombatTimeFeedback.CancelForPause();
            Object.DestroyImmediate(actor); Object.DestroyImmediate(source); Object.DestroyImmediate(settings);
        }
    }

    static void BuildContent()
    {
        const string folder = "output/goblin-audio/content-build";
        Directory.CreateDirectory(folder);
        var manifest = BuildPipeline.BuildAssetBundles(folder, new[] { new AssetBundleBuild {
            assetBundleName = "goblin-audio", assetNames = new[] { SettingsPath }
        } }, BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        Check(manifest != null, "Windows content bundle builds");
        var bundle = AssetBundle.LoadFromFile(folder + "/goblin-audio");
        Check(bundle != null, "Windows content bundle reloads");
        try { CheckSettings(bundle.LoadAsset<GoblinSettings>(SettingsPath)); }
        finally { bundle.Unload(true); }
        Check(true, "All seven clips remain readable through settings in built content");
    }
}
