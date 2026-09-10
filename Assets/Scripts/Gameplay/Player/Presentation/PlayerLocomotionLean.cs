using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Remove last frame's cosmetic tilt before the motor writes this frame's facing.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class PlayerLocomotionLean : MonoBehaviour
    {
        PlayerAnimationDriver driver;
        Transform appliedTo;
        Quaternion baseRotation;
        Vector3 basePosition;
        float lean;
        float bank;
        float previousSpeed;
        Vector3 previousForward;
        bool sampled;
        Movement.PlayerMotor motor;

        void Awake() { driver = GetComponent<PlayerAnimationDriver>(); motor = GetComponent<Movement.PlayerMotor>(); }
        void Update() => Restore();
        void LateUpdate()
        {
            if (driver == null || !driver.enabled || driver.Animator == null || motor == null) return;
            appliedTo = driver.Animator.transform;
            baseRotation = appliedTo.localRotation;
            basePosition = appliedTo.localPosition;
            float dt = Time.deltaTime;
            float speed = motor.Speed;
            Vector3 forward = Vector3.ProjectOnPlane(appliedTo.forward, Vector3.up).normalized;
            bool locomotion = driver.ActionClip == null && (driver.Motion == CharacterMotion.Idle || driver.Motion == CharacterMotion.Walk || driver.Motion == CharacterMotion.Run);
            float acceleration = sampled && dt > 0f ? Mathf.Clamp((speed-previousSpeed)/dt,-25f,25f) : 0f;
            float turnRate = sampled && dt > 0f ? Vector3.SignedAngle(previousForward,forward,Vector3.up)/dt : 0f;
            float target = locomotion ? Mathf.Clamp(speed*1.15f + acceleration*.16f,-5f,12f) : 0f;
            float targetBank = locomotion ? Mathf.Clamp(-turnRate*.025f,-8f,8f)*Mathf.Clamp01(speed/3f) : 0f;
            float blend = 1f-Mathf.Exp(-dt/.10f);
            lean = Mathf.Lerp(lean,target,blend);
            bank = Mathf.Lerp(bank,targetBank,blend);
            appliedTo.localRotation = baseRotation * Quaternion.Euler(lean, 0, bank);
            // A small visual compression preserves the running leg cycle and never moves the collider.
            float age = driver.LandingAge;
            if(locomotion && motor.IsGrounded && speed>.15f && age>=0f && age<.24f)
                appliedTo.position += Vector3.down*(.055f*Mathf.Sin(Mathf.PI*age/.24f));
            previousSpeed=speed;previousForward=forward;sampled=true;
        }
        void Restore()
        {
            if (appliedTo == null) return;
            appliedTo.localRotation = baseRotation;
            appliedTo.localPosition = basePosition;
            appliedTo = null;
        }
        void OnDisable() { Restore(); lean = bank = 0; sampled=false; }
    }
}
