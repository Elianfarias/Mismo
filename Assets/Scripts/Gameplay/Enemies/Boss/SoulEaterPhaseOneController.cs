using System;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Voxels;
using Mismo.Gameplay.Player.World;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health), typeof(DamageReceiver), typeof(CapsuleCollider))]
    public sealed partial class SoulEaterPhaseOneController : MonoBehaviour, IEnemyDamageTarget, IParryResponder, IBossMusicThreat, IBossPhaseCheatTarget
    {
        [SerializeField] SoulEaterPhaseOneSettings settings;
        [SerializeField] VoxelRigInstance rig;
        [SerializeField] Transform upperMouth, lowerMouth;
        [SerializeField] Transform[] tailBones;
        [SerializeField] SphereCollider headCollider;
        [SerializeField] BoxCollider groundBody;
        public BoxCollider GroundBody => groundBody;
        [SerializeField] SoulEaterEffects effects;
        [SerializeField] bool autoEngage = true;
        Health health, targetHealth;
        CombatState combat;
        NavMeshAgent agent;
        CapsuleCollider body;
        SoulEaterAnimation animation;
        Transform target;
        Vector3 home, aim, jumpStart, jumpEnd, chargeDirection, previousMouth;
        Quaternion homeRotation;
        Vector3[] previousTail;
        readonly Collider[] overlaps = new Collider[64];
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly HashSet<DamageReceiver> struck = new HashSet<DamageReceiver>();
        float elapsed, duration, clock, locomotionTime, nextBite, nextTail, nextBreath, rearTime, nextTick, chargeTravel, chargeLimit, nextSearch;
        bool initialized, pendingStagger, phaseReported, pursuitCharge, followingTarget;
        float nextPursuitCharge, nextPursuitProbe;
        float staggerSeconds;
        long attackId;
        SoulEaterAction lastAction;
        int repetitions;
        public SoulEaterPhaseOneSettings Settings => settings;
        public Health Health => health;
        public CombatState Combat => combat;
        public SoulEaterState State { get; private set; }
        public SoulEaterAction Action { get; private set; }
        public Transform Target => target;
        public bool ChargeUsed { get; private set; }
        public bool PhaseOneComplete => State == SoulEaterState.PhaseTwoReady;
        public float Progress => duration > 0 ? Mathf.Clamp01(elapsed / duration) : 0;
        public Vector3 MouthPosition => upperMouth != null && lowerMouth != null ? (upperMouth.position + lowerMouth.position) * .5f : transform.position + transform.forward * 3 + Vector3.up * 2;
        public Vector3 LockedDirection => aim;
        public Vector3 RetreatDestination => jumpEnd;
        public event Action PhaseTwoRequested;
        public event Action Defeated;
        public void SetAutomaticEngagement(bool value) => autoEngage=value;
        float arenaRadiusOverride;
        float ArenaRadius => arenaRadiusOverride>0?arenaRadiusOverride:settings.arenaRadius;
        public void SetArenaRadius(float value) => arenaRadiusOverride=Mathf.Max(12,value);

        public void Configure(SoulEaterPhaseOneSettings data, VoxelRigInstance visual, Transform upper, Transform lower, Transform[] tail, SphereCollider head, SoulEaterEffects presentation)
        { settings = data; rig = visual; upperMouth = upper; lowerMouth = lower; tailBones = tail; headCollider = head; groundBody=GetComponent<BoxCollider>(); effects = presentation; }
        void Awake()
        {
            health = GetComponent<Health>(); body = GetComponent<CapsuleCollider>(); agent = GetComponent<NavMeshAgent>();
            if(groundBody==null)groundBody=GetComponent<BoxCollider>();
            combat = GetComponent<CombatState>() ?? gameObject.AddComponent<CombatState>();
        }
        void OnEnable()
        {
            health.Died += Die; health.Damaged += HurtSound; combat.PostureBroken += PostureBroken; PlayerMusic.RegisterBoss(this);
            if (initialized) ResetEncounter();
        }
        void OnDisable()
        {
            health.Died -= Die; health.Damaged -= HurtSound; combat.PostureBroken -= PostureBroken; PlayerMusic.UnregisterBoss(this);
            StopNavigation(); battlefield?.Clear(); effects?.StopAll(); animation?.Dispose(); animation = null;
        }
        void Start()
        {
            if (settings == null || rig == null || rig.animator == null) { Debug.LogError("SoulEater necesita settings y rig.", this); enabled = false; return; }
            InitializePresentation();InitializePhaseTwo();
            home = transform.position; homeRotation = transform.rotation; initialized = true;
            health.ConfigureMaximum(settings.health); combat.ConfigurePosture(settings.posture);
            GetComponent<ActorCombatVisuals>()?.DelayDeathEffect(settings.die != null ? settings.die.length : 2);
            ResetEncounter();
        }
        void Update() => Tick(Time.deltaTime);
        public void ResetEncounter()
        {
            if (!initialized) return;
            ClearPhaseTwo();localPath?.Clear();routeMoving=followingTarget=pursuitCharge=false;nextPath=nextPursuitCharge=nextPursuitProbe=movingUntil=0;
            StopNavigation(); transform.SetPositionAndRotation(home, homeRotation); health.Revive(); combat.ResetCombat();
            target = null; targetHealth = null; ChargeUsed = phaseReported = pendingStagger = false;
            propRetryAt=0;
            clock = locomotionTime = nextBite = nextTail = nextBreath = rearTime = nextSearch = 0;
            lastAction = SoulEaterAction.None; repetitions = 0;
            body.enabled = true; if(groundBody!=null)groundBody.enabled=true; if (headCollider != null) headCollider.enabled = true;
            animation ??= new SoulEaterAnimation(rig.animator); RestoreNavigation();
            Enter(SoulEaterState.Dormant); Pose(0, true);SupportFeet(); effects?.StopAll();
        }
        public void BeginEncounter(Transform player)
        {
            if (!initialized || player == null || State == SoulEaterState.Dead || PhaseOneComplete) return;
            targetHealth = player.GetComponentInParent<Health>(); target = targetHealth != null ? targetHealth.transform : player;
            if (State == SoulEaterState.Dormant) { Enter(SoulEaterState.Hunting, DecisionPause); effects?.Cue(SoulEaterCue.Emerge); }
        }
        public bool IsFightingPlayer(Transform player) => initialized && isActiveAndEnabled && target != null &&
            State != SoulEaterState.Dormant && State != SoulEaterState.Returning && State != SoulEaterState.Dead && !PhaseOneComplete &&
            (target == player || target.IsChildOf(player));

        public void Tick(float deltaTime)
        {
            if (!initialized || !enabled || deltaTime <= 0) return;
            // Substeps keep fast charges and attack boundaries safe even on a slow frame.
            float remaining = Mathf.Min(deltaTime, .5f);
            while (remaining > .00001f) { float dt = Mathf.Min(remaining, 1f / 60); Step(dt); remaining -= dt; }
        }
        void Step(float dt)
        {
            elapsed += dt; clock += dt; locomotionTime += dt;
            battlefield?.Tick(dt);
            if (State == SoulEaterState.Dead) { Pose(dt); return; }
            if (health.IsDead) { Die(default); return; }
            if (State == SoulEaterState.Dormant && autoEngage && clock >= nextSearch)
            {
                nextSearch = clock + .5f;
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null && Vector3.Distance(home, player.transform.position) <= settings.detectionRange && ClearLine(MouthPosition, player.transform.position + Vector3.up, player.transform))
                    BeginEncounter(player.transform);
            }
            bool valid = target != null && (targetHealth == null || !targetHealth.IsDead);
            if (State != SoulEaterState.Dormant && State != SoulEaterState.Returning && !PhaseOneComplete &&
                !valid)
            { LandSafely(); battlefield?.Clear(); Enter(SoulEaterState.Returning); }

            switch (State)
            {
                case SoulEaterState.Hunting: Hunt(dt); break;
                case SoulEaterState.Windup:
                    if (Action == SoulEaterAction.Bite && Progress < settings.biteAimLock) { Face(target.position, dt); aim = (TargetPoint-MouthPosition).normalized; }
                    if (elapsed >= duration)
                    {
                        Enter(SoulEaterState.Active, settings.Active(Action), true); CapturePreviousPose();
                        if(effects!=null&&effects.AudioProfile!=null&&Action!=SoulEaterAction.Breath)
                            effects.Cue(Action==SoulEaterAction.Tail?SoulEaterCue.Tail:SoulEaterCue.Bite);
                    }
                    break;
                case SoulEaterState.Active:
                    if (elapsed >= duration) Enter(SoulEaterState.Recovery, settings.Recovery(Action), true);
                    break;
                case SoulEaterState.Recovery:
                case SoulEaterState.Staggered:
                    if (elapsed >= duration) Enter(SoulEaterState.Hunting, DecisionPause);
                    break;
                case SoulEaterState.SpecialRoar:
                    if (elapsed >= duration)
                    {
                        if (TryRetreat(out jumpEnd)) { jumpStart = transform.position; DisableNavigation(); Enter(SoulEaterState.RetreatJump, settings.jumpDuration); effects?.Cue(SoulEaterCue.Jump); }
                        else { aim = Planar(TargetPoint - transform.position); Enter(SoulEaterState.ChargeWindup, settings.chargeWindup); }
                    }
                    break;
                case SoulEaterState.RetreatJump:
                    transform.position = Vector3.Lerp(jumpStart, jumpEnd, Progress) + Vector3.up * (Mathf.Sin(Progress * Mathf.PI) * settings.jumpHeight);
                    Face(target.position, dt, 2);
                    if (elapsed >= duration)
                    {
                        transform.position = jumpEnd; effects?.GroundImpact(transform.position, 1);
                        if (pendingStagger) { pendingStagger = false; Enter(SoulEaterState.Staggered, staggerSeconds); }
                        else Enter(SoulEaterState.ChargeWindup, settings.chargeWindup);
                    }
                    break;
                case SoulEaterState.ChargeWindup:
                    if (Progress < settings.chargeAimLock) Face(target.position, dt, 4);
                    if (elapsed >= duration)
                    {
                        chargeDirection = transform.forward; aim = chargeDirection; chargeTravel = 0; attackId = AttackIdentity.Next(); struck.Clear();
                        chargeLimit = Mathf.Min(pursuitCharge?settings.pursuitChargeMaxDistance:settings.chargeDistance, Planar(target.position-transform.position).magnitude+4);
                        DisableNavigation(); Enter(SoulEaterState.Charging, chargeLimit / settings.chargeSpeed); effects?.Cue(SoulEaterCue.Charge);
                    }
                    break;
                case SoulEaterState.Charging:
                    Vector3 old = transform.position;
                    bool moved = MoveGround(chargeDirection * settings.chargeSpeed * dt);
                    chargeTravel += Vector3.Distance(old, transform.position);
                    ChargeHits(old, transform.position);
                    if (State == SoulEaterState.Charging && (!moved || elapsed >= duration || chargeTravel >= chargeLimit)) { Enter(SoulEaterState.Braking, settings.brakeDuration); }
                    break;
                case SoulEaterState.Braking:
                    if (Progress < settings.brakeTravelFraction) MoveGround(chargeDirection * (settings.chargeSpeed * settings.brakeSpeedFraction * (1 - Progress / Mathf.Max(.01f,settings.brakeTravelFraction)) * dt));
                    if (elapsed >= duration) { RestoreNavigation(); Enter(SoulEaterState.Hunting, DecisionPause); }
                    break;
                case SoulEaterState.PhaseTransition:
                    if (elapsed >= duration)
                    {
                        Enter(SoulEaterState.PhaseTwoReady);
                        if (!phaseReported) { phaseReported = true; PhaseTwoRequested?.Invoke(); }
                        if(settings.enablePhaseTwo)BeginPhaseTwo();
                    }
                    break;
                case SoulEaterState.Returning:
                    if (Planar(transform.position - home).magnitude < .3f) { ResetEncounter(); break; }
                    Face(home, dt); MoveTowards(home, dt); break;
            }
            AerialStep(dt);TrackBreathBody(dt);Pose(dt);
            rig.transform.localRotation=visualRest*Quaternion.Euler(State==SoulEaterState.Diving?Mathf.Lerp(20,65,Progress):0,0,0);
            SupportFeet();AimBreathHead(dt);ChargeDustContact();
            if (headCollider != null) headCollider.transform.position = MouthPosition - transform.forward * (body.radius * (.25f / 1.4f));
            if (State == SoulEaterState.Active)
            {
                if (Action == SoulEaterAction.Breath)
                {
                    float reach = BreathReach(Mathf.Min(settings.breathRange, elapsed * settings.breathPropagationSpeed));
                    effects?.Breath(MouthPosition, aim, reach, settings.breathHalfAngle, dt, battlefield.GroundY(MouthPosition),settings.breathBaseHalfWidth,settings.breathGroundBackreach);
                    if (clock >= nextTick) { breathReleased = true; nextTick = clock + settings.breathTickInterval; BreathHit(reach); }
                    LeaveGroundFire(reach);
                }
                else MeleeHits();
            }
            else if(State!=SoulEaterState.AerialBreath)effects?.Prepare(MouthPosition, aim, State == SoulEaterState.Windup && Action == SoulEaterAction.Breath ? Progress : 0, dt);
            if (State == SoulEaterState.SpecialRoar || State == SoulEaterState.PhaseTransition) effects?.EyeIntensity(1 + Mathf.Sin(Progress * Mathf.PI) * 3);
            AerialPresentation(dt);
            CapturePreviousPose();
        }
        Vector3 TargetPoint => target != null ? target.position + Vector3.up : transform.position + transform.forward * 10;
        void Hunt(float dt)
        {
            if (elapsed < duration || target == null) return;
            if (!phaseTwo && health.Normalized <= settings.phaseThreshold) { Enter(SoulEaterState.PhaseTransition, settings.phaseRoar); effects?.Cue(SoulEaterCue.Roar); return; }
            if(PhaseTwoDecision())return;
            if (!phaseTwo && !ChargeUsed && health.Normalized <= settings.chargeThreshold) { ChargeUsed = true; Enter(SoulEaterState.SpecialRoar, settings.specialRoar); effects?.Cue(SoulEaterCue.Roar); return; }
            Vector3 delta = Planar(target.position - transform.position); float distance = delta.magnitude;
            if(TryPursuitCharge(delta))return;
            float facing = Vector3.Dot(transform.forward, delta.normalized);
            rearTime = facing < -.3f && distance <= settings.tailRange ? rearTime + dt : 0;
            if (rearTime >= settings.rearDwellTime && clock >= nextTail && ClearLine(transform.position + Vector3.up, TargetPoint, target)) { TryStartAttack(SoulEaterAction.Tail); return; }
            // Give the rear player time to read the tail; do not spin away faster than its warning.
            if ((facing >= -.3f || distance > settings.tailRange) && localPath?.HasRoute!=true && localPath?.Searching!=true) Face(target.position, dt);
            if (facing > .55f && ClearLine(MouthPosition, TargetPoint, target))
            {
                if (distance <= settings.biteRange && clock >= nextBite && (lastAction != SoulEaterAction.Bite || repetitions < 2 || clock < nextBreath))
                { TryStartAttack(SoulEaterAction.Bite); return; }
                if (distance > settings.biteRange * .75f && Planar(target.position-MouthPosition).magnitude <= settings.breathRange && clock >= nextBreath)
                { TryStartAttack(SoulEaterAction.Breath); return; }
            }
            float stop=settings.biteRange*.85f;
            bool blocked=!ClearLine(MouthPosition,TargetPoint,target);
            followingTarget=blocked || distance>(followingTarget?stop:stop+settings.pursuitStopMargin);
            if(followingTarget)MoveTowards(target.position,dt);
            else {routeMoving=false;movingUntil=0;localPath?.Clear();StopNavigation();}
        }
        // Also used by the attack workshop and deterministic combat checks.
        public bool TryStartAttack(SoulEaterAction action)
        {
            if(action==SoulEaterAction.Dive||action==SoulEaterAction.AerialBreath)return TryStartAerial(action);
            if (!initialized || State != SoulEaterState.Hunting || target == null || action == SoulEaterAction.None) return false;
            Action = action; attackId = AttackIdentity.Next(); struck.Clear(); nextTick = clock;
            repetitions = lastAction == action ? repetitions + 1 : 1; lastAction = action;
            float total = settings.Windup(action) + settings.Active(action) + settings.Recovery(action);
            if (action == SoulEaterAction.Bite) nextBite = clock + total + settings.biteCooldown;
            if (action == SoulEaterAction.Tail) { nextTail = clock + total + settings.tailCooldown; rearTime = 0; }
            if (action == SoulEaterAction.Breath) nextBreath = clock + total + settings.breathCooldown;
            aim = Action==SoulEaterAction.Breath?GroundBreathDirection():(TargetPoint-MouthPosition).normalized;
            breathReleased=false;nextFire=clock;
            Enter(SoulEaterState.Windup, settings.Windup(action), true);
            if(action==SoulEaterAction.Breath||effects==null||effects.AudioProfile==null)
                effects?.Cue(action == SoulEaterAction.Breath ? SoulEaterCue.Inhale : action == SoulEaterAction.Tail ? SoulEaterCue.Tail : SoulEaterCue.Bite);
            return true;
        }
        void Enter(SoulEaterState next, float seconds = 0, bool keepAction = false)
        {
            if(pursuitCharge && next!=SoulEaterState.ChargeWindup && next!=SoulEaterState.Charging && next!=SoulEaterState.Braking)
            {pursuitCharge=false;nextPursuitCharge=clock+settings.pursuitChargeCooldown;}
            routeMoving=false;movingUntil=0;
            StopNavigation(); effects?.StopBreath(); effects?.EyeIntensity(1);
            if(next==SoulEaterState.Staggered||next==SoulEaterState.Returning)effects?.CancelVoice();
            if(next!=SoulEaterState.Hunting && next!=SoulEaterState.Returning)localPath?.Clear();
            if(next==SoulEaterState.ChargeWindup||next==SoulEaterState.Braking){chargeDustArmed=false;chargeDustDone=false;}
            State = next; elapsed = 0; duration = seconds;
            if (!keepAction) Action = SoulEaterAction.None;
            combat.Recovering = next == SoulEaterState.ImpactRecovery || next == SoulEaterState.Recovery || next == SoulEaterState.Braking || next == SoulEaterState.Staggered;
        }
        void Pose(float dt, bool immediate = false)
        {
            animation ??= new SoulEaterAnimation(rig.animator);
            AnimationClip clip = settings.idle; float p = Loop(clip);
            if (State == SoulEaterState.Windup || State == SoulEaterState.Active || State == SoulEaterState.Recovery)
            {
                clip = settings.Clip(Action);
                float start = Action == SoulEaterAction.Tail ? .32f : Action == SoulEaterAction.Breath ? .48f : .38f;
                float end = Action == SoulEaterAction.Tail ? .8f : Action == SoulEaterAction.Breath ? .58f : .7f;
                p = State == SoulEaterState.Windup ? Mathf.Lerp(0, start, Progress) : State == SoulEaterState.Active ? Mathf.Lerp(start, end, Progress) : Mathf.Lerp(end, 1, Progress);
            }
            else if (State == SoulEaterState.SpecialRoar || State == SoulEaterState.PhaseTransition) { clip = settings.roar; p = Progress; }
            else if (State == SoulEaterState.RetreatJump)
            {
                float change=Mathf.Clamp(settings.retreatPoseSwitch,.3f,.7f);
                bool rising=Progress<change;clip=rising?settings.takeOff:settings.land;
                p=rising?Mathf.Lerp(0,settings.retreatTakeOffEnd,Progress/change):Mathf.Lerp(settings.retreatLandStart,1,(Progress-change)/(1-change));
            }
            else if (State == SoulEaterState.ChargeWindup) { clip = settings.chargePose != null ? settings.chargePose : settings.run; p = Progress; }
            else if (State == SoulEaterState.Charging) { clip = settings.run; p = Mathf.Repeat(elapsed * 1.6f / Mathf.Max(.1f, clip.length), 1); }
            else if (State == SoulEaterState.Braking) { clip = settings.brake != null ? settings.brake : settings.land; p = Progress; }
            else if (State == SoulEaterState.Staggered) { clip = settings.hit; p = Progress; }
            else if (State == SoulEaterState.Dead) { clip = settings.die; p = Progress; }
            else if (State == SoulEaterState.Returning || State == SoulEaterState.Hunting && (routeMoving || clock<movingUntil)) { clip = settings.walk; p = Loop(clip); }
            AerialPose(ref clip,ref p);
            rig.transform.localPosition = visualPosition;
            animation.Sample(clip, p, dt, immediate);
            bool wingMotion=State==SoulEaterState.AerialAim||State==SoulEaterState.AerialBreath||State==SoulEaterState.Ascending&&Progress>=.25f;
            bool footsteps=State==SoulEaterState.Charging||State==SoulEaterState.Returning||State==SoulEaterState.Hunting&&(routeMoving||clock<movingUntil);
            effects?.AnimateMovement(clip,p,wingMotion,footsteps);
        }
        float Loop(AnimationClip c) => c != null ? Mathf.Repeat(locomotionTime / Mathf.Max(.1f, c.length), 1) : 0;
        void CapturePreviousPose()
        {
            previousMouth = MouthPosition;
            if (tailBones == null) return;
            if (previousTail == null || previousTail.Length != tailBones.Length) previousTail = new Vector3[tailBones.Length];
            for (int i = 0; i < tailBones.Length; i++) previousTail[i] = tailBones[i].position;
        }
        void MeleeHits()
        {
            if (Action == SoulEaterAction.Bite) HitCapsule(previousMouth, MouthPosition, settings.biteRadius, settings.biteDamage, true);
            else if (tailBones != null)
            {
                for (int i = 1; i < tailBones.Length; i++)
                {
                    HitCapsule(tailBones[i - 1].position, tailBones[i].position, settings.tailRadius, settings.tailDamage, false);
                    if (previousTail != null) HitCapsule(previousTail[i], tailBones[i].position, settings.tailRadius, settings.tailDamage, false);
                }
            }
        }
        void HitCapsule(Vector3 a, Vector3 b, float radius, float damage, bool parryable)
        {
            int count = Physics.OverlapCapsuleNonAlloc(a, b, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var receiver = overlaps[i].GetComponentInParent<DamageReceiver>();
                if (!IsTarget(receiver) || struck.Contains(receiver) || !ClearLine(a, receiver.transform.position + Vector3.up, receiver.transform)) continue;
                struck.Add(receiver);
                receiver.ReceiveDamage(new DamageInfo(damage, gameObject, overlaps[i].ClosestPoint(b), receiver.transform.position - transform.position,
                    attackId, origin: a, parryable: parryable));
                if (State != SoulEaterState.Active && State != SoulEaterState.Charging) break;
            }
        }
        bool IsTarget(DamageReceiver receiver) => receiver != null && target != null && receiver.transform == target && !receiver.Health.IsDead;
        void ChargeHits(Vector3 a, Vector3 b) { var offset=transform.rotation*settings.chargeHitOffset; HitCapsule(a+offset,b+offset,settings.chargeHitRadius,settings.chargeDamage,false); }
        void BreathHit(float reach)
        {
            if (target == null) return;
            var receiver = target.GetComponent<DamageReceiver>(); if (!IsTarget(receiver)) return;
            Vector3 point = TargetPoint;
            var targetCollider=target.GetComponent<Collider>();
            if(GroundBreath)
            {
                Vector3 mouth=MouthPosition,flat=Planar(aim).normalized,right=Vector3.Cross(Vector3.up,flat);
                float floor=battlefield.GroundY(mouth),along=Vector3.Dot(point-mouth,flat);
                float top=mouth.y+aim.y*Mathf.Max(0,along)+.22f+Mathf.Max(0,along)*Mathf.Tan(settings.breathHalfAngle*Mathf.Deg2Rad);
                var axis=mouth+flat*Mathf.Clamp(along,-settings.breathGroundBackreach,reach);axis.y=Mathf.Clamp(point.y,floor,Mathf.Max(floor,top));
                if(targetCollider!=null)point=targetCollider.ClosestPoint(axis);
                along=Vector3.Dot(point-mouth,flat);
                float width=.22f+settings.breathBaseHalfWidth+Mathf.Max(0,along)*Mathf.Tan(settings.breathHalfAngle*Mathf.Deg2Rad);
                if(along < -settings.breathGroundBackreach || along>reach || Mathf.Abs(Vector3.Dot(point-mouth,right))>width || point.y<floor-.1f || point.y>top || !ClearLine(mouth,point,target))return;
                receiver.ReceiveDamage(new DamageInfo(settings.breathTickDamage,gameObject,point,aim,AttackIdentity.Next(),area:true,origin:mouth,parryable:false));return;
            }
            if(targetCollider!=null)
            {
                float along=Mathf.Clamp(Vector3.Dot(targetCollider.bounds.center-MouthPosition,aim),0,reach);
                point=targetCollider.ClosestPoint(MouthPosition+aim*along);
            }
            Vector3 delta = point - MouthPosition; float forward = Vector3.Dot(delta, aim);
            float radius = .22f + forward * Mathf.Tan(settings.breathHalfAngle * Mathf.Deg2Rad);
            if (forward < 0 || forward > reach || (delta - aim * forward).sqrMagnitude > radius * radius || !ClearLine(MouthPosition, point, target)) return;
            receiver.ReceiveDamage(new DamageInfo(settings.breathTickDamage, gameObject, point, aim, AttackIdentity.Next(), area: true, origin: MouthPosition, parryable: false));
        }
        float BreathReach(float maximum)
        {
            int n=Physics.RaycastNonAlloc(MouthPosition,aim,hits,maximum,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)if(!OwnOrTarget(hits[i].transform))maximum=Mathf.Min(maximum,hits[i].distance);
            return maximum;
        }
        void PostureBroken(float seconds)
        {
            if (!initialized || State == SoulEaterState.Dead || PhaseOneComplete || State == SoulEaterState.PhaseTransition) return;
            if (State == SoulEaterState.RetreatJump || Airborne) { pendingStagger = true; staggerSeconds = seconds; return; }
            RestoreNavigation(); Enter(SoulEaterState.Staggered, seconds);
        }
        public void OnAttackParried(DamageInfo damage)
        {
            if (State != SoulEaterState.Active || Action != SoulEaterAction.Bite || damage.AttackId != attackId) return;
            Enter(SoulEaterState.Staggered, combat.Broken ? Mathf.Max(2, settings.parryRecovery) : settings.parryRecovery);
        }
        void HurtSound(DamageInfo damage)
        {
            if(initialized&&!health.IsDead&&damage.StatusEffect==StatusEffectType.None)effects?.Cue(SoulEaterCue.Hurt);
        }
        void Die(DamageInfo _)
        {
            if (!initialized || State == SoulEaterState.Dead) return;
            battlefield?.Clear();rig.transform.localRotation=visualRest;LandSafely(); Enter(SoulEaterState.Dead, settings.die != null ? settings.die.length : 2); DisableNavigation();
            body.enabled = false; if(groundBody!=null)groundBody.enabled=false; if (headCollider != null) headCollider.enabled = false; effects?.StopAll(); effects?.Cue(SoulEaterCue.Death); Defeated?.Invoke();
        }
        static Vector3 Planar(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up);
        void Face(Vector3 p, float dt, float multiplier = 1)
        { Vector3 d = Planar(p - transform.position); if (d.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), settings.turnSpeed * multiplier * dt); }
        bool Navigating => agent != null && agent.enabled && agent.isOnNavMesh;
        void StopNavigation() { if (Navigating) { agent.isStopped = true; agent.ResetPath(); } }
        void DisableNavigation() { StopNavigation(); if (agent != null) agent.enabled = false; }
        void RestoreNavigation()
        {
            if (agent != null && Ground(transform.position,out var floor) && NavMesh.SamplePosition(transform.position, out var p, 2, agent.areaMask) && Mathf.Abs(p.position.y-floor.y)<settings.groundStepHeight)
            {
                // Navigation chooses a route; physical terrain supplies the actual height, including fresh craters.
                var position=transform.position;agent.enabled=true;agent.updateRotation=false;agent.updatePosition=false;
                agent.Warp(p.position);transform.position=position;agent.nextPosition=position;
            }
        }
        void MoveTowards(Vector3 destination, float dt)
        {
            FollowGroundPath(destination,dt);
            if(Navigating)agent.nextPosition=transform.position;
        }

        bool MoveGround(Vector3 step)
        {
            if(step.sqrMagnitude<.000001f)return true;
            Vector3 destination=transform.position+step;
            if(!Ground(destination,out var ground) || Mathf.Abs(ground.y-transform.position.y)>settings.groundStepHeight)return false;
            ClearPropsForStep(step,ground.y);
            if(!GroundSweep(step,ground.y))return false;
            transform.position=ground;
            if(Navigating)agent.nextPosition=ground;
            return true;
        }
        bool GroundSweep(Vector3 step,float groundY,bool includeTarget=true,bool allowProps=false)
        {
            // Planning can pass through eligible trees. Actual movement still requires their colliders to be removed.
            float baseY=Mathf.Max(transform.position.y,groundY)+settings.groundStepHeight;
            int n;
            if(groundBody!=null)
            {
                var center=transform.TransformPoint(groundBody.center);center.y=baseY+groundBody.size.y*.5f;
                n=Physics.BoxCastNonAlloc(center,groundBody.size*.5f,step.normalized,hits,transform.rotation,step.magnitude+.05f,~0,QueryTriggerInteraction.Ignore);
            }
            else
            {
                var bottom=new Vector3(transform.position.x,baseY+body.radius,transform.position.z);
                var top=bottom+Vector3.up*Mathf.Max(0,body.height-2*body.radius);
                n=Physics.CapsuleCastNonAlloc(bottom,top,body.radius*.9f,step.normalized,hits,step.magnitude+.05f,~0,QueryTriggerInteraction.Ignore);
            }
            if(n==hits.Length)return false;
            for(int i=0;i<n;i++)if(!hits[i].transform.IsChildOf(transform) && (includeTarget || target==null || !hits[i].transform.IsChildOf(target)) && hits[i].normal.y<.6f && !(allowProps&&PropCanBeCleared(hits[i].collider)))return false;
            return true;
        }
        bool TryRetreat(out Vector3 destination)
        {
            Vector3 away = Planar(transform.position - target.position).normalized;
            foreach (float distance in new[] { settings.retreatDistance, settings.retreatDistance * .65f, settings.retreatDistance * .4f })
                foreach (float angle in new[] { 0f, 35f, -35f, 70f, -70f })
                {
                    Vector3 candidate = transform.position + Quaternion.Euler(0, angle, 0) * away * distance;
                    if (!Ground(candidate, out candidate)) continue;
                    if (Mathf.Abs(candidate.y - transform.position.y) > 1) continue;
                    bool blocked = false;
                    for (int sample = 1; sample <= 10 && !blocked; sample++)
                    {
                        float t = sample / 10f; Vector3 p = Vector3.Lerp(transform.position, candidate, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * settings.jumpHeight);
                        int n = groundBody!=null ? Physics.OverlapBoxNonAlloc(p+transform.rotation*groundBody.center+Vector3.up*.06f,new Vector3(groundBody.size.x*.5f,groundBody.size.y*.5f-.05f,groundBody.size.z*.5f),overlaps,transform.rotation,~0,QueryTriggerInteraction.Ignore) : Physics.OverlapCapsuleNonAlloc(p+body.center,p+body.center,body.radius*.85f,overlaps,~0,QueryTriggerInteraction.Ignore);
                        for (int i = 0; i < n; i++) if (!OwnOrTarget(overlaps[i].transform)) { blocked = true; break; }
                    }
                    if (blocked || !ClearLine(candidate + Vector3.up, TargetPoint, target)) continue;
                    destination = candidate; return true;
                }
            destination = transform.position; return false;
        }
        bool Ground(Vector3 p, out Vector3 ground)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 8, Vector3.down, hits, 20, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity; ground = p;
            for (int i = 0; i < n; i++) if (!OwnOrTarget(hits[i].transform) && hits[i].collider.GetComponentInParent<Health>() == null && hits[i].collider.GetComponentInParent<WorldDestructible>() == null && hits[i].normal.y > .7f && hits[i].distance < nearest)
            { nearest = hits[i].distance; ground = hits[i].point; }
            if(!float.IsPositiveInfinity(nearest))return true;
            // A distant pursuer may be outside rendered chunks. Keep following the same world heightfield.
            return battlefield!=null && battlefield.TryTerrainPoint(p,out ground);
        }
        bool OwnOrTarget(Transform t) => t.IsChildOf(transform) || target != null && t.IsChildOf(target);
        bool ClearLine(Vector3 a, Vector3 b, Transform recipient,bool allowProps=false)
        {
            Vector3 delta = b - a; int n = Physics.RaycastNonAlloc(a, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if(n==hits.Length)return false;
            for (int i = 0; i < n; i++) if (!hits[i].transform.IsChildOf(transform) && (recipient == null || !hits[i].transform.IsChildOf(recipient)) && !(allowProps&&PropCanBeCleared(hits[i].collider))) return false;
            return true;
        }
        void LandSafely() { var probe=transform.position;probe.y=home.y;if (Ground(probe, out var p)) transform.position = p; else if (Ground(home, out p)) transform.position = p; RestoreNavigation(); }
    }
}
