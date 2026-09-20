using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

/// <summary>Keep Addressables' profile data beside the project's other settings.</summary>
[InitializeOnLoad]
public static class ProjectPackagePaths
{
    static ProjectPackagePaths() { Configure(); }

    public static void Configure()
    {
        // Addressables 2.x exposes no public setter and otherwise recreates a root
        // folder when its profile window/build integration asks for these settings.
        // Keep this compatibility check explicit when upgrading the package.
        var field = typeof(ProfileDataSourceSettings).GetField("DEFAULT_SETTING_PATH", BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null || field.FieldType != typeof(string))
            throw new InvalidOperationException("Addressables cambió su configuración de rutas. Revisar ProjectPackagePaths antes de generar assets.");
        field.SetValue(null, "Assets/Data/Addressables/ProfileDataSourceSettings.asset");
    }
}
