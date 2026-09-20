using System;
using System.IO;
using Mismo.Core;
using UnityEngine;
public static class CatalogPlayerSmoke
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Verify()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "CatalogSmoke") return;
        try
        {
            foreach (var key in new[] { "ItemCatalog", "Audio/GameSounds", "Audio/Music/Exploration", "InventoryUIIcons", "CombatRules", "GatheringSettings", "CombatParticles", "Fonts/Cagliostro-Regular", "Localization/Translations" })
                if (ProjectAssets.Load<UnityEngine.Object>(key) == null) throw new Exception("Missing " + key);
            if (ProjectAssets.Load<UnityEngine.Audio.AudioMixer>("Audio/AudioMixer") == null) throw new Exception("AudioMixer missing");
            if (ProjectAssets.Load<Material>("TerrainSurface") == null || ProjectAssets.Load<Shader>("TerrainSurface") == null) throw new Exception("Typed TerrainSurface missing");
            if (ProjectAssets.LoadAll<ScriptableObject>("Materials").Length < 9) throw new Exception("Material definitions missing");
            File.WriteAllText("C:/Users/elian/Mismo/output/project-organization/catalog-player-result.txt", "CATALOG_PLAYER_OK: preloaded catalog, inventory, audio mixer, audio clips, UI, localization, typed material/shader and folder lookup loaded without AssetDatabase.");
            Debug.Log("CATALOG_PLAYER_OK"); Application.Quit(0);
        }
        catch (Exception e) { File.WriteAllText("C:/Users/elian/Mismo/output/project-organization/catalog-player-result.txt", e.ToString()); Debug.LogException(e); Application.Quit(1); }
    }
}
