using Mismo.Gameplay.Player.Presentation;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Uses the same clip layering as weapons, retaining legacy enemy controllers as fallback.</summary>
    public sealed class EnemyActionPlayback : System.IDisposable
    {
        private WeaponActionPlayback playback;
        private Animator target;
        private AnimationClip movementClip;
        private float movementTime;
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
            // Read the resolved AI state: damage alone does not mean an attack was interrupted.
            bool attacking=binding!=null||legacyMotion==3||IsPerformingAttack(animator);
            if(attacking)reaction=null;
            var customization=animator.GetComponentInParent<EnemyEquipment>();
            if(customization!=null&&!customization.isActiveAndEnabled)customization=null;
            if(attacking&&customization!=null)customization.CancelHitReaction();
            if(!attacking&&customization!=null&&customization.PlayingPostureBreak)
            {reaction=customization.postureBreakClip;reactionProgress=customization.PostureBreakProgress;reactionMask=null;}
            else if(!attacking&&customization!=null&&customization.PlayingParry)
            {reaction=customization.ParryClip;reactionProgress=customization.ParryProgress;reactionMask=null;}
            else if(!attacking&&customization!=null&&customization.PlayingHit)
            {reaction=customization.hitClip;reactionProgress=customization.HitProgress;reactionMask=customization.hitMask;}
            AnimationClip movement=null;
            bool alive=animator.GetComponentInParent<Mismo.Gameplay.Combat.Health>()?.IsDead!=true;
            var goblin=animator.GetComponentInParent<GoblinController>();
            bool staggered=goblin!=null&&goblin.State==GoblinState.Stagger;
            if(customization!=null&&alive&&!staggered&&reaction==null&&binding==null&&legacyMotion<=2)
                movement=legacyMotion==2?customization.runClip:legacyMotion==1?customization.walkClip:customization.idleClip;
            if(movementClip!=movement){movementClip=movement;movementTime=0;}
            float movementProgress=0;
            if(movement!=null)
            {
                float reference=legacyMotion==2?customization.runReferenceSpeed:customization.walkReferenceSpeed;
                float movementRate=legacyMotion==0?1:Mathf.Clamp(speed/Mathf.Max(.1f,reference),.25f,2);
                movementTime+=Mathf.Max(0,deltaTime)*movementRate;
                movementProgress=Mathf.Repeat(movementTime/Mathf.Max(.01f,movement.length),1);
            }
            ActionClip = reaction != null ? reaction : binding != null ? binding.PlaybackClip : movement;
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
                playback.SetAction(ActionClip, reaction != null ? reactionProgress : movement!=null?movementProgress:ActionClip != null ? binding.Sample(phase, progress) : 0f,
                    reaction != null ? .045f : movement!=null?.08f:ActionClip != null ? binding.blendSeconds : 0f,
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

        public static bool IsPerformingAttack(Component actor)
        {
            var goblin=actor.GetComponentInParent<GoblinController>();
            if(goblin!=null)return goblin.State==GoblinState.Telegraph||goblin.State==GoblinState.Attack||goblin.State==GoblinState.Recovery;
            var boss=actor.GetComponentInParent<BossController>();
            return boss!=null&&(boss.State==BossState.Telegraph||boss.State==BossState.Attack||boss.State==BossState.Recovery);
        }

        public void Dispose()
        {
            playback?.Dispose(); playback = null; target = null; controller = null; ActionClip = null;
        }
    }
}
