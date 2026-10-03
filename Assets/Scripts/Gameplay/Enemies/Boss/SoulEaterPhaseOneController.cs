using System;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Voxels;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health), typeof(DamageReceiver), typeof(CapsuleCollider))]
    public sealed class SoulEaterPhaseOneController : MonoBehaviour, IParryResponder, IBossMusicThreat
    {
        [SerializeField] SoulEaterPhaseOneSettings settings;
        [SerializeField] VoxelRigInstance rig;
        [SerializeField] Transform upperMouth, lowerMouth;
        [SerializeField] Transform[] tailBones;
        [SerializeField] SphereCollider headCollider;
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
        bool initialized, pendingStagger, phaseReported;
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

        public void Configure(SoulEaterPhaseOneSettings data, VoxelRigInstance visual, Transform upper, Transform lower, Transform[] tail, SphereCollider head, SoulEaterEffects presentation)
        { settings = data; rig = visual; upperMouth = upper; lowerMouth = lower; tailBones = tail; headCollider = head; effects = presentation; }
        void Awake()
        {
            health = GetComponent<Health>(); body = GetComponent<CapsuleCollider>(); agent = GetComponent<NavMeshAgent>();
            combat = GetComponent<CombatState>() ?? gameObject.AddComponent<CombatState>();
        }
        void OnEnable()
        {
            health.Died += Die; combat.PostureBroken += PostureBroken; PlayerMusic.RegisterBoss(this);
            if (initialized) ResetEncounter();
        }
        void OnDisable()
        {
            health.Died -= Die; combat.PostureBroken -= PostureBroken; PlayerMusic.UnregisterBoss(this);
            StopNavigation(); effects?.StopAll(); animation?.Dispose(); animation = null;
        }
        void Start()
        {
            if (settings == null || rig == null || rig.animator == null) { Debug.LogError("SoulEater necesita settings y rig.", this); enabled = false; return; }
            home = transform.position; homeRotation = transform.rotation; initialized = true;
            health.ConfigureMaximum(settings.health); combat.ConfigurePosture(settings.posture);
            GetComponent<ActorCombatVisuals>()?.DelayDeathEffect(settings.die != null ? settings.die.length : 2);
            ResetEncounter();
        }
        void Update() => Tick(Time.deltaTime);
        public void ResetEncounter()
        {
            if (!initialized) return;
            StopNavigation(); transform.SetPositionAndRotation(home, homeRotation); health.Revive(); combat.ResetCombat();
            target = null; targetHealth = null; ChargeUsed = phaseReported = pendingStagger = false;
            clock = locomotionTime = nextBite = nextTail = nextBreath = rearTime = nextSearch = 0;
            lastAction = SoulEaterAction.None; repetitions = 0;
            body.enabled = true; if (headCollider != null) headCollider.enabled = true;
            animation ??= new SoulEaterAnimation(rig.animator); RestoreNavigation();
            Enter(SoulEaterState.Dormant); Pose(0, true); effects?.StopAll();
        }
        public void BeginEncounter(Transform player)
        {
            if (!initialized || player == null || State == SoulEaterState.Dead || PhaseOneComplete) return;
            targetHealth = player.GetComponentInParent<Health>(); target = targetHealth != null ? targetHealth.transform : player;
            if (State == SoulEaterState.Dormant) Enter(SoulEaterState.Hunting, settings.decisionPause);
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
                (!valid || Vector3.Distance(home, target.position) > settings.arenaRadius + 8))
            { LandSafely(); Enter(SoulEaterState.Returning); }

            switch (State)
            {
                case SoulEaterState.Hunting: Hunt(dt); break;
                case SoulEaterState.Windup:
                    if (Action != SoulEaterAction.Tail && Progress < .72f) { Face(target.position, dt); aim = (TargetPoint - MouthPosition).normalized; }
                    if (elapsed >= duration) { Enter(SoulEaterState.Active, settings.Active(Action), true); CapturePreviousPose(); }
                    break;
                case SoulEaterState.Active:
                    if (elapsed >= duration) Enter(SoulEaterState.Recovery, settings.Recovery(Action), true);
                    break;
                case SoulEaterState.Recovery:
                case SoulEaterState.Staggered:
                    if (elapsed >= duration) Enter(SoulEaterState.Hunting, settings.decisionPause);
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
                    if (Progress < .72f) Face(target.position, dt, 4);
                    if (elapsed >= duration)
                    {
                        chargeDirection = transform.forward; aim = chargeDirection; chargeTravel = 0; attackId = AttackIdentity.Next(); struck.Clear();
                        chargeLimit = Mathf.Min(settings.chargeDistance, Planar(target.position-transform.position).magnitude+4);
                        DisableNavigation(); Enter(SoulEaterState.Charging, chargeLimit / settings.chargeSpeed); effects?.Cue(SoulEaterCue.Charge);
                    }
                    break;
                case SoulEaterState.Charging:
                    Vector3 old = transform.position;
                    bool moved = MoveGround(chargeDirection * settings.chargeSpeed * dt);
                    chargeTravel += Vector3.Distance(old, transform.position);
                    ChargeHits(old, transform.position);
                    if (State == SoulEaterState.Charging && (!moved || elapsed >= duration || chargeTravel >= chargeLimit)) { Enter(SoulEaterState.Braking, settings.brakeDuration); effects?.GroundImpact(transform.position, .65f); }
                    break;
                case SoulEaterState.Braking:
                    if (Progress < .35f) MoveGround(chargeDirection * (settings.chargeSpeed * .3f * (1 - Progress / .35f) * dt));
                    if (elapsed >= duration) { RestoreNavigation(); Enter(SoulEaterState.Hunting, settings.decisionPause); }
                    break;
                case SoulEaterState.PhaseTransition:
                    if (elapsed >= duration)
                    {
                        Enter(SoulEaterState.PhaseTwoReady);
                        if (!phaseReported) { phaseReported = true; PhaseTwoRequested?.Invoke(); }
                    }
                    break;
                case SoulEaterState.Returning:
                    if (Planar(transform.position - home).magnitude < .3f) { ResetEncounter(); break; }
                    Face(home, dt); MoveTowards(home, dt); break;
            }
            Pose(dt);
            if (headCollider != null) headCollider.transform.position = MouthPosition - transform.forward * .25f;
            if (State == SoulEaterState.Active)
            {
                if (Action == SoulEaterAction.Breath)
                {
                    float reach = BreathReach(Mathf.Min(settings.breathRange, elapsed * 24));
                    effects?.Breath(MouthPosition, aim, reach, settings.breathHalfAngle, dt);
                    if (clock >= nextTick) { nextTick = clock + settings.breathTickInterval; BreathHit(reach); }
                }
                else MeleeHits();
            }
            else effects?.Prepare(MouthPosition, aim, State == SoulEaterState.Windup && Action == SoulEaterAction.Breath ? Progress : 0, dt);
            if (State == SoulEaterState.SpecialRoar || State == SoulEaterState.PhaseTransition) effects?.EyeIntensity(1 + Mathf.Sin(Progress * Mathf.PI) * 3);
            CapturePreviousPose();
        }
        Vector3 TargetPoint => target != null ? target.position + Vector3.up : transform.position + transform.forward * 10;
        void Hunt(float dt)
        {
            if (elapsed < duration || target == null) return;
            if (health.Normalized <= settings.phaseThreshold) { Enter(SoulEaterState.PhaseTransition, settings.phaseRoar); effects?.Cue(SoulEaterCue.Roar); return; }
            if (!ChargeUsed && health.Normalized <= settings.chargeThreshold) { ChargeUsed = true; Enter(SoulEaterState.SpecialRoar, settings.specialRoar); effects?.Cue(SoulEaterCue.Roar); return; }
            Vector3 delta = Planar(target.position - transform.position); float distance = delta.magnitude;
            float facing = Vector3.Dot(transform.forward, delta.normalized);
            rearTime = facing < -.3f && distance <= settings.tailRange ? rearTime + dt : 0;
            if (rearTime >= settings.rearDwellTime && clock >= nextTail && ClearLine(transform.position + Vector3.up, TargetPoint, target)) { TryStartAttack(SoulEaterAction.Tail); return; }
            // Give the rear player time to read the tail; do not spin away faster than its warning.
            if (facing >= -.3f || distance > settings.tailRange) Face(target.position, dt);
            if (facing > .55f && ClearLine(MouthPosition, TargetPoint, target))
            {
                if (distance <= settings.biteRange && clock >= nextBite && (lastAction != SoulEaterAction.Bite || repetitions < 2 || clock < nextBreath))
                { TryStartAttack(SoulEaterAction.Bite); return; }
                if (distance > settings.biteRange * .75f && distance <= settings.breathRange && clock >= nextBreath)
                { TryStartAttack(SoulEaterAction.Breath); return; }
            }
            if (distance > settings.biteRange * .85f) MoveTowards(target.position, dt); else StopNavigation();
        }
        // Also used by the attack workshop and deterministic combat checks.
        public bool TryStartAttack(SoulEaterAction action)
        {
            if (!initialized || State != SoulEaterState.Hunting || target == null || action == SoulEaterAction.None) return false;
            Action = action; attackId = AttackIdentity.Next(); struck.Clear(); nextTick = clock;
            repetitions = lastAction == action ? repetitions + 1 : 1; lastAction = action;
            float total = settings.Windup(action) + settings.Active(action) + settings.Recovery(action);
            if (action == SoulEaterAction.Bite) nextBite = clock + total + settings.biteCooldown;
            if (action == SoulEaterAction.Tail) { nextTail = clock + total + settings.tailCooldown; rearTime = 0; }
            if (action == SoulEaterAction.Breath) nextBreath = clock + total + settings.breathCooldown;
            aim = (TargetPoint - MouthPosition).normalized;
            Enter(SoulEaterState.Windup, settings.Windup(action), true);
            effects?.Cue(action == SoulEaterAction.Breath ? SoulEaterCue.Inhale : action == SoulEaterAction.Tail ? SoulEaterCue.Tail : SoulEaterCue.Bite);
            return true;
        }
        void Enter(SoulEaterState next, float seconds = 0, bool keepAction = false)
        {
            StopNavigation(); effects?.StopBreath(); effects?.EyeIntensity(1);
            State = next; elapsed = 0; duration = seconds;
            if (!keepAction) Action = SoulEaterAction.None;
            combat.Recovering = next == SoulEaterState.Recovery || next == SoulEaterState.Braking || next == SoulEaterState.Staggered;
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
            else if (State == SoulEaterState.RetreatJump) { clip = Progress < .58f ? settings.takeOff : settings.land; p = Progress < .58f ? Mathf.Lerp(.12f, .7f, Progress / .58f) : Mathf.Lerp(.3f, 1, (Progress - .58f) / .42f); }
            else if (State == SoulEaterState.ChargeWindup) { clip = settings.chargePose != null ? settings.chargePose : settings.run; p = Progress; }
            else if (State == SoulEaterState.Charging) { clip = settings.run; p = Mathf.Repeat(elapsed * 1.6f / Mathf.Max(.1f, clip.length), 1); }
            else if (State == SoulEaterState.Braking) { clip = settings.brake != null ? settings.brake : settings.land; p = Progress; }
            else if (State == SoulEaterState.Staggered) { clip = settings.hit; p = Progress; }
            else if (State == SoulEaterState.Dead) { clip = settings.die; p = Progress; }
            else if (State == SoulEaterState.Returning || State == SoulEaterState.Hunting && target != null && Vector3.Distance(transform.position, target.position) > settings.biteRange * .85f) { clip = settings.walk; p = Loop(clip); }
            animation.Sample(clip, p, dt, immediate);
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
        void ChargeHits(Vector3 a, Vector3 b) => HitCapsule(a + Vector3.up * 1.1f + chargeDirection * 2.4f, b + Vector3.up * 1.1f + chargeDirection * 2.4f, 1.25f, settings.chargeDamage, false);
        void BreathHit(float reach)
        {
            if (target == null) return;
            var receiver = target.GetComponent<DamageReceiver>(); if (!IsTarget(receiver)) return;
            Vector3 point = TargetPoint, delta = point - MouthPosition; float forward = Vector3.Dot(delta, aim);
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
            if (State == SoulEaterState.RetreatJump) { pendingStagger = true; staggerSeconds = seconds; return; }
            RestoreNavigation(); Enter(SoulEaterState.Staggered, seconds);
        }
        public void OnAttackParried(DamageInfo damage)
        {
            if (State != SoulEaterState.Active || Action != SoulEaterAction.Bite || damage.AttackId != attackId) return;
            Enter(SoulEaterState.Staggered, combat.Broken ? Mathf.Max(2, settings.parryRecovery) : settings.parryRecovery);
        }
        void Die(DamageInfo _)
        {
            if (!initialized || State == SoulEaterState.Dead) return;
            LandSafely(); Enter(SoulEaterState.Dead, settings.die != null ? settings.die.length : 2); DisableNavigation();
            body.enabled = false; if (headCollider != null) headCollider.enabled = false; effects?.StopAll(); Defeated?.Invoke();
        }
        static Vector3 Planar(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up);
        void Face(Vector3 p, float dt, float multiplier = 1)
        { Vector3 d = Planar(p - transform.position); if (d.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), settings.turnSpeed * multiplier * dt); }
        bool Navigating => agent != null && agent.enabled && agent.isOnNavMesh;
        void StopNavigation() { if (Navigating) { agent.isStopped = true; agent.ResetPath(); } }
        void DisableNavigation() { StopNavigation(); if (agent != null) agent.enabled = false; }
        void RestoreNavigation()
        {
            if (agent != null && NavMesh.SamplePosition(transform.position, out var p, 2, agent.areaMask)) { agent.enabled = true; agent.updateRotation = false; agent.Warp(p.position); }
        }
        void MoveTowards(Vector3 destination, float dt)
        {
            if (Navigating) { agent.speed = settings.movementSpeed; agent.stoppingDistance = State == SoulEaterState.Returning ? .1f : settings.biteRange * .8f; agent.isStopped = false; agent.SetDestination(destination); }
            else MoveGround(Planar(destination - transform.position).normalized * settings.movementSpeed * dt);
        }
        bool MoveGround(Vector3 step)
        {
            Vector3 destination = transform.position + step;
            if (Planar(destination - home).magnitude > settings.arenaRadius || !Ground(destination, out Vector3 ground) || Mathf.Abs(ground.y - transform.position.y) > .65f) return false;
            int n = Physics.CapsuleCastNonAlloc(transform.position + Vector3.up * body.radius, transform.position + Vector3.up * Mathf.Max(body.radius,body.height-body.radius), body.radius * .9f, step.normalized, hits, step.magnitude + .1f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!OwnOrTarget(hits[i].transform) && hits[i].normal.y < .6f) return false;
            if (Navigating) agent.Move(ground - transform.position); else transform.position = ground;
            return true;
        }
        bool TryRetreat(out Vector3 destination)
        {
            Vector3 away = Planar(transform.position - target.position).normalized;
            foreach (float distance in new[] { settings.retreatDistance, settings.retreatDistance * .65f, settings.retreatDistance * .4f })
                foreach (float angle in new[] { 0f, 35f, -35f, 70f, -70f })
                {
                    Vector3 candidate = transform.position + Quaternion.Euler(0, angle, 0) * away * distance;
                    if (Planar(candidate - home).magnitude > settings.arenaRadius - 3 || !Ground(candidate, out candidate)) continue;
                    if (Mathf.Abs(candidate.y - transform.position.y) > 1) continue;
                    bool blocked = false;
                    for (int sample = 1; sample <= 10 && !blocked; sample++)
                    {
                        float t = sample / 10f; Vector3 p = Vector3.Lerp(transform.position, candidate, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * settings.jumpHeight);
                        int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 1.4f, p + Vector3.up * 2.2f, body.radius * .85f, overlaps, ~0, QueryTriggerInteraction.Ignore);
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
            for (int i = 0; i < n; i++) if (!OwnOrTarget(hits[i].transform) && hits[i].collider.GetComponentInParent<Health>() == null && hits[i].normal.y > .7f && hits[i].distance < nearest)
            { nearest = hits[i].distance; ground = hits[i].point; }
            return !float.IsPositiveInfinity(nearest);
        }
        bool OwnOrTarget(Transform t) => t.IsChildOf(transform) || target != null && t.IsChildOf(target);
        bool ClearLine(Vector3 a, Vector3 b, Transform recipient)
        {
            Vector3 delta = b - a; int n = Physics.RaycastNonAlloc(a, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!hits[i].transform.IsChildOf(transform) && (recipient == null || !hits[i].transform.IsChildOf(recipient))) return false;
            return true;
        }
        void LandSafely() { if (Ground(transform.position, out var p)) transform.position = p; else if (Ground(home, out p)) transform.position = p; RestoreNavigation(); }
    }
}
