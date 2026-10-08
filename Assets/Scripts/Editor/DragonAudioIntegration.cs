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

public static class DragonAudioIntegration
{
    public const string PrefabPath = "Assets/Art/Prefabs/DragonBosses/SoulEater_PhaseOne.prefab";
    public const string ProfilePath = "Assets/Data/Audio/SoulEaterAudio.asset";
    const string ClipsPath = "Assets/Art/Audio/Enemies/SoulEater/Selected/";
    const string Output = "output/dragon-audio";
    const string Pending = "Mismo.DragonAudio.Play";
    static readonly List<string> Report = new List<string>();
    static readonly List<SoulEaterCue> Played = new List<SoulEaterCue>();
    static IEnumerator routine;
    static double deadline;
    static int lastFrame = -1;
    static NavMeshData navigation;
    static NavMeshDataInstance nav;

    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        Report.Add("PASS " + message);
    }
    static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(ClipsPath + "Dragon_" + name + ".wav") ??
        throw new InvalidOperationException("Missing selected sound: " + name);

    [MenuItem("Mismo/Audio/Integrar pack del dragón")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var profile = AssetDatabase.LoadAssetAtPath<SoulEaterAudioProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<SoulEaterAudioProfile>();
            SoulEaterAudioProfile.Sound Sound(SoulEaterCue cue, string name, float volume) =>
                new SoulEaterAudioProfile.Sound { cue = cue, clip = Clip(name), volume = volume };
            profile.sounds = new[] {
                Sound(SoulEaterCue.Bite,"Attack",.7f), Sound(SoulEaterCue.Tail,"Attack",.65f),
                Sound(SoulEaterCue.Inhale,"Fire_Prepare",.65f), Sound(SoulEaterCue.Roar,"Roar",.8f),
                Sound(SoulEaterCue.Jump,"Wing",.65f), Sound(SoulEaterCue.Charge,"Attack",.7f),
                Sound(SoulEaterCue.Land,"Ground",.85f), Sound(SoulEaterCue.Wing,"Wing",.55f),
                Sound(SoulEaterCue.Footstep,"Ground",.4f), Sound(SoulEaterCue.Hurt,"Hurt",.5f),
                Sound(SoulEaterCue.Death,"Death",.8f), Sound(SoulEaterCue.Emerge,"Roar",.75f)
            };
            profile.fireStart = Clip("Fire_Start"); profile.fireLoop = Clip("Fire_Loop");
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var effects = root.GetComponent<SoulEaterEffects>();
            AudioSource Source(string name)
            {
                var child = root.transform.Find(name);
                if (child == null) { child = new GameObject(name).transform; child.SetParent(root.transform, false); }
                var source = child.GetComponent<AudioSource>();
                if (source == null) source = child.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.clip = null;
                source.spatialBlend = 1; source.minDistance = 5; source.maxDistance = 50;
                source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0;
                source.outputAudioMixerGroup = AudioRuntime.SfxGroup;
                return source;
            }
            Source("Voice"); Source("BreathLoop");
            effects.ConfigureAudio(profile, Source("Foley"), Source("Hurt"));
            var serialized = new SerializedObject(effects);
            serialized.FindProperty("cues").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    static void CheckProfile(SoulEaterAudioProfile profile)
    {
        Check(profile != null, "Selected audio profile is serialized");
        foreach (SoulEaterCue cue in Enum.GetValues(typeof(SoulEaterCue)))
            Check(profile.TryGet(cue, out var sound) && sound.volume > 0, "Assigned cue: " + cue);
        var clips = profile.sounds.Select(s => s.clip).Concat(new[] { profile.fireStart, profile.fireLoop }).Distinct().ToArray();
        Check(clips.Length == 9 && clips.All(c => c != null), "Nine selected clips, all roles covered");
        foreach (var clip in clips)
        {
            clip.LoadAudioData(); var samples = new float[clip.samples * clip.channels];
            Check(clip.channels == 1 && clip.frequency == 48000 && clip.GetData(samples, 0) &&
                samples.Any(v => Mathf.Abs(v) > .1f) && samples.All(v => Mathf.Abs(v) < .8f), "Decodes mono 48 kHz with headroom: " + clip.name);
        }
    }

    [MenuItem("Mismo/Audio/Verificar pack del dragón")]
    public static void Verify()
    {
        Report.Clear(); Directory.CreateDirectory(Output);
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            CheckProfile(prefab.GetComponent<SoulEaterEffects>().AudioProfile);
            var sources = prefab.GetComponentsInChildren<AudioSource>();
            Check(sources.Length == 4 && sources.All(s => !s.playOnAwake && s.spatialBlend == 1 && s.dopplerLevel == 0 && s.outputAudioMixerGroup == AudioRuntime.SfxGroup),
                "Four spatial sources share SFX mixer without autoplay or Doppler");
            var paths = AssetDatabase.GetDependencies(PrefabPath, true);
            Check(paths.Count(p => p.StartsWith(ClipsPath) && p.EndsWith(".wav")) == 9, "Prefab includes all nine selected sounds");
            Check(!paths.Any(p => p.Contains("/SoulEater/Concept/") || p.Contains("/SoulEater/Source/") || p.Contains("/SoulEater/SoulEater_")),
                "No rejected concepts, source files or procedural placeholders in boss dependencies");
            try { ProjectOrganizationChecks.Run(); File.WriteAllText(Output + "/organization.txt", "PASS\n"); }
            catch (InvalidOperationException error)
            { File.WriteAllText(Output + "/organization.txt", "FAIL\n" + error.Message); Report.Add("WARN Existing project organization findings; see organization.txt."); }
            const string build = ".validation/dragon-audio/content-build";
            Directory.CreateDirectory(build);
            var manifest = BuildPipeline.BuildAssetBundles(build, new[] { new AssetBundleBuild {
                assetBundleName = "dragon-audio", assetNames = new[] { PrefabPath }
            } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
            Check(manifest != null, "Windows boss content bundle builds");
            var bundle = AssetBundle.LoadFromFile(build + "/dragon-audio");
            Check(bundle != null, "Windows boss content bundle reloads");
            try { CheckProfile(bundle.LoadAsset<GameObject>(PrefabPath).GetComponent<SoulEaterEffects>().AudioProfile); }
            finally { bundle.Unload(true); }
        }
        catch (Exception e) { Report.Add("FAIL " + e); throw; }
        finally { File.WriteAllLines(Output + "/integration-checks.txt", Report); }
    }

    public static void RunBatch()
    {
        try
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch process required.");
            Configure(); Verify();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Report.Clear(); SessionState.SetBool(Pending, true); Resume(); EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        deadline = EditorApplication.timeSinceStartup + 150;
        EditorApplication.update -= Step; EditorApplication.update += Step;
    }
    static void Step()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Dragon audio play checks");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount; routine ??= PlayChecks();
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Step; SessionState.EraseBool(Pending);
        if (nav.valid) nav.Remove(); if (navigation != null) Object.Destroy(navigation);
        CombatTimeFeedback.CancelForPause(); Time.timeScale = 1;
        Report.Add(error == null ? "PASS DRAGON_AUDIO_PLAY_COMPLETE" : "FAIL " + error);
        File.WriteAllLines(Output + "/play-checks.txt", Report);
        Debug.Log(Report.Last()); EditorApplication.Exit(error == null ? 0 : 1);
    }
    static void Advance(SoulEaterPhaseOneController boss, float seconds)
    { for (float t = 0; t < seconds; t += .02f) boss.Tick(Mathf.Min(.02f, seconds - t)); }

    static IEnumerator PlayChecks()
    {
        Time.timeScale = 1;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = Vector3.down * .5f; floor.transform.localScale = new Vector3(160, 1, 160);
        navigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource> {
            new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box, transform=floor.transform.localToWorldMatrix, size=Vector3.one }
        }, new Bounds(Vector3.zero, new Vector3(160, 20, 160)), Vector3.zero, Quaternion.identity);
        Check(navigation != null, "Temporary navigation for real boss prefab"); nav = NavMesh.AddNavMeshData(navigation);
        new GameObject("Dragon audio listener").AddComponent<AudioListener>();
        var target = new GameObject("Dragon target"); target.transform.position = Vector3.forward * 18;
        var collider = target.AddComponent<CapsuleCollider>(); collider.height = 2; collider.center = Vector3.up;
        var life = target.AddComponent<Health>(); life.ConfigureMaximum(10000); life.Revive(); target.AddComponent<DamageReceiver>();
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        var boss = root.GetComponent<SoulEaterPhaseOneController>(); boss.SetAutomaticEngagement(false);
        var effects = root.GetComponent<SoulEaterEffects>();
        effects.CuePlayed += (cue, clip) => Played.Add(cue);
        yield return null; yield return null;
        Check(AudioRuntime.SfxGroup != null, "Runtime SFX mixer available");
        var voice = root.transform.Find("Voice").GetComponent<AudioSource>();
        var fire = root.transform.Find("BreathLoop").GetComponent<AudioSource>();
        var foley = root.transform.Find("Foley").GetComponent<AudioSource>();
        void Reset()
        {
            boss.ResetEncounter(); life.Revive(); target.transform.position = Vector3.forward * 18;
            Physics.SyncTransforms(); boss.BeginEncounter(target.transform); Played.Clear();
        }
        boss.BeginEncounter(target.transform);
        Check(Played.Count(c => c == SoulEaterCue.Emerge) == 1, "Encounter starts with one pack roar");
        foreach (var action in new[] { SoulEaterAction.Bite, SoulEaterAction.Tail })
        {
            Reset(); Check(boss.TryStartAttack(action), "Starts " + action);
            Advance(boss, boss.Settings.Windup(action) - .03f);
            Check(Played.Count == 0, "No premature attack sound during " + action + " windup");
            Advance(boss, .04f);
            var cue = action == SoulEaterAction.Bite ? SoulEaterCue.Bite : SoulEaterCue.Tail;
            Check(Played.Count(c => c == cue) == 1 && voice.clip.name == "Dragon_Attack" && voice.isPlaying, "One selected attack sound at " + action + " release");
        }
        Reset(); boss.TryStartAttack(SoulEaterAction.Breath);
        Check(Played.SequenceEqual(new[] { SoulEaterCue.Inhale }) && voice.clip.name == "Dragon_Fire_Prepare", "Approved fire opening at warning");
        Advance(boss, boss.Settings.breathWindup + .04f);
        Check(fire.isPlaying && fire.clip.name == "Dragon_Fire_Start" && !fire.loop, "Original fire remainder starts only at flame release");
        fire.Stop(); effects.Breath(boss.MouthPosition, Vector3.forward, 1, 10, .02f);
        Check(fire.clip.name == "Dragon_Fire_Loop" && fire.loop && fire.isPlaying, "Extended breath uses flame-only continuation");
        Time.timeScale = 0; yield return null; yield return null;
        int count = Played.Count; int sample = fire.timeSamples;
        effects.Cue(SoulEaterCue.Hurt); yield return null;
        Check(Played.Count == count && fire.timeSamples == sample, "Pause freezes fire and suppresses new cues");
        Time.timeScale = 1; yield return null; yield return null;
        Check(fire.isPlaying, "Fire resumes after pause");
        boss.Combat.DamagePosture(10000);
        Check(boss.State == SoulEaterState.Staggered, "Stagger interrupts breath");
        float stopAt = Time.time + .15f; while (Time.time < stopAt) yield return null;
        Check(!fire.isPlaying, "Interrupted fire fades out and stops");
        Reset();
        boss.Health.ApplyDamage(new DamageInfo(1, target, Vector3.zero, Vector3.back, statusEffect: StatusEffectType.Burn));
        Check(!Played.Contains(SoulEaterCue.Hurt), "Status damage does not spam vocal pain");
        boss.Health.ApplyDamage(new DamageInfo(1, target, Vector3.zero, Vector3.back));
        boss.Health.ApplyDamage(new DamageInfo(1, target, Vector3.zero, Vector3.back));
        Check(Played.Count(c => c == SoulEaterCue.Hurt) == 1, "Direct damage emits one cooldown-limited hurt");
        Reset(); target.transform.position = Vector3.forward * 50; Physics.SyncTransforms();
        float chargeDistance = boss.Settings.pursuitChargeTriggerDistance;
        try { boss.Settings.pursuitChargeTriggerDistance = 100; Advance(boss, 2); }
        finally { boss.Settings.pursuitChargeTriggerDistance = chargeDistance; }
        Check(Played.Contains(SoulEaterCue.Footstep), "Actual walking animation emits ground footsteps");
        Reset(); Check(boss.TryCheatPhaseTwo(), "Phase two enabled through real controller");
        Advance(boss, boss.Settings.phaseRoar + .05f); Played.Clear();
        Check(boss.TryStartAerial(SoulEaterAction.AerialBreath), "Aerial flame starts");
        Check(Played.Contains(SoulEaterCue.Jump), "Takeoff uses pack wing sound");
        float wingWait = Time.time + .4f; while (Time.time < wingWait) yield return null;
        Advance(boss, boss.Settings.ascentTime + boss.Settings.aerialAimTime + .05f);
        Check(Played.Contains(SoulEaterCue.Wing), "Flight animation emits wing beats");
        Check(fire.isPlaying && fire.clip.name == "Dragon_Fire_Start", "Aerial flame uses approved fire");
        Advance(boss, boss.Settings.AerialPassDuration + boss.Settings.landingTime + .1f);
        Check(Played.Contains(SoulEaterCue.Land) && foley.clip.name == "Dragon_Ground", "Aerial landing uses pack ground impact");
        Reset(); Played.Clear();
        effects.DustImpact(Vector3.zero, SoulEaterDustKind.Dive);
        effects.GroundImpact(Vector3.zero, 1);
        Check(Played.Count(c => c == SoulEaterCue.Land) == 1, "Ground impact and dust do not double-trigger");
        boss.Health.ApplyDamage(new DamageInfo(100000, target, Vector3.zero, Vector3.back));
        Check(boss.State == SoulEaterState.Dead && Played.Count(c => c == SoulEaterCue.Death) == 1 &&
            !fire.isPlaying && voice.clip.name == "Dragon_Death" && voice.isPlaying, "Death stops combat sounds and plays the pack death once");
        effects.StopAll(); Check(root.GetComponentsInChildren<AudioSource>().All(s => !s.isPlaying), "Reset/disable cleanup stops every channel");
        Object.Destroy(root); Object.Destroy(target); Object.Destroy(floor);
    }
}
