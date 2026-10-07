using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class StatusEffectPlayChecks
{
    const string Key = "Mismo.StatusEffectPlayChecks";
    const string ScenePath = "Assets/Scenes/Validation/StatusEffectValidation.unity";
    const string Output = StatusEffectSetup.Output;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] sealed class SavedScenes { public SceneSetup[] scenes; }
    static IEnumerator routine;
    static int frame;
    static double deadline;

    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged -= State; EditorApplication.playModeStateChanged += State;
        if (SessionState.GetBool(Key, false)) deadline = EditorApplication.timeSinceStartup + 120;
    }
    [MenuItem("Mismo/Combate/Estados/Probar flecha y veneno en Play Mode")]
    public static void Run()
    {
        RestoreLastRun();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Hay una escena con cambios sin guardar; se conserva.");
        if (File.Exists(ScenePath)) throw new IOException("Ya existe la escena temporal de validación.");
        Directory.CreateDirectory(Output);
        SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(new SavedScenes { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        ProjectAssetOrganizer.EnsureFolder("Assets/Scenes/Validation");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Success", false);
        deadline = EditorApplication.timeSinceStartup + 120;
        File.WriteAllText(Output + "/play-checks.txt", "Real projectile / runtime status checks\n");
        EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) { Application.runInBackground = true; routine = null; frame = -1; deadline = EditorApplication.timeSinceStartup + 120; }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        SessionState.SetBool(Key, false);
        RestoreLastRun();
        if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Key + ".Success", false) ? 0 : 1);
    }
    static void RestoreLastRun()
    {
        string json = SessionState.GetString(Key + ".Scenes", ""); if (string.IsNullOrEmpty(json)) return;
        var saved = JsonUtility.FromJson<SavedScenes>(json);
        if (saved.scenes.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.DeleteAsset(ScenePath); SessionState.EraseString(Key + ".Scenes");
    }
    static void Poll()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || Time.frameCount < 5 || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try { routine ??= Checks(); if (!routine.MoveNext()) Finish(true, "All real projectile / runtime status checks passed"); }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        File.AppendAllText(Output + "/play-checks.txt", (success ? "PASS " : "FAIL ") + message + "\n");
        SessionState.SetBool(Key + ".Success", success); Time.timeScale = 1; EditorApplication.ExitPlaymode();
    }
    static void Check(bool pass, string message)
    {
        if (!pass) throw new Exception(message);
        File.AppendAllText(Output + "/play-checks.txt", "PASS " + message + "\n");
    }
    static IEnumerator Checks()
    {
        var camera = new GameObject("Status camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.gameObject.AddComponent<AudioListener>();
        camera.backgroundColor = new Color(.12f, .15f, .17f); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.nearClipPlane = .03f; camera.farClipPlane = 100; camera.fieldOfView = 34;
        var sun = new GameObject("Status key light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.5f;
        sun.transform.rotation = Quaternion.Euler(32, 155, 0); RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.5f, .55f, .6f);
        Screen.SetResolution(1000, 1000, false);
        var holder = new GameObject("Isolated goblin"); holder.SetActive(false);
        var goblin = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/Goblin.prefab"), holder.transform);
        foreach (var brain in goblin.GetComponentsInChildren<EnemyController>()) brain.enabled = false;
        foreach (var agent in goblin.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>()) agent.enabled = false;
        holder.SetActive(true);
        var health = goblin.GetComponent<Health>();
        var body = goblin.GetComponent<Collider>();
        Physics.SyncTransforms();
        var center = body.bounds.center;
        camera.transform.position = center + new Vector3(.4f, .17f, 1).normalized * body.bounds.size.y * 2.6f;
        camera.transform.LookAt(center + Vector3.up * .15f);
        var source = new GameObject("Bow source"); source.transform.position = goblin.transform.position + Vector3.forward * 4;
        var runner = source.AddComponent<AbilityRunner>(); runner.enabled = false;
        var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Data/Weapons/Bow/PoisonArrow.asset");
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Bow/Bow.asset");
        var action = ability.actions.OfType<PoisonArrowAction>().Single();
        var cast = new AbilityExecution(runner, weapon, ability, Vector3.back, center) { AimPoint = center };
        yield return null;
        Capture(camera, "goblin-play-normal.png");
        for (int i = 0; i < 3; i++) yield return null;
        DamageInfo lastTick = default; int ticks = 0;
        health.Damaged += d => { if (d.IsStatusTick) { lastTick = d; ticks++; } };
        float initial = health.Current; action.Begin(cast);
        float until = Time.time + .8f; while (Time.time < until) yield return null;
        var state = goblin.GetComponent<CombatAilment>(); var visual = goblin.GetComponent<StatusEffectVisual>();
        Check(health.Current < initial && state != null && state.IsActive(StatusEffectType.Poison) && visual.IsPlaying,
            "The existing PoisonArrow asset hits the real goblin and starts its poison aura");
        until = Time.time + .65f; while (Time.time < until) yield return null;
        Check(ticks == 1 && lastTick.StatusEffect == StatusEffectType.Poison && lastTick.AbilityId == ability.Id && lastTick.AbilityUseId == cast.AttackId,
            "Real Update tick retains ability attribution and poison type");
        var numbers = Object.FindAnyObjectByType<DamageNumbers>();
        var entries = (IList)typeof(DamageNumbers).GetField("entries", Private).GetValue(numbers);
        var entry = entries[entries.Count - 1];
        Check((Color)entry.GetType().GetField("color").GetValue(entry) == StatusEffectPresentation.Current.ColorFor(StatusEffectType.Poison), "Real poison tick creates a green damage number");
        Capture(camera, "goblin-play-poison.png");
        for (int i = 0; i < 3; i++) yield return null;
        var overlay = goblin.scene.GetRootGameObjects().Single(g => g.name == "Poison status VFX");
        var mesh = goblin.GetComponentsInChildren<Renderer>().First(r => r.enabled && r is SkinnedMeshRenderer);
        var copy = overlay.GetComponentsInChildren<Renderer>().First(r => r.name == mesh.name + " poison overlay");
        goblin.transform.position += Vector3.right; Physics.SyncTransforms(); yield return null; yield return null;
        Check(Vector3.Distance(mesh.transform.position, copy.transform.position) < .001f, "Overlay follows the moving animated goblin");
        Time.timeScale = 0; float current = health.Current;
        double resume = EditorApplication.timeSinceStartup + 1.2; while (EditorApplication.timeSinceStartup < resume) yield return null;
        Check(health.Current == current, "Pausing freezes poison ticks"); Time.timeScale = 1;
        until = Time.time + action.duration + .2f; while (Time.time < until) yield return null;
        Check(ticks == Mathf.FloorToInt(action.duration) && !visual.IsPlaying && !state.IsActive(StatusEffectType.Poison), "Full duration produces all ticks and releases the aura");
        // A dodged new arrow must not apply poison.
        center = body.bounds.center; cast = new AbilityExecution(runner, weapon, ability, Vector3.back, center) { AimPoint = center };
        goblin.GetComponent<DefenseWindow>().OpenDodge(2); action.Begin(cast);
        until = Time.time + .6f; while (Time.time < until) yield return null;
        Check(!state.IsActive(StatusEffectType.Poison) && !visual.IsPlaying, "A dodged projectile cannot apply poison");
        CombatAilment.Poison(goblin, source, "Bow", 1, 5);
        goblin.SetActive(false); yield return null;
        Check(!visual.IsPlaying && !state.IsActive(StatusEffectType.Poison) && overlay == null, "Disable/pooling removes runtime overlays and status");
    }
    static void Capture(Camera camera, string file)
    {
        var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        var rt = RenderTexture.GetTemporary(1000, 1000, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var image = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + file, image.EncodeToPNG());
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.Destroy(image); }
    }
}
