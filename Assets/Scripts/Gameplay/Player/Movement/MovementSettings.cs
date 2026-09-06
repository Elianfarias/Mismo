using UnityEngine;

namespace Mismo.Gameplay.Player.Movement
{
    /// <summary>
    /// Stores the tunable values used by player locomotion, rotation and jumping.
    /// </summary>
    [CreateAssetMenu(fileName = "DefaultMovementSettings", menuName = "Mismo/Player/Movement Settings")]
    public sealed class MovementSettings : ScriptableObject
    {
        [Header("Ground Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 5.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 8.8f;
        [SerializeField, Min(0f)] private float acceleration = 30f;
        [SerializeField, Min(0f)] private float deceleration = 40f;
        [SerializeField, Min(0f)] private float rotationSpeed = 720f;

        [Header("Air Movement")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.6f;

        [Header("Jump And Gravity")]
        [SerializeField, Min(0f)] private float jumpHeight = 1.8f;
        [SerializeField, Min(1)] private int maximumJumpCount = 2;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float groundedVerticalSpeed = -2f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;

        /// <summary>
        /// Gets the maximum walking speed in metres per second.
        /// </summary>
        public float WalkSpeed => walkSpeed;

        /// <summary>
        /// Gets the maximum sprinting speed in metres per second.
        /// </summary>
        public float SprintSpeed => sprintSpeed;

        /// <summary>
        /// Gets the grounded acceleration in metres per second squared.
        /// </summary>
        public float Acceleration => acceleration;

        /// <summary>
        /// Gets the grounded deceleration in metres per second squared.
        /// </summary>
        public float Deceleration => deceleration;

        /// <summary>
        /// Gets the maximum rotation speed in degrees per second.
        /// </summary>
        public float RotationSpeed => rotationSpeed;

        /// <summary>
        /// Gets the proportion of movement control retained while airborne.
        /// </summary>
        public float AirControl => airControl;

        /// <summary>
        /// Gets the target jump height in metres.
        /// </summary>
        public float JumpHeight => jumpHeight;

        /// <summary>
        /// Gets the total number of jumps available before touching the ground again.
        /// </summary>
        public int MaximumJumpCount => maximumJumpCount;

        /// <summary>
        /// Gets the vertical acceleration caused by gravity.
        /// </summary>
        public float Gravity => gravity;

        /// <summary>
        /// Gets the downward speed maintained while grounded.
        /// </summary>
        public float GroundedVerticalSpeed => groundedVerticalSpeed;

        /// <summary>
        /// Gets how long a jump remains valid after leaving the ground.
        /// </summary>
        public float CoyoteTime => coyoteTime;

        /// <summary>
        /// Gets how long an early jump press remains buffered.
        /// </summary>
        public float JumpBufferTime => jumpBufferTime;

        /// <summary>Mantiene valores físicos válidos durante la edición.</summary>
        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            sprintSpeed = Mathf.Max(walkSpeed, sprintSpeed);
            acceleration = Mathf.Max(0.01f, acceleration);
            deceleration = Mathf.Max(0.01f, deceleration);
            gravity = Mathf.Min(-0.01f, gravity);
            groundedVerticalSpeed = Mathf.Min(-0.01f, groundedVerticalSpeed);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            maximumJumpCount = Mathf.Max(1, maximumJumpCount);
        }
    }
}
