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
        float lean;

        void Awake() => driver = GetComponent<PlayerAnimationDriver>();
        void Update() => Restore();
        void LateUpdate()
        {
            if (driver == null || !driver.enabled || driver.Animator == null) return;
            appliedTo = driver.Animator.transform;
            baseRotation = appliedTo.localRotation;
            float target = driver.Motion == CharacterMotion.Run ? 12f : driver.Motion == CharacterMotion.Walk ? 6f : 0f;
            lean = Mathf.MoveTowards(lean, target, Time.deltaTime * 65f);
            appliedTo.localRotation = baseRotation * Quaternion.Euler(lean, 0, 0);
        }
        void Restore()
        {
            if (appliedTo == null) return;
            appliedTo.localRotation = baseRotation;
            appliedTo = null;
        }
        void OnDisable() { Restore(); lean = 0; }
    }
}
