using System;
using System.IO;
using System.Linq;
using Mismo.Core;
using UnityEditor;
using UnityEngine;

public static class ProjectOrganizationChecks
{
    [MenuItem("Mismo/Proyecto/Verificar organización y referencias")]
    public static void Run()
    {
        var errors = new System.Collections.Generic.List<string>();
        foreach (string folder in Directory.GetDirectories("Assets", "*", SearchOption.AllDirectories))
            if (string.Equals(Path.GetFileName(folder), "Resources", StringComparison.OrdinalIgnoreCase)) errors.Add("Carpeta Resources prohibida: " + folder);
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(p)))
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            string expected = null;
            switch (extension)
            {
                case ".prefab": expected = "Assets/Art/Prefabs/"; break;
                case ".mat": expected = "Assets/Art/Materials/"; break;
                case ".anim": case ".controller": case ".overridecontroller": case ".mask": expected = "Assets/Art/Animations/"; break;
                case ".wav": case ".mp3": case ".ogg": case ".mixer": expected = "Assets/Art/Audio/"; break;
                case ".ttf": case ".otf": expected = "Assets/Art/Fonts/"; break;
                case ".shader": case ".shadergraph": case ".compute": expected = "Assets/Art/Shaders/"; break;
                case ".cs": case ".asmdef": expected = "Assets/Scripts/"; break;
                case ".unity": expected = "Assets/Scenes/"; break;
                case ".fbx": expected = path.StartsWith("Assets/Art/Animations/") ? "Assets/Art/Animations/" : "Assets/Art/FBX/"; break;
                case ".obj": case ".mtl": case ".glb": case ".dae": expected = "Assets/Art/Models/"; break;
                case ".png": case ".jpg": case ".jpeg": case ".exr": case ".psd": case ".tga":
                    if (!(path.StartsWith("Assets/Art/Textures/") || path.StartsWith("Assets/Art/UI/") || path.StartsWith("Assets/Art/Sprites/"))) errors.Add("Imagen fuera de Art/Textures, UI o Sprites: " + path);
                    break;
                case ".asset":
                    Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                    if (type != null && typeof(ScriptableObject).IsAssignableFrom(type)) expected = "Assets/Data/";
                    else if (type != null && typeof(Mesh).IsAssignableFrom(type)) expected = "Assets/Art/Meshes/";
                    else if (type != null && typeof(Texture).IsAssignableFrom(type)) expected = "Assets/Art/Textures/";
                    break;
            }
            if (expected != null && !path.StartsWith(expected, StringComparison.Ordinal)) errors.Add(path + " debe estar en " + expected);
        }
        var catalog = AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        if (catalog == null) errors.Add("Falta RuntimeAssetCatalog.");
        else
        {
            if (!PlayerSettings.GetPreloadedAssets().Contains(catalog)) errors.Add("El catálogo no está registrado en Preloaded Assets.");
            foreach (var entry in catalog.entries)
                if (entry.assets == null || entry.assets.Length == 0 || entry.assets.Any(a => a == null)) errors.Add("Referencia rota en catálogo: " + entry.key);
            if (catalog.entries.Select(e => e.key).Distinct(StringComparer.Ordinal).Count() != catalog.entries.Length) errors.Add("Claves duplicadas en catálogo.");
        }
        foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path);
            // Build the forbidden strings here so the checker does not flag its own source.
            if (source.Contains("Resources" + ".Load") || source.Contains("Assets/" + "Resources")) errors.Add("Carga/ruta antigua en " + path);
        }
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        Debug.Log("PROJECT_LAYOUT_CHECKS_OK: categorías, catálogo y eliminación de cargas Resources verificados.");
    }
}
