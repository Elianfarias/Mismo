using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

public static class FeelBuildChecks
{
    const string Output = "output/feel-integration";
    [InitializeOnLoadMethod] static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer || !File.Exists("Temp/FeelBuildChecks.request")) return;
        string command = File.ReadAllText("Temp/FeelBuildChecks.request").Trim();
        File.Delete("Temp/FeelBuildChecks.request");
        if (command == "content") { ValidateContent(); return; }
        Run();
    }
    [MenuItem("Mismo/Feedback/Verificar build con Feel")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
            {
                target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone,
                options = ScriptCompilationOptions.None
            }, ".validation/FeelPlayerScripts");
            if (result.assemblies == null || !result.assemblies.Any(p => p.EndsWith("Mismo.Gameplay.Player.dll")) ||
                !result.assemblies.Any(p => p.EndsWith("MoreMountains.Tools.dll"))) throw new Exception("Faltan assemblies runtime de Player o Feel.");
            File.WriteAllLines(Output + "/player-compilation.txt", new[] { "PASS: compilación Windows sin UNITY_EDITOR" }.Concat(result.assemblies));
        }
        catch (Exception e) { File.WriteAllText(Output + "/player-compilation.txt", "FAIL\n" + e); }
        try
        {
            Directory.CreateDirectory(".validation/FeelContent");
            var manifest = BuildPipeline.BuildAssetBundles(".validation/FeelContent", new[]
            {
                new AssetBundleBuild { assetBundleName = "feel-combat", assetNames = new[] { "Assets/Data/Combat/Feedback/SwordFeedback.asset" } }
            }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new Exception("No se pudo construir el contenido de combate");
            ValidateContent();
        }
        catch (Exception e) { File.WriteAllText(Output + "/content-build.txt", "FAIL\n" + e); }
        var buildErrors = new System.Collections.Generic.List<string>();
        Application.LogCallback capture = (message, stack, type) =>
        { if (type == LogType.Error || type == LogType.Exception) buildErrors.Add(message); };
        Application.logMessageReceived += capture;
        try
        {
            var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = ".validation/FeelGameBuild/Mismo.exe", target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText(Output + "/game-build.txt", build.summary.result + "\nErrors: " + build.summary.totalErrors + "\n" +
                string.Join("\n", buildErrors.Concat(build.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content)).Distinct()));
        }
        catch (Exception e) { File.WriteAllText(Output + "/game-build.txt", "FAIL\n" + e); }
        finally { Application.logMessageReceived -= capture; }
    }
    static void ValidateContent()
    {
        try
        {
            var bundle = AssetBundle.LoadFromFile(".validation/FeelContent/feel-combat");
            if (bundle == null) throw new Exception("No se pudo cargar el bundle generado");
            try
            {
                // Dependencies aren't necessarily addressable by name: follow the actual serialized references.
                var profile = bundle.LoadAsset<Mismo.Gameplay.Player.Presentation.CombatFeedbackProfile>("Assets/Data/Combat/Feedback/SwordFeedback.asset");
                if (profile == null) throw new Exception("Perfil ausente del contenido");
                var cues = new[] { profile.impact, profile.heavy, profile.postureBreak, profile.block, profile.parry };
                foreach (var cue in cues)
                    if (cue.feel == null || cue.feel.FeedbacksList.Count == 0 || cue.feel.FeedbacksList.Any(f => f == null))
                        throw new Exception("Secuencia o tipo serializado ausente del contenido");
                File.WriteAllLines(Output + "/content-build.txt", new[] { "PASS: perfil cargado desde build; 5 secuencias Feel y sus tipos serializados resueltos" }.Concat(cues.Select(c => c.feel.name)));
            }
            finally { bundle.Unload(true); }
        }
        catch (Exception e) { File.WriteAllText(Output + "/content-build.txt", "FAIL\n" + e); }
    }
}
