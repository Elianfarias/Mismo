using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public enum CharacterMotion { Idle, Walk, Run, Jump, Fall, Land, Attack1, Attack2, Attack3, Dash, Parry, Lunge, Spin }

    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Min(.1f)] private float referenceWalkSpeed = 5.5f;
        [SerializeField, Min(.1f)] private float referenceRunSpeed = 8.8f;
        private PlayerMotor motor;
        private PlayerController controller;
        private Health health;
        private BasicSwordCombo combo;
        private SwordParry parry;
        private SwordLunge lunge;
        private SwordSpinAttack spin;
        private bool moving;
        private float jumpedAt = -10f;
        private float landedAt = -10f;
        public CharacterMotion Motion { get; private set; }
        public Animator Animator => animator;
        public void Configure(Animator target) => animator = target;
        private void Awake()
        {
            motor = GetComponent<PlayerMotor>(); controller = GetComponent<PlayerController>(); health = GetComponent<Health>();
            combo = GetComponentInChildren<BasicSwordCombo>(); parry = GetComponentInChildren<SwordParry>();
            lunge = GetComponentInChildren<SwordLunge>(); spin = GetComponentInChildren<SwordSpinAttack>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }
        private void OnEnable() { if(motor != null) { motor.Jumped += OnJump; motor.Landed += OnLand; } }
        private void OnDisable() { if(motor != null) { motor.Jumped -= OnJump; motor.Landed -= OnLand; } }
        private void OnJump() => jumpedAt = Time.time;
        private void OnLand() => landedAt = Time.time;
        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null || motor == null) return;
            float speed = motor.Speed;
            moving = speed > (moving ? .08f : .15f);
            float actionTime = 0f;
            if (health != null && health.Current <= 0f) Motion = CharacterMotion.Idle;
            else if (parry != null && parry.IsWindowOpen) { Motion=CharacterMotion.Parry; actionTime=1f-parry.WindowNormalized; }
            else if (spin != null && spin.IsActive) { Motion=CharacterMotion.Spin; actionTime=1f-spin.ActiveRemaining/spin.Duration; }
            else if (lunge != null && lunge.IsActive) { Motion=CharacterMotion.Lunge; actionTime=lunge.ActiveNormalized; }
            else if (combo != null && combo.IsActive)
            {
                Motion=(CharacterMotion)((int)CharacterMotion.Attack1+Mathf.Clamp(combo.CurrentStepIndex,0,2));
                actionTime=combo.CurrentStepNormalized;
            }
            else if (motor.LastMovementWasControlled) Motion=CharacterMotion.Dash;
            else if (!motor.IsGrounded)
            {
                Motion=motor.VerticalSpeed>0 ? CharacterMotion.Jump : CharacterMotion.Fall;
                actionTime=Mathf.Clamp01((Time.time-jumpedAt)/.30f);
            }
            else if (Time.time-landedAt<.18f) Motion=CharacterMotion.Land;
            else if (moving) Motion=(controller != null && controller.IsSprinting) || speed>referenceWalkSpeed*1.18f ? CharacterMotion.Run : CharacterMotion.Walk;
            else Motion=CharacterMotion.Idle;
            float reference=Motion==CharacterMotion.Run?referenceRunSpeed:referenceWalkSpeed;
            animator.SetFloat("PlaybackRate",Mathf.Clamp(speed/reference,.75f,1.4f),.10f,Time.deltaTime);
            // Combat pose time follows the existing hitbox clock; animation never changes damage timing.
            animator.SetFloat("ActionTime",Mathf.Clamp01(actionTime));
            animator.SetInteger("Motion",(int)Motion);
        }
    }
}
