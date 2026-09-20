using Mismo.Gameplay.Player.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using System.Collections.Generic;

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
        private RaycastHit[] obstacleHits = new RaycastHit[16];
        private float currentDistance;
        private bool distanceInitialized;
        private UnityEngine.Camera view;
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

        private void OnEnable()
        {
            view = GetComponent<UnityEngine.Camera>();
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }

        /// <summary>Conecta la cámara con el objetivo y el input locales.</summary>
        public void Configure(Transform followTarget, PlayerInputReader reader)
        {
            target = followTarget;
            input = reader;
            if (target != null) pivot = target.position + Vector3.up * height;
            smoothVelocity = Vector3.zero;
            distanceInitialized = false;
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
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if (Presentation.WorldMapPanel.BlocksGameplay) return;
            if(input!=null&&input.GetComponent<World.GatheringPlayer>()?.BlocksGameplay==true)return;
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
            float scale = input.LookUsesPointer ? mouseSensitivity*Mathf.Clamp(PlayerPrefs.GetFloat("Mismo.LookSensitivity",1),.2f,3) : stickDegreesPerSecond * Time.deltaTime;
            if(PlayerPrefs.GetInt("Mismo.InvertLookY",0)==1)look.y=-look.y;
            yaw += look.x * scale;
            pitch = Mathf.Clamp(pitch - look.y * scale, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>Sigue la posición final del motor y acorta la distancia cuando hay geometría delante.</summary>
        private void LateUpdate()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if (target == null) return;
            Vector3 desiredPivot = target.position + Vector3.up * height;
            if (Vector3.Distance(pivot, desiredPivot) > 12f)
            {
                pivot = desiredPivot;
                smoothVelocity = Vector3.zero;
                distanceInitialized = false;
            }
            pivot = Vector3.SmoothDamp(pivot, desiredPivot, ref smoothVelocity, followTime);
            Vector3 backward = -transform.forward;
            float actualDistance = ObstacleDistance(pivot, backward);
            // Move inward immediately to avoid walls; ease outward after an obstruction clears.
            currentDistance = !distanceInitialized || actualDistance < currentDistance ? actualDistance :
                Mathf.Lerp(currentDistance, actualDistance, 1f - Mathf.Exp(-Time.deltaTime / .2f));
            distanceInitialized = true;
            transform.position = pivot + backward * currentDistance;
        }

        private float ObstacleDistance(Vector3 origin, Vector3 direction)
        {
            float radius = .25f;
            if (view != null && !view.orthographic)
            {
                float halfHeight = view.nearClipPlane * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
                radius = Mathf.Max(radius, new Vector3(halfHeight * view.aspect, halfHeight, view.nearClipPlane).magnitude);
            }
            int count;
            // A full buffer may omit the closest world hit: grow and repeat rather than truncate.
            while ((count = Physics.SphereCastNonAlloc(origin, radius, direction, obstacleHits, distance,
                obstacleMask, QueryTriggerInteraction.Ignore)) == obstacleHits.Length)
                System.Array.Resize(ref obstacleHits, obstacleHits.Length * 2);
            float result = distance;
            for (int i = 0; i < count; i++)
            {
                var hit = obstacleHits[i];
                if (target != null && (hit.transform.IsChildOf(target) || target.IsChildOf(hit.transform))) continue;
                result = Mathf.Min(result, Mathf.Max(0f, hit.distance - .1f));
            }
            return result;
        }

        // Hide only during this camera's render when a real wall forces it inside the avatar.
        // The minimap and inventory preview retain their character renderers.
        private void BeforeCamera(ScriptableRenderContext context, UnityEngine.Camera camera)
        {
            if (camera != view || target == null || Vector3.Distance(transform.position, target.position + Vector3.up * height) >= 1.4f) return;
            RestoreRenderers();
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
                if (!renderer.forceRenderingOff) { hiddenRenderers.Add(renderer); renderer.forceRenderingOff = true; }
        }
        private void AfterCamera(ScriptableRenderContext context, UnityEngine.Camera camera)
        { if (camera == view) RestoreRenderers(); }
        private void OnPreCull() => BeforeCamera(default, view);
        private void OnPostRender() => RestoreRenderers();
        private void RestoreRenderers()
        {
            foreach (var renderer in hiddenRenderers) if (renderer != null) renderer.forceRenderingOff = false;
            hiddenRenderers.Clear();
        }

        /// <summary>Libera el cursor cuando se desactiva la cámara.</summary>
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            RestoreRenderers();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
