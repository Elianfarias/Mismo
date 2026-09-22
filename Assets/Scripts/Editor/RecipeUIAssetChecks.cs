using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Checks only the prepared recipe artwork; does not register or activate it.</summary>
public static class RecipeUIAssetChecks
{
    [Serializable] sealed class Manifest { public Entry[] entries; public string iconSources; }
    [Serializable] sealed class Entry { public string key; public string path; public int[] border; }
    [Serializable] sealed class IconSources { public IconEntry[] icons; }
    [Serializable] sealed class IconEntry { public string key; public string path; public string inventoryField; public int codepoint; }

    [MenuItem("Mismo/UI/Verificar assets de recetas")]
    public static void Run()
    {
        const string manifestPath = "Assets/Art/UI/Recipes/RecipeUIManifest.json";
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
        if (manifest?.entries == null || manifest.entries.Length == 0)
            throw new InvalidOperationException("Empty recipe UI manifest.");
        if (manifest.entries.Select(e => e.key).Distinct().Count() != manifest.entries.Length)
            throw new InvalidOperationException("Duplicate recipe UI key.");
        foreach (var entry in manifest.entries)
        {
            if (!entry.path.StartsWith("Assets/Art/UI/Recipes/", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected recipe sprite path: " + entry.path);
            AssetDatabase.ImportAsset(entry.path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(entry.path) as TextureImporter;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.path);
            if (importer == null || sprite == null)
                throw new InvalidOperationException("Missing recipe sprite: " + entry.key);
            if (entry.border == null || entry.border.Length != 4)
                throw new InvalidOperationException("Invalid border in manifest: " + entry.key);
            var border = new Vector4(entry.border[0], entry.border[1], entry.border[2], entry.border[3]);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.alphaIsTransparency ||
                sprite.border != border || sprite.rect.width <= border.x + border.z || sprite.rect.height <= border.y + border.w)
                throw new InvalidOperationException("Unexpected recipe sprite settings: " + entry.key);
            if (!importer.DoesSourceTextureHaveAlpha())
                throw new InvalidOperationException("Recipe sprite requires alpha: " + entry.key);
        }
        if (Directory.GetFiles("Assets/Art/UI/Recipes", "Icon*.png").Length != 0)
            throw new InvalidOperationException("Recipe UI must not contain generated replacement icons.");
        var sources = JsonUtility.FromJson<IconSources>(File.ReadAllText(manifest.iconSources));
        if (sources?.icons == null || sources.icons.Length != 15 || sources.icons.Select(i => i.key).Distinct().Count() != 15)
            throw new InvalidOperationException("Missing or duplicated recipe icon mapping.");
        var inventory = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Data/UI/InventoryUIIcons.asset"));
        foreach (var icon in sources.icons)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(icon.path);
            if (texture == null) throw new InvalidOperationException("Missing recipe icon: " + icon.key);
            if (!string.IsNullOrEmpty(icon.inventoryField) && inventory.FindProperty(icon.inventoryField)?.objectReferenceValue != texture)
                throw new InvalidOperationException("Recipe icon differs from the existing inventory reference: " + icon.key);
            if (icon.codepoint > 0)
            {
                var importer = AssetImporter.GetAtPath(icon.path) as TextureImporter;
                if (!icon.path.StartsWith("Assets/Art/UI/Flaticon/Recipes/", StringComparison.Ordinal) ||
                    importer == null || importer.filterMode != FilterMode.Bilinear || importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.DoesSourceTextureHaveAlpha() ||
                    texture.width != 512 || texture.height != 512)
                    throw new InvalidOperationException("Unexpected Flaticon import: " + icon.key);
            }
        }
        ProjectOrganizationChecks.Run();
        Debug.Log("RECIPE_UI_ASSETS_OK: " + manifest.entries.Length + " UI sprites; 15 icon references, including existing inventory icons and official Flaticon imports.");
    }
}
