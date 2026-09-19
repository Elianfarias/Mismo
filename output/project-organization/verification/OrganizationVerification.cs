using System;
using System.IO;
using System.Linq;
using Mismo.Core;
using Mismo.Gameplay.Player.Editor;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Localization;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class OrganizationVerification
{
    public static void Run()
    {
        const string oldScript = "Assets/Art/Source/WeaponCombat/Human Animations/Scripts/SpineProxy.cs";
        if (File.Exists(oldScript))
        {
            ProjectAssetOrganizer.EnsureFolder("Assets/Scripts/ThirdParty/WeaponCombat");
            string error = AssetDatabase.MoveAsset(oldScript, "Assets/Scripts/ThirdParty/WeaponCombat/SpineProxy.cs");
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        if (AssetDatabase.LoadMainAssetAtPath("Assets/Art/Textures/Crimson-Bold SDF.asset") != null)
        {
            ProjectAssetOrganizer.EnsureFolder("Assets/Data/UI/Fonts");
            string error = AssetDatabase.MoveAsset("Assets/Art/Textures/Crimson-Bold SDF.asset", "Assets/Data/UI/Fonts/Crimson-Bold SDF.asset");
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);
        }
        ProjectPackagePaths.Configure();
        ProjectAssetOrganizer.RebuildCatalogFromPlan();
        RuntimeCatalogBuilder.Refresh();
        ProjectOrganizationChecks.Run();
        if (ProjectAssets.Load<Material>("TerrainSurface") == null || ProjectAssets.Load<Shader>("TerrainSurface") == null) throw new Exception("Typed assets sharing a logical key");
        if (ProjectAssets.Load<ItemCatalog>("ItemCatalog") == null || ProjectAssets.LoadAll<MaterialDefinition>("Materials").Length < 9) throw new Exception("Inventory catalog migration");
        if (!GameLanguage.Languages.Contains("es") || !GameLanguage.Languages.Contains("en")) throw new Exception("Localization catalog migration");
        foreach (string key in new[] { "Audio/GameSounds", "Audio/AudioMixer", "Audio/Music/Exploration", "Audio/Music/Combat", "InventoryUIIcons", "CombatRules", "GatheringSettings", "CombatParticles", "Fonts/Cagliostro-Regular", "Localization/Translations" })
            if (ProjectAssets.Load<UnityEngine.Object>(key) == null) throw new Exception("Missing runtime key " + key);
        DragonVoxelChecks.Run();
        VoxelizerChecks.RunAll();
        Debug.Log("ORGANIZATION_INTEGRATION_CHECKS_OK");
        PlayerSettings.companyName = "MismoValidation";
        PlayerSettings.productName = "OrganizationSmoke";
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        // Isolated catalog smoke build: avoid the project's URP keyword-filter memory explosion.
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
        int quality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = null; }
        QualitySettings.SetQualityLevel(quality, false);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/Scenes/CatalogSmoke.unity");
        string output = "C:/Users/elian/Mismo/output/project-organization/catalog-build/Mismo.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/CatalogSmoke.unity" },
            locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Standalone build failed: " + report.summary.result);
        Debug.Log("ORGANIZATION_PLAYER_BUILD_OK " + report.summary.totalSize);
    }
}
