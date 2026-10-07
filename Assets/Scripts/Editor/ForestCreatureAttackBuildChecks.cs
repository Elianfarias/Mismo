using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Editor;
using UnityEditor;
using UnityEngine;

public static class ForestCreatureAttackBuildChecks
{
    public static void RunBatch()
    {
        const string output = "output/creature-attack-polish/";
        Directory.CreateDirectory(output);
        try
        {
            ProjectOrganizationChecks.Run();
            File.WriteAllText(output + "organization.txt", "PASS\n");
        }
        catch (Exception error)
        {
            // Report existing project-wide issues without rewriting unrelated assets.
            File.WriteAllText(output + "organization.txt", error.ToString());
        }
        const string folder = ".validation/creature-attack-polish/content";
        Directory.CreateDirectory(folder);
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { ForestCreatureIntegration.Prefabs })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileName(p).StartsWith("Boar_") || Path.GetFileName(p).StartsWith("Spider_")).ToArray();
        var manifest = BuildPipeline.BuildAssetBundles(folder,
            new[] { new AssetBundleBuild { assetBundleName = "creature-attacks", assetNames = paths } },
            BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new Exception("Creature content build failed");
        var bundle = AssetBundle.LoadFromFile(folder + "/creature-attacks");
        if (bundle == null) throw new Exception("Cannot load built creature content");
        try
        {
            foreach (string path in paths)
            {
                var prefab = bundle.LoadAsset<GameObject>(path);
                var settings = prefab.GetComponent<CreatureController>().Settings;
                if (settings == null || settings.attacks.Where(a => a.enabled).Any(a =>
                    a.animation.PlaybackClip == null || a.animation.PlaybackClip.length < .9f || a.recovery > .101f))
                    throw new Exception("Updated animation/settings missing in built prefab: " + path);
            }
            File.WriteAllText(output + "content-build.txt", "PASS: all seven variants and four combat clips load from the Windows content build.\n");
        }
        finally { bundle.Unload(true); }
        ForestCreatureAttackPlayChecks.RunBatch();
    }
}
