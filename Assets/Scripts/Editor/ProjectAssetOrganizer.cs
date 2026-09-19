using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>One-time, GUID-preserving migration driven by a reviewable manifest.</summary>
[InitializeOnLoad]
public static class ProjectAssetOrganizer
{
    public const string PlanPath = "output/project-organization/migration-plan.json";
    const string Request = "Temp/ProjectOrganization.request";
    [Serializable] public sealed class Item { public string source, destination, guid, key, updatedText; }
    [Serializable] public sealed class Plan { public Item[] items; }
    static ProjectAssetOrganizer() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        try { Run(); }
        catch (Exception e) { File.WriteAllText("output/project-organization/FAILED.txt", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Mismo/Proyecto/Aplicar organización de assets")]
    public static void Run()
    {
        try { RunCore(); }
        finally { SessionState.SetBool("Mismo.Organization.Running", false); }
    }

    static void RunCore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode antes de reorganizar assets.");
        var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
        foreach (var item in plan.items)
        {
            if (!item.source.StartsWith("Assets/") || !item.destination.StartsWith("Assets/") || item.destination.Contains("..")) throw new InvalidOperationException("Ruta fuera de Assets");
            if (item.source == item.destination || string.IsNullOrEmpty(item.guid)) continue;
            string current = AssetDatabase.GUIDToAssetPath(item.guid);
            if (current != item.source && current != item.destination) throw new InvalidOperationException("Cambió el asset: " + item.source + " -> " + current);
            if (File.Exists(item.destination) && AssetDatabase.AssetPathToGUID(item.destination) != item.guid) throw new InvalidOperationException("Colisión: " + item.destination);
        }
        SessionState.SetBool("Mismo.Organization.Running", true);
        // Register destination folders before batching: CreateFolder inside StartAssetEditing
        // leaves parents unavailable to MoveAsset until imports resume.
        foreach (string folder in plan.items.Where(i => i.source != i.destination).Select(i => Path.GetDirectoryName(i.destination).Replace('\\', '/')).Distinct().OrderBy(p => p.Length)) EnsureFolder(folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.DisallowAutoRefresh(); AssetDatabase.StartAssetEditing();
        int count = 0;
        try
        {
            foreach (var item in plan.items)
            {
                if (item.source == item.destination || string.IsNullOrEmpty(item.guid)) continue;
                string current = AssetDatabase.GUIDToAssetPath(item.guid);
                if (current == item.destination) continue;
                EnsureFolder(Path.GetDirectoryName(item.destination).Replace('\\', '/'));
                string error = AssetDatabase.MoveAsset(current, item.destination);
                if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                count++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); AssetDatabase.AllowAutoRefresh(); }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var item in plan.items.Where(i => i.updatedText != null))
        {
            File.WriteAllText(item.destination, item.updatedText);
            AssetDatabase.ImportAsset(item.destination);
        }
        SeedCatalog(plan);
        // Remove only empty directories, never source content or user assets.
        foreach (string directory in Directory.GetDirectories("Assets", "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) AssetDatabase.DeleteAsset(directory.Replace('\\', '/'));
        AssetDatabase.SaveAssets();
        foreach (var item in plan.items.Where(i => !string.IsNullOrEmpty(i.guid)))
            if (AssetDatabase.GUIDToAssetPath(item.guid) != item.destination) throw new InvalidOperationException("GUID cambió: " + item.source);
        SessionState.SetBool("Mismo.Organization.Running", false);
        Directory.CreateDirectory("output/project-organization");
        File.WriteAllText("output/project-organization/COMPLETE.txt", $"Moved {count} assets; verified {plan.items.Length} manifest entries; catalog registered in Preloaded Assets.");
        Debug.Log("PROJECT_ORGANIZATION_OK: " + count + " assets moved, GUIDs preserved.");
    }

    static void SeedCatalog(Plan plan)
    {
        EnsureFolder("Assets/Data/System");
        var catalog = AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<RuntimeAssetCatalog>(); AssetDatabase.CreateAsset(catalog, ProjectAssets.CatalogPath); }
        var entries = new List<RuntimeAssetCatalog.Entry>();
        var folders = new Dictionary<string, RuntimeAssetCatalog.Folder>();
        foreach (var item in plan.items.Where(i => !string.IsNullOrEmpty(i.key)))
        {
            var assets = RuntimeCatalogBuilder.RuntimeObjects(item.destination);
            if (assets.Length == 0) continue; // Editor-only table collections do not belong in a player.
            entries.Add(new RuntimeAssetCatalog.Entry { key = item.key, assets = assets });
            if (!item.key.Contains("/")) continue;
            string path = Path.GetDirectoryName(item.destination).Replace('\\', '/');
            string prefix = item.key.Substring(0, item.key.LastIndexOf('/'));
            folders[path + "|" + prefix] = new RuntimeAssetCatalog.Folder { path = path, keyPrefix = prefix };
        }
        catalog.entries = entries.GroupBy(e => e.key, StringComparer.Ordinal).Select(group => new RuntimeAssetCatalog.Entry
        { key = group.Key, assets = group.SelectMany(e => e.assets).Distinct().ToArray() }).ToArray();
        catalog.discoveryFolders = folders.Values.ToArray();
        catalog.Invalidate(); EditorUtility.SetDirty(catalog);
        RuntimeCatalogBuilder.RegisterPreloaded(catalog);
    }

    public static void RebuildCatalogFromPlan() => SeedCatalog(JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath)));

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

public static class RuntimeCatalogBuilder
{
    public static Object[] RuntimeObjects(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .Where(o => o != null && !(o is MonoScript) &&
            (o is UnityEngine.Audio.AudioMixer || o is UnityEngine.Audio.AudioMixerGroup || o is UnityEngine.Audio.AudioMixerSnapshot || o is RuntimeAnimatorController ||
            (!o.GetType().Assembly.GetName().Name.Contains("Editor") && !(o.GetType().Namespace ?? "").StartsWith("UnityEditor", StringComparison.Ordinal)))).ToArray();

    [MenuItem("Mismo/Proyecto/Actualizar catálogo de assets")]
    public static void Refresh()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Primero aplicar la organización de assets.");
        var entries = catalog.entries.Where(e => e != null && e.assets != null && e.assets.Any(a => a != null))
            .GroupBy(e => e.key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => new RuntimeAssetCatalog.Entry
            { key = g.Key, assets = g.SelectMany(e => e.assets).Where(a => a != null).Distinct().ToArray() }, StringComparer.Ordinal);
        foreach (var folder in catalog.discoveryFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder.path)) throw new InvalidOperationException("Falta carpeta de catálogo: " + folder.path);
            foreach (string path in AssetDatabase.FindAssets("", new[] { folder.path }).Select(AssetDatabase.GUIDToAssetPath))
            {
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != folder.path || AssetDatabase.IsValidFolder(path)) continue;
                var values = RuntimeObjects(path); if (values.Length == 0) continue;
                string key = folder.keyPrefix + "/" + Path.GetFileNameWithoutExtension(path);
                if (entries.TryGetValue(key, out var existing))
                {
                    var others = existing.assets.Where(a => a != null && AssetDatabase.GetAssetPath(a) != path).ToArray();
                    if (others.Any(a => values.Any(b => a.GetType() == b.GetType()))) throw new InvalidOperationException("Clave y tipo duplicados en catálogo: " + key);
                    values = others.Concat(values).ToArray();
                }
                entries[key] = new RuntimeAssetCatalog.Entry { key = key, assets = values };
            }
        }
        catalog.entries = entries.Values.OrderBy(e => e.key, StringComparer.Ordinal).ToArray();
        catalog.Invalidate(); EditorUtility.SetDirty(catalog); RegisterPreloaded(catalog); AssetDatabase.SaveAssets();
    }
    public static void RegisterPreloaded(RuntimeAssetCatalog catalog)
    {
        var assets = PlayerSettings.GetPreloadedAssets().Where(a => a != null && !(a is RuntimeAssetCatalog)).ToList();
        assets.Add(catalog); PlayerSettings.SetPreloadedAssets(assets.ToArray());
    }
}

sealed class RuntimeCatalogBuildStep : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        if (Directory.GetDirectories("Assets", "Resources", SearchOption.AllDirectories).Length > 0) throw new BuildFailedException("No se permiten carpetas Resources. Registrar los assets en RuntimeAssetCatalog.");
        RuntimeCatalogBuilder.Refresh();
        ProjectOrganizationChecks.Run();
    }
}
