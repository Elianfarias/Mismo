using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public enum CharacterMotion { Idle, Walk, Run, Jump, Fall, Land, Attack1, Attack2, Attack3, Dash, Parry, Lunge, Spin }

    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerLocomotionLean))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Min(.1f)] private float referenceWalkSpeed = 5.5f;
        [SerializeField, Min(.1f)] private float referenceRunSpeed = 8.8f;
        private PlayerMotor motor;
        private PlayerController controller;
        private Health health;
        private LegacyCombatAnimationAdapter legacy;
        private Equipment.AbilityRunner abilityRunner;
        private bool moving;
        private bool running;
        private float smoothedPlaybackRate = 1f;
        private float locomotionSpeed;
        private bool hasLocomotionBlend;
        private RuntimeAnimatorController baseController;
        private RuntimeAnimatorController appliedController;
        private WeaponAnimationSet appliedSet;
        private AvatarMask appliedMask;
        private WeaponActionPlayback playback;
        private Equipment.EquipmentLoadout loadout;
        private float jumpedAt = -10f;
        private float hitAt = -10f;
        private AnimationClip hitClip;
        private AvatarMask hitMask;
        private const float HitDuration = .36f;
        private Transform torsoBone;
        private Quaternion torsoBeforeCorrection;
        private bool torsoCorrectionApplied;
        private float torsoCorrection;
        private float targetTorsoCorrection;
        private float landedAt = -10f;
        private float lastGroundedAt = -10f;
        private const float GroundGrace = .12f;
        public CharacterMotion Motion { get; private set; }
        public Animator Animator => animator;
        public AnimationClip ActionClip {get;private set;}
        public float LandingAge => Time.time - landedAt;
        public void Configure(Animator target) => animator = target;
        private void Awake()
        {
            if (GetComponent<PlayerLocomotionLean>() == null) gameObject.AddComponent<PlayerLocomotionLean>();
            motor = GetComponent<PlayerMotor>(); controller = GetComponent<PlayerController>(); health = GetComponent<Health>();
            legacy = new LegacyCombatAnimationAdapter(gameObject);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            abilityRunner = GetComponent<Equipment.AbilityRunner>();
            if (animator != null) animator.applyRootMotion = false;
            if (animator != null) baseController = animator.runtimeAnimatorController;
            loadout = GetComponent<Equipment.EquipmentLoadout>();
            hitClip = Resources.Load<AnimationClip>("CombatPresentation/Human_Player_CombatDamage01");
            hitMask = Resources.Load<AvatarMask>("CombatPresentation/PlayerUpperBody");
        }
        private void OnEnable() { if(motor != null) { motor.Jumped += OnJump; motor.Landed += OnLand; } if(health!=null)health.Damaged+=OnHit; }
        private void OnDisable() { RestoreTorso();torsoCorrection=targetTorsoCorrection=0;if(motor != null) { motor.Jumped -= OnJump; motor.Landed -= OnLand; } if(health!=null)health.Damaged-=OnHit;hitAt=-10f;playback?.Dispose();playback=null;ActionClip=null; }
        private void RestoreTorso()
        {
            if(torsoCorrectionApplied&&torsoBone!=null)torsoBone.localRotation=torsoBeforeCorrection;
            torsoCorrectionApplied=false;
        }
        private void LateUpdate()
        {
            if(animator==null||!animator.enabled)return;
            if(torsoBone==null)
                foreach(var bone in animator.GetComponentsInChildren<Transform>())
                    if(bone.name=="Spine"){torsoBone=bone;break;}
            if(torsoBone==null)return;
            torsoCorrection=Mathf.Lerp(torsoCorrection,targetTorsoCorrection,1f-Mathf.Exp(-Time.deltaTime/.06f));
            if(Mathf.Abs(torsoCorrection)<.01f)return;
            torsoBeforeCorrection=torsoBone.localRotation;
            torsoBone.rotation=Quaternion.AngleAxis(-torsoCorrection,animator.transform.right)*torsoBone.rotation;
            torsoCorrectionApplied=true;
        }
        private void OnHit(DamageInfo _) { if(health!=null&&!health.IsDead)hitAt=Time.time; }
        private void OnJump() => jumpedAt = Time.time;
        private void OnLand()
        {
            // Tiny losses of contact on voxel steps must not restart locomotion.
            if (Time.time - lastGroundedAt > GroundGrace) landedAt = Time.time;
        }
        private void Update()
        {
            RestoreTorso();
            targetTorsoCorrection=0;
            var profile = loadout != null && loadout.ActiveDefinition != null ? loadout.ActiveDefinition.poseProfile : null;
            var family = loadout != null && loadout.ActiveDefinition != null ? loadout.ActiveDefinition.family : null;
            var animationSet=family!=null?family.animations:null;
            var actionMask=animationSet!=null?animationSet.actionMask:null;
            var desiredController=profile!=null && profile.animations!=null ? profile.animations
                : animationSet!=null && animationSet.locomotion!=null ? animationSet.locomotion : baseController;
            if (animator != null && (appliedController!=desiredController || appliedSet!=animationSet || appliedMask!=actionMask || animationSet!=null && playback==null))
            {
                playback?.Dispose();playback=null;
                appliedController=desiredController;appliedSet=animationSet;appliedMask=actionMask;
                animator.runtimeAnimatorController=desiredController;
                hasLocomotionBlend=false;
                foreach(var parameter in animator.parameters)
                    if(parameter.name=="LocomotionSpeed" && parameter.type==AnimatorControllerParameterType.Float) hasLocomotionBlend=true;
                if(animationSet!=null && desiredController!=null)playback=new WeaponActionPlayback(animator,desiredController,actionMask);
            }
            if (abilityRunner == null) abilityRunner = GetComponent<Equipment.AbilityRunner>();
            if (animator == null || animator.runtimeAnimatorController == null || motor == null) return;
            float speed = motor.Speed;
            moving = speed > (moving ? .08f : .15f);
            // Follow actual movement, not the sprint button; hysteresis avoids threshold flicker.
            running = moving && speed > referenceWalkSpeed * (running ? 1.08f : 1.18f);
            if (motor.IsGrounded) lastGroundedAt = Time.time;
            bool airborne = !motor.IsGrounded &&
                (motor.VerticalSpeed > 0f || Time.time - lastGroundedAt > GroundGrace);
            float actionTime = 0f;
            if (health != null && health.Current <= 0f) Motion = CharacterMotion.Idle;
            else if (motor.LastMovementWasControlled) Motion=CharacterMotion.Dash;
            else if (airborne)
            {
                Motion=motor.VerticalSpeed>0 ? CharacterMotion.Jump : CharacterMotion.Fall;
                actionTime=Mathf.Clamp01((Time.time-jumpedAt)/.30f);
            }
            else if (!moving && Time.time-landedAt<.18f) Motion=CharacterMotion.Land;
            else if (moving) Motion=running ? CharacterMotion.Run : CharacterMotion.Walk;
            else Motion=CharacterMotion.Idle;
            ActionClip=null;float clipTime=0,blend=.06f;AvatarMask resolvedMask=actionMask;bool unstoppable=false;
            if((health==null || !health.IsDead) && animationSet!=null && abilityRunner!=null && abilityRunner.TryGetAnimationFrame(out var actionFrame))
            {
                var binding=animationSet.Find(actionFrame.Ability);
                if(binding!=null && binding.TrySample(actionFrame,out var actionClip,out clipTime))
                {ActionClip=actionClip;blend=binding.blendSeconds;resolvedMask=binding.ResolveMask(actionMask);targetTorsoCorrection=binding.torsoUprightDegrees;unstoppable=actionFrame.Ability!=null&&actionFrame.Ability.unstoppable;}
            }
            if(ActionClip==null && (health==null || !health.IsDead))
            {var motion=Motion;legacy.Resolve(abilityRunner,ref motion,ref actionTime);Motion=motion;}
            if (hitClip != null && !unstoppable && health != null && !health.IsDead && Time.time - hitAt < HitDuration)
            {
                ActionClip = hitClip; clipTime = Mathf.Clamp01((Time.time - hitAt) / HitDuration);
                blend = .045f; resolvedMask = hitMask;
                targetTorsoCorrection=0;
            }
            float reference=Motion==CharacterMotion.Run?referenceRunSpeed:referenceWalkSpeed;
            float blendTarget=speed<=referenceWalkSpeed ? speed/referenceWalkSpeed : 1f+(speed-referenceWalkSpeed)/Mathf.Max(.1f,referenceRunSpeed-referenceWalkSpeed);
            locomotionSpeed=Mathf.Lerp(locomotionSpeed,Mathf.Clamp(blendTarget,0f,2f),1f-Mathf.Exp(-Time.deltaTime/.08f));
            if(hasLocomotionBlend) reference=Mathf.Lerp(referenceWalkSpeed,referenceRunSpeed,Mathf.Clamp01(locomotionSpeed-1f));
            float playbackRate=moving ? Mathf.Clamp(speed/reference,.2f,1.4f) : 1f;
            // Idle/walk blending already expresses low speed; avoid slowing that cycle twice.
            if(hasLocomotionBlend && locomotionSpeed<1f) playbackRate=1f;
            smoothedPlaybackRate = Mathf.Lerp(smoothedPlaybackRate, playbackRate, 1f-Mathf.Exp(-Time.deltaTime/.10f));
            int stateMotion=hasLocomotionBlend && (int)Motion<=(int)CharacterMotion.Run ? (int)CharacterMotion.Idle : (int)Motion;
            if(playback!=null)
            {
                playback.SetParameters(stateMotion,Mathf.Clamp01(actionTime),smoothedPlaybackRate);
                if(hasLocomotionBlend) playback.SetLocomotionSpeed(locomotionSpeed);
                playback.SetAction(ActionClip,clipTime,blend,resolvedMask);playback.Tick(Time.deltaTime);
                return;
            }
            animator.SetFloat("PlaybackRate",smoothedPlaybackRate);
            if(hasLocomotionBlend) animator.SetFloat("LocomotionSpeed",locomotionSpeed);
            // Combat pose time follows the existing hitbox clock; animation never changes damage timing.
            animator.SetFloat("ActionTime",Mathf.Clamp01(actionTime));
            animator.SetInteger("Motion",stateMotion);
        }
    }
}
