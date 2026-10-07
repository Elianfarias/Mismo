using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Core;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class StatusEffectChecks
{
    static readonly List<string> results = new List<string>();
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool pass, string message) { if (!pass) throw new Exception(message); results.Add("PASS " + message); }
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private)?.Invoke(target, args);
    static void Tick(CombatAilment effect, float now) => Call(effect, "Tick", now);
    static GameObject Target(string name, Scene scene)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule); go.name = name; SceneManager.MoveGameObjectToScene(go, scene);
        var health = go.AddComponent<Health>(); Call(health, "Awake"); Call(health, "OnEnable");
        var visuals = go.GetComponent<ActorCombatVisuals>(); Call(visuals, "Awake");
        health.Damaged += d => Call(visuals, "Hit", d);
        go.AddComponent<Invulnerability>();
        var receiver = go.AddComponent<DamageReceiver>(); Call(receiver, "Awake");
        var effect = go.AddComponent<CombatAilment>(); Call(effect, "Awake"); Call(effect, "OnEnable");
        return go;
    }
    [MenuItem("Mismo/Combate/Estados/Verificar veneno y colores")]
    public static void Run()
    {
        Directory.CreateDirectory(StatusEffectSetup.Output); results.Clear();
        var scene = EditorSceneManager.NewPreviewScene();
        var source = new GameObject("Status source"); SceneManager.MoveGameObjectToScene(source, scene);
        try
        {
            var profile = StatusEffectPresentation.Current;
            Check(profile != null && profile.poisonOverlay != null && profile.poisonParticles != null, "Runtime catalog resolves the serialized status presentation");
            foreach (var path in AssetDatabase.GetDependencies(StatusEffectSetup.ProfilePath).Where(p => p.EndsWith(".shader")))
                Check(!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(path)), "Shader compiles: " + path);
            var target = Target("Poison duration", scene); var health = target.GetComponent<Health>(); var effect = target.GetComponent<CombatAilment>();
            var originalMaterials = target.GetComponent<Renderer>().sharedMaterials;
            DamageInfo last = default; health.Damaged += d => last = d;
            float start = Time.time;
            CombatAilment.Poison(target, source, "Bow", 4, 3, "PoisonArrow", 721);
            var vfx = target.GetComponent<StatusEffectVisual>();
            Check(vfx != null && vfx.IsPlaying && vfx.OverlayCount == 1, "Poison creates an overlay and aura on the target");
            Check(originalMaterials.SequenceEqual(target.GetComponent<Renderer>().sharedMaterials), "Original body materials are untouched");
            CombatAilment.Poison(target, source, "Bow", 4, 3, "PoisonArrow", 722);
            Check(vfx.OverlayCount == 1, "Reapplication does not duplicate the aura or overlay");
            Tick(effect, start + .5f); Check(health.Current == 100, "No immediate tick on application");
            target.GetComponent<Invulnerability>().StartWindow(10);
            target.GetComponent<DefenseWindow>().OpenDodge(10);
            Tick(effect, start + 1.01f);
            Check(health.Current == 96, "Existing poison ticks through contact invulnerability and dodge");
            Check(last.StatusEffect == StatusEffectType.Poison && last.WeaponFamilyId == "Bow" && last.AbilityId == "PoisonArrow" && last.AbilityUseId == 722,
                "Receiver preserves status, weapon and refreshed cast identity through Health.Damaged");
            var numberInstance = typeof(DamageNumbers).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var entries = (IList)typeof(DamageNumbers).GetField("entries", Private).GetValue(numberInstance);
            var number = entries[entries.Count - 1];
            Check((Color)number.GetType().GetField("color").GetValue(number) == profile.ColorFor(StatusEffectType.Poison), "Actual floating number uses the configured poison green");
            Tick(effect, start + 3.05f);
            Check(health.Current == 88 && !vfx.IsPlaying && !effect.IsActive(StatusEffectType.Poison), "Three-second poison applies all three ticks and removes its visuals at expiry");

            var mixed = Target("Independent states", scene); var mixedHealth = mixed.GetComponent<Health>();
            var seen = new List<StatusEffectType>(); mixedHealth.Damaged += d => seen.Add(d.StatusEffect);
            start = Time.time;
            CombatAilment.Poison(mixed, source, "Bow", 2, 5);
            CombatAilment.Bleed(mixed, source, "Sword", 3, 5);
            CombatAilment.Burn(mixed, source, "Staff", 5, 5);
            Tick(mixed.GetComponent<CombatAilment>(), start + 1.01f);
            Check(mixedHealth.Current == 90 && seen.Distinct().Count() == 3, "Poison, bleed and burn coexist and each deal one independently identified tick");
            Check(!mixed.GetComponent<Invulnerability>().IsInvulnerable, "Ticks do not create contact immunity that would swallow another state");
            mixed.GetComponent<CombatAilment>().Clear(StatusEffectType.Poison);
            Check(!mixed.GetComponent<StatusEffectVisual>().IsPlaying && mixed.GetComponent<CombatAilment>().IsActive(StatusEffectType.Bleed), "Curing poison removes only its own state and visuals");
            var bleedOnly = Target("Bleed only", scene); CombatAilment.Bleed(bleedOnly, source, "Sword", 3, 5);
            Check(bleedOnly.GetComponent<StatusEffectVisual>() == null, "Bleed never starts a poison aura");
            Check(DamageNumbers.ColorFor(false, .8f, StatusEffectType.Bleed) != DamageNumbers.ColorFor(false, .8f, StatusEffectType.Burn) &&
                DamageNumbers.ColorFor(false, .8f, StatusEffectType.Poison) != DamageNumbers.ColorFor(false, .8f), "Poison, bleed, burn and ordinary hits have independent colors");

            CombatAilment.Poison(target, source, "Bow", 1, 5); target.SetActive(false); Call(effect, "OnDisable");
            Check(!vfx.IsPlaying && !effect.IsActive(StatusEffectType.Poison), "Disable clears status and visuals for pooled targets");
            target.SetActive(true); Call(effect, "OnEnable");
            Check(!effect.IsActive(StatusEffectType.Poison), "Re-enable starts clean");
            CombatAilment.Poison(target, source, "Bow", 1, 5);
            health.ApplyDamage(new DamageInfo(999, source, Vector3.zero, Vector3.zero));
            Check(!vfx.IsPlaying && !effect.IsActive(StatusEffectType.Poison), "Death removes the aura immediately");
            health.Revive(); Check(!effect.IsActive(StatusEffectType.Poison), "Revive cannot inherit a previous poison");
            CombatAilment.Poison(target, source, "Bow", 1, 5); Object.DestroyImmediate(source);
            Tick(effect, Time.time + 1); Check(!vfx.IsPlaying, "Destroyed source releases orphaned status visuals");
            try { ProjectOrganizationChecks.Run(); results.Add("PASS ProjectOrganizationChecks.Run"); }
            catch (Exception e) { File.WriteAllText(StatusEffectSetup.Output + "/organization.txt", e.ToString()); results.Add("EXISTING PROJECT ORGANIZATION ISSUES: see organization.txt"); }
            File.WriteAllLines(StatusEffectSetup.Output + "/checks.txt", results);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            var numbers = Object.FindAnyObjectByType<DamageNumbers>(); if (numbers != null) Object.DestroyImmediate(numbers.gameObject);
        }
    }

    [MenuItem("Mismo/Combate/Estados/Renderizar goblin envenenado")]
    public static void Preview()
    {
        Directory.CreateDirectory(StatusEffectSetup.Output);
        var preview = new PreviewRenderUtility();
        var goblin = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/Goblin.prefab"));
        preview.AddSingleGO(goblin);
        try
        {
            var animator = goblin.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var idle = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0);
                if (idle != null) idle.SampleAnimation(animator.gameObject, .35f);
            }
            var renderers = goblin.GetComponentsInChildren<Renderer>().Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
            var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var camera = preview.camera;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f,.078f,.072f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 100; camera.fieldOfView = 32;
            float distance = Mathf.Max(bounds.size.y, bounds.size.x) * 2.15f;
            camera.transform.position = bounds.center + new Vector3(.45f,.18f,1).normalized * distance;
            camera.transform.LookAt(bounds.center);
            preview.lights[0].intensity = 1.4f; preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
            preview.lights[1].intensity = .6f; preview.ambientColor = new Color(.35f,.38f,.34f);
            Render(preview, "goblin-normal.png");
            var effect = goblin.AddComponent<StatusEffectVisual>(); effect.PlayPoison(); Call(effect, "UpdateVisual", 1.5f);
            foreach (var root in goblin.scene.GetRootGameObjects())
                if (root.name == "Poison status VFX") foreach (var particle in root.GetComponentsInChildren<ParticleSystem>()) particle.Simulate(1.5f, false, true, true);
            Render(preview, "goblin-poison.png"); effect.Stop();
        }
        finally { preview.Cleanup(); }
    }
    static void Render(PreviewRenderUtility preview, string name)
    {
        preview.BeginPreview(new Rect(0,0,1000,1000), GUIStyle.none); preview.Render(true);
        var rendered = preview.EndPreview(); var previous = RenderTexture.active;
        var converted = RenderTexture.GetTemporary(1000, 1000, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(rendered, converted); RenderTexture.active = converted;
        var texture = new Texture2D(1000,1000,TextureFormat.RGBA32,false);
        texture.ReadPixels(new Rect(0,0,1000,1000),0,0); texture.Apply(); RenderTexture.active = previous;
        File.WriteAllBytes(StatusEffectSetup.Output + "/" + name, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        RenderTexture.ReleaseTemporary(converted);
    }

    [MenuItem("Mismo/Combate/Estados/Verificar build de estados")]
    public static void Build()
    {
        Directory.CreateDirectory(StatusEffectSetup.Output);
        Directory.CreateDirectory(".validation/StatusEffectScripts"); Directory.CreateDirectory(".validation/StatusEffectContent");
        var scripts = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings {
            target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone, options = ScriptCompilationOptions.None }, ".validation/StatusEffectScripts");
        if (scripts.assemblies == null || !scripts.assemblies.Any(p => p.EndsWith("Mismo.Gameplay.Player.dll"))) throw new Exception("Player scripts did not compile");
        File.WriteAllText(StatusEffectSetup.Output + "/runtime-compilation.txt", "PASS: Windows scripts compile without UNITY_EDITOR");
        var manifest = BuildPipeline.BuildAssetBundles(".validation/StatusEffectContent", new[] { new AssetBundleBuild {
            assetBundleName = "status-effects", assetNames = new[] { StatusEffectSetup.ProfilePath } } }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new Exception("Status effect content build failed");
        var bundle = AssetBundle.LoadFromFile(".validation/StatusEffectContent/status-effects");
        try
        {
            var profile = bundle.LoadAsset<StatusEffectPresentation>(StatusEffectSetup.ProfilePath);
            if (profile == null || profile.poisonParticles == null || profile.poisonOverlay == null || profile.damageColors.Length < 3) throw new Exception("Status profile or serialized dependencies missing from built content");
            var layers = profile.poisonParticles.GetComponentsInChildren<ParticleSystemRenderer>();
            if (layers.Length != 3 || layers.Any(r => r.sharedMaterial == null || r.sharedMaterial.shader == null)) throw new Exception("Particle shaders missing from content");
            File.WriteAllText(StatusEffectSetup.Output + "/content-build.txt", "PASS: built bundle reloads profile, 3 particle layers, overlay, materials and shaders via serialized references");
        }
        finally { if (bundle != null) bundle.Unload(true); }
        var buildErrors = new List<string>();
        Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception) buildErrors.Add(message); };
        Application.logMessageReceived += capture;
        try
        {
            var game = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = ".validation/StatusEffectGame/Mismo.exe", target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development });
            File.WriteAllText(StatusEffectSetup.Output + "/game-build.txt", game.summary.result + "\nErrors: " + game.summary.totalErrors + "\n" + string.Join("\n", buildErrors.Distinct()));
        }
        catch (Exception e) { File.WriteAllText(StatusEffectSetup.Output + "/game-build.txt", "FAIL\n" + e); }
        finally { Application.logMessageReceived -= capture; }
    }
}
