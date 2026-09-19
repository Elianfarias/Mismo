using System;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Cooldowns;
using Mismo.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    public enum BossState
    {
        Idle,
        Approach,
        Position,
        Telegraph,
        Attack,
        Recovery,
        Stagger,
        Return,
        Dead
    }

    /// <summary>
    /// Coordinador del primer boss. Mantiene la IA intencionalmente pequeña: una selección
    /// ponderada de patrones de uno o dos ataques, con tiempos explícitos y sin tracking oculto.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(DamageReceiver))]
    [DisallowMultipleComponent]
    public sealed class BossController : MonoBehaviour, IParryResponder, Mismo.Gameplay.Player.Presentation.IBossMusicThreat
    {
        [SerializeField] private BossSettings settings;
        [SerializeField] private DamageDealer weapon;
        [SerializeField] private Transform target;
        [SerializeField] private bool autoFindTarget = true;

        private CombatState combat;
        private Health health;
        private Health targetHealth;
        private NavMeshAgent agent;
        private readonly CooldownBook cooldowns = new CooldownBook();
        private readonly HashSet<UnityEngine.Object> hitTargets = new HashSet<UnityEngine.Object>();
        private readonly List<BossPatternDefinition> candidates = new List<BossPatternDefinition>();
        private readonly List<float> candidateWeights = new List<float>();

        private Vector3 home;
        private Vector3 lastSeen;
        private Vector3 attackDirection;
        private BossAttackDefinition attack;
        private BossPatternDefinition pattern;
        private BossAttackId lastAttackId;
        private BossPatternId lastPatternId;
        private BossState state;
        private float stateTimer;
        private float decisionTimer;
        private float repathTimer;
        private float lostTime;
        private float searchTimer;
        private float staggerResistanceTimer;
        private int patternAttackIndex = -1;
        private int repeatedPatternCount;
        private bool started;
        private bool hasLastAttack;
        private bool hasLastPattern;
        private bool defeatRaised;
        private bool wasAggressive;

        public BossState State => state;
        public bool IsFightingPlayer(Transform player) => started && isActiveAndEnabled &&
            settings != null && health != null && !health.IsDead &&
            state != BossState.Idle && state != BossState.Return && state != BossState.Dead &&
            target != null && (target == player || target.IsChildOf(player)) &&
            lostTime < settings.memoryDuration && IsValidTarget();
        public BossSettings Settings => settings;
        public Health Health => health;
        public Transform Target => target;
        public BossAttackDefinition CurrentAttack => attack;
        public BossPatternDefinition CurrentPattern => pattern;
        public int CurrentPatternAttackIndex => patternAttackIndex;
        public bool IsAggressive => health != null && health.Normalized <= Mathf.Clamp01(settings != null ? settings.aggressionHealthThreshold : 0.5f);
        public float StateProgress => attack != null ? Mathf.Clamp01(1f - stateTimer / StateDurationForPresentation()) : 0f;
        public float CurrentTimer => Mathf.Max(0f, stateTimer);

        public event Action<BossState> StateChanged;
        public event Action<BossPatternDefinition> PatternStarted;
        public event Action<BossAttackDefinition> AttackStarted;
        public event Action<BossAttackDefinition> AttackFinished;
        public event Action<bool> AggressionChanged;
        public event Action Defeated;

        public void Configure(BossSettings configuration, DamageDealer source)
        {
            settings = configuration;
            weapon = source;
        }

        public void SetTarget(Transform value)
        {
            target = value;
            targetHealth = value != null ? value.GetComponentInParent<Health>() : null;
            lostTime = 0f;
        }

        private void Awake()
        {
            if(GetComponent<EnemyProgressionReward>()==null)gameObject.AddComponent<EnemyProgressionReward>();
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            combat=GetComponent<CombatState>()??gameObject.AddComponent<CombatState>();
            combat.ConfigurePosture(150);
            if(GetComponent<EnemyNameplate>()==null)gameObject.AddComponent<EnemyNameplate>();
            if (weapon == null) weapon = GetComponentInChildren<DamageDealer>();
        }

        private void OnEnable()
        {
            Mismo.Gameplay.Player.Presentation.PlayerMusic.RegisterBoss(this);
            if (health == null) health = GetComponent<Health>();
            if (health != null)
            {
                combat.PostureBroken += StartStagger;
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
            if (started && !defeatRaised) ResetLife();
        }

        private void Start()
        {
            if (settings == null || weapon == null)
            {
                Debug.LogError("First Boss necesita BossSettings y DamageDealer.", this);
                enabled = false;
                return;
            }

            started = true;
            home = transform.position;
            health.ConfigureMaximum(settings.health*(GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>()?.HealthMultiplier??1));
            (GetComponent<CombatAilment>()??gameObject.AddComponent<CombatAilment>()).ConfigureArmor(settings.armor);
            ResetLife();
        }

        private void OnDisable()
        {
            Mismo.Gameplay.Player.Presentation.PlayerMusic.UnregisterBoss(this);
            if (health != null)
            {
                combat.PostureBroken -= StartStagger;
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
            Stop();
            hitTargets.Clear();
        }

        private bool CanNavigate => agent != null && agent.enabled && agent.isOnNavMesh;

        private void Stop()
        {
            if (!CanNavigate) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        private void Enter(BossState next, float duration = 0f)
        {
            if (next != BossState.Approach && next != BossState.Position && next != BossState.Return)
                Stop();
            state = next;
            combat.Recovering=next==BossState.Recovery;
            stateTimer = Mathf.Max(0f, duration);
            repathTimer = 0f;
            StateChanged?.Invoke(next);
        }

        private void ResetLife()
        {
            combat.ResetCombat();
            if (settings == null || health == null) return;
            if (agent != null)
            {
                agent.speed = settings.speed;
                agent.stoppingDistance = 0.1f;
                agent.updateRotation = false;
                if (!agent.enabled) agent.enabled = true;
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, agent.areaMask))
                    agent.Warp(hit.position);
            }

            foreach (Collider item in GetComponentsInChildren<Collider>()) item.enabled = true;
            health.Revive();
            target = null;
            targetHealth = null;
            attack = null;
            pattern = null;
            patternAttackIndex = -1;
            hitTargets.Clear();
            decisionTimer = 0f;
            repathTimer = 0f;
            lostTime = 0f;
            searchTimer = 0f;
            staggerResistanceTimer = 0f;
            hasLastAttack = false;
            hasLastPattern = false;
            repeatedPatternCount = 0;
            defeatRaised = false;
            wasAggressive = false;
            Enter(BossState.Idle);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (!started || !enabled || deltaTime <= 0f || state == BossState.Dead) return;
            if (health == null || health.IsDead)
            {
                if (state != BossState.Dead) OnDied(default);
                return;
            }

            cooldowns.Tick(deltaTime);
            if (target != null && state != BossState.Idle && state != BossState.Return)
                target.GetComponentInParent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>()?.MarkCombat();
            decisionTimer = Mathf.Max(0f, decisionTimer - deltaTime);
            repathTimer = Mathf.Max(0f, repathTimer - deltaTime);
            searchTimer = Mathf.Max(0f, searchTimer - deltaTime);
            staggerResistanceTimer = Mathf.Max(0f, staggerResistanceTimer - deltaTime);

            bool aggressive = IsAggressive;
            if (aggressive != wasAggressive)
            {
                wasAggressive = aggressive;
                AggressionChanged?.Invoke(aggressive);
            }

            AcquireTargetIfNeeded();
            bool validTarget = IsValidTarget();
            if (!validTarget && state != BossState.Return && state != BossState.Idle && state != BossState.Stagger)
            {
                AbortPattern();
                Enter(BossState.Return);
            }

            if (state == BossState.Return)
            {
                MoveTo(home, settings.speed);
                if (Vector3.Distance(transform.position, home) <= 0.45f) ResetLife();
                return;
            }

            if (state == BossState.Stagger)
            {
                stateTimer -= deltaTime;
                if (stateTimer <= 0f)
                {
                    staggerResistanceTimer = settings.staggerResistance;
                    decisionTimer = CurrentDecisionPause();
                    Enter(BossState.Position);
                }
                return;
            }

            if (!validTarget)
            {
                if (state != BossState.Idle) Enter(BossState.Idle);
                return;
            }

            bool sight = HasSight(target);
            if (sight)
            {
                lostTime = 0f;
                lastSeen = target.position;
            }
            else lostTime += deltaTime;

            if (lostTime >= settings.memoryDuration)
            {
                SetTarget(null);
                AbortPattern();
                Enter(BossState.Return);
                return;
            }

            if (state == BossState.Telegraph)
            {
                stateTimer -= deltaTime;
                if (stateTimer <= 0f) BeginActiveAttack();
                return;
            }

            if (state == BossState.Attack)
            {
                TickAttack(deltaTime);
                return;
            }

            if (state == BossState.Recovery)
            {
                stateTimer -= deltaTime;
                if (stateTimer <= 0f) AdvancePattern();
                return;
            }

            Vector3 offset = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            float distance = offset.magnitude;
            Face(offset, deltaTime);
            bool sameHeight = Mathf.Abs(target.position.y - transform.position.y) <= settings.sameHeightTolerance;

            if (sight && sameHeight && decisionTimer <= 0f && TrySelectPattern()) return;

            if (!sight || distance > settings.frontSlash.MaxRange + 0.35f)
            {
                if (state != BossState.Approach) Enter(BossState.Approach);
                MoveTo(sight ? target.position : lastSeen, settings.speed);
            }
            else
            {
                if (state != BossState.Position) Enter(BossState.Position);
                Vector3 radial = offset.sqrMagnitude > 0.001f ? -offset.normalized : -transform.forward;
                Vector3 desired = target.position + radial * 1.7f + Vector3.Cross(Vector3.up, radial) * 0.55f;
                MoveTo(desired, settings.positioningSpeed);
            }
        }

        private void AcquireTargetIfNeeded()
        {
            if (!autoFindTarget || target != null || searchTimer > 0f) return;
            searchTimer = 0.5f;
            foreach (PlayerController player in FindObjectsByType<PlayerController>())
            {
                Health candidate = player.GetComponent<Health>();
                if ((candidate == null || !candidate.IsDead) &&
                    Vector3.Distance(transform.position, player.transform.position) <= settings.detectionRange &&
                    HasSight(player.transform))
                {
                    SetTarget(player.transform);
                    break;
                }
            }
        }

        private bool IsValidTarget()
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            if (targetHealth == null) targetHealth = target.GetComponentInParent<Health>();
            if (targetHealth != null && targetHealth.IsDead) return false;
            return Vector3.Distance(home, transform.position) <= settings.leashRange &&
                Vector3.Distance(transform.position, target.position) <= settings.loseRange;
        }

        private bool TrySelectPattern()
        {
            candidates.Clear();
            candidateWeights.Clear();
            if (settings.patterns == null) return false;

            foreach (BossPatternDefinition candidate in settings.patterns)
            {
                if (candidate == null || candidate.attacks == null || candidate.attacks.Length == 0 || candidate.attacks.Length > 2)
                    continue;
                BossAttackDefinition first = settings.GetAttack(candidate.attacks[0]);
                if (first == null || !CanStartAttack(first)) continue;
                if (candidate.attacks.Length > 1 && !cooldowns.IsReady(settings.GetAttack(candidate.attacks[1]).CooldownKey)) continue;

                float weight = candidate.Weight(IsAggressive);
                if (hasLastPattern && candidate.id == lastPatternId)
                {
                    if (repeatedPatternCount >= 2) weight = 0f;
                    else weight *= 0.25f;
                }
                if (hasLastAttack && candidate.attacks[0] == lastAttackId) weight *= 0.5f;
                if (weight <= 0f) continue;
                candidates.Add(candidate);
                candidateWeights.Add(weight);
            }

            if (candidates.Count == 0) return false;
            float total = 0f;
            for (int i = 0; i < candidateWeights.Count; i++) total += candidateWeights[i];
            float pick = UnityEngine.Random.value * total;
            BossPatternDefinition selected = candidates[candidates.Count - 1];
            for (int i = 0; i < candidates.Count; i++)
            {
                pick -= candidateWeights[i];
                if (pick <= 0f) { selected = candidates[i]; break; }
            }

            RegisterPatternUse(selected);
            pattern = selected;
            patternAttackIndex = 0;
            PatternStarted?.Invoke(pattern);
            if (!BeginCurrentPatternAttack())
            {
                AbortPattern();
                decisionTimer = settings.attackDecisionInterval;
                return false;
            }
            return true;
        }

        private bool BeginCurrentPatternAttack()
        {
            if (pattern == null || pattern.attacks == null || patternAttackIndex < 0 || patternAttackIndex >= pattern.attacks.Length)
                return false;
            BossAttackDefinition chosen = settings.GetAttack(pattern.attacks[patternAttackIndex]);
            if (chosen == null || !CanStartAttack(chosen) || !cooldowns.TryStart(chosen.CooldownKey, chosen.Cooldown)) return false;

            attack = chosen;
            lastAttackId = chosen.id;
            hasLastAttack = true;
            Vector3 offset = target != null ? Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up) : transform.forward;
            attackDirection = offset.sqrMagnitude > 0.001f ? offset.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(attackDirection);
            Enter(BossState.Telegraph, chosen.Windup);
            AttackStarted?.Invoke(chosen);
            return true;
        }

        private bool CanStartAttack(BossAttackDefinition candidate)
        {
            if (candidate == null || target == null || !cooldowns.IsReady(candidate.CooldownKey)) return false;
            Vector3 offset = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            float distance = offset.magnitude;
            if (distance < candidate.MinRange || distance > candidate.MaxRange) return false;
            if (Mathf.Abs(target.position.y - transform.position.y) > settings.sameHeightTolerance || !HasSight(target)) return false;
            if (candidate.Travel > 0f && CanNavigate && NavMesh.Raycast(transform.position, transform.position + offset.normalized * candidate.Travel, out _, agent.areaMask))
                return false;
            return true;
        }

        private void BeginActiveAttack()
        {
            if (attack == null || target == null)
            {
                AbortPattern();
                Enter(BossState.Position);
                return;
            }
            hitTargets.Clear();
            weapon.Configure(attack.Damage*(GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>()?.DamageMultiplier??1));
            Enter(BossState.Attack, attack.Active);
            ApplyHits();
        }

        private void TickAttack(float deltaTime)
        {
            if (attack == null) { Enter(BossState.Position); return; }
            float step = Mathf.Min(deltaTime, stateTimer);
            if (attack.Travel > 0f) MoveCharge(attackDirection * (attack.Travel * step / attack.Active));
            if (state != BossState.Attack) return;
            ApplyHits();
            if (state != BossState.Attack) return;
            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                AttackFinished?.Invoke(attack);
                Enter(BossState.Recovery, attack.Recovery);
            }
        }

        private void AdvancePattern()
        {
            if (pattern != null && patternAttackIndex + 1 < pattern.attacks.Length)
            {
                patternAttackIndex++;
                if (BeginCurrentPatternAttack()) return;
            }

            float pause = pattern != null ? pattern.finalPause : 0.6f;
            AbortPattern();
            decisionTimer = pause + CurrentDecisionPause();
            Enter(BossState.Position);
        }

        private float CurrentDecisionPause() => IsAggressive ? settings.aggressiveDecisionPause : settings.decisionPause;

        private void RegisterPatternUse(BossPatternDefinition used)
        {
            if (used == null) return;
            if (hasLastPattern && used.id == lastPatternId) repeatedPatternCount++;
            else repeatedPatternCount = 1;
            lastPatternId = used.id;
            hasLastPattern = true;
        }

        private void AbortPattern()
        {
            if (attack != null) hitTargets.Clear();
            attack = null;
            pattern = null;
            patternAttackIndex = -1;
            hitTargets.Clear();
        }

        private void StartStagger(float duration)
        {
            AbortPattern();
            Enter(BossState.Stagger, duration);
        }

        private void OnDamaged(DamageInfo damage)
        {
            if (combat.UsesPosture || settings == null || health == null || health.IsDead || staggerResistanceTimer > 0f) return;
            if ((state == BossState.Approach || state == BossState.Position) && damage.Amount >= settings.staggerDamageThreshold)
                StartStagger(settings.staggerDuration);
        }

        public void OnAttackParried(DamageInfo damage)
        {
            if (settings != null && health != null && !health.IsDead && !combat.Broken && state == BossState.Attack)
                StartStagger(.2f);
        }

        /// <summary>Herramienta de playtest para validar cada telegraph sin esperar al selector.</summary>
        public bool DebugForceAttack(BossAttackId attackId)
        {
            if (!started || health == null || health.IsDead || target == null ||
                (state != BossState.Idle && state != BossState.Position)) return false;
            BossAttackDefinition forced = settings.GetAttack(attackId);
            if (forced == null || !CanStartAttack(forced)) return false;
            AbortPattern();
            pattern = new BossPatternDefinition
            {
                id = BossPatternId.SlashOnly,
                attacks = new[] { attackId },
                finalPause = 0.6f,
                normalWeight = 1f,
                aggressiveWeight = 1f,
                chainSignal = "DEBUG"
            };
            patternAttackIndex = 0;
            RegisterPatternUse(pattern);
            return BeginCurrentPatternAttack();
        }

        [ContextMenu("Debug/Force Front Slash")]
        private void DebugForceFrontSlash() => DebugForceAttack(BossAttackId.FrontSlash);

        [ContextMenu("Debug/Force Overhead Smash")]
        private void DebugForceOverheadSmash() => DebugForceAttack(BossAttackId.OverheadSmash);

        [ContextMenu("Debug/Force Straight Charge")]
        private void DebugForceStraightCharge() => DebugForceAttack(BossAttackId.StraightCharge);

        private void OnDied(DamageInfo damage)
        {
            if (defeatRaised) return;
            defeatRaised = true;
            AbortPattern();
            Enter(BossState.Dead);
            if (agent != null && agent.enabled) agent.enabled = false;
            foreach (Collider item in GetComponentsInChildren<Collider>()) item.enabled = false;
            Defeated?.Invoke();
        }

        private void Face(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 360f * deltaTime);
        }

        private void MoveTo(Vector3 position, float speed)
        {
            if (!CanNavigate || repathTimer > 0f) return;
            repathTimer = Mathf.Max(0.05f, settings.attackDecisionInterval);
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, agent.areaMask)) { Stop(); return; }
            agent.speed = speed * (GetComponent<Mismo.Gameplay.Combat.CombatAilment>()?.SpeedMultiplier ?? 1);
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }

        private bool HasSight(Transform other)
        {
            if (other == null) return false;
            Vector3 start = transform.position + Vector3.up * 0.9f;
            Vector3 end = other.position + Vector3.up * 0.9f;
            Vector3 ray = end - start;
            foreach (RaycastHit hit in Physics.RaycastAll(start, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(other)) continue;
                return false;
            }
            return true;
        }

        private void MoveCharge(Vector3 displacement)
        {
            if (!CanNavigate || displacement.sqrMagnitude <= 0.000001f) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / 0.12f));
            Vector3 delta = displacement / steps;
            for (int i = 0; i < steps && state == BossState.Attack; i++)
            {
                Vector3 from = transform.position;
                if (NavMesh.Raycast(from, from + delta, out _, agent.areaMask)) break;
                bool blocked = false;
                foreach (RaycastHit hit in Physics.CapsuleCastAll(from + Vector3.up * 0.4f, from + Vector3.up * 1.1f,
                    0.42f, delta.normalized, delta.magnitude + 0.02f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.transform.IsChildOf(transform)) { blocked = true; break; }
                }
                if (blocked) break;
                agent.Move(delta * (GetComponent<Mismo.Gameplay.Combat.CombatAilment>()?.SpeedMultiplier ?? 1));
                ApplyHits();
            }
        }

        private void ApplyHits()
        {
            if (state != BossState.Attack || attack == null || target == null) return;
            Vector3 center = transform.position + Vector3.up * 0.85f + attackDirection * attack.ForwardOffset;
            foreach (Collider other in Physics.OverlapBox(center, attack.HalfExtents, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (state != BossState.Attack) break;
                DamageReceiver receiver = other.GetComponentInParent<DamageReceiver>();
                if (receiver == null || receiver.transform.root != target.transform.root || !HasSight(target)) continue;
                if (!hitTargets.Add(receiver)) continue;
                weapon.ApplyTo(receiver.gameObject, other.ClosestPoint(center), attackDirection);
            }
        }

        private float StateDurationForPresentation()
        {
            if (attack == null) return 1f;
            switch (state)
            {
                case BossState.Telegraph: return attack.Windup;
                case BossState.Attack: return attack.Active;
                case BossState.Recovery: return attack.Recovery;
                default: return attack.Windup;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (settings == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, settings.detectionRange);
            if (attack == null) return;
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * 0.85f + transform.forward * attack.ForwardOffset,
                transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, attack.HalfExtents * 2f);
        }
    }
}
