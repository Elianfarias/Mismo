using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Dash;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Feedback provisional y recuperación manual para probar el playground.</summary>
    public sealed class MovementDebugView : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Stamina stamina;
        [SerializeField] private BeltDash dash;
        private Vector3 spawn;

        /// <summary>Conecta las fuentes de estado del HUD provisional.</summary>
        public void Configure(PlayerMotor playerMotor, Stamina resource, BeltDash belt)
        {
            motor = playerMotor;
            stamina = resource;
            dash = belt;
        }

        /// <summary>Recuerda el punto de inicio del playground.</summary>
        private void Start() => spawn = motor.transform.position;

        /// <summary>Recupera caídas del escenario o reinicia posición con Backspace.</summary>
        private void Update()
        {
            if (motor.transform.position.y < -20f || (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame))
            {
                dash.Cancel();
                motor.ResetPosition(spawn);
            }
        }

        /// <summary>Muestra controles, stamina y disponibilidad del cinturón.</summary>
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 390, 155), GUI.skin.box);
            GUILayout.Label("MOVEMENT PROTOTYPE");
            GUILayout.Label("WASD mover | Mouse cámara | Shift sprint");
            GUILayout.Label("Space salto | C dash | Backspace volver");
            GUILayout.Label("Esc liberar cursor | Click capturar");
            GUILayout.Label($"Stamina: {stamina.Current:0} ({stamina.Normalized:P0})");
            GUILayout.Label(dash.IsActive ? "Dash activo" : dash.CooldownRemaining > 0f ? $"Dash: {dash.CooldownRemaining:0.0}s" : "Dash listo");
            GUILayout.Label($"Velocidad: {motor.Speed:0.0} m/s | {(motor.IsGrounded ? "Suelo" : "Aire")}");
            GUILayout.EndArea();
        }
    }
}
