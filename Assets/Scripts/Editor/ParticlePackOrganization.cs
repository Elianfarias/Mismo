using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Scoped import migration; preserves GUIDs and does not reseed the runtime catalog.</summary>
[InitializeOnLoad]
public static class ParticlePackOrganization
{
    const string Source = "Assets/UnityTechnologies/ParticlePack";
    const string Output = "output/particle-pack-organization";
    const string Request = "Temp/ParticlePackOrganization.request";
    [Serializable] public sealed class Entry
    {
        public string source, destination, guid, type;
        public string[] dependencies;
        public int missingScripts;
    }
    [Serializable] public sealed class Manifest { public Entry[] items; }
    static ParticlePackOrganization() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer || !File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        Directory.CreateDirectory(Output);
        try
        {
            if (command == "plan") Plan();
            else if (command == "move") Move();
            else if (command == "verify") Verify();
            else if (command == "refine") Refine();
            else if (command == "build") Build();
            else throw new ArgumentException("Unknown request: " + command);
        }
        catch (Exception e) { File.WriteAllText(Output + "/" + command + "-FAILED.txt", e.ToString()); Debug.LogException(e); }
    }
    static string[] Dependencies(string path) => AssetDatabase.GetDependencies(path, true)
        .Where(p => !AssetDatabase.IsValidFolder(p)).Select(AssetDatabase.AssetPathToGUID)
        .Where(g => !string.IsNullOrEmpty(g)).Distinct().OrderBy(g => g, StringComparer.Ordinal).ToArray();
    static int MissingScripts(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab == null ? 0 : prefab.GetComponentsInChildren<Transform>(true)
            .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
    }
    [MenuItem("Mismo/Proyecto/Particle Pack/Preparar organizacion")]
    public static void Plan()
    {
        Directory.CreateDirectory(Output);
        if (File.Exists(Output + "/manifest.json")) throw new InvalidOperationException("El manifiesto ya existe; conservar la auditoria original.");
        var items = new List<Entry>();
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(Source + "/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(p)).OrderBy(p => p, StringComparer.Ordinal))
        {
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            items.Add(new Entry { source = path, destination = Destination(path, type), guid = AssetDatabase.AssetPathToGUID(path),
                type = type?.FullName, dependencies = Dependencies(path), missingScripts = MissingScripts(path) });
        }
        if (items.Count == 0) throw new InvalidOperationException("No se encontro Particle Pack.");
        if (items.GroupBy(i => i.destination, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) throw new InvalidOperationException("Destinos duplicados.");
        foreach (var item in items)
            if (File.Exists(item.destination)) throw new InvalidOperationException("Destino ocupado: " + item.destination);
        File.WriteAllText(Output + "/manifest.json", JsonUtility.ToJson(new Manifest { items = items.ToArray() }, true));
        File.WriteAllText(Output + "/plan.txt", "PASS: " + items.Count + " assets; destinos sin colisiones; dependencias y scripts registrados.");
    }
    static string Destination(string path, Type type, string currentPath = null)
    {
        string relative = path.Substring(Source.Length + 1);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        // Complete the installed TMP shader family so relative includes remain valid.
        if (relative.StartsWith("TextMesh Pro/Shaders/", StringComparison.Ordinal)) return "Assets/Art/Shaders/TextMeshPro/" + Path.GetFileName(path);
        string[] redundant = { "EffectExamples", "Materials", "Textures", "Models", "Prefabs", "Shaders", "Scripts", "Resources", "Fonts & Materials" };
        relative = string.Join("/", relative.Split('/').Where(s => !redundant.Contains(s)));
        if (ext == ".cs") return "Assets/Scripts/ThirdParty/ParticlePack/" + relative;
        if (ext == ".unity") return "Assets/Scenes/Examples/ParticlePack/" + Path.GetFileName(path);
        if (ext == ".asset")
        {
            if (type != null && typeof(Mesh).IsAssignableFrom(type)) return "Assets/Art/Meshes/VFX/ParticlePack/" + relative;
            if (type != null && typeof(Texture).IsAssignableFrom(type)) return "Assets/Art/Textures/VFX/ParticlePack/" + relative;
            if (type != null && typeof(ScriptableObject).IsAssignableFrom(type)) return "Assets/Data/VFX/ParticlePack/" + relative;
            if (type == typeof(LightingDataAsset)) return "Assets/Scenes/Examples/ParticlePack/" + relative.Substring("Scenes/".Length);
            throw new InvalidOperationException("Tipo .asset sin clasificar: " + path + " (" + type + ")");
        }
        string category;
        switch (ext)
        {
            case ".prefab": category = "Prefabs"; break;
            case ".mat": category = "Materials"; break;
            case ".fbx": category = path.Contains("/Animations/") ? "Animations" : "FBX"; break;
            case ".controller": case ".anim": category = "Animations"; break;
            case ".shader": case ".shadergraph": case ".cginc": case ".hlsl": category = "Shaders"; break;
            case ".ttf": return "Assets/Art/Fonts/ParticlePack/" + relative;
            case ".txt":
                if (path.Contains("/Fonts/") && Path.GetFileName(path).Contains("OFL")) return "Assets/Art/Fonts/ParticlePack/" + relative;
                if (Path.GetFileName(path).StartsWith("LineBreaking", StringComparison.Ordinal)) return "Assets/Data/VFX/ParticlePack/" + relative;
                return "Assets/Documentation/ParticlePack/" + relative;
            case ".png": case ".tif": case ".exr": case ".tga":
                if (relative.StartsWith("Shared/Sprites/", StringComparison.Ordinal) || relative.StartsWith("TextMesh Pro/", StringComparison.Ordinal) || relative.StartsWith("TutorialInfo/Icons/", StringComparison.Ordinal))
                    category = "UI";
                else category = AssetImporter.GetAtPath(currentPath ?? path) is TextureImporter texture && texture.textureType == TextureImporterType.Sprite ? "Sprites" : "Textures";
                break;
            default: throw new InvalidOperationException("Extension sin clasificar: " + path);
        }
        return "Assets/Art/" + category + "/VFX/ParticlePack/" + relative;
    }
    static void Refine()
    {
        var manifest = ReadManifest();
        var oldFolders = new List<string>();
        foreach (var item in manifest.items)
        {
            string current = AssetDatabase.GUIDToAssetPath(item.guid);
            if (current != item.destination) throw new InvalidOperationException("GUID/path: " + item.source);
            string destination = Destination(item.source, AssetDatabase.GetMainAssetTypeAtPath(current), current);
            if (destination == current) continue;
            if (File.Exists(destination)) throw new IOException("Destino ocupado: " + destination);
            ProjectAssetOrganizer.EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
            string error = AssetDatabase.MoveAsset(current, destination);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
            oldFolders.Add(Path.GetDirectoryName(current).Replace('\\', '/'));
            item.destination = destination;
            File.WriteAllText(Output + "/manifest.json", JsonUtility.ToJson(manifest, true));
        }
        foreach (string folder in oldFolders.Distinct().OrderByDescending(p => p.Length))
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) AssetDatabase.DeleteAsset(folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Verify();
    }

    static Manifest ReadManifest() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Output + "/manifest.json"));
    [MenuItem("Mismo/Proyecto/Particle Pack/Aplicar organizacion")]
    public static void Move()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
        var manifest = ReadManifest();
        foreach (var item in manifest.items)
        {
            string current = AssetDatabase.GUIDToAssetPath(item.guid);
            if (!item.source.StartsWith(Source + "/", StringComparison.Ordinal) || item.destination.Contains("..") || !item.destination.StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidOperationException("Ruta fuera del alcance.");
            if (current != item.source && current != item.destination) throw new InvalidOperationException("GUID no coincide: " + item.source);
            if (File.Exists(item.destination) && AssetDatabase.AssetPathToGUID(item.destination) != item.guid) throw new InvalidOperationException("Destino ocupado: " + item.destination);
        }
        // Freeze external model materials before separating them from lookup folders.
        foreach (var item in manifest.items.Where(i => Path.GetExtension(i.source).Equals(".fbx", StringComparison.OrdinalIgnoreCase)))
        {
            string current = AssetDatabase.GUIDToAssetPath(item.guid);
            if (current == item.destination) continue;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(current);
            var importer = (ModelImporter)AssetImporter.GetAtPath(current);
            var map = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (var material in model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
            {
                if (AssetDatabase.GetAssetPath(material) == current) continue;
                var key = new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name);
                if (map.ContainsKey(key)) continue;
                importer.AddRemap(key, material); changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }
        foreach (string folder in manifest.items.Select(i => Path.GetDirectoryName(i.destination).Replace('\\', '/')).Distinct().OrderBy(p => p.Length)) ProjectAssetOrganizer.EnsureFolder(folder);
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var item in manifest.items)
            {
                string current = AssetDatabase.GUIDToAssetPath(item.guid);
                if (current == item.destination) continue;
                string error = AssetDatabase.MoveAsset(current, item.destination);
                if (!string.IsNullOrEmpty(error)) throw new IOException(error);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        // Only remove emptied import folders, never unrelated project directories.
        if (Directory.Exists(Source))
        {
            foreach (string folder in Directory.GetDirectories(Source, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
                if (!Directory.EnumerateFileSystemEntries(folder).Any()) AssetDatabase.DeleteAsset(folder.Replace('\\', '/'));
            if (!Directory.EnumerateFileSystemEntries(Source).Any()) AssetDatabase.DeleteAsset(Source);
        }
        const string vendor = "Assets/UnityTechnologies";
        if (Directory.Exists(vendor) && !Directory.EnumerateFileSystemEntries(vendor).Any()) AssetDatabase.DeleteAsset(vendor);
        File.WriteAllText(Output + "/move.txt", "PASS: " + manifest.items.Length + " assets moved through AssetDatabase.MoveAsset.");
    }
    [MenuItem("Mismo/Proyecto/Particle Pack/Verificar organizacion")]
    public static void Verify()
    {
        VerifyMigration();
        ProjectOrganizationChecks.Run();
        File.WriteAllText(Output + "/organization.txt", "PASS: ProjectOrganizationChecks.Run");
    }
    static void VerifyMigration()
    {
        var manifest = ReadManifest();
        var errors = new List<string>();
        foreach (var item in manifest.items)
        {
            if (AssetDatabase.GUIDToAssetPath(item.guid) != item.destination) { errors.Add("GUID/path: " + item.source); continue; }
            if (AssetDatabase.LoadMainAssetAtPath(item.destination) == null) errors.Add("Asset no importado: " + item.destination);
            string[] missing = item.dependencies.Except(Dependencies(item.destination)).ToArray();
            if (missing.Length > 0) errors.Add("Dependencias perdidas: " + item.destination + " => " + string.Join(", ", missing.Select(AssetDatabase.GUIDToAssetPath)));
            if (MissingScripts(item.destination) > item.missingScripts) errors.Add("Scripts perdidos: " + item.destination);
        }
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        File.WriteAllText(Output + "/verification.txt", "PASS: migracion de Particle Pack; " + manifest.items.Length + " GUIDs, assets y dependencias conservados; sin nuevos scripts ausentes.");
        Debug.Log("PARTICLE_PACK_ORGANIZATION_OK");
    }
    public static void Build()
    {
        VerifyMigration();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = ".validation/ParticlePackBuild/Mismo.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        File.WriteAllText(Output + "/build.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\n" +
            string.Join("\n", report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Distinct()));
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build: " + report.summary.result);
    }
}


