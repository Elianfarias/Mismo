using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Prueba batch del circuito real de input, coordinación y física en Play Mode.</summary>
    public static class MovementPlayModeCheck
    {
        private const string PendingKey = "Mismo.MovementPlayModeCheck";
        private static double start;
        private static PlayerMotor motor;
        private static Stamina stamina;
        private static BeltDash dash;
        private static Vector3 initialPosition;
        private static float initialStamina;
        private static float highest;
        private static float fastest;
        private static bool consumed;
        private static bool dashed;
        private static bool initialized;

        /// <summary>Abre el prototipo en una instancia batch y empieza la prueba sin modificar sus assets.</summary>
        public static void Run()
        {
            if (!Application.isBatchMode) return;
            EditorSceneManager.OpenScene(MovementPrototypeBuilder.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Reanuda la comprobación tras la recarga de dominio de Play Mode.</summary>
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (Application.isBatchMode && SessionState.GetBool(PendingKey, false))
                EditorApplication.update += Tick;
        }

        /// <summary>Inyecta controles de teclado y comprueba que los sistemas reaccionen durante seis segundos.</summary>
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || !Application.isPlaying || Time.frameCount < 2) return;
            if (motor == null)
            {
                motor = Object.FindFirstObjectByType<PlayerMotor>();
                if (motor == null) { Finish(false, "Falta PlayerMotor"); return; }
                stamina = motor.GetComponent<Stamina>();
                dash = motor.GetComponent<BeltDash>();
            }
            if (!initialized)
            {
                if (stamina.Current <= 0f) return;
                initialPosition = motor.transform.position;
                initialStamina = stamina.Current;
                start = EditorApplication.timeSinceStartup;
                Application.targetFrameRate = 60;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Keyboard testKeyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
                Mouse testMouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
                PlayerInput playerInput = motor.GetComponent<PlayerInput>();
                playerInput.enabled = false;
                playerInput.enabled = true;
                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", testKeyboard, testMouse);
                initialized = true;
            }
            float elapsed = (float)(EditorApplication.timeSinceStartup - start);
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) { Finish(false, "No hay teclado en Input System"); return; }
            if (elapsed > 0.2f && elapsed < 0.8f)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            else if (elapsed >= 0.8f && elapsed < 1f)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.LeftShift));
            else if (elapsed >= 1.2f && elapsed < 1.5f)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            else if (elapsed >= 1.75f && elapsed < 1.9f)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            else if (elapsed >= 3f && elapsed < 3.15f)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C));
            else InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            highest = Mathf.Max(highest, motor.transform.position.y - initialPosition.y);
            fastest = Mathf.Max(fastest, motor.Speed);
            consumed |= stamina.Current < initialStamina - 1f;
            dashed |= dash.CooldownRemaining > 0f;
            if (elapsed < 6f) return;
            bool passed = highest > 2f && fastest > 5.8f && consumed && dashed && motor.IsGrounded;
            Finish(passed, $"salto={highest:0.00}m velocidad={fastest:0.00} stamina={consumed} dash={dashed} suelo={motor.IsGrounded}");
        }

        /// <summary>Registra el resultado y cierra solamente la instancia batch de prueba.</summary>
        private static void Finish(bool passed, string details)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(PendingKey, false);
            Debug.Log((passed ? "PLAY MODE CHECK PASSED: " : "PLAY MODE CHECK FAILED: ") + details);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
