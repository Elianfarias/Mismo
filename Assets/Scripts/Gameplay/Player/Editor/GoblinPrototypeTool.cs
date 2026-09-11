using System;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Camera;
using Mismo.Gameplay.Player.Input;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class GoblinPrototypeTool
    {
        public const string ScenePath = "Assets/Scenes/GoblinArena.unity";
        public const string PrefabPath = "Assets/Prefabs/Enemies/Goblin.prefab";
        public const string ElitePrefabPath = "Assets/Prefabs/Enemies/GoblinElite.prefab";
        public const string SettingsPath = "Assets/Data/Enemies/BaseGoblin.asset";
        public const string EliteScenePath = "Assets/Scenes/GoblinEliteArena.unity";

        [MenuItem("Mismo/Prototype/Goblin/Build Goblin Arena")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                else { Debug.LogWarning("Guardá la escena sin título antes de crear la arena."); return; }
            }
            EnsureFolder("Assets/Prefabs/Enemies"); EnsureFolder("Assets/Data/Enemies"); EnsureFolder("Assets/Art/Materials/Goblin");
            var settings = AssetDatabase.LoadAssetAtPath<GoblinSettings>(SettingsPath);
            if (settings == null)
            { settings = ScriptableObject.CreateInstance<GoblinSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePrefab(settings);
            GameObject elitePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElitePrefabPath);
            if (elitePrefab == null) elitePrefab = CreateElitePrefab(prefab);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateArena(prefab);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EliteScenePath) == null) CreateArena(elitePrefab, EliteScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("Goblin listo. Abrí Assets/Scenes/GoblinArena.unity y presioná Play. Ajustes: " + SettingsPath);
        }

        [MenuItem("Mismo/Prototype/Goblin/Add Goblin To Current Scene")]
        public static void AddToScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Build(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(instance, "Add Goblin");
            instance.transform.position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            Selection.activeGameObject = instance;
            Debug.Log("Goblin agregado. Colocalo sobre un NavMesh; la arena incluida ya genera su navegación.");
        }

        [MenuItem("Mismo/Prototype/Goblin/Remove Selected Goblin")]
        private static void RemoveSelected()
        {
            GoblinController goblin = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<GoblinController>() : null;
            if (goblin != null && !EditorUtility.IsPersistent(goblin)) Undo.DestroyObjectImmediate(goblin.gameObject);
        }

        [MenuItem("Mismo/Prototype/Goblin/Build Elite Variant")]
        public static void BuildEliteVariant()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                else { Debug.LogWarning("Guardá la escena sin título antes de crear el Elite."); return; }
            }
            EnsureFolder("Assets/Prefabs/Enemies"); EnsureFolder("Assets/Data/Enemies"); EnsureFolder("Assets/Art/Materials/Goblin");
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (basePrefab == null)
            {
                Build();
                basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }
            var elitePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElitePrefabPath);
            if (elitePrefab == null) elitePrefab = CreateElitePrefab(basePrefab);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Selection.activeObject = elitePrefab;
            EditorGUIUtility.PingObject(elitePrefab);
            Debug.Log("Goblin Elite listo: reutiliza Goblin.prefab con escala, tinte, aura y feedback provisionales.");
        }

        private static GameObject CreatePrefab(GoblinSettings settings)
        {
            var root = new GameObject("Goblin");
            try
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.enabled = false; agent.radius = 0.4f; agent.height = 1.5f; agent.acceleration = 18f; agent.angularSpeed = 360f;
                var collider = root.AddComponent<CapsuleCollider>(); collider.center = Vector3.up * 0.75f; collider.height = 1.5f; collider.radius = 0.4f;
                root.AddComponent<Health>(); root.AddComponent<DamageReceiver>();
                var body = new GameObject("Visual").transform; body.SetParent(root.transform, false);
                Material green = Material("Skin", new Color(0.31f, 0.62f, 0.18f));
                Material brown = Material("Leather", new Color(0.29f, 0.18f, 0.1f));
                Material steel = Material("Steel", new Color(0.68f, 0.74f, 0.78f));
                Material dark = Material("Dark", new Color(0.07f, 0.07f, 0.045f));
                Material eye = Material("Eyes", new Color(1f, 0.8f, 0.22f));
                Part("Torso", body, new Vector3(0, 0.73f, 0), new Vector3(0.62f, 0.6f, 0.38f), brown);
                Part("Head", body, new Vector3(0, 1.22f, 0.025f), new Vector3(0.67f, 0.51f, 0.48f), green);
                Part("Nose", body, new Vector3(0, 1.15f, 0.34f), new Vector3(0.19f, 0.18f, 0.23f), green);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Transform ear = Part("Ear", body, new Vector3(sign * 0.46f, 1.3f, 0), new Vector3(0.32f, 0.19f, 0.18f), green).transform;
                    ear.localRotation = Quaternion.Euler(0, 0, sign * 20f);
                    Part("Eye", body, new Vector3(sign * 0.17f, 1.29f, 0.276f), new Vector3(0.13f, 0.085f, 0.035f), eye);
                    Part("Pupil", body, new Vector3(sign * 0.17f, 1.29f, 0.30f), new Vector3(0.04f, 0.08f, 0.02f), dark);
                    Part("Leg", body, new Vector3(sign * 0.2f, 0.24f, 0), new Vector3(0.22f, 0.48f, 0.25f), green);
                    Part("Boot", body, new Vector3(sign * 0.2f, 0.08f, 0.07f), new Vector3(0.26f, 0.16f, 0.38f), brown);
                    Part("Arm", body, new Vector3(sign * 0.42f, 0.78f, 0), new Vector3(0.2f, 0.47f, 0.24f), green);
                }
                Transform sword = new GameObject("Weapon Pivot").transform; sword.SetParent(body, false); sword.localPosition = new Vector3(0.47f, 0.66f, 0.16f);
                Part("Grip", sword, Vector3.zero, new Vector3(0.1f, 0.3f, 0.1f), brown);
                Part("Guard", sword, new Vector3(0, 0.16f, 0), new Vector3(0.32f, 0.08f, 0.12f), steel);
                Part("Blade", sword, new Vector3(0, 0.53f, 0), new Vector3(0.15f, 0.65f, 0.08f), steel);
                var source = new GameObject("Attack Source"); source.transform.SetParent(root.transform, false);
                source.AddComponent<BoxCollider>().enabled = false;
                DamageDealer dealer = source.AddComponent<DamageDealer>();
                root.AddComponent<GoblinController>().Configure(settings, dealer);

                Transform board = new GameObject("Status").transform; board.SetParent(root.transform, false); board.localPosition = Vector3.up * 1.9f;
                Part("Health Background", board, Vector3.zero, new Vector3(1.05f, 0.08f, 0.02f), dark);
                Transform fill = new GameObject("Health Fill").transform; fill.SetParent(board, false); fill.localPosition = new Vector3(-0.5f, 0, -0.025f);
                Part("Bar", fill, new Vector3(0.5f, 0, 0), new Vector3(1f, 0.05f, 0.01f), green);
                var label = new GameObject("State Label").AddComponent<TextMesh>(); label.transform.SetParent(board, false);
                label.transform.localPosition = new Vector3(0, 0.19f, 0); label.text = "GOBLIN"; label.anchor = TextAnchor.MiddleCenter;
                label.fontSize = 48; label.characterSize = 0.045f; label.color = Color.white;
                root.AddComponent<GoblinPresentation>().Configure(body, sword, fill, board, label);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject CreateElitePrefab(GameObject basePrefab)
        {
            if (basePrefab == null) throw new InvalidOperationException("Falta el prefab base del Goblin.");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                instance.name = "Goblin Elite";
                GoblinVisualStyle style = instance.GetComponent<GoblinVisualStyle>();
                if (style == null) style = instance.AddComponent<GoblinVisualStyle>();
                style.Configure("ELITE", new Color(0.74f, 0.32f, 1f, 1f), 0.78f);
                foreach (TextMesh text in instance.GetComponentsInChildren<TextMesh>(true))
                    text.text = "ELITE GOBLIN";
                GoblinEliteVisual modifier = instance.GetComponent<GoblinEliteVisual>();
                if (modifier == null) modifier = instance.AddComponent<GoblinEliteVisual>();
                modifier.Configure(1.28f, new Color(0.74f, 0.32f, 1f, 1f), new Color(0.88f, 0.28f, 1f, 1f), 0.78f);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, ElitePrefabPath);
                if (saved == null) throw new InvalidOperationException("No se pudo crear el prefab Elite.");
                return saved;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static void CreateArena(GameObject goblinPrefab) => CreateArena(goblinPrefab, ScenePath);

        private static void CreateArena(GameObject goblinPrefab, string scenePath)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            if (playerPrefab == null) throw new InvalidOperationException("Falta el prefab Player del prototipo.");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Transform geometry = new GameObject("Arena Geometry").transform;
                Material ground = Material("Arena Ground", new Color(0.23f, 0.3f, 0.24f));
                Material rock = Material("Arena Stone", new Color(0.38f, 0.43f, 0.4f));
                Part("Ground", geometry, new Vector3(0, -0.5f, 0), new Vector3(36, 1, 36), ground, true);
                Part("Sight And Path Obstacle", geometry, new Vector3(-4, 1.25f, 3), new Vector3(2, 2.5f, 5), rock, true);
                Part("Rock", geometry, new Vector3(5, 0.75f, 5), new Vector3(2, 1.5f, 2), rock, true);
                new GameObject("Arena Navigation").AddComponent<GoblinNavigation>().Configure(geometry);
                GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
                if (player.GetComponent<Health>() == null) player.AddComponent<Health>();
                if (player.GetComponent<Invulnerability>() == null) player.AddComponent<Invulnerability>();
                if (player.GetComponent<DamageReceiver>() == null) player.AddComponent<DamageReceiver>();
                if (player.GetComponent<PlayerCombatLife>() == null) player.AddComponent<PlayerCombatLife>();
                player.transform.position = new Vector3(0, 0.1f, -3);
                if (player.GetComponent<SwordAnimationFeedback>() == null) player.AddComponent<SwordAnimationFeedback>();
                PlayerMotor motor = player.GetComponent<PlayerMotor>();
                if (player.transform.GetComponentInChildren<MeshRenderer>() != null &&
                    Array.Find(player.GetComponentsInChildren<Transform>(), t => t.name == "BasicSword") == null)
                {
                    GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/BasicSword.prefab");
                    if (swordPrefab != null)
                    {
                        var sword = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab, scene);
                        sword.transform.SetParent(motor.Visual, false);
                        sword.transform.localPosition = new Vector3(0.55f, 1.05f, 0.35f);
                        sword.transform.localRotation = Quaternion.Euler(18, 0, -22);
                    }
                }
                var camera = new GameObject("Main Camera", typeof(UnityEngine.Camera), typeof(AudioListener)).GetComponent<UnityEngine.Camera>();
                camera.tag = "MainCamera"; camera.fieldOfView = 60; camera.nearClipPlane = 0.1f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.36f, 0.49f, 0.59f);
                camera.transform.SetPositionAndRotation(new Vector3(0, 5, -12), Quaternion.Euler(25, 0, 0));
                camera.gameObject.AddComponent<ThirdPersonCamera>().Configure(player.transform, player.GetComponent<PlayerInputReader>());
                player.GetComponent<PlayerController>().Configure(camera.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.GetComponent<PlayerController>());
                var goblin = (GameObject)PrefabUtility.InstantiatePrefab(goblinPrefab, scene);
                goblin.transform.SetPositionAndRotation(new Vector3(0, 0, 4), Quaternion.Euler(0, 180, 0));
                var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = 1.1f; light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(48, -30, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.7f);
                new GameObject("Goblin Arena HUD").AddComponent<GoblinArenaHUD>();
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static GameObject Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = false)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }
        private static Material Material(string name, Color color, string shaderName = "Standard")
        {
            string path = "Assets/Art/Materials/Goblin/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(shaderName)) { color = color };
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        /// <summary>Render de revisión en una instancia batch con gráficos habilitados.</summary>
        public static void RenderPreviewBatch()
        {
            if (!Application.isBatchMode) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var camera = UnityEngine.Camera.main;
            camera.transform.position = new Vector3(2.6f, 2.4f, 0.2f);
            camera.transform.LookAt(new Vector3(0, 0.95f, 4));
            foreach (GoblinPresentation goblin in Object.FindObjectsByType<GoblinPresentation>())
            {
                Transform status = goblin.transform.Find("Status");
                if (status != null) status.rotation = camera.transform.rotation;
            }
            var target = new RenderTexture(960, 720, 24);
            var image = new Texture2D(960, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); image.Apply();
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllBytes("Logs/Goblin-preview.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        /// <summary>Render de revisión del prefab Elite en su escena dedicada.</summary>
        public static void RenderElitePreviewBatch()
        {
            if (!Application.isBatchMode) return;
            EditorSceneManager.OpenScene(EliteScenePath, OpenSceneMode.Single);
            SessionState.SetBool("Mismo.GoblinElitePreview.Pending", true);
            EditorApplication.update += RenderElitePreviewAfterPlay;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void ResumeElitePreview()
        {
            if (Application.isBatchMode && SessionState.GetBool("Mismo.GoblinElitePreview.Pending", false))
                EditorApplication.update += RenderElitePreviewAfterPlay;
        }

        private static void RenderElitePreviewAfterPlay()
        {
            if (!EditorApplication.isPlaying || !Application.isPlaying || Time.frameCount < 4) return;
            EditorApplication.update -= RenderElitePreviewAfterPlay;
            SessionState.SetBool("Mismo.GoblinElitePreview.Pending", false);
            GoblinEliteVisual eliteVisual = Object.FindAnyObjectByType<GoblinEliteVisual>();
            if (eliteVisual != null)
            {
                GoblinController eliteController = eliteVisual.GetComponent<GoblinController>();
                if (eliteController != null) { eliteController.SetTarget(null); eliteController.enabled = false; }
            }
            var camera = UnityEngine.Camera.main;
            Vector3 focus = eliteVisual != null ? eliteVisual.transform.position + Vector3.up * 0.9f : new Vector3(0, 0.95f, 4);
            camera.transform.position = focus + new Vector3(2.6f, 1.8f, -3.8f);
            camera.transform.LookAt(focus);
            if (eliteVisual != null)
            {
                Transform status = eliteVisual.transform.Find("Status");
                if (status != null) status.rotation = camera.transform.rotation;
            }
            var target = new RenderTexture(960, 720, 24);
            var image = new Texture2D(960, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); image.Apply();
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllBytes("Logs/Goblin-elite-preview.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
            EditorApplication.Exit(0);
        }
    }
}
