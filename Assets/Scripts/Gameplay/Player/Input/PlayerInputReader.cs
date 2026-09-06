using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Input
{
    /// <summary>
    /// Exposes the local player's raw input without applying gameplay rules.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string MoveActionName = "Move";
        private const string LookActionName = "Look";
        private const string SprintActionName = "Sprint";
        private const string JumpActionName = "Jump";
        private const string DashActionName = "Dash";
        private const string AttackActionName = "Attack";
        private const string ParryActionName = "Parry";
        private const string SpinAttackActionName = "SpinAttack";
        private const string LungeActionName = "Lunge";

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;
        private InputAction _dashAction;
        private InputAction _attackAction;
        private InputAction _parryAction;
        private InputAction _spinAttackAction;
        private InputAction _lungeAction;

        /// <summary>
        /// Gets the current two-dimensional movement input.
        /// </summary>
        public Vector2 Move => _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        /// <summary>
        /// Gets the current two-dimensional camera input.
        /// </summary>
        public Vector2 Look => _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;

        /// <summary>Indica si Look entrega desplazamiento de puntero en vez de un eje de mando.</summary>
        public bool LookUsesPointer => _lookAction?.activeControl?.device is Pointer;

        /// <summary>
        /// Gets whether the sprint control is currently held.
        /// </summary>
        public bool SprintHeld => _sprintAction?.IsPressed() ?? false;

        /// <summary>
        /// Gets whether the jump control is currently held.
        /// </summary>
        public bool JumpHeld => _jumpAction?.IsPressed() ?? false;

        /// <summary>
        /// Gets whether the dash control is currently held.
        /// </summary>
        public bool DashHeld => _dashAction?.IsPressed() ?? false;

        /// <summary>Gets whether the attack control is currently held.</summary>
        public bool AttackHeld => _attackAction?.IsPressed() ?? false;

        /// <summary>Gets whether the parry control is currently held.</summary>
        public bool ParryHeld => _parryAction?.IsPressed() ?? false;

        /// <summary>Gets whether the sword spin control is currently held.</summary>
        public bool SpinAttackHeld => _spinAttackAction?.IsPressed() ?? false;

        /// <summary>Gets whether the sword lunge control is currently held.</summary>
        public bool LungeHeld => _lungeAction?.IsPressed() ?? false;

        /// <summary>
        /// Resolves the actions owned by the attached PlayerInput component.
        /// </summary>
        private void Start()
        {
            PlayerInput playerInput = GetComponent<PlayerInput>();
            if (playerInput.actions == null)
            {
                Debug.LogError("Player Input Reader: PlayerInput no tiene un InputActionAsset asignado.", this);
                enabled = false;
                return;
            }

            InputActionMap playerMap = playerInput.actions.FindActionMap(PlayerMapName, true);

            _moveAction = playerMap.FindAction(MoveActionName, true);
            _lookAction = playerMap.FindAction(LookActionName, true);
            _sprintAction = playerMap.FindAction(SprintActionName, true);
            _jumpAction = playerMap.FindAction(JumpActionName, true);
            _dashAction = playerMap.FindAction(DashActionName, true);
            _attackAction = playerMap.FindAction(AttackActionName, true);
            _parryAction = playerMap.FindAction(ParryActionName, true);
            _spinAttackAction = playerMap.FindAction(SpinAttackActionName, true);
            _lungeAction = playerMap.FindAction(LungeActionName, true);
        }

        /// <summary>
        /// Reports whether jump was pressed during the current frame.
        /// </summary>
        public bool WasJumpPressedThisFrame()
        {
            return _jumpAction?.WasPressedThisFrame() ?? false;
        }

        /// <summary>
        /// Reports whether dash was pressed during the current frame.
        /// </summary>
        public bool WasDashPressedThisFrame()
        {
            return _dashAction?.WasPressedThisFrame() ?? false;
        }

        /// <summary>Reports whether attack was pressed during the current frame.</summary>
        public bool WasAttackPressedThisFrame()
        {
            return _attackAction?.WasPressedThisFrame() ?? false;
        }

        /// <summary>Reports whether parry was pressed during the current frame.</summary>
        public bool WasParryPressedThisFrame()
        {
            return _parryAction?.WasPressedThisFrame() ?? false;
        }

        /// <summary>Reports whether spin attack was pressed during the current frame.</summary>
        public bool WasSpinAttackPressedThisFrame()
        {
            return _spinAttackAction?.WasPressedThisFrame() ?? false;
        }

        /// <summary>Reports whether lunge was pressed during the current frame.</summary>
        public bool WasLungePressedThisFrame()
        {
            return _lungeAction?.WasPressedThisFrame() ?? false;
        }
    }
}
