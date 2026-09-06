using System;
using System.Reflection;
using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Pruebas reproducibles de física y recursos sobre una escena temporal sin guardar cambios.</summary>
    public static class MovementPrototypeChecks
    {
        /// <summary>Comprueba salto, suelo, colisiones, stamina y distancia de dash a distintas tasas.</summary>
        [MenuItem("Mismo/Prototype/Validate Movement Prototype")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetSceneByPath(MovementPrototypeBuilder.ScenePath).isLoaded)
            {
                Debug.LogWarning("Cerrá MovementPrototype antes de validar su copia temporal.");
                return;
            }
            if (Application.isBatchMode)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(MovementPrototypeBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameObject player = Array.Find(scene.GetRootGameObjects(), root => root.GetComponent<PlayerMotor>() != null);
            try
            {
                var movementSettings = AssetDatabase.LoadAssetAtPath<MovementSettings>("Assets/Data/Player/DefaultMovementSettings.asset");
                var dashSettings = AssetDatabase.LoadAssetAtPath<DashSettings>("Assets/Data/Player/BasicDashSettings.asset");
                var staminaSettings = AssetDatabase.LoadAssetAtPath<StaminaSettings>("Assets/Data/Player/DefaultStaminaSettings.asset");
                Require(player != null, "Player encontrado");
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "Sin scripts perdidos");
                var motor = player.GetComponent<PlayerMotor>();
                var resource = player.GetComponent<Stamina>();
                var dash = player.GetComponent<BeltDash>();
                InitializeForEditMode(motor);
                InitializeForEditMode(resource);
                foreach (int fps in new[] { 30, 60, 144 })
                {
                    float dt = 1f / fps;
                    motor.ResetPosition(new Vector3(0, 0.1f, 0));
                    Physics.SyncTransforms();
                    for (int i = 0; i < fps; i++) motor.Tick(Vector3.zero, false, false, true, false, Vector3.zero, dt);
                    Require(motor.IsGrounded, $"Suelo estable a {fps} fps");
                    float groundY = player.transform.position.y;
                    float highest = groundY;
                    for (int i = 0; i < fps * 2; i++)
                    {
                        motor.Tick(Vector3.zero, false, i == 0, true, false, Vector3.zero, dt);
                        highest = Mathf.Max(highest, player.transform.position.y);
                    }
                    Require(Mathf.Abs(highest - groundY - movementSettings.JumpHeight) < 0.35f, $"Altura de salto a {fps} fps");
                    Require(motor.IsGrounded, $"Aterrizaje a {fps} fps");
                    float doubleJumpHeight = SimulateJumpSequence(motor, player, fps, 0, fps / 4);
                    float thirdPressHeight = SimulateJumpSequence(motor, player, fps, 0, fps / 4, fps / 2);
                    Require(doubleJumpHeight > movementSettings.JumpHeight * 1.5f, $"Segundo salto a {fps} fps");
                    Require(Mathf.Abs(thirdPressHeight - doubleJumpHeight) < 0.1f, $"Tercer salto rechazado a {fps} fps");
                    dash.TickCooldown(100f);
                    Require(dash.TryStart(Vector3.forward, true), "Dash disponible");
                    float distance = 0f;
                    while (dash.IsActive) distance += dash.Step(dt).magnitude;
                    Require(Mathf.Abs(distance - dashSettings.Distance) < 0.01f, $"Distancia de dash a {fps} fps");
                    Require(!dash.TryStart(Vector3.forward, true), "Cooldown rechaza segundo dash");
                    Debug.Log($"PASS {fps} fps: suelo, salto, aterrizaje y dash ({distance:0.000} m).");
                }
                resource.Tick(true, staminaSettings.Maximum / Mathf.Max(0.01f, staminaSettings.SprintCostPerSecond) + 1f);
                Require(resource.Current >= 0f, "Stamina nunca negativa");
                dash.TickCooldown(100f);
                Require(dash.TryStart(Vector3.forward, true), "Dash independiente de stamina");
                dash.Cancel();
                for (int i = 0; i < 600; i++) resource.Tick(false, 1f / 60f);
                Require(Mathf.Abs(resource.Normalized - 1f) < 0.001f, "Regeneración completa");
                motor.ResetPosition(new Vector3(4, 0.1f, -6));
                Physics.SyncTransforms();
                for (int i = 0; i < 60; i++) motor.Tick(Vector3.zero, false, false, false, false, Vector3.zero, 1f / 60f);
                motor.Tick(Vector3.zero, false, false, false, true, Vector3.right * 10f, 1f / 60f);
                Require(player.transform.position.x < 6.6f, "Dash no atraviesa pared");
                Debug.Log("MOVEMENT CHECKS PASSED: recursos, cooldown, colisión y física a 30/60/144 fps.");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>Interrumpe la validación cuando una condición esperada no se cumple.</summary>
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Movement check failed: " + message);
        }

        /// <summary>Simula una secuencia de pulsaciones y devuelve la altura máxima relativa al suelo.</summary>
        private static float SimulateJumpSequence(PlayerMotor motor, GameObject player, int fps, params int[] pressFrames)
        {
            float dt = 1f / fps;
            motor.ResetPosition(new Vector3(0, 0.1f, 0));
            Physics.SyncTransforms();
            for (int i = 0; i < fps; i++) motor.Tick(Vector3.zero, false, false, true, false, Vector3.zero, dt);
            float groundY = player.transform.position.y;
            float highest = groundY;
            for (int i = 0; i < fps * 3; i++)
            {
                motor.Tick(Vector3.zero, false, Array.IndexOf(pressFrames, i) >= 0, true, false, Vector3.zero, dt);
                highest = Mathf.Max(highest, player.transform.position.y);
            }
            Require(motor.IsGrounded, $"Aterrizaje tras secuencia de saltos a {fps} fps");
            return highest - groundY;
        }

        /// <summary>Inicializa solamente el componente probado sin despachar mensajes Unity en Edit Mode.</summary>
        private static void InitializeForEditMode(Component component)
        {
            component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
        }
    }
}
