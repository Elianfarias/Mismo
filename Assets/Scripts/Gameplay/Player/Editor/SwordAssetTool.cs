using System.Linq;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Crea la definición de espada y la equipa en el personaje seleccionado.</summary>
    public static class SwordAssetTool
    {
        private const string DataFolder = "Assets/Data";
        private const string PlayerDataFolder = "Assets/Data/Player";
        private const string SwordAssetPath = PlayerDataFolder + "/BasicSword.asset";
        private const string SwordPrefabPath = "Assets/Art/Prefabs/Player/BasicSword.prefab";

        [MenuItem("Mismo/Prototype/Create Basic Sword On Selected Character")]
        public static void CreateAndEquip()
        {
            GameObject character = ResolveCharacter(Selection.activeGameObject);
            if (character == null)
            {
                Debug.LogWarning("Seleccioná un personaje que tenga PlayerController o PlayerMotor.");
                return;
            }

            EnsureFolders();
            WeaponDefinition definition = LoadOrCreateSwordAsset();

            if (PrefabUtility.IsPartOfPrefabAsset(character))
            {
                string prefabPath = AssetDatabase.GetAssetPath(character);
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    EquipOnCharacter(prefabContents, definition, false);
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabContents);
                }

                Selection.activeObject = definition;
                EditorGUIUtility.PingObject(definition);
                Debug.Log($"Espada equipada en el prefab {prefabPath}: {AssetDatabase.GetAssetPath(definition)}");
                return;
            }

            EquipOnCharacter(character, definition, true);
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
            Debug.Log($"Espada equipada en {character.name}: {AssetDatabase.GetAssetPath(definition)}", character);
        }

        private static void EquipOnCharacter(GameObject character, WeaponDefinition definition, bool useUndo)
        {
            EquippedWeapon weapon = character.GetComponent<EquippedWeapon>();
            if (weapon == null)
            {
                weapon = useUndo ? Undo.AddComponent<EquippedWeapon>(character) : character.AddComponent<EquippedWeapon>();
                weapon.Configure(definition);
            }
            else
            {
                if (useUndo) Undo.RecordObject(weapon, "Equip Basic Sword");
                weapon.Configure(definition);
            }

            EquipmentLoadout loadout = character.GetComponent<EquipmentLoadout>();
            if (loadout == null)
                loadout = useUndo ? Undo.AddComponent<EquipmentLoadout>(character) : character.AddComponent<EquipmentLoadout>();
            if (useUndo) Undo.RecordObject(loadout, "Assign Basic Sword Loadout");
            loadout.Initialize();
            loadout.Configure(weapon, loadout.BeltComponent);

            EditorUtility.SetDirty(weapon);
            EditorUtility.SetDirty(loadout);
            if (useUndo)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(weapon);
                PrefabUtility.RecordPrefabInstancePropertyModifications(loadout);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Mismo/Prototype/Create Basic Sword On Selected Character", true)]
        private static bool ValidateCreateAndEquip()
        {
            return ResolveCharacter(Selection.activeGameObject) != null;
        }

        [MenuItem("Mismo/Prototype/Attach Basic Sword Visual To Selected Character")]
        public static void AttachVisual()
        {
            GameObject character = ResolveCharacter(Selection.activeGameObject);
            GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            if (character == null || swordPrefab == null)
            {
                Debug.LogWarning("Seleccioná un personaje válido y verificá que exista BasicSword.prefab.");
                return;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(character))
            {
                string prefabPath = AssetDatabase.GetAssetPath(character);
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    AttachVisualToCharacter(prefabContents, swordPrefab);
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabContents);
                }

                Debug.Log($"Prefab de espada adjuntado a {prefabPath}.");
                return;
            }

            GameObject instance = AttachVisualToCharacter(character, swordPrefab);
            EditorSceneManager.MarkSceneDirty(character.scene);
            Selection.activeGameObject = instance;
            Debug.Log($"Prefab de espada adjuntado a {character.name}.", instance);
        }

        [MenuItem("Mismo/Prototype/Attach Basic Sword Visual To Selected Character", true)]
        private static bool ValidateAttachVisual() => ResolveCharacter(Selection.activeGameObject) != null;

        private static GameObject AttachVisualToCharacter(GameObject character, GameObject swordPrefab)
        {
            PlayerMotor motor = character.GetComponent<PlayerMotor>();
            Transform anchor = motor != null && motor.Visual != null ? motor.Visual : character.transform;
            Transform existing = character.transform.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "BasicSword");
            if (existing != null)
            {
                if (existing.parent != anchor)
                {
                    existing.SetParent(anchor, false);
                    existing.localPosition = new Vector3(0.55f, 1.05f, 0.35f);
                    existing.localRotation = Quaternion.Euler(18f, 0f, -22f);
                    existing.localScale = Vector3.one;
                }
                return existing.gameObject;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab, character.scene);
            instance.name = "BasicSword";
            instance.transform.SetParent(anchor, false);
            instance.transform.localPosition = new Vector3(0.55f, 1.05f, 0.35f);
            instance.transform.localRotation = Quaternion.Euler(18f, 0f, -22f);
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static GameObject ResolveCharacter(GameObject selected)
        {
            if (selected == null) return null;
            PlayerController controller = selected.GetComponentInParent<PlayerController>();
            if (controller != null) return controller.gameObject;
            PlayerMotor motor = selected.GetComponentInParent<PlayerMotor>();
            return motor != null ? motor.gameObject : null;
        }

        private static WeaponDefinition LoadOrCreateSwordAsset()
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordAssetPath);
            if (definition != null) return definition;

            string path = AssetDatabase.GenerateUniqueAssetPath(SwordAssetPath);
            definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.Configure("sword.basic", "Espada básica", 0.45f);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(PlayerDataFolder)) AssetDatabase.CreateFolder(DataFolder, "Player");
        }
    }
}
