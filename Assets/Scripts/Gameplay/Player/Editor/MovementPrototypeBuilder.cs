using System.Linq;
using Mismo.Gameplay.Player.Camera;
using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Input;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Construye una escena independiente y un prefab reutilizable para esta milestone.</summary>
    public static class MovementPrototypeBuilder
    {
        public const string ScenePath = "Assets/Scenes/MovementPrototype.unity";
        private const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";

        /// <summary>Genera los assets faltantes sin sobrescribir una escena o un prefab existentes.</summary>
        [MenuItem("Mismo/Prototype/Build Complete Movement Prototype")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                Debug.LogWarning("Guardá la escena sin título antes de crear el prototipo aditivamente.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                Debug.Log("MovementPrototype ya existe. Abrí la escena seleccionada; se conservaron tus cambios.");
                return;
            }
            PlayerSettingsBuilder.Build();
            EnsureFolder("Assets/Prefabs/Player");
            EnsureFolder("Assets/Art/Materials");
            var movement = AssetDatabase.LoadAssetAtPath<MovementSettings>("Assets/Data/Player/DefaultMovementSettings.asset");
            var stamina = AssetDatabase.LoadAssetAtPath<StaminaSettings>("Assets/Data/Player/DefaultStaminaSettings.asset");
            var dashSettings = AssetDatabase.LoadAssetAtPath<DashSettings>("Assets/Data/Player/BasicDashSettings.asset");
            const string weaponPath = "Assets/Data/Player/BasicSword.asset";
            var weaponDefinition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPath);
            if (weaponDefinition == null)
            {
                weaponDefinition = ScriptableObject.CreateInstance<WeaponDefinition>();
                weaponDefinition.Configure("sword.basic", "Espada básica", 0.45f);
                AssetDatabase.CreateAsset(weaponDefinition, weaponPath);
            }
            const string beltPath = "Assets/Data/Player/BasicBeltDash.asset";
            var belt = AssetDatabase.LoadAssetAtPath<BasicDashBehaviour>(beltPath);
            if (belt == null)
            {
                belt = ScriptableObject.CreateInstance<BasicDashBehaviour>();
                belt.Configure(dashSettings);
                AssetDatabase.CreateAsset(belt, beltPath);
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Material ground = MaterialAsset("PrototypeGround", new Color(0.24f, 0.34f, 0.3f));
                Material stone = MaterialAsset("PrototypeStone", new Color(0.48f, 0.56f, 0.65f));
                Material orange = MaterialAsset("PrototypePlayer", new Color(1f, 0.45f, 0.12f));
                Material dark = MaterialAsset("PrototypeFace", new Color(0.08f, 0.1f, 0.15f));
                GameObject playground = new GameObject("Movement Playground");
                Block("Ground", playground.transform, new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40), ground);
                GameObject ramp = Block("Ramp 15 degrees", playground.transform, new Vector3(-7, 0.65f, 4), new Vector3(6, 0.5f, 4), stone);
                ramp.transform.rotation = Quaternion.Euler(0, 0, 15);
                for (int i = 0; i < 6; i++)
                {
                    float h = 0.25f * (i + 1);
                    Block($"Step {i + 1}", playground.transform, new Vector3(-4 + i, h / 2, -6), new Vector3(1, h, 3), stone);
                }
                Block("Jump Platform", playground.transform, new Vector3(5, 0.5f, 4), new Vector3(3, 1, 3), stone);
                Block("High Platform", playground.transform, new Vector3(10, 1.2f, 4), new Vector3(3, 2.4f, 3), stone);
                Block("Dash Wall", playground.transform, new Vector3(7, 2, -6), new Vector3(1, 4, 7), stone);
                Block("Low Ceiling", playground.transform, new Vector3(-7, 2.6f, -7), new Vector3(4, 0.5f, 4), stone);
                for (int i = 0; i < 5; i++)
                    Block($"Slalom {i + 1}", playground.transform, new Vector3(-8 + i * 4, 1, 12), new Vector3(1, 2, 1), stone);
                Light light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
                RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.65f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                GameObject player;
                if (prefab != null) player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                else
                {
                    player = new GameObject("Player");
                    player.SetActive(false);
                    player.layer = 2;
                    CharacterController body = player.AddComponent<CharacterController>();
                    body.height = 2f;
                    body.radius = 0.4f;
                    body.center = Vector3.up;
                    body.stepOffset = 0.3f;
                    body.slopeLimit = 45f;
                    body.skinWidth = 0.04f;
                    body.minMoveDistance = 0f;
                    PlayerInput input = player.AddComponent<PlayerInput>();
                    input.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                    input.defaultActionMap = "Player";
                    input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
                    player.AddComponent<PlayerInputReader>();
                    Transform visual = new GameObject("Visual").transform;
                    visual.SetParent(player.transform, false);
                    GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    capsule.name = "Body";
                    capsule.transform.SetParent(visual, false);
                    capsule.transform.localPosition = Vector3.up;
                    capsule.transform.localScale = new Vector3(0.8f, 1, 0.8f);
                    capsule.GetComponent<Renderer>().sharedMaterial = orange;
                    Object.DestroyImmediate(capsule.GetComponent<Collider>());
                    GameObject face = Block("Facing Marker", visual, new Vector3(0, 1.6f, 0.38f), new Vector3(0.35f, 0.22f, 0.2f), dark);
                    Object.DestroyImmediate(face.GetComponent<Collider>());
                    foreach (Transform child in player.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
                    PlayerMotor motor = player.AddComponent<PlayerMotor>();
                    motor.Configure(movement, visual);
                    Stamina resource = player.AddComponent<Stamina>();
                    resource.Configure(stamina);
                    BeltDash dash = player.AddComponent<BeltDash>();
                    dash.Configure(belt);
                    EquippedWeapon weapon = player.AddComponent<EquippedWeapon>();
                    weapon.Configure(weaponDefinition);
                    EquipmentLoadout loadout = player.AddComponent<EquipmentLoadout>();
                    loadout.Configure(weapon, dash);
                    player.AddComponent<PlayerController>();
                    player.SetActive(true);
                    PrefabUtility.SaveAsPrefabAssetAndConnect(player, PrefabPath, InteractionMode.AutomatedAction);
                }
                player.transform.position = new Vector3(0, 0.1f, 0);
                UnityEngine.Camera camera = new GameObject("Main Camera", typeof(UnityEngine.Camera), typeof(AudioListener)).GetComponent<UnityEngine.Camera>();
                camera.tag = "MainCamera";
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 200f;
                camera.fieldOfView = 60f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.45f, 0.65f, 0.8f);
                camera.transform.SetPositionAndRotation(new Vector3(0, 5, -9), Quaternion.Euler(25, 0, 0));
                camera.gameObject.AddComponent<ThirdPersonCamera>().Configure(player.transform, player.GetComponent<PlayerInputReader>());
                player.GetComponent<PlayerController>().Configure(camera.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.GetComponent<PlayerController>());
                new GameObject("Prototype HUD").AddComponent<MovementDebugView>().Configure(player.GetComponent<PlayerMotor>(), player.GetComponent<Stamina>(), player.GetComponent<BeltDash>());
                EditorSceneManager.SaveScene(scene, ScenePath);
                var scenes = EditorBuildSettings.scenes.ToList();
                if (!scenes.Any(entry => entry.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("MovementPrototype listo: Assets/Scenes/MovementPrototype.unity. Abrí la escena y presioná Play.");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>Crea un bloque estático del playground.</summary>
        private static GameObject Block(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        /// <summary>Reutiliza materiales existentes sin reemplazar ajustes del usuario.</summary>
        private static Material MaterialAsset(string name, Color color)
        {
            string path = $"Assets/Art/Materials/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Crea recursivamente una carpeta de assets faltante.</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
