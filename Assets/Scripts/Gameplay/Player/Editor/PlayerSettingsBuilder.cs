using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>
    /// Creates the default configuration assets used by the movement prototype.
    /// </summary>
    public static class PlayerSettingsBuilder
    {
        private const string DataFolder = "Assets/Data";
        private const string PlayerDataFolder = "Assets/Data/Player";
        private const string MovementAssetPath = PlayerDataFolder + "/DefaultMovementSettings.asset";
        private const string StaminaAssetPath = PlayerDataFolder + "/DefaultStaminaSettings.asset";
        private const string DashAssetPath = PlayerDataFolder + "/BasicDashSettings.asset";

        /// <summary>
        /// Creates any missing player configuration assets and selects them in the Project window.
        /// </summary>
        [MenuItem("Mismo/Prototype/Create Default Player Settings")]
        public static void Build()
        {
            EnsureDataFoldersExist();

            MovementSettings movement = CreateAssetIfMissing<MovementSettings>(MovementAssetPath);
            StaminaSettings stamina = CreateAssetIfMissing<StaminaSettings>(StaminaAssetPath);
            DashSettings dash = CreateAssetIfMissing<DashSettings>(DashAssetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.objects = new Object[] { movement, stamina, dash };
            EditorGUIUtility.PingObject(movement);
            Debug.Log("Player Settings: los assets de configuración están listos en Assets/Data/Player.");
        }

        /// <summary>
        /// Creates the data folders when Unity has not imported them yet.
        /// </summary>
        private static void EnsureDataFoldersExist()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            if (!AssetDatabase.IsValidFolder(PlayerDataFolder))
            {
                AssetDatabase.CreateFolder(DataFolder, "Player");
            }
        }

        /// <summary>
        /// Loads an existing asset or creates a new instance at the supplied path.
        /// </summary>
        private static T CreateAssetIfMissing<T>(string assetPath) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }
    }
}
