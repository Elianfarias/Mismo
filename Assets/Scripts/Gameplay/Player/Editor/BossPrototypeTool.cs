using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Genera el asset, prefab y arena del primer boss a partir del goblin existente.</summary>
    public static class BossPrototypeTool
    {
        public const string ScenePath = "Assets/Scenes/BossArena.unity";
        public const string PrefabPath = "Assets/Art/Prefabs/Enemies/FirstBoss.prefab";
        public const string SettingsPath = "Assets/Data/Enemies/FirstBoss.asset";
        private const string BasePrefabPath = "Assets/Art/Prefabs/Enemies/Goblin.prefab";
        private const string BaseScenePath = "Assets/Scenes/GoblinArena.unity";

        [MenuItem("Mismo/Prototype/Boss/Build First Boss")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureFolder("Assets/Art/Prefabs/Enemies");
            EnsureFolder("Assets/Data/Enemies");

            BossSettings settings = AssetDatabase.LoadAssetAtPath<BossSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BossSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            ConfigureSettings(settings);
            EditorUtility.SetDirty(settings);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreateBossPrefab(settings);
            CreateBossArena(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("First Boss listo. Abrí Assets/Scenes/BossArena.unity y presioná Play.");
        }

        [MenuItem("Mismo/Prototype/Boss/Add First Boss To Current Scene")]
        public static void AddToCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Build();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(instance, "Add First Boss");
            instance.transform.position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            Selection.activeGameObject = instance;
        }

        private static void ConfigureSettings(BossSettings settings)
        {
            settings.health = 300f;
            settings.detectionRange = 12f;
            settings.loseRange = 18f;
            settings.leashRange = 22f;
            settings.memoryDuration = 3f;
            settings.speed = 3.2f;
            settings.positioningSpeed = 1.6f;
            settings.decisionPause = 0.45f;
            settings.aggressionHealthThreshold = 0.5f;
            settings.aggressiveDecisionPause = 0.25f;
            settings.sameHeightTolerance = 1.2f;
            settings.attackDecisionInterval = 0.2f;
            settings.staggerDamageThreshold = 18f;
            settings.staggerDuration = 0.45f;
            settings.staggerResistance = 2f;
            settings.parryStaggerDuration = 1.25f;
        }

        private static GameObject CreateBossPrefab(BossSettings settings)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath) == null)
                throw new System.InvalidOperationException("No existe Goblin.prefab. Generá primero el enemigo base.");

            GameObject root = PrefabUtility.LoadPrefabContents(BasePrefabPath);
            try
            {
                root.name = "FirstBoss";
                DestroyComponent<GoblinController>(root);
                DestroyComponent<GoblinPresentation>(root);
                DestroyComponent<GoblinEliteVisual>(root);
                DestroyComponent<GoblinVisualStyle>(root);

                NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                    agent.radius = 0.42f;
                    agent.height = 1.6f;
                    agent.acceleration = 24f;
                    agent.angularSpeed = 360f;
                    agent.stoppingDistance = 0.1f;
                }

                Health health = root.GetComponent<Health>();
                if (health != null)
                {
                    SerializedObject healthObject = new SerializedObject(health);
                    healthObject.FindProperty("maximum").floatValue = settings.health;
                    healthObject.FindProperty("resetOnEnable").boolValue = false;
                    healthObject.ApplyModifiedPropertiesWithoutUndo();
                }

                DamageDealer dealer = root.GetComponentInChildren<DamageDealer>();
                if (dealer == null) throw new System.InvalidOperationException("Goblin.prefab necesita un DamageDealer hijo.");
                BossController controller = root.AddComponent<BossController>();
                controller.Configure(settings, dealer);

                Transform visual = root.transform.Find("Visual");
                Transform weaponPivot = FindChild(root.transform, "Weapon Pivot");
                Transform healthFill = FindChild(root.transform, "Health Fill");
                Transform billboard = FindChild(root.transform, "Billboard");
                TextMesh label = FindChildComponent<TextMesh>(root.transform, "State Label");
                BossPresentation presentation = root.AddComponent<BossPresentation>();
                presentation.Configure(visual, weaponPivot, healthFill, billboard, label);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateBossArena(GameObject bossPrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(BaseScenePath, ScenePath))
                    throw new System.InvalidOperationException("No se pudo copiar GoblinArena.unity.");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Vector3 bossPosition = new Vector3(0f, 0f, 4f);
            Quaternion bossRotation = Quaternion.identity;
            BossController oldBoss = Object.FindAnyObjectByType<BossController>();
            if (oldBoss != null)
            {
                bossPosition = oldBoss.transform.position;
                bossRotation = oldBoss.transform.rotation;
                Object.DestroyImmediate(oldBoss.gameObject);
            }
            bool positionCaptured = oldBoss != null;
            foreach (GoblinController goblin in Object.FindObjectsByType<GoblinController>())
            {
                if (!positionCaptured)
                {
                    bossPosition = goblin.transform.position;
                    bossRotation = goblin.transform.rotation;
                    positionCaptured = true;
                }
                Object.DestroyImmediate(goblin.gameObject);
            }
            GameObject bossInstance = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, scene);
            bossInstance.transform.SetPositionAndRotation(bossPosition, bossRotation);

            foreach (BossArenaHUD oldHud in Object.FindObjectsByType<BossArenaHUD>())
                Object.DestroyImmediate(oldHud.gameObject);
            foreach (GoblinArenaHUD oldHud in Object.FindObjectsByType<GoblinArenaHUD>())
                Object.DestroyImmediate(oldHud.gameObject);

            BossController boss = Object.FindAnyObjectByType<BossController>();
            GameObject gate = GameObject.Find("Boss Passage Gate");
            if (gate == null) gate = CreateGate();
            GameObject reward = GameObject.Find("Boss Reward Indicator");
            if (reward == null) reward = CreateRewardIndicator();

            BossEncounter encounter = Object.FindAnyObjectByType<BossEncounter>();
            if (encounter == null)
            {
                GameObject encounterObject = new GameObject("Boss Encounter");
                encounter = encounterObject.AddComponent<BossEncounter>();
            }
            encounter.Configure(boss, gate, reward);

            GameObject hudObject = new GameObject("Boss Arena HUD");
            hudObject.AddComponent<BossArenaHUD>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject CreateGate()
        {
            GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "Boss Passage Gate";
            gate.transform.SetPositionAndRotation(new Vector3(0f, 1f, 16f), Quaternion.identity);
            gate.transform.localScale = new Vector3(6f, 2f, 0.5f);
            Renderer renderer = gate.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = CreateMaterial("Boss Gate", new Color(0.22f, 0.08f, 0.12f));
            return gate;
        }

        private static GameObject CreateRewardIndicator()
        {
            GameObject reward = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reward.name = "Boss Reward Indicator";
            reward.transform.SetPositionAndRotation(new Vector3(0f, 0.25f, 15.2f), Quaternion.identity);
            reward.transform.localScale = new Vector3(1.3f, 0.25f, 1.3f);
            Renderer renderer = reward.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = CreateMaterial("Boss Reward", new Color(1f, 0.72f, 0.12f));
            return reward;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        private static void DestroyComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component != null) Object.DestroyImmediate(component, true);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static T FindChildComponent<T>(Transform root, string name) where T : Component
        {
            Transform child = FindChild(root, name);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
