using Mismo.Gameplay.Player.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Camera
{
    /// <summary>Controla una cámara orbital alejada con protección frente a obstáculos.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private PlayerInputReader input;
        [SerializeField, Min(1f)] private float distance = 9f;
        [SerializeField] private float height = 1.3f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Min(0f)] private float stickDegreesPerSecond = 150f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField, Min(0.01f)] private float followTime = 0.06f;
        [SerializeField] private LayerMask obstacleMask = ~(1 << 2);
        private float yaw;
        private float pitch = 25f;
        private Vector3 pivot;
        private Vector3 smoothVelocity;

        /// <summary>Conecta la cámara con el objetivo y el input locales.</summary>
        public void Configure(Transform followTarget, PlayerInputReader reader)
        {
            target = followTarget;
            input = reader;
        }

        /// <summary>Inicializa la órbita y captura el cursor.</summary>
        private void Start()
        {
            if (target != null)
            {
                pivot = target.position + Vector3.up * height;
                yaw = target.eulerAngles.y;
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Actualiza la orientación antes de calcular el movimiento relativo a cámara.</summary>
        private void Update()
        {
            if (Presentation.WorldMapPanel.BlocksGameplay) return;
            if (input != null && input.GetComponent<Equipment.Inventory.InventoryPanel>() is Equipment.Inventory.InventoryPanel panel && panel.BlocksGameplay) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (input == null || Cursor.lockState != CursorLockMode.Locked) return;
            Vector2 look = input.Look;
            float scale = input.LookUsesPointer ? mouseSensitivity : stickDegreesPerSecond * Time.deltaTime;
            yaw += look.x * scale;
            pitch = Mathf.Clamp(pitch - look.y * scale, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>Sigue la posición final del motor y acorta la distancia cuando hay geometría delante.</summary>
        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 desiredPivot = target.position + Vector3.up * height;
            if (Vector3.Distance(pivot, desiredPivot) > 12f) pivot = desiredPivot;
            pivot = Vector3.SmoothDamp(pivot, desiredPivot, ref smoothVelocity, followTime);
            Vector3 backward = -transform.forward;
            float actualDistance = distance;
            if (Physics.SphereCast(pivot, 0.25f, backward, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
                actualDistance = Mathf.Max(0f, hit.distance - 0.1f);
            transform.position = pivot + backward * actualDistance;
        }

        /// <summary>Libera el cursor cuando se desactiva la cámara.</summary>
        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
