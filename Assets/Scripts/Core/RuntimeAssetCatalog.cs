using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Core
{
    /// <summary>Explicit build dependencies, independent of asset locations and Resources folders.</summary>
    public sealed class RuntimeAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string key; public Object[] assets; }
        [Serializable] public sealed class Folder { public string path, keyPrefix; }
        public Entry[] entries = Array.Empty<Entry>();
        [Tooltip("Editor-only discovery locations. Runtime loading uses the references above.")]
        public Folder[] discoveryFolders = Array.Empty<Folder>();
        internal static RuntimeAssetCatalog Current;
        Dictionary<string, Object[]> lookup;

        void OnEnable() { Current = this; lookup = null; }
        void OnDisable() { if (Current == this) Current = null; }
        void OnValidate() { lookup = null; }
        public void Invalidate() { lookup = null; }

        internal T Find<T>(string key) where T : Object
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, Object[]>(StringComparer.Ordinal);
                foreach (var entry in entries) if (entry != null && !string.IsNullOrEmpty(entry.key)) lookup[entry.key] = entry.assets;
            }
            if (lookup.TryGetValue(key, out var assets) && assets != null)
                foreach (var asset in assets) if (asset is T match) return match;
            return null;
        }

        internal T[] FindAll<T>(string key) where T : Object
        {
            key = (key ?? "").TrimEnd('/');
            var result = new List<T>(); var seen = new HashSet<T>();
            foreach (var entry in entries)
            {
                if (entry == null || entry.assets == null || !(key.Length == 0 || entry.key == key || entry.key.StartsWith(key + "/", StringComparison.Ordinal))) continue;
                foreach (var asset in entry.assets) if (asset is T match && seen.Add(match)) result.Add(match);
            }
            return result.ToArray();
        }
    }

    public static class ProjectAssets
    {
        public const string CatalogPath = "Assets/Data/System/RuntimeAssetCatalog.asset";
        static RuntimeAssetCatalog Catalog
        {
            get
            {
#if UNITY_EDITOR
                if (RuntimeAssetCatalog.Current == null)
                    RuntimeAssetCatalog.Current = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(CatalogPath);
#endif
                if (RuntimeAssetCatalog.Current == null)
                    throw new InvalidOperationException("Falta RuntimeAssetCatalog en Preloaded Assets. Ejecutá Mismo/Proyecto/Actualizar catálogo de assets.");
                return RuntimeAssetCatalog.Current;
            }
        }
        public static T Load<T>(string key) where T : Object => Catalog.Find<T>(key);
        public static T[] LoadAll<T>(string key) where T : Object => Catalog.FindAll<T>(key);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Initialize() { Catalog.Invalidate(); }
    }
}
