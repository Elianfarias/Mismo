using System;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health), typeof(DamageReceiver), typeof(CapsuleCollider))]
    public sealed class DragonBossController : MonoBehaviour, IParryResponder, IBossMusicThreat
    {
        [SerializeField] DragonBossSettings settings;
        [SerializeField] Animator animator;
        [SerializeField] Transform mouth;
        [SerializeField] Transform target;
        [SerializeField] bool autoFindTarget = true;
        Health health, targetHealth;
        CombatState combat;
        DefenseWindow defense;
        NavMeshAgent agent;
        CapsuleCollider body;
        DragonBreathVfx breath;
        Vector3 home, aim, flightStart;
        float elapsed, duration, clock, nextBreath, nextFlight, nextSearch, nextBreathTick, hurtCooldown;
        bool initialized, dealtMelee, enraged, aerialAttack;
        long attackId;
        DragonMotion motion;
        readonly RaycastHit[] obstacles = new RaycastHit[32];
        public DragonBossSettings Settings => settings;
        public Health Health => health;
        public Transform Target => target;
        public DragonBossState State { get; private set; }
        public DragonMotion Motion => motion;
        public bool Enraged => enraged;
        public float Progress => duration > 0 ? Mathf.Clamp01(elapsed / duration) : 0;
        public bool Telegraphing => State == DragonBossState.Attacking && Progress < .35f;
        public event Action Defeated;

        public void Configure(DragonBossSettings data, Animator rig, Transform muzzle)
        { settings = data; animator = rig; mouth = muzzle; }
        public void SetTarget(Transform value)
        { targetHealth = value != null ? value.GetComponentInParent<Health>() : null; target = targetHealth != null ? targetHealth.transform : value; }
        public bool IsFightingPlayer(Transform player) => initialized && isActiveAndEnabled && !health.IsDead &&
            State != DragonBossState.Sleeping && State != DragonBossState.Returning && target != null &&
            (target == player || target.IsChildOf(player));

        void Awake()
        {
            health = GetComponent<Health>(); body = GetComponent<CapsuleCollider>(); agent = GetComponent<NavMeshAgent>();
            combat = GetComponent<CombatState>() ?? gameObject.AddComponent<CombatState>();
            defense = GetComponent<DefenseWindow>() ?? gameObject.AddComponent<DefenseWindow>();
            breath = GetComponent<DragonBreathVfx>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }
        void OnEnable()
        {
            health.Died += OnDied; health.Damaged += OnDamaged; combat.PostureBroken += Stagger;
            PlayerMusic.RegisterBoss(this);
            if (initialized) ResetEncounter();
        }
        void OnDisable()
        {
            health.Died -= OnDied; health.Damaged -= OnDamaged; combat.PostureBroken -= Stagger;
            PlayerMusic.UnregisterBoss(this); StopNavigation(); breath?.SetEmitting(false); defense.CloseGuard();
        }
        void Start()
        {
            if (settings == null || animator == null) { Debug.LogError("Dragon boss necesita configuración y Animator.", this); enabled = false; return; }
            home = transform.position; health.ConfigureMaximum(settings.health); health.Revive(); combat.ConfigurePosture(settings.posture);
            animator.applyRootMotion = false;
            GetComponent<ActorCombatVisuals>()?.DelayDeathEffect(settings.Duration(DragonMotion.Die));
            if (agent != null) { agent.updateRotation = false; agent.stoppingDistance = settings.meleeRange * .8f; }
            nextFlight = settings.flightCooldown; initialized = true; RestoreNavigation();
            Enter(DragonBossState.Sleeping, DragonMotion.Sleep);
        }
        void ResetEncounter()
        {
            home = transform.position; clock = nextBreath = nextSearch = hurtCooldown = 0;
            nextFlight = settings.flightCooldown; enraged = false; SetTarget(null);
            health.Revive(); combat.ResetCombat();
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = true;
            RestoreNavigation(); Enter(DragonBossState.Sleeping, DragonMotion.Sleep);
        }
        void Update() => Tick(Time.deltaTime);
        public void Tick(float deltaTime)
        {
            if (!initialized || !enabled || deltaTime <= 0 || State == DragonBossState.Dead) return;
            float dt = deltaTime; clock += dt; elapsed += dt;
            if (health.IsDead) { OnDied(default); return; }
            if (autoFindTarget && target == null && clock >= nextSearch && State != DragonBossState.Returning)
            {
                nextSearch = clock + 1;
                var player = FindAnyObjectByType<PlayerController>();
                if (player != null && Vector3.Distance(home, player.transform.position) <= settings.detectionRange) SetTarget(player.transform);
            }
            bool valid = target != null && (targetHealth == null || !targetHealth.IsDead);
            if (State != DragonBossState.Sleeping && State != DragonBossState.Returning &&
                (!valid || Vector3.Distance(home, target.position) > settings.leashRange || Vector3.Distance(home, transform.position) > settings.leashRange))
                Enter(DragonBossState.Returning, DragonMotion.Walk);
            if (!enraged && health.Normalized <= settings.enrageThreshold && State == DragonBossState.Hunting)
            { enraged = true; Enter(DragonBossState.Roaring, DragonMotion.Scream, settings.Duration(DragonMotion.Scream)); }

            switch (State)
            {
                case DragonBossState.Sleeping:
                    if (valid && Vector3.Distance(transform.position, target.position) <= settings.detectionRange && CanSeeTarget())
                        Enter(DragonBossState.Roaring, DragonMotion.Scream, settings.Duration(DragonMotion.Scream));
                    break;
                case DragonBossState.Roaring:
                case DragonBossState.Staggered:
                case DragonBossState.Recovering:
                case DragonBossState.Defending:
                    if (elapsed >= duration) Enter(DragonBossState.Hunting, DragonMotion.idle01);
                    break;
                case DragonBossState.Hunting:
                    Hunt(dt); break;
                case DragonBossState.Attacking:
                    // Direction locks before the active frames; dodging the telegraph is meaningful.
                    if (Progress < .25f) { Face(target.position, dt); aim = (TargetPoint() - MouthPosition()).normalized; }
                    bool active = elapsed >= duration * .35f && elapsed - dt <= duration * .67f;
                    bool fire = motion == DragonMotion.attackFlame || motion == DragonMotion.FlyFlame;
                    breath?.Aim(aim, settings.breathRange, settings.accent);
                    breath?.SetEmitting(active && fire);
                    if (active && CanSeeTarget())
                    {
                        if (fire && clock >= nextBreathTick) { nextBreathTick = clock + .35f; HitTarget(true); }
                        else if (!fire && !dealtMelee) { dealtMelee = true; HitTarget(false); }
                    }
                    combat.Recovering = Progress > .67f;
                    if (elapsed >= duration)
                    {
                        if (aerialAttack) Enter(DragonBossState.Landing, DragonMotion.Land, settings.Duration(DragonMotion.Land));
                        else if (motion == DragonMotion.attackHand && target != null &&
                            Vector3.Distance(transform.position, target.position) <= settings.meleeRange && UnityEngine.Random.value < settings.comboChance)
                            BeginAttack(DragonMotion.attackMouth, false);
                        else Enter(DragonBossState.Recovering, DragonMotion.idle02, settings.recovery * (enraged ? .65f : 1));
                    }
                    break;
                case DragonBossState.TakingOff:
                    SetAltitude(Mathf.Lerp(flightStart.y, home.y + settings.flightHeight, Mathf.SmoothStep(0, 1, Progress)));
                    if (elapsed >= duration) Enter(DragonBossState.Flying, DragonMotion.FlyForward, settings.flightDuration);
                    break;
                case DragonBossState.Flying:
                    Face(target.position, dt);
                    Play(Progress < .35f ? DragonMotion.FlyForward : Progress < .7f ? DragonMotion.FlyGlide : DragonMotion.FlyIdle);
                    if (elapsed >= duration) BeginAttack(DragonMotion.FlyFlame, true);
                    break;
                case DragonBossState.Landing:
                    SetAltitude(Mathf.Lerp(flightStart.y, GroundHeight(transform.position), Mathf.SmoothStep(0, 1, Progress)));
                    if (elapsed >= duration) { RestoreNavigation(); Enter(DragonBossState.Recovering, DragonMotion.idle01, settings.recovery); }
                    break;
                case DragonBossState.Returning:
                    // Return on the ground, even if the target disappeared during a flight or attack.
                    SetAltitude(Mathf.MoveTowards(transform.position.y, GroundHeight(transform.position), dt * 6));
                    Move(home, dt, .7f);
                    if (Vector3.Distance(transform.position, home) < 1)
                    {
                        transform.position = home; health.Revive(); combat.ResetCombat(); enraged = false; SetTarget(null);
                        nextBreath = clock; nextFlight = clock + settings.flightCooldown; RestoreNavigation();
                        Enter(DragonBossState.Sleeping, DragonMotion.Sleep);
                    }
                    break;
            }
        }
        void Hunt(float dt)
        {
            if (target == null) return;
            float distance = Vector3.Distance(transform.position, target.position);
            Face(target.position, dt);
            if (clock >= nextFlight && settings.flightHeight > 0 && CanSeeTarget())
            {
                nextFlight = clock + settings.flightCooldown * (enraged ? .7f : 1);
                if (agent != null && agent.enabled) agent.enabled = false;
                Enter(DragonBossState.TakingOff, DragonMotion.takeOff, settings.Duration(DragonMotion.takeOff)); return;
            }
            if (distance > settings.meleeRange * .8f && distance <= settings.breathRange && clock >= nextBreath && CanSeeTarget())
            { nextBreath = clock + settings.breathCooldown; BeginAttack(DragonMotion.attackFlame, false); return; }
            if (distance <= settings.meleeRange && CanSeeTarget())
            {
                if (UnityEngine.Random.value < settings.defendChance)
                    Enter(DragonBossState.Defending, DragonMotion.Defend, settings.Duration(DragonMotion.Defend));
                else BeginAttack(UnityEngine.Random.value < settings.biteChance ? DragonMotion.attackMouth : DragonMotion.attackHand, false);
                return;
            }
            Move(target.position, dt, enraged ? 1.2f : 1);
            Play(enraged || distance > settings.meleeRange * 2 ? DragonMotion.Run : DragonMotion.Walk);
        }
        void BeginAttack(DragonMotion action, bool flying)
        {
            aerialAttack = flying; dealtMelee = false; attackId = AttackIdentity.Next(); nextBreathTick = clock;
            aim = (TargetPoint() - MouthPosition()).normalized;
            Enter(DragonBossState.Attacking, action, settings.Duration(action) / (enraged ? 1.12f : 1));
        }
        void Enter(DragonBossState next, DragonMotion animation, float seconds = 0)
        {
            StopNavigation(); breath?.SetEmitting(false); combat.Recovering = next == DragonBossState.Recovering;
            State = next; elapsed = 0; duration = seconds; flightStart = transform.position;
            defense.CloseGuard();
            if (next == DragonBossState.Defending) defense.OpenGuard(seconds);
            Play(animation, true);
            if (seconds > 0) animator.speed = settings.Duration(animation) / seconds;
        }
        void Play(DragonMotion next, bool restart = false)
        {
            if (!restart && motion == next) return;
            motion = next; animator.speed = 1;
            animator.CrossFadeInFixedTime(next.ToString(), .12f, 0, 0);
        }
        void HitTarget(bool fire)
        {
            if (target == null) return;
            Vector3 origin = fire ? MouthPosition() : transform.position + Vector3.up * 1.5f;
            Vector3 point = TargetPoint(), delta = point - origin;
            if (fire)
            {
                if (delta.magnitude > settings.breathRange || Vector3.Dot(delta.normalized, aim) < Mathf.Cos(18 * Mathf.Deg2Rad)) return;
            }
            else if (Vector3.Distance(transform.position, target.position) > settings.meleeRange + .6f ||
                Vector3.Dot(Vector3.ProjectOnPlane(delta, Vector3.up).normalized, transform.forward) < .3f) return;
            var receiver = target.GetComponentInParent<IDamageReceiver>();
            float amount = fire ? settings.breathDamage : motion == DragonMotion.attackMouth ? settings.biteDamage : settings.clawDamage;
            receiver?.ReceiveDamage(new DamageInfo(amount * (enraged ? 1.2f : 1), gameObject, point, delta,
                fire ? AttackIdentity.Next() : attackId, area: fire, origin: origin, parryable: !fire));
        }
        Vector3 MouthPosition() => mouth != null ? mouth.position : transform.position + transform.forward * 3 + Vector3.up * 2;
        Vector3 TargetPoint() => target != null ? target.position + Vector3.up : transform.position + transform.forward;
        bool CanSeeTarget()
        {
            return ClearLine(transform.position + Vector3.up * 2) && ClearLine(MouthPosition());
        }
        bool ClearLine(Vector3 origin)
        {
            Vector3 delta = TargetPoint() - origin;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, obstacles, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!obstacles[i].transform.IsChildOf(transform) && (target == null || !obstacles[i].transform.IsChildOf(target))) return false;
            return true;
        }
        void Face(Vector3 point, float dt)
        {
            Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), settings.turnSpeed * dt);
        }
        bool Navigating => agent != null && agent.enabled && agent.isOnNavMesh;
        void StopNavigation() { if (Navigating) { agent.isStopped = true; agent.ResetPath(); } }
        void RestoreNavigation()
        {
            if (agent != null && NavMesh.SamplePosition(transform.position, out var hit, 2, agent.areaMask))
            { agent.enabled = true; agent.Warp(hit.position); }
        }
        void Move(Vector3 point, float dt, float multiplier)
        {
            Face(point, dt);
            if (Navigating) { agent.speed = settings.moveSpeed * multiplier; agent.isStopped = false; agent.SetDestination(point); return; }
            Vector3 delta = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            Vector3 step = Vector3.ClampMagnitude(delta, settings.moveSpeed * multiplier * dt);
            if (step.sqrMagnitude < .000001f) return;
            Vector3 center = transform.TransformPoint(body.center);
            int count = Physics.CapsuleCastNonAlloc(center + Vector3.up * Mathf.Max(0, body.height * .5f - body.radius),
                center - Vector3.up * Mathf.Max(0, body.height * .5f - body.radius), body.radius * .95f,
                step.normalized, obstacles, step.magnitude + .05f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (!obstacles[i].transform.IsChildOf(transform) && obstacles[i].normal.y < .6f) return;
            Vector3 destination = transform.position + step;
            if (!TryGroundHeight(destination, out float ground)) return;
            if (Mathf.Abs(ground - transform.position.y) > .8f) return;
            destination.y = ground; transform.position = destination;
        }
        float GroundHeight(Vector3 point)
        { return TryGroundHeight(point, out float height) ? height : home.y; }
        bool TryGroundHeight(Vector3 point, out float height)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 12, Vector3.down, obstacles, 40, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity; height = home.y;
            for (int i = 0; i < count; i++)
                if (!obstacles[i].transform.IsChildOf(transform) && obstacles[i].normal.y > .6f &&
                    obstacles[i].collider.GetComponentInParent<Health>() == null && obstacles[i].distance < nearest)
                { nearest = obstacles[i].distance; height = obstacles[i].point.y; }
            return !float.IsPositiveInfinity(nearest);
        }
        void SetAltitude(float y) { var p = transform.position; p.y = y; transform.position = p; }
        void OnDamaged(DamageInfo damage)
        {
            if (!initialized || health.IsDead) return;
            if (target == null && damage.Source != null) SetTarget(damage.Source.transform);
            if (State == DragonBossState.Sleeping) Enter(DragonBossState.Roaring, DragonMotion.Scream, settings.Duration(DragonMotion.Scream));
            else if (clock >= hurtCooldown && State == DragonBossState.Hunting)
            { hurtCooldown = clock + 3; Enter(DragonBossState.Staggered, DragonMotion.getHit, settings.Duration(DragonMotion.getHit)); }
        }
        void Stagger(float seconds)
        {
            if (!initialized || health.IsDead || State == DragonBossState.Dead) return;
            if (transform.position.y > GroundHeight(transform.position) + 1)
                Enter(DragonBossState.Landing, DragonMotion.Land, settings.Duration(DragonMotion.Land));
            else Enter(DragonBossState.Staggered, DragonMotion.getHit, Mathf.Max(seconds, .6f));
        }
        public void OnAttackParried(DamageInfo _) => Stagger(1.5f);
        void OnDied(DamageInfo _)
        {
            if (State == DragonBossState.Dead) return;
            Enter(DragonBossState.Dead, DragonMotion.Die, settings.Duration(DragonMotion.Die));
            if (agent != null) agent.enabled = false;
            SetAltitude(GroundHeight(transform.position));
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            Defeated?.Invoke();
        }
    }
}
