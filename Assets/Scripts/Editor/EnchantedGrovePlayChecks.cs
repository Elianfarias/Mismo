using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch-only check: load the delivered scene and exercise its actual startup path.
[InitializeOnLoad]
public static class EnchantedGrovePlayChecks
{
    const string Key = "EnchantedGrovePlayChecks";
    static EnchantedGrovePlayChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Changed;
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate batch validation project");
        EditorSceneManager.OpenScene("Assets/Scenes/Previews/EnchantedGrove.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + "Finished", false);
        SessionState.SetBool(Key + "Failed", false);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(SessionState.GetBool(Key + "Failed", false) ? 1 : 0);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || SessionState.GetBool(Key + "Finished", false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 90) { Finish("Timed out starting the preview scene", true); return; }
        if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < 3) return;
        try
        {
            var world = VegetationMotionWorld.Current;
            if (world == null || world.Settings?.shader == null || Shader.GetGlobalVector("_MismoMotion").x <= 0) throw new InvalidOperationException("Preview wind did not start/tick");
            var bindings = UnityEngine.Object.FindObjectsByType<VegetationMotionBinding>();
            int renderers = 0;
            foreach (var binding in bindings) foreach (var renderer in binding.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.sharedMaterial.shader != world.Settings.shader) throw new InvalidOperationException("Unbound renderer " + renderer.name);
                renderers++;
            }
            if (renderers < 20) throw new InvalidOperationException("Preview vegetation unexpectedly missing");
            var flies = UnityEngine.Object.FindObjectsByType<ParticleSystem>().Single(p => p.name == "Fireflies");
            if (flies.particleCount == 0 || flies.particleCount > 24) throw new InvalidOperationException("Firefly emission/count failed");
            Finish("PASS: saved preview loaded in Play Mode; wind updated and bound " + renderers + " LOD renderers; " + flies.particleCount + " fireflies emitted; no world generation required.", false);
        }
        catch (Exception e) { Finish(e.ToString(), true); }
    }
    static void Finish(string message, bool failed)
    {
        Directory.CreateDirectory("output/enchanted-grove");
        File.WriteAllText("output/enchanted-grove/play-mode.txt", message);
        Debug.Log(message);
        SessionState.SetBool(Key + "Finished", true);
        SessionState.SetBool(Key + "Failed", failed);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else { SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}
