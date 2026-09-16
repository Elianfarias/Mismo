using Mismo.Gameplay.Player.Presentation;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Uses the same clip layering as weapons, retaining legacy enemy controllers as fallback.</summary>
    public sealed class EnemyActionPlayback : System.IDisposable
    {
        private WeaponActionPlayback playback;
        private Animator target;
        private RuntimeAnimatorController controller;
        public AnimationClip ActionClip { get; private set; }

        public void Tick(Animator animator, EnemyAttackAnimation binding, EnemyAttackPhase phase,
            float progress, int legacyMotion, float legacyTime, float speed, float deltaTime,
            AnimationClip reaction = null, float reactionProgress = 0, AvatarMask reactionMask = null)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            { Dispose(); return; }
            if (target != animator || controller != animator.runtimeAnimatorController)
            {
                Dispose(); target = animator; controller = animator.runtimeAnimatorController;
            }
            ActionClip = reaction != null ? reaction : binding != null ? binding.PlaybackClip : null;
            // Lazily create a graph only when the enemy actually uses an authored action.
            if (ActionClip != null && playback == null)
            {
                animator.applyRootMotion = false;
                playback = new WeaponActionPlayback(animator, controller);
            }
            int motion = ActionClip != null ? (speed > .1f ? (speed > 2.1f ? 2 : 1) : 0) : legacyMotion;
            float rate = Mathf.Clamp(speed / (motion == 2 ? 3.2f : 1.6f), .7f, 1.5f);
            if (playback != null)
            {
                playback.SetParameters(motion, Mathf.Clamp01(legacyTime), rate);
                // Interruptions immediately release the attack layer, including stagger and death.
                playback.SetAction(ActionClip, reaction != null ? reactionProgress : ActionClip != null ? binding.Sample(phase, progress) : 0f,
                    reaction != null ? .045f : ActionClip != null ? binding.blendSeconds : 0f,
                    reaction != null ? reactionMask : binding != null ? binding.mask : null);
                playback.Tick(deltaTime);
            }
            else
            {
                animator.SetInteger("Motion", motion);
                animator.SetFloat("ActionTime", Mathf.Clamp01(legacyTime));
                animator.SetFloat("PlaybackRate", rate);
            }
        }

        public void Dispose()
        {
            playback?.Dispose(); playback = null; target = null; controller = null; ActionClip = null;
        }
    }
}
