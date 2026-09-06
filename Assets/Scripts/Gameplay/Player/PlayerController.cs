using Mismo.Gameplay.Player.Input;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player
{
    /// <summary>Coordina intención local, recursos y motor sin conocer cinturones concretos.</summary>
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor), typeof(Stamina))]
    [RequireComponent(typeof(EquipmentLoadout))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform cameraBasis;
        private PlayerInputReader input;
        private PlayerMotor motor;
        private Stamina stamina;
        private IBelt belt;
        private BasicSwordCombo swordCombo;
        private SwordParry swordParry;
        private SwordSpinAttack swordSpinAttack;
        private SwordLunge swordLunge;
        public bool IsSprinting { get; private set; }

        /// <summary>Conecta la orientación de cámara utilizada para interpretar WASD.</summary>
        public void Configure(Transform basis) => cameraBasis = basis;

        /// <summary>Resuelve las dependencias locales del personaje.</summary>
        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            motor = GetComponent<PlayerMotor>();
            stamina = GetComponent<Stamina>();
            // El combo pertenece al arma equipada; buscarlo en hijos permite cambiar de
            // espada sin convertir el ataque básico en una habilidad global del jugador.
            swordCombo = GetComponentInChildren<BasicSwordCombo>();
            swordParry = GetComponentInChildren<SwordParry>();
            swordSpinAttack = GetComponentInChildren<SwordSpinAttack>();
            swordLunge = GetComponentInChildren<SwordLunge>();
            EquipmentLoadout loadout = GetComponent<EquipmentLoadout>();
            if (loadout == null) loadout = gameObject.AddComponent<EquipmentLoadout>();
            loadout.Initialize();
            belt = loadout.Belt;
            if (belt == null) Debug.LogError("Player necesita un cinturón equipado para mover el dash.", this);
            if (GetComponent<Presentation.MovementFeedback>() == null)
                gameObject.AddComponent<Presentation.MovementFeedback>();
        }

        /// <summary>Resuelve las prioridades y envía un solo paso de movimiento al motor.</summary>
        private void Update()
        {
            float dt = Time.deltaTime;
            swordCombo?.Tick(dt);
            swordParry?.Tick(dt);
            swordSpinAttack?.Tick(dt);
            swordLunge?.TickCooldown(dt);
            if (cameraBasis == null || !input.isActiveAndEnabled) return;
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            Vector3 forward = Vector3.ProjectOnPlane(cameraBasis.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = forward * move.y + right * move.x;
            belt?.TickCooldown(dt);
            if (input.WasDashPressedThisFrame())
                belt?.TryStart(direction.sqrMagnitude > 0f ? direction : motor.Facing, motor.IsGrounded);
            if (input.WasAttackPressedThisFrame()) swordCombo?.RequestAttack();
            if (input.WasParryPressedThisFrame()) swordParry?.RequestParry();
            bool lungeActiveBeforeStep = swordLunge != null && swordLunge.IsActive;
            bool spinActiveBeforeStep = swordSpinAttack != null && swordSpinAttack.IsActive;
            if (input.WasSpinAttackPressedThisFrame() && !lungeActiveBeforeStep) swordSpinAttack?.RequestSpin();
            if (input.WasLungePressedThisFrame() && !spinActiveBeforeStep) swordLunge?.RequestLunge(direction);
            bool special = belt != null && belt.IsActive;
            if (special)
            {
                Vector3 displacement = belt.Step(dt);
                motor.RequestControlledDisplacement(displacement, displacement, priority: 0);
            }
            Vector3 lungeDisplacement = swordLunge != null ? swordLunge.Step(dt) : Vector3.zero;
            if (lungeDisplacement.sqrMagnitude > 0.000001f)
                motor.RequestControlledDisplacement(lungeDisplacement, swordLunge.Direction, priority: 10);
            bool weaponMovement = lungeActiveBeforeStep || (swordLunge != null && swordLunge.IsActive);
            IsSprinting = stamina.Tick(input.SprintHeld && move.sqrMagnitude > 0.01f && motor.Speed > 0.05f && motor.IsGrounded && !special && !weaponMovement, dt);
            CollisionFlags collisions = motor.Tick(direction, IsSprinting, input.WasJumpPressedThisFrame(), input.JumpHeld, dt);
            if (special && (collisions & CollisionFlags.Sides) != 0) belt.Cancel();
            if (swordLunge != null && (collisions & CollisionFlags.Sides) != 0) swordLunge.Cancel();
        }
    }
}
