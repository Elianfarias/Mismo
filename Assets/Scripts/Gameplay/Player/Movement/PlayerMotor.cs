using UnityEngine;

namespace Mismo.Gameplay.Player.Movement
{
    /// <summary>Única autoridad de desplazamiento, rotación y colisiones del personaje.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private MovementSettings settings;
        [SerializeField] private Transform visual;
        private CharacterController body;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float coyoteRemaining;
        private float jumpBufferRemaining;
        private float airborneSpeed;
        private int jumpsUsed;
        private bool wasGrounded;
        private bool hasGroundState;
        private float footstepDistance;
        private float airborneSoundTime;
        private GameSoundCatalog soundCatalog;
        private ControlledMovementRequest? pendingControlledMovement;
        public bool IsGrounded => body != null && body.isGrounded && verticalVelocity <= 0f;
        public float Speed => body != null ? Vector3.ProjectOnPlane(body.velocity, Vector3.up).magnitude : 0f;
        public Vector3 Facing => visual != null ? visual.forward : transform.forward;
        public Transform Visual => visual;
        public float VerticalSpeed => verticalVelocity;
        public CollisionFlags LastCollisionFlags { get; private set; }
        public bool LastMovementWasControlled { get; private set; }
        public event System.Action Jumped;
        public event System.Action Landed;

        /// <summary>Conecta los datos y la presentación del motor.</summary>
        public void Configure(MovementSettings configuration, Transform visualRoot)
        {
            settings = configuration;
            visual = visualRoot;
        }

        /// <summary>Obtiene el componente físico local.</summary>
        private void Awake()
        {
            body = GetComponent<CharacterController>();
            soundCatalog = Resources.Load<GameSoundCatalog>(GameSoundCatalog.ResourcePath);
        }
        public void ClearControlledMovement() => pendingControlledMovement = null;
        public void Face(Vector3 direction)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (visual != null && direction.sqrMagnitude > .001f) visual.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// Solicita un desplazamiento especial para el siguiente tick. Si varias habilidades
        /// solicitan el mismo frame, solo se ejecuta la de mayor prioridad.
        /// </summary>
        public bool RequestControlledDisplacement(Vector3 displacement, Vector3 facing = default, int priority = 0, bool blocksJump = true)
        {
            return RequestControlledDisplacement(new ControlledMovementRequest(displacement, facing, priority, blocksJump));
        }

        /// <summary>Registra una solicitud ya construida para el siguiente tick.</summary>
        public bool RequestControlledDisplacement(ControlledMovementRequest request)
        {
            if (pendingControlledMovement.HasValue && request.Priority < pendingControlledMovement.Value.Priority) return false;
            pendingControlledMovement = request;
            return true;
        }

        /// <summary>Resuelve locomoción, salto y la solicitud especial consumida en este frame.</summary>
        public CollisionFlags Tick(Vector3 direction, bool sprint, bool jumpPressed, bool jumpHeld, float dt)
        {
            ControlledMovementRequest? controlledMovement = pendingControlledMovement;
            pendingControlledMovement = null;
            LastMovementWasControlled = controlledMovement.HasValue;
            LastCollisionFlags = CollisionFlags.None;
            if (settings == null || dt <= 0f) return CollisionFlags.None;

            bool specialMovement = controlledMovement.HasValue;
            Vector3 specialDisplacement = specialMovement ? controlledMovement.Value.Displacement : Vector3.zero;
            bool allowJump = !specialMovement || !controlledMovement.Value.BlocksJump;
            bool grounded = IsGrounded;
            coyoteRemaining = grounded ? settings.CoyoteTime : Mathf.Max(0f, coyoteRemaining - dt);
            jumpBufferRemaining = jumpPressed ? Mathf.Max(dt, settings.JumpBufferTime) : Mathf.Max(0f, jumpBufferRemaining - dt);
            if (grounded)
            {
                verticalVelocity = settings.GroundedVerticalSpeed;
                jumpsUsed = 0;
            }
            else if (coyoteRemaining <= 0f && jumpsUsed == 0)
            {
                jumpsUsed = 1;
            }
            bool canUseGroundJump = grounded || coyoteRemaining > 0f;
            bool canUseAirJump = !canUseGroundJump && jumpsUsed < settings.MaximumJumpCount;
            if (allowJump && jumpBufferRemaining > 0f && (canUseGroundJump || canUseAirJump))
            {
                verticalVelocity = Mathf.Sqrt(-2f * settings.Gravity * settings.JumpHeight);
                jumpsUsed++;
                coyoteRemaining = 0f;
                jumpBufferRemaining = 0f;
                Jumped?.Invoke();
                GameAudio.Play(GameSound.Jump);
            }
            float gravity = settings.Gravity * (!jumpHeld && verticalVelocity > 0f ? 2f : 1f);
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -50f);
            if(specialMovement&&controlledMovement.Value.SuspendsGravity)verticalVelocity=0;
            if (grounded) airborneSpeed = sprint ? settings.SprintSpeed : settings.WalkSpeed;
            float speed = grounded ? (sprint ? settings.SprintSpeed : settings.WalkSpeed) : Mathf.Max(settings.WalkSpeed, airborneSpeed);
            Vector3 target = direction * speed;
            float rate = direction.sqrMagnitude > 0f ? settings.Acceleration : settings.Deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, rate * (grounded ? 1f : settings.AirControl) * dt);
            Vector3 facing = specialMovement
                ? (controlledMovement.Value.Facing.sqrMagnitude > 0.0001f ? controlledMovement.Value.Facing : specialDisplacement)
                : direction;
            if (visual != null && facing.sqrMagnitude > 0.0001f)
                visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.LookRotation(facing), settings.RotationSpeed * dt);
            Vector3 horizontalStep = specialMovement ? specialDisplacement : horizontalVelocity * dt;
            Vector3 positionBeforeMove = transform.position;
            CollisionFlags flags = body.Move(horizontalStep + Vector3.up * verticalVelocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0f) verticalVelocity = settings.GroundedVerticalSpeed;
            bool groundedAfterMove = IsGrounded;
            UpdateMovementSounds(groundedAfterMove, specialMovement, positionBeforeMove, dt);
            if (hasGroundState && groundedAfterMove && !wasGrounded) Landed?.Invoke();
            wasGrounded = groundedAfterMove;
            hasGroundState = true;
            LastCollisionFlags = flags;
            return flags;
        }

        private void UpdateMovementSounds(bool grounded, bool controlled, Vector3 previousPosition, float dt)
        {
            if (!grounded)
            {
                airborneSoundTime += dt;
                footstepDistance = 0f;
                return;
            }
            // Ignore tiny losses of ground contact on terrain steps.
            if (hasGroundState && !wasGrounded && airborneSoundTime >= .12f) GameAudio.Play(GameSound.Land);
            airborneSoundTime = 0f;
            float distance = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude;
            if (controlled || distance / dt < .15f) { footstepDistance = 0f; return; }
            bool running = distance / dt > settings.WalkSpeed * 1.1f;
            float stride = soundCatalog != null ? (running ? soundCatalog.runStepDistance : soundCatalog.walkStepDistance) : (running ? 2.5f : 2f);
            footstepDistance += distance;
            if (footstepDistance < Mathf.Max(.1f, stride)) return;
            footstepDistance %= Mathf.Max(.1f, stride);
            GameAudio.Play(running ? GameSound.FootstepRun : GameSound.FootstepWalk);
        }

        /// <summary>
        /// Compatibilidad con la firma anterior: convierte el desplazamiento especial en una
        /// solicitud para el motor antes de resolver el tick.
        /// </summary>
        public CollisionFlags Tick(Vector3 direction, bool sprint, bool jumpPressed, bool jumpHeld,
            bool specialMovement, Vector3 specialDisplacement, float dt)
        {
            if (specialMovement)
                RequestControlledDisplacement(specialDisplacement, specialDisplacement);
            return Tick(direction, sprint, jumpPressed, jumpHeld, dt);
        }

        /// <summary>Reubica al personaje y reinicia sus velocidades para recuperar una caída del playground.</summary>
        public void ResetPosition(Vector3 position)
        {
            body.enabled = false;
            transform.position = position;
            body.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            coyoteRemaining = jumpBufferRemaining = 0f;
            jumpsUsed = 0;
            wasGrounded = true;
            footstepDistance = airborneSoundTime = 0f;
            hasGroundState = true;
            pendingControlledMovement = null;
            LastCollisionFlags = CollisionFlags.None;
            LastMovementWasControlled = false;
        }
    }
}
