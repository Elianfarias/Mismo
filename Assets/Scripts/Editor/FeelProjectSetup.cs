using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Core;
using Mismo.Gameplay.Player.Presentation;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit, repeatable Feel installation; never runs without a menu/request.</summary>
public static class FeelProjectSetup
{
    const string Original = "Assets/Feel";
    const string Code = "Assets/Scripts/ThirdParty/Feel";
    const string Output = "output/feel-integration";
    static readonly List<string> report = new List<string>();
    static readonly string[] DemoRoots = { "FeelDemos", "FeelDemosHDRP", "MMFeedbacks/Demos", "MMTools/Demos", "NiceVibrations/Demo", "NiceVibrations/HapticSamples" };
    [InitializeOnLoadMethod] static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string request = "Temp/FeelProjectSetup.request";
        if (!File.Exists(request)) return;
        File.Delete(request); Run();
    }
    [MenuItem("Mismo/Feedback/Configurar Feel y limpiar demos")]
    public static void Run()
    {
        report.Clear(); Directory.CreateDirectory(Output);
        try
        {
            CleanAndOrganize();
            ConfigureCombat();
            RegisterRuntimeDefaults();
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Output + "/setup.txt", new[] { "PASS" }.Concat(report));
            Debug.Log("FEEL_SETUP_OK");
        }
        catch (Exception e) { File.WriteAllLines(Output + "/setup.txt", new[] { "FAIL", e.ToString() }.Concat(report)); Debug.LogException(e); }
        finally { AssetDatabase.Refresh(); }
    }

    static bool IsDemo(string path) => DemoRoots.Any(d => path.StartsWith(Original + "/" + d + "/", StringComparison.Ordinal));
    static bool IsDocumentation(string path) => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        path.IndexOf("license", StringComparison.OrdinalIgnoreCase) >= 0 ||
        path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(path).StartsWith("nv-");

    static void CleanAndOrganize()
    {
        var all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p)).ToArray();
        var candidates = new HashSet<string>(all.Where(p => IsDemo(p) && !IsDocumentation(p)));
        // Anything used by a retained asset is protected, including transitive textures/scripts.
        var retained = all.Where(p => !candidates.Contains(p) && !p.EndsWith(".cs") && !p.EndsWith(".dll") && !p.EndsWith(".asmdef") && !p.EndsWith(".asmref")).ToArray();
        var needed = new HashSet<string>(AssetDatabase.GetDependencies(retained, true));
        var deleting = candidates.Where(p => !needed.Contains(p)).OrderBy(p => p).ToArray();
        var migration = new List<string> { "action\tguid\tfrom\tto" };
        if (AssetDatabase.IsValidFolder(Original))
        {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var path in deleting)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!AssetDatabase.DeleteAsset(path)) throw new Exception("No se pudo borrar " + path);
                migration.Add("delete-unused-demo\t" + guid + "\t" + path + "\t");
            }
            report.Add("Eliminados " + deleting.Length + " archivos de demostración sin referencias desde contenido retenido.");
            foreach (string path in candidates.Where(needed.Contains)) report.Add("Dependencia conservada: " + path);
            EnsureFolder("Assets/Scripts/ThirdParty");
            Move(Original, Code);
        }
        finally { AssetDatabase.StopAssetEditing(); }
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var files = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(Code + "/") && !AssetDatabase.IsValidFolder(p)).ToArray();
        var mappings = new Dictionary<string, string>();
        foreach (var path in files)
        {
            string category = Category(path);
            if (category == null) continue;
            string destination = "Assets/" + category + "/Feel/" + path.Substring(Code.Length + 1).Replace("/Resources/", "/Defaults/");
            EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
        }
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var path in files)
            {
                string category = Category(path);
                if (category == null) continue;
                string relative = path.Substring(Code.Length + 1).Replace("/Resources/", "/Defaults/");
                string destination = "Assets/" + category + "/Feel/" + relative;
                string guid = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path + ".meta"), "guid: ([a-f0-9]+)").Groups[1].Value;
                Move(path, destination);
                if (!File.ReadAllText(destination + ".meta").Contains("guid: " + guid)) throw new Exception("GUID cambió: " + path);
                mappings[path.Replace(Code, Original)] = destination;
                migration.Add("move\t" + guid + "\t" + path.Replace(Code, Original) + "\t" + destination);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        // Preserve the assembly layout, but remove empty Resources/demo directories.
        foreach (var folder in Directory.GetDirectories(Code, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
            if (!Directory.EnumerateFileSystemEntries(folder).Any()) AssetDatabase.DeleteAsset(folder.Replace('\\', '/'));
        foreach (var file in Directory.GetFiles(Code, "*.cs", SearchOption.AllDirectories))
        {
            string before = File.ReadAllText(file), after = before;
            foreach (var mapping in mappings) after = after.Replace(mapping.Key, mapping.Value);
            after = after.Replace("Assets/MMTools/MMTween/Editor/", "Assets/Data/Feedback/Feel/Curves/");
            if (before != after) File.WriteAllText(file, after);
        }
        foreach (var file in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(Code + "/") && !AssetDatabase.IsValidFolder(p)))
            migration.Add("move\t" + AssetDatabase.AssetPathToGUID(file) + "\t" + file.Replace(Code, Original) + "\t" + file);
        File.WriteAllLines(Output + "/migration.tsv", migration);
        report.Add("Traslados con AssetDatabase.MoveAsset y GUID conservados; licencias preservadas.");
    }
    static string Category(string path)
    {
        if (IsDocumentation(path)) return "Documentation";
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".prefab": return "Art/Prefabs";
            case ".mat": return "Art/Materials";
            case ".anim": case ".controller": case ".overridecontroller": case ".mask": return "Art/Animations";
            case ".png": case ".jpg": case ".jpeg": case ".psd": case ".tga": case ".exr": return path.Contains("/Editor/") ? "Art/UI" : "Art/Textures";
            case ".fbx": return "Art/FBX";
            case ".obj": case ".mtl": return "Art/Models";
            case ".shader": case ".shadergraph": case ".shadersubgraph": case ".cginc": case ".hlsl": case ".compute": return "Art/Shaders";
            case ".wav": case ".mp3": case ".ogg": case ".mixer": case ".haptic": case ".ahap": return "Art/Audio";
            case ".ttf": case ".otf": return "Art/Fonts";
            case ".unity": return "Scenes";
            case ".rendertexture": return "Art/Textures";
            case ".txt": return "Data/Feedback";
            case ".asset":
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type != null && typeof(Mesh).IsAssignableFrom(type)) return "Art/Meshes";
                if (type != null && typeof(Texture).IsAssignableFrom(type)) return "Art/Textures";
                return "Data/Feedback";
            default: return null;
        }
    }
    static void Move(string from, string to)
    {
        string error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error)) throw new Exception(error);
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void ConfigureCombat()
    {
        const string folder = "Assets/Art/Prefabs/Feedback/Feel";
        EnsureFolder(folder);
        var profile = AssetDatabase.LoadAssetAtPath<CombatFeedbackProfile>("Assets/Data/Combat/Feedback/SwordFeedback.asset");
        if (profile == null) throw new Exception("Falta SwordFeedback");
        foreach (CombatCue cue in new[] { CombatCue.Impact, CombatCue.Heavy, CombatCue.PostureBreak, CombatCue.Block, CombatCue.Parry })
        {
            string path = folder + "/Combat" + cue + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var root = new GameObject("Combat" + cue);
                try
                {
                    var player = root.AddComponent<MMF_Player>();
                    player.InitializationMode = MMFeedbacks.InitializationModes.Awake;
                    player.AutoPlayOnStart = player.AutoPlayOnEnable = false;
                    player.ForceTimescaleMode = true; player.ForcedTimescaleMode = TimescaleModes.Unscaled;
                    player.PlayerTimescaleMode = TimescaleModes.Unscaled; player.CooldownDuration = .045f;
                    float amplitude = cue == CombatCue.Impact ? .025f : cue == CombatCue.Block ? .018f : cue == CombatCue.Heavy ? .065f : cue == CombatCue.Parry ? .075f : .1f;
                    float duration = cue == CombatCue.Impact || cue == CombatCue.Block ? .09f : .16f;
                    var shake = (MMF_CameraShake)player.AddFeedback(typeof(MMF_CameraShake));
                    shake.Label = "Sacudida " + cue; shake.Channel = CombatFeelPlayer.Channel;
                    shake.CameraShakeProperties = new MMCameraShakeProperties(duration, amplitude, 24);
                    if (cue == CombatCue.Parry || cue == CombatCue.PostureBreak)
                    {
                        var flash = (MMF_Flash)player.AddFeedback(typeof(MMF_Flash));
                        flash.Label = "Destello " + cue; flash.Channel = CombatFeelPlayer.Channel;
                        flash.FlashColor = cue == CombatCue.Parry ? new Color(1, .78f, .3f) : new Color(1, .5f, .2f);
                        flash.FlashDuration = .12f; flash.FlashAlpha = cue == CombatCue.Parry ? .055f : .04f;
                    }
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { Object.DestroyImmediate(root); }
            }
            // Preserve any user-assigned custom sequence when running setup again.
            if (profile.Get(cue).feel == null) profile.Get(cue).feel = prefab.GetComponent<MMF_Player>();
            report.Add("Secuencia " + cue + ": " + path);
        }
        EditorUtility.SetDirty(profile);
    }
    static void RegisterRuntimeDefaults()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        var entries = catalog.entries.ToList();
        foreach (var name in new[] { "nv-pattern-template", "nv-emphasis-template", "nv-constant-template", "MMDebugOnScreenConsole" })
        {
            var asset = FeelAssets.Load(name);
            if (asset == null) throw new Exception("Falta default Feel: " + name);
            string key = "Feel/" + name;
            var entry = entries.FirstOrDefault(e => e.key == key);
            if (entry == null) { entry = new RuntimeAssetCatalog.Entry { key = key }; entries.Add(entry); }
            entry.assets = new[] { asset };
        }
        catalog.entries = entries.ToArray(); catalog.Invalidate(); EditorUtility.SetDirty(catalog);
        report.Add("Sólo defaults de runtime registrados en catálogo; plantillas de editor resueltas por GUID.");
    }
}
