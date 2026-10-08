using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Assigns the selected spider sounds and checks the real creature prefabs.</summary>
public static class SpiderAudioIntegration
{
    const string SettingsPath = "Assets/Data/Enemies/ForestCreatures/Spider.asset";
    const string AudioPath = "Assets/Art/Audio/Enemies/Spider/Concept/Spider_Hissing_";
    const string Output = "output/spider-audio";
    const string Pending = "Mismo.SpiderAudio.Play";
    static readonly string[] Prefabs = { "Spider_Standard", "Spider_Forest_Moss", "Spider_Dark_Cave", "Spider_Albino" };
    static readonly List<string> Report = new List<string>();
    static readonly List<AudioClip> Heard = new List<AudioClip>();
    static readonly List<float> Volumes = new List<float>();
    static IEnumerator routine;
    static double deadline;
    static int lastFrame = -1;
    static NavMeshData navigation;
    static NavMeshDataInstance nav;

    static string PrefabPath(string name) => "Assets/Art/Prefabs/Enemies/ForestCreatures/" + name + ".prefab";
    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        Report.Add("PASS " + message);
    }

    [MenuItem("Mismo/Audio/Integrar sonidos B de la araña")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var settings = AssetDatabase.LoadAssetAtPath<CreatureSettings>(SettingsPath);
        if (settings == null || settings.attacks.Length != 3 || settings.attacks[0].label != "Attack_Bite" ||
            settings.attacks[1].label != "Jump" || settings.attacks[2].enabled)
            throw new InvalidOperationException("Expected the current spider bite, jump and disabled web action.");
        var clips = new[] { "Bite_Prepare", "Bite_Execute", "Jump_Prepare", "Jump_Execute", "Hit" }
            .Select(role => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + role + ".wav") ??
                throw new InvalidOperationException("Missing spider clip: " + role)).ToArray();
        string before = NonAudioJson(settings);
        for (int i = 0; i < 2; i++)
        {
            settings.attacks[i].preparationSfx = clips[i * 2];
            settings.attacks[i].preparationSfxVolume = .8f;
            settings.attacks[i].executionSfx = clips[i * 2 + 1];
            settings.attacks[i].executionSfxVolume = .8f;
        }
        settings.hitSfx = new[] { clips[4] }; settings.hitSfxVolume = .75f;
        if (before != NonAudioJson(settings)) throw new InvalidOperationException("Unexpected non-audio change.");
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
    }

    static string NonAudioJson(EnemySettings settings)
    {
        var copy = Object.Instantiate(settings);
        try
        {
            copy.hitSfx = Array.Empty<AudioClip>(); copy.hitSfxVolume = 0;
            foreach (var action in copy.attacks.Take(2))
            {
                action.preparationSfx = action.executionSfx = null;
                action.preparationSfxVolume = action.executionSfxVolume = 0;
            }
            return EditorJsonUtility.ToJson(copy);
        }
        finally { Object.DestroyImmediate(copy); }
    }

    static void CheckSettings(EnemySettings settings)
    {
        Check(settings != null && settings.attacks.Length == 3 && settings.attacks[0].enabled &&
            settings.attacks[1].enabled && !settings.attacks[2].enabled, "Bite and jump active; web remains disabled");
        var roles = new[] { "Bite_Prepare", "Bite_Execute", "Jump_Prepare", "Jump_Execute", "Hit" };
        Check(settings.hitSfx != null && settings.hitSfx.Length == 1, "One selected hurt sound");
        var clips = new[] { settings.attacks[0].preparationSfx, settings.attacks[0].executionSfx,
            settings.attacks[1].preparationSfx, settings.attacks[1].executionSfx, settings.hitSfx[0] };
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            Check(clip != null && clip.name == "Spider_Hissing_" + roles[i], "Selected B clip: " + roles[i]);
            clip.LoadAudioData(); var samples = new float[clip.samples * clip.channels];
            Check(clip.channels == 1 && clip.frequency == 48000 && clip.GetData(samples, 0) &&
                samples.Any(v => Mathf.Abs(v) > .05f) && samples.All(v => Mathf.Abs(v) < .8f), "Decodes mono 48 kHz without clipping: " + roles[i]);
        }
        Check(settings.attacks.Take(2).All(a => a.preparationSfx.length <= a.windup), "Preparations fit existing windups");
    }

    [MenuItem("Mismo/Audio/Verificar sonidos de la araña")]
    public static void Verify()
    {
        Report.Clear(); Directory.CreateDirectory(Output);
        try
        {
            var settings = AssetDatabase.LoadAssetAtPath<CreatureSettings>(SettingsPath);
            CheckSettings(settings);
            foreach (var name in Prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));
                Check(prefab != null && prefab.GetComponent<CreatureController>().Settings == settings, name + " shares configured settings");
                var dependencies = AssetDatabase.GetDependencies(PrefabPath(name), true);
                Check(dependencies.Count(p => p.StartsWith(AudioPath) && p.EndsWith(".wav")) == 5 &&
                    !dependencies.Any(p => p.Contains("Spider_Dry_") || p.EndsWith("_Audition.wav")), name + " includes exactly five B clips");
            }
            try { ProjectOrganizationChecks.Run(); File.WriteAllText(Output + "/organization.txt", "PASS\n"); }
            catch (InvalidOperationException error)
            {
                File.WriteAllText(Output + "/organization.txt", "FAIL\n" + error.Message);
                Report.Add("WARN Global organization issues; see organization.txt.");
            }
            const string folder = ".validation/spider-audio/content-build";
            Directory.CreateDirectory(folder);
            var manifest = BuildPipeline.BuildAssetBundles(folder, new[] { new AssetBundleBuild {
                assetBundleName = "spider-audio", assetNames = Prefabs.Select(PrefabPath).ToArray()
            } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
            Check(manifest != null, "Windows spider content bundle builds");
            var bundle = AssetBundle.LoadFromFile(folder + "/spider-audio");
            Check(bundle != null, "Windows spider content bundle reloads");
            try
            {
                foreach (var name in Prefabs)
                {
                    var built = bundle.LoadAsset<GameObject>(PrefabPath(name));
                    CheckSettings(built.GetComponent<CreatureController>().Settings);
                    Check(true, name + " retains and decodes all five B clips in the build");
                }
            }
            finally { bundle.Unload(true); }
        }
        catch (Exception error) { Report.Add("FAIL " + error); throw; }
        finally { File.WriteAllLines(Output + "/integration-checks.txt", Report); }
    }

    public static void RunBatch()
    {
        try
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Dedicated batch process required.");
            Configure(); Verify();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Report.Clear(); SessionState.SetBool(Pending, true); Resume(); EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update -= Step; EditorApplication.update += Step;
    }

    static void Step()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Spider playback checks");
            if (!Application.isPlaying || Time.frameCount < 4 || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount; routine ??= PlayChecks();
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    static void Listen(AudioClip clip, float volume) { Heard.Add(clip); Volumes.Add(volume); }
    static void Finish(Exception error)
    {
        AudioEvents.OnPlayAbilitySFX -= Listen;
        SessionState.SetBool(Pending, false); EditorApplication.update -= Step;
        if (nav.valid) nav.Remove();
        if (navigation != null) Object.Destroy(navigation);
        CombatTimeFeedback.CancelForPause(); Time.timeScale = 1;
        Report.Add(error == null ? "PASS SPIDER_AUDIO_PLAY_COMPLETE" : "FAIL " + error);
        File.WriteAllLines(Output + "/play-checks.txt", Report);
        Debug.Log(Report.Last()); EditorApplication.Exit(error == null ? 0 : 1);
    }

    static IEnumerator PlayChecks()
    {
        Time.timeScale = 0;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = Vector3.down * .5f; floor.transform.localScale = new Vector3(80, 1, 80);
        navigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource> {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = floor.transform.localToWorldMatrix, size = Vector3.one }
        }, new Bounds(Vector3.zero, new Vector3(80, 10, 80)), Vector3.zero, Quaternion.identity);
        Check(navigation != null, "Temporary navigation surface"); nav = NavMesh.AddNavMeshData(navigation);
        new GameObject("Spider audio listener").AddComponent<AudioListener>();
        Check(AudioRuntime.Instance != null && AudioRuntime.SfxGroup != null, "Existing audio service and SFX mixer available");
        var target = new GameObject("Spider audio target");
        var collider = target.AddComponent<CapsuleCollider>(); collider.height = 2; collider.center = Vector3.up;
        var health = target.AddComponent<Health>(); health.ConfigureMaximum(10000); health.Revive(); target.AddComponent<DamageReceiver>();
        AudioEvents.OnPlayAbilitySFX += Listen;
        foreach (var name in Prefabs)
        foreach (int index in new[] { 0, 1 })
        {
            Vector3 origin = new Vector3(10, 0, 6);
            target.transform.position = origin + Vector3.forward * (index == 0 ? 1.3f : 5f);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name)), origin, Quaternion.identity);
            yield return null;
            var enemy = root.GetComponent<CreatureController>();
            Check(root.GetComponent<NavMeshAgent>().isOnNavMesh, name + " initializes on navigation");
            Heard.Clear(); Volumes.Clear(); Physics.SyncTransforms(); enemy.SetTarget(target.transform);
            enemy.Tick(.01f);
            var action = enemy.Settings.attacks[index];
            Check(enemy.State == EnemyState.Telegraph && enemy.CurrentAttack == action, name + " selects " + action.label);
            Check(Heard.Count == 1 && Heard[0] == action.preparationSfx && Mathf.Approximately(Volumes[0], .8f), "B preparation once at telegraph start");
            enemy.Tick(action.windup - .01f);
            Check(Heard.Count == 1 && enemy.State == EnemyState.Telegraph, "Execution stays silent during preparation");
            enemy.Tick(.02f);
            Check(Heard.Count == 2 && Heard[1] == action.executionSfx && Mathf.Approximately(Volumes[1], .8f), "B execution once at attack start");
            enemy.Tick(action.active + .01f); enemy.Tick(.01f);
            Check(Heard.Count == 2, "No duplicate sound in active/recovery frames");
            var receiver = root.GetComponent<DamageReceiver>();
            receiver.Resolve(new DamageInfo(1, target, root.transform.position, Vector3.back, AttackIdentity.Next(), 0, statusEffect: StatusEffectType.Burn));
            Check(Heard.Count == 2, "Status tick has no hurt sound");
            receiver.Resolve(new DamageInfo(1, target, root.transform.position, Vector3.back, AttackIdentity.Next(), 0));
            Check(Heard.Count == 3 && Heard[2] == enemy.Settings.hitSfx[0] && Mathf.Approximately(Volumes[2], .75f), "Confirmed damage plays B hurt sound");
            receiver.Resolve(new DamageInfo(1, target, root.transform.position, Vector3.back, AttackIdentity.Next(), 0));
            Check(Heard.Count == 3, "Simultaneous hits do not stack hurt sounds");
            Object.Destroy(root); yield return null;
        }
        var interruptedRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(Prefabs[0])), new Vector3(10, 0, 6), Quaternion.identity);
        yield return null;
        var interrupted = interruptedRoot.GetComponent<CreatureController>();
        Heard.Clear(); Volumes.Clear(); interrupted.SetTarget(target.transform); interrupted.Tick(.01f);
        Check(interrupted.State == EnemyState.Telegraph && Heard.Count == 1, "Interrupt fixture entered jump preparation");
        interruptedRoot.GetComponent<CombatState>().DamagePosture(10000);
        interrupted.Tick(.61f);
        Check(Heard.Count == 1, "Interrupted jump does not play execution");
        Object.Destroy(interruptedRoot); Object.Destroy(target); Object.Destroy(floor);
    }
}
