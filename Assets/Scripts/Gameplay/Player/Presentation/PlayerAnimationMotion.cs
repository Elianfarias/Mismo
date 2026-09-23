using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Receives motion on the visual's Animator. Only the player motor
    /// may apply it; the visual and collider must never move independently.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class PlayerAnimationMotion : MonoBehaviour
    {
        Animator animator;
        public Action<Vector3> Move { private get; set; }
        void Awake() => animator = GetComponent<Animator>();
        void OnAnimatorMove()
        {
            if (animator == null) animator = GetComponent<Animator>();
            // Keeping this callback present also discards locomotion root motion
            // and safely stops motion when the owning driver is disabled.
            Move?.Invoke(animator.deltaPosition);
        }
    }
}
