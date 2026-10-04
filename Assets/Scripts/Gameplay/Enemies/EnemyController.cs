using System;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Position,
        Telegraph,
        Attack,
        Recovery,
        Stagger,
        Return,
        Dead
    }

    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(DamageReceiver))]
    [DisallowMultipleComponent]
    public abstract partial class EnemyController : MonoBehaviour, IParryResponder
    {
        [SerializeField] private EnemySettings settings;
        [SerializeField] private DamageDealer weapon;
        [SerializeField] private Transform target;

        private CombatState combat;
        private bool pressure;
        private Health health;
        private Health targetHealth;
        private NavMeshAgent agent;

        private Vector3 home;
        private Vector3 lastSeen;
        private Vector3 attackDirection;

        private GoblinAttack attack;

        private float timer;
        private float duration;
        private float decision;
        private float staggerResistance;
        private float lostTime;
        private float repath;
        private float search;

        private bool started;
        private bool comboResponsePending;

        private float provokedLoseRange;
        private float provokedLeashRange;

        private int orbitSign = 1;

        private readonly HashSet<UnityEngine.Object> hitTargets =
            new HashSet<UnityEngine.Object>();

        protected readonly EnemyAttackSelection AttackSelection = new EnemyAttackSelection();

        bool projectileReleased;
        Vector3 aimPoint;

        public EnemyState State { get; private set; }

        public EnemySettings Settings => settings;
        public GoblinAttack CurrentAttack => attack;
        public Transform Target => target;

        public float AttackElapsed =>
            attack == null
                ? 0
                : State == EnemyState.Telegraph
                    ? attack.windup * StateProgress
                    : State == EnemyState.Attack
                        ? attack.windup + attack.active * StateProgress
                        : State == EnemyState.Recovery
                            ? attack.windup + attack.active + attack.recovery * StateProgress
                            : 0;

        public Vector3 AttackDirection => attackDirection;

        public float StateProgress =>
            duration > 0f
                ? Mathf.Clamp01(1f - timer / duration)
                : 0f;

        // Subclasses decide; this controller owns timing, cancellation and execution.
        protected abstract GoblinAttack ChooseAttack(float distance, bool pressure);
        protected virtual float PreferredRange => AttackSelection.HasActions(settings) ? settings.preferredRange : settings.slash.range;
        protected virtual bool EvaluatePressure() => EnemyPerception.IsPreparingAimedAction(target);
        protected virtual bool UsesAdditiveHitReaction => false;
        protected virtual bool AllowsHeavyRecoveryInterrupt => false;
        protected virtual float BreakRecoveryDuration => 0f;
        protected virtual bool UsesComboBackstep => false;
        protected virtual void ComboResponseFinished() { }
        protected virtual void ResetTactics() { }
        protected virtual void PrepareDecision(float dt, float distance, bool sight) { }
        protected virtual void AttackFinished(GoblinAttack completed) { }
        protected virtual bool MoveTactically(Vector3 offset, float distance, bool sight) => false;
        protected Vector3 LastKnownTargetPosition => lastSeen;
        public virtual float NameplateHeight => (GetComponent<CapsuleCollider>()?.height ?? 2f) + .2f;

        protected virtual Vector3 PositionAroundTarget(Vector3 offset, float preferred, int direction)
        {
            Vector3 radial = offset.sqrMagnitude > .001f ? -offset.normalized : -transform.forward;
            return target.position + radial * (preferred - .15f) + Vector3.Cross(Vector3.up, radial) * (.65f * direction);
        }

        public event Action<EnemyState> StateChanged;

        public void Configure(
            EnemySettings configuration,
            DamageDealer source)
        {
            settings = configuration;
            weapon = source;
        }

        public void SetTarget(Transform value)
        {
            if (target != value)
            {
                CancelBackstep();
                comboResponsePending = false;
                ResetTactics();
            }
            target = value;
            targetHealth =
                value != null
                    ? value.GetComponentInParent<Health>()
                    : null;

            lostTime = 0f;
            provokedLoseRange = 0f;
            provokedLeashRange = 0f;
        }

        protected void Awake()
        {
            if (GetComponent<EnemyProgressionReward>() == null)
                gameObject.AddComponent<EnemyProgressionReward>();

            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();

            combat =
                GetComponent<CombatState>() ??
                gameObject.AddComponent<CombatState>();

            combat.ConfigurePosture(70);

            if (UsesAdditiveHitReaction &&
                GetComponent<GoblinHitReaction>() == null)
            {
                gameObject.AddComponent<GoblinHitReaction>();
            }

            if (GetComponent<EnemyNameplate>() == null)
                gameObject.AddComponent<EnemyNameplate>();
        }

        protected void OnEnable()
        {
            combat.PostureBroken += Stagger;
            health.Damaged += OnDamaged;
            health.Died += OnDied;

            GetComponent<DamageReceiver>().Resolved += OnResolved;

            if (started)
                ResetLife();
        }

        protected void Start()
        {
            if (settings == null || weapon == null)
            {
                Debug.LogError(
                    "El enemigo necesita Settings y DamageDealer.",
                    this
                );

                enabled = false;
                return;
            }

            started = true;

            combat.ConfigurePosture(settings.posture);

            combat.ConfigureBreakRecovery(
                settings.isBoss ? 0 : BreakRecoveryDuration
            );

            home = transform.position;

            health.ConfigureMaximum(
                settings.health *
                (
                    GetComponent<
                        Mismo.Gameplay.Player.World.WorldEnemyIdentity
                    >()?.HealthMultiplier ?? 1
                )
            );

            (GetComponent<CombatAilment>() ?? gameObject.AddComponent<CombatAilment>())
                .ConfigureArmor(settings.armor);

            health.Revive();

            ResetLife();
        }

        private void ResetLife()
        {
            CancelBackstep();
            comboResponsePending = false;
            combat.ResetCombat();

            agent.updateRotation = false;
            agent.speed = settings.speed;
            agent.stoppingDistance = 0.1f;

            if (
                NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit hit,
                    2f,
                    agent.areaMask
                )
            )
            {
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning(
                    "Enemigo fuera del NavMesh. Colocalo sobre una superficie navegable.",
                    this
                );
            }

            var body = GetComponent<CapsuleCollider>();

            if (body != null)
                body.enabled = true;

            decision = 0f;

            AttackSelection.Reset();
            ResetTactics();
            staggerResistance = 0f;
            lostTime = 0f;
            repath = 0f;

            attack = null;

            SetTarget(null);

            Enter(EnemyState.Idle);
        }

        protected void OnDisable()
        {
            CancelBackstep();
            comboResponsePending = false;
            combat.PostureBroken -= Stagger;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;

            GetComponent<DamageReceiver>().Resolved -= OnResolved;

            Stop();

            hitTargets.Clear();
        }

        private bool CanNavigate =>
            agent != null &&
            agent.enabled &&
            agent.isOnNavMesh;

        private void Enter(
            EnemyState state,
            float seconds = 0f)
        {
            Stop();

            State = state;

            combat.Recovering =
                state == EnemyState.Recovery;

            duration = timer = seconds;

            repath = 0f;

            StateChanged?.Invoke(state);
        }

        protected void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (
                !started ||
                !enabled ||
                dt <= 0f ||
                State == EnemyState.Dead
            )
                return;

            AttackSelection.Tick(dt);

            if (health.IsDead)
            {
                Enter(EnemyState.Dead);
                return;
            }

            if (
                target != null &&
                State != EnemyState.Idle &&
                State != EnemyState.Return
            )
            {
                target
                    .GetComponentInParent<
                        Mismo.Gameplay.Player.Equipment.EquipmentLoadout
                    >()
                    ?.MarkCombat();
            }

            staggerResistance =
                Mathf.Max(0f, staggerResistance - dt);

            decision -= dt;
            repath -= dt;
            search -= dt;

            if (!CanNavigate)
                return;

            if (State == EnemyState.Stagger)
            {
                timer -= dt;

                if (timer <= 0f && !combat.Broken)
                {
                    bool respond = comboResponsePending;
                    comboResponsePending = false;
                    decision = respond ? 0f : Mathf.Max(.35f, settings.decisionPause);
                    Enter(EnemyState.Position);
                    if (respond)
                    {
                        GetComponent<EnemyEquipment>()?.CancelHitReaction();
                        if (!UsesComboBackstep || !TryStartBackstep()) ComboResponseFinished();
                    }
                }

                return;
            }

            if (
                target == null &&
                State != EnemyState.Return &&
                search <= 0f
            )
            {
                search = 0.5f;

                foreach (
                    PlayerController player
                    in FindObjectsByType<PlayerController>()
                )
                {
                    Health candidate =
                        player.GetComponent<Health>();

                    if (
                        (candidate == null || !candidate.IsDead) &&
                        Vector3.Distance(
                            transform.position,
                            player.transform.position
                        ) <= settings.detectionRange &&
                        HasSight(player.transform)
                    )
                    {
                        SetTarget(player.transform);
                        break;
                    }
                }
            }

            bool valid =
                target != null &&
                target.gameObject.activeInHierarchy &&
                (
                    targetHealth == null ||
                    !targetHealth.IsDead
                );

            if (
                valid &&
                (
                    Vector3.Distance(
                        home,
                        transform.position
                    ) >
                    Mathf.Max(
                        settings.leashRange,
                        provokedLeashRange
                    ) ||
                    Vector3.Distance(
                        transform.position,
                        target.position
                    ) >
                    Mathf.Max(
                        settings.loseRange,
                        provokedLoseRange
                    )
                )
            )
            {
                valid = false;
            }

            if (
                !valid &&
                State != EnemyState.Return &&
                State != EnemyState.Idle
            )
            {
                SetTarget(null);
                attack = null;
                Enter(EnemyState.Return);
            }

            if (State == EnemyState.Return)
            {
                MoveTo(home, settings.speed);

                if (
                    Vector3.Distance(
                        transform.position,
                        home
                    ) < 0.45f
                )
                {
                    Enter(EnemyState.Idle);
                }

                return;
            }

            if (!valid)
            {
                SetTarget(null);
                return;
            }

            bool sight = HasSight(target);

            if (sight)
            {
                lostTime = 0f;
                lastSeen = target.position;
            }
            else
            {
                lostTime += dt;
            }

            if (lostTime >= settings.memoryDuration)
            {
                SetTarget(null);
                attack = null;

                Enter(EnemyState.Return);

                return;
            }

            // =====================================================
            // TELEGRAPH
            // =====================================================

            if (IsBackstepping)
            {
                Face(Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up), dt);
                TickBackstep(dt);
                return;
            }

            if (State == EnemyState.Telegraph)
            {
                // Solamente los proyectiles siguen apuntando
                // al jugador durante todo el windup.
                if (
                    attack != null &&
                    attack.kind == CreatureAttackKind.Projectile &&
                    target != null
                )
                {
                    Vector3 direction =
                        Vector3.ProjectOnPlane(
                            target.position -
                            transform.position,
                            Vector3.up
                        );

                    if (direction.sqrMagnitude > 0.001f)
                    {
                        attackDirection =
                            direction.normalized;

                        // Seguimiento visual.
                        Face(direction, dt);

                        // Actualiza el objetivo real del proyectil.
                        aimPoint =
                            target.position +
                            Vector3.up * .8f;
                    }
                }

                timer -= dt;

                if (timer <= 0f)
                {
                    hitTargets.Clear();

                    weapon.Configure(
                        attack.damage *
                        (
                            GetComponent<
                                Mismo.Gameplay.Player.World.WorldEnemyIdentity
                            >()?.DamageMultiplier ?? 1
                        )
                    );

                    Enter(
                        EnemyState.Attack,
                        attack.active
                    );

                    PlayExecutionSound(attack);

                    if (
                        attack.kind ==
                        CreatureAttackKind.Projectile
                    )
                    {
                        ReleaseProjectile();
                    }

                    ApplyHits();
                }

                return;
            }

            // =====================================================
            // ATTACK
            // =====================================================

            if (State == EnemyState.Attack)
            {
                float step =
                    Mathf.Min(
                        dt,
                        Mathf.Max(0f, timer)
                    );

                if (attack.travel > 0f)
                {
                    MoveCharge(
                        attackDirection *
                        (
                            attack.travel *
                            step /
                            Mathf.Max(
                                0.02f,
                                attack.active
                            )
                        )
                    );
                }

                if (State != EnemyState.Attack)
                    return;

                ApplyHits();

                if (State != EnemyState.Attack)
                    return;

                timer -= dt;

                ApplyHits();

                if (State != EnemyState.Attack)
                    return;

                if (timer <= 0f)
                {
                    Enter(
                        EnemyState.Recovery,
                        attack.recovery
                    );
                }

                return;
            }

            // =====================================================
            // RECOVERY
            // =====================================================

            if (State == EnemyState.Recovery)
            {
                timer -= dt;

                if (timer <= 0f)
                {
                    decision =
                        settings.decisionPause;

                    orbitSign *= -1;
                    AttackFinished(attack);

                    Enter(EnemyState.Position);
                }

                return;
            }

            Vector3 offset =
                Vector3.ProjectOnPlane(
                    target.position -
                    transform.position,
                    Vector3.up
                );

            float distance = offset.magnitude;

            Face(offset, dt);
            PrepareDecision(dt, distance, sight);

            bool sameHeight =
                Mathf.Abs(
                    target.position.y -
                    transform.position.y
                ) <
                settings.allowedHeightDifference;

            if (
                sight &&
                sameHeight &&
                decision <= 0f
            )
            {
                pressure = EvaluatePressure();
                GoblinAttack chosen = ChooseAttack(distance, pressure);

                if (chosen != null)
                {
                    attack = chosen;

                    projectileReleased = false;

                    AttackSelection.Commit(chosen, settings);

                    attackDirection =
                        offset.sqrMagnitude > 0.001f
                            ? offset.normalized
                            : transform.forward;

                    aimPoint =
                        target.position +
                        Vector3.up * .8f;

                    transform.rotation =
                        Quaternion.LookRotation(
                            attackDirection
                        );

                    PlayPreparationSound(attack);

                    Enter(
                        EnemyState.Telegraph,
                        attack.windup
                    );

                    return;
                }
            }

            if (MoveTactically(offset, distance, sight)) return;

            float preferred = PreferredRange;

            if (
                !sight ||
                distance >
                preferred +
                (pressure ? -.2f : .3f)
            )
            {
                if (State != EnemyState.Chase)
                    Enter(EnemyState.Chase);

                MoveTo(
                    sight
                        ? target.position
                        : lastSeen,
                    settings.speed
                );
            }
            else
            {
                if (State != EnemyState.Position)
                    Enter(EnemyState.Position);

                Vector3 position = PositionAroundTarget(offset, preferred, orbitSign);

                MoveTo(
                    position,
                    settings.positioningSpeed
                );
            }
        }

        private bool HasSight(Transform other) => EnemyPerception.HasSight(transform, other, settings.sightHeight);

        protected void OnDrawGizmosSelected()
        {
            if (settings == null)
                return;

            Gizmos.color =
                Color.yellow;

            Gizmos.DrawWireSphere(
                transform.position,
                settings.detectionRange
            );

            if (attack == null)
                return;

            Gizmos.color =
                Color.red;

            Gizmos.matrix =
                Matrix4x4.TRS(
                    transform.position +
                    Vector3.up * 0.85f +
                    transform.forward *
                    attack.forwardOffset,
                    transform.rotation,
                    Vector3.one
                );

            Gizmos.DrawWireCube(
                Vector3.zero,
                attack.halfExtents * 2f
            );
        }
    }
}
