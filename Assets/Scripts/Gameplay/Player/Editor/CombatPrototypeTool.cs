using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>
    /// Genera en una sola acción el feedback procedural de espada, el prefab de dummy y su
    /// instancia en MovementPrototype. Es idempotente: no duplica componentes ni objetivos.
    /// </summary>
    public static class CombatPrototypeTool
    {
        private const string ScenePath = "Assets/Scenes/MovementPrototype.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string SwordPrefabPath = "Assets/Prefabs/Player/BasicSword.prefab";
        private const string DummyPrefabPath = "Assets/Prefabs/Combat/TrainingDummy.prefab";

        [MenuItem("Mismo/Prototype/Build Sword Combat Training Setup")]
        public static void BuildSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Salí de Play Mode antes de generar el setup de combate.");
                return;
            }

            EnsureFolder("Assets/Prefabs/Combat");
            EnsurePlayerFeedback();
            GameObject dummyPrefab = EnsureDummyPrefab();
            EnsureDummyInPrototype(dummyPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = dummyPrefab;
            EditorGUIUtility.PingObject(dummyPrefab);
            Debug.Log("Setup de combate listo: feedback de espada, prefab TrainingDummy e instancia en MovementPrototype.");
        }

        [MenuItem("Mismo/Prototype/Build Sword Combat Training Setup", true)]
        private static bool ValidateBuildSetup() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void EnsurePlayerFeedback()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"No se encontró {PlayerPrefabPath}; se generará el dummy igualmente.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (contents.GetComponent<SwordAnimationFeedback>() == null)
                {
                    SwordAnimationFeedback feedback = contents.AddComponent<SwordAnimationFeedback>();
                    feedback.name = "Sword Animation Feedback";
                    EditorUtility.SetDirty(contents);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static GameObject EnsureDummyPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            if (existing != null) return existing;

            GameObject dummy = new GameObject("Training Dummy");
            dummy.layer = 0;
            BoxCollider collider = dummy.AddComponent<BoxCollider>();
            collider.center = Vector3.up;
            collider.size = new Vector3(0.9f, 2f, 0.8f);
            Health health = dummy.AddComponent<Health>();
            health.ConfigureMaximum(120f);
            dummy.AddComponent<DamageReceiver>();
            dummy.AddComponent<TrainingDummy>();

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Dummy Body";
            body.transform.SetParent(dummy.transform, false);
            body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Dummy Head";
            head.transform.SetParent(dummy.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            head.transform.localScale = Vector3.one * 0.48f;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(dummy, DummyPrefabPath);
            Object.DestroyImmediate(dummy);
            return saved;
        }

        private static void EnsureDummyInPrototype(GameObject dummyPrefab)
        {
            Scene prototype = SceneManager.GetSceneByPath(ScenePath);
            bool openedAdditively = false;
            Scene previous = SceneManager.GetActiveScene();
            if (!prototype.IsValid() || !prototype.isLoaded)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    Debug.LogWarning($"No se encontró {ScenePath}; abrí y generá MovementPrototype primero.");
                    return;
                }
                prototype = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                openedAdditively = true;
            }

            try
            {
                SceneManager.SetActiveScene(prototype);
                GameObject player = prototype.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(t => t.gameObject)
                    .FirstOrDefault(go => go.GetComponent<PlayerController>() != null);
                if (player != null)
                {
                    EnsureSwordVisual(player, prototype);
                    if (player.GetComponent<SwordAnimationFeedback>() == null)
                    {
                        player.AddComponent<SwordAnimationFeedback>();
                        EditorSceneManager.MarkSceneDirty(prototype);
                    }
                }

                GameObject existing = prototype.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.name == "Training Dummy")
                    .Select(t => t.gameObject)
                    .FirstOrDefault();
                if (existing == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab, prototype);
                    instance.name = "Training Dummy";
                    instance.transform.position = new Vector3(0f, 0f, 1.85f);
                    instance.transform.rotation = Quaternion.identity;
                    EditorSceneManager.MarkSceneDirty(prototype);
                    EditorSceneManager.SaveScene(prototype);
                }
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                if (openedAdditively) EditorSceneManager.CloseScene(prototype, true);
            }
        }

        private static void EnsureSwordVisual(GameObject player, Scene scene)
        {
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            Transform anchor = motor != null && motor.Visual != null ? motor.Visual : player.transform;
            Transform existing = player.transform.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "BasicSword");
            if (existing != null)
            {
                if (existing.parent != anchor)
                {
                    existing.SetParent(anchor, false);
                    existing.localPosition = new Vector3(0.55f, 1.05f, 0.35f);
                    existing.localRotation = Quaternion.Euler(18f, 0f, -22f);
                    existing.localScale = Vector3.one;
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                return;
            }
            GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            if (swordPrefab == null)
            {
                Debug.LogWarning($"No se encontró {SwordPrefabPath}; la animación esperará al arma visual.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab, scene);
            instance.name = "BasicSword";
            instance.transform.SetParent(anchor, false);
            instance.transform.localPosition = new Vector3(0.55f, 1.05f, 0.35f);
            instance.transform.localRotation = Quaternion.Euler(18f, 0f, -22f);
            instance.transform.localScale = Vector3.one;
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
