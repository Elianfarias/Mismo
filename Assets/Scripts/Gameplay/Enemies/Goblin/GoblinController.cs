using System;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    public enum GoblinState { Idle, Chase, Position, Telegraph, Attack, Recovery, Stagger, Return, Dead }

    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(DamageReceiver))]
    [DisallowMultipleComponent]
    public sealed class GoblinController : MonoBehaviour, IParryResponder
    {
        [SerializeField] private GoblinSettings settings;
        [SerializeField] private DamageDealer weapon;
        [SerializeField] private Transform target;
        private CombatState combat;
        private bool pressure;
        private Health health;
        private Health targetHealth;
        private NavMeshAgent agent;
        private Vector3 home, lastSeen, attackDirection;
        private GoblinAttack attack;
        private float timer, duration, decision, chargeCooldown, staggerResistance, lostTime, repath, search;
        private bool started;
        private float provokedLoseRange, provokedLeashRange;
        private int orbitSign = 1;
        private readonly HashSet<UnityEngine.Object> hitTargets = new HashSet<UnityEngine.Object>();
        readonly Dictionary<GoblinAttack,float> attackReady=new Dictionary<GoblinAttack,float>();
        float clock;
        bool projectileReleased;
        Vector3 aimPoint;
        public GoblinState State { get; private set; }
        public GoblinSettings Settings => settings;
        public GoblinAttack CurrentAttack => attack;
        public Transform Target => target;
        public float AttackElapsed=>attack==null?0:State==GoblinState.Telegraph?attack.windup*StateProgress:
            State==GoblinState.Attack?attack.windup+attack.active*StateProgress:State==GoblinState.Recovery?attack.windup+attack.active+attack.recovery*StateProgress:0;
        public Vector3 AttackDirection=>attackDirection;
        public float StateProgress => duration > 0f ? Mathf.Clamp01(1f - timer / duration) : 0f;
        public event Action<GoblinState> StateChanged;

        public void Configure(GoblinSettings configuration, DamageDealer source)
        { settings = configuration; weapon = source; }

        public void SetTarget(Transform value)
        {
            target = value;
            targetHealth = value != null ? value.GetComponentInParent<Health>() : null;
            lostTime = 0f;
            provokedLoseRange = provokedLeashRange = 0f;
        }

        private void Awake()
        {
            if(GetComponent<EnemyProgressionReward>()==null)gameObject.AddComponent<EnemyProgressionReward>();
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            combat=GetComponent<CombatState>()??gameObject.AddComponent<CombatState>();
            combat.ConfigurePosture(70);
            if(!(settings is CreatureSettings) && GetComponent<GoblinHitReaction>()==null)gameObject.AddComponent<GoblinHitReaction>();
            if(GetComponent<EnemyNameplate>()==null)gameObject.AddComponent<EnemyNameplate>();
        }

        private void OnEnable()
        {
            combat.PostureBroken += Stagger;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            GetComponent<DamageReceiver>().Resolved += OnResolved;
            if (started) ResetLife();
        }

        private void Start()
        {
            if (settings == null || weapon == null)
            { Debug.LogError("Goblin necesita Settings y DamageDealer.", this); enabled = false; return; }
            started = true;
            combat.ConfigurePosture(settings.posture);
            combat.ConfigureBreakRecovery(settings is CreatureSettings || settings.isBoss ? 0 : 1.5f);
            home = transform.position;
            health.ConfigureMaximum(settings.health*(GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>()?.HealthMultiplier??1));
            health.Revive();
            ResetLife();
        }

        private void ResetLife()
        {
            combat.ResetCombat();
            agent.updateRotation = false;
            agent.speed = settings.speed;
            agent.stoppingDistance = 0.1f;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, agent.areaMask))
            { agent.enabled = true; agent.Warp(hit.position); }
            else Debug.LogWarning("Goblin fuera del NavMesh. Colocalo sobre una superficie navegable.", this);
            var body = GetComponent<CapsuleCollider>();
            if (body != null) body.enabled = true;
            decision = 0f;
            attackReady.Clear();clock=0;
            chargeCooldown = staggerResistance = lostTime = repath = 0f;
            attack = null;
            SetTarget(null);
            Enter(GoblinState.Idle);
        }

        private void OnDisable()
        {
            combat.PostureBroken -= Stagger;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            GetComponent<DamageReceiver>().Resolved -= OnResolved;
            Stop();
            hitTargets.Clear();
        }

        private bool CanNavigate => agent != null && agent.enabled && agent.isOnNavMesh;
        private void Stop() { if (CanNavigate) { agent.isStopped = true; agent.ResetPath(); } }
        private void Enter(GoblinState state, float seconds = 0f)
        {
            Stop();
            State = state;
            combat.Recovering=state==GoblinState.Recovery;
            duration = timer = seconds;
            repath = 0f;
            StateChanged?.Invoke(state);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            if (!started || !enabled || dt <= 0f || State == GoblinState.Dead) return;
            clock+=dt;
            if (health.IsDead) { Enter(GoblinState.Dead); return; }
            if (target != null && State != GoblinState.Idle && State != GoblinState.Return)
                target.GetComponentInParent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>()?.MarkCombat();
            chargeCooldown = Mathf.Max(0f, chargeCooldown - dt);
            staggerResistance = Mathf.Max(0f, staggerResistance - dt);
            decision -= dt; repath -= dt; search -= dt;
            if (!CanNavigate) return;
            if (State == GoblinState.Stagger)
            {
                timer -= dt;
                if (timer <= 0f && !combat.Broken) { decision = Mathf.Max(.35f, settings.decisionPause); Enter(GoblinState.Position); }
                return;
            }
            if (target == null && State != GoblinState.Return && search <= 0f)
            {
                search = 0.5f;
                foreach (PlayerController player in FindObjectsByType<PlayerController>())
                {
                    Health candidate = player.GetComponent<Health>();
                    if ((candidate == null || !candidate.IsDead) && Vector3.Distance(transform.position, player.transform.position) <= settings.detectionRange
                        && HasSight(player.transform)) { SetTarget(player.transform); break; }
                }
            }
            bool valid = target != null && target.gameObject.activeInHierarchy && (targetHealth == null || !targetHealth.IsDead);
            if (valid && (Vector3.Distance(home, transform.position) > Mathf.Max(settings.leashRange, provokedLeashRange) ||
                Vector3.Distance(transform.position, target.position) > Mathf.Max(settings.loseRange, provokedLoseRange))) valid = false;
            if (!valid && State != GoblinState.Return && State != GoblinState.Idle)
            { SetTarget(null); attack = null; Enter(GoblinState.Return); }
            if (State == GoblinState.Return)
            {
                MoveTo(home, settings.speed);
                if (Vector3.Distance(transform.position, home) < 0.45f) Enter(GoblinState.Idle);
                return;
            }
            if (!valid) { SetTarget(null); return; }
            bool sight = HasSight(target);
            if (sight) { lostTime = 0f; lastSeen = target.position; } else lostTime += dt;
            if (lostTime >= settings.memoryDuration)
            { SetTarget(null); attack = null; Enter(GoblinState.Return); return; }

            if (State == GoblinState.Telegraph)
            {
                // Dirección fijada al comenzar el aviso: el jugador puede salir de la trayectoria.
                timer -= dt;
                if (timer <= 0f)
                {
                    hitTargets.Clear(); weapon.Configure(attack.damage*(GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>()?.DamageMultiplier??1));
                    Enter(GoblinState.Attack, attack.active);
                    if(attack.kind==CreatureAttackKind.Projectile)ReleaseProjectile();
                    ApplyHits();
                }
                return;
            }
            if (State == GoblinState.Attack)
            {
                float step = Mathf.Min(dt, Mathf.Max(0f, timer));
                if (attack.travel > 0f) MoveCharge(attackDirection * (attack.travel * step / Mathf.Max(0.02f, attack.active)));
                if (State != GoblinState.Attack) return; // Un parry puede interrumpir durante MoveCharge.
                ApplyHits();
                if (State != GoblinState.Attack) return;
                timer -= dt;
                ApplyHits();
                if(State!=GoblinState.Attack)return;
                if (timer <= 0f) Enter(GoblinState.Recovery, attack.recovery);
                return;
            }
            if (State == GoblinState.Recovery)
            {
                timer -= dt;
                if (timer <= 0f) { decision = settings.decisionPause; orbitSign *= -1; Enter(GoblinState.Position); }
                return;
            }

            Vector3 offset = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            float distance = offset.magnitude;
            Face(offset, dt);
            bool sameHeight = Mathf.Abs(target.position.y - transform.position.y) < settings.allowedHeightDifference;
            if (sight && sameHeight && decision <= 0f)
            {
                var casting=target.GetComponent<Mismo.Gameplay.Player.Equipment.AbilityRunner>();
                pressure=casting!=null&&casting.IsBusy&&!casting.Current.Began&&casting.Current.Definition.aimFromCamera;
                GoblinAttack chosen = pressure&&distance>=2.5f&&distance<=settings.charge.range&&chargeCooldown<=0 ? settings.charge : distance <= settings.slash.range ? settings.slash :
                    (distance >= 2.5f && distance <= settings.charge.range && chargeCooldown <= 0f ? settings.charge : null);
                if(settings.attacks!=null&&settings.attacks.Length>0)chosen=SelectAttack(distance);
                if (chosen != null)
                {
                    attack = chosen;
                    projectileReleased=false;attackReady[chosen]=clock+Mathf.Max(0,chosen.cooldown)+chosen.windup+chosen.active+chosen.recovery;
                    attackDirection = offset.sqrMagnitude > 0.001f ? offset.normalized : transform.forward;
                    aimPoint=target.position+Vector3.up*.8f;
                    transform.rotation = Quaternion.LookRotation(attackDirection);
                    if (chosen == settings.charge) chargeCooldown = settings.chargeCooldown;
                    Enter(GoblinState.Telegraph, attack.windup);
                    return;
                }
            }
            float preferred=settings.attacks!=null&&settings.attacks.Length>0?settings.preferredRange:settings.slash.range;
            if (!sight || distance > preferred + (pressure ? -.2f : .3f))
            {
                if (State != GoblinState.Chase) Enter(GoblinState.Chase);
                MoveTo(sight ? target.position : lastSeen, settings.speed);
            }
            else
            {
                if (State != GoblinState.Position) Enter(GoblinState.Position);
                Vector3 radial = offset.sqrMagnitude > 0.001f ? -offset.normalized : -transform.forward;
                Vector3 position = target.position + radial * (preferred - 0.15f) +
                    Vector3.Cross(Vector3.up, radial) * (0.65f * orbitSign);
                MoveTo(position, settings.positioningSpeed);
            }
        }

        GoblinAttack SelectAttack(float distance)
        {
            float total=0;foreach(var action in settings.attacks)
                if(Eligible(action,distance))total+=action.weight;
            float roll=UnityEngine.Random.value*total;
            foreach(var action in settings.attacks)if(Eligible(action,distance)){roll-=action.weight;if(roll<0)return action;}
            return null;
        }
        bool Eligible(GoblinAttack action,float distance)=>action!=null&&action.enabled&&action.weight>0&&distance>=action.minimumRange&&distance<=action.range&&
            (!attackReady.TryGetValue(action,out float ready)||clock>=ready)&&
            (action.kind!=CreatureAttackKind.Projectile||action.projectileVisual!=null);
        void ReleaseProjectile()
        {
            if(projectileReleased||attack==null||attack.projectileVisual==null||target==null)return;
            projectileReleased=true;
            var presentation=GetComponent<CreatureAnimationDriver>();
            presentation?.PrepareRelease(attack);
            Vector3 origin=presentation!=null?presentation.ReleaseOrigin:transform.position+Vector3.up*settings.sightHeight+attackDirection*2;
            presentation?.ReleaseProp();
            // Aim is locked during anticipation, including vertical aim; no homing after release.
            Vector3 direction=(aimPoint-origin).normalized;
            var projectile=Mismo.Gameplay.Player.Equipment.ProjectileInstance.Spawn(gameObject,origin,direction,
                attack.damage*(GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>()?.DamageMultiplier??1),attack.projectileSpeed,attack.projectileRange,attack.projectileRadius,attack.projectileVisual);
            projectile.name=settings.displayName+" projectile";
        }

        private void Face(Vector3 direction, float dt)
        {
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 360f * dt);
        }

        private void MoveTo(Vector3 position, float speed)
        {
            if (!CanNavigate || repath > 0f) return;
            repath = 0.2f;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, agent.areaMask)) { Stop(); return; }
            agent.speed = speed * (GetComponent<Mismo.Gameplay.Combat.CombatAilment>()?.SpeedMultiplier ?? 1);
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }

        private bool HasSight(Transform other)
        {
            Vector3 start = transform.position + Vector3.up * settings.sightHeight;
            Vector3 end = other.position + Vector3.up * 0.9f;
            foreach (RaycastHit hit in Physics.RaycastAll(start, (end - start).normalized, Vector3.Distance(start, end), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(other)) continue;
                return false;
            }
            return true;
        }

        private void MoveCharge(Vector3 displacement)
        {
            // Subpasos espaciales mantienen las colisiones y el impacto incluso a bajo framerate.
            int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / 0.12f));
            Vector3 delta = displacement / steps;
            for (int i = 0; i < steps && State == GoblinState.Attack; i++)
            {
                Vector3 from = transform.position;
                if (NavMesh.Raycast(from, from + delta, out NavMeshHit edge, agent.areaMask)) break;
                bool blocked = false;
                foreach (RaycastHit hit in Physics.CapsuleCastAll(from + Vector3.up * (agent.radius+.1f), from + Vector3.up * Mathf.Max(agent.radius+.1f,agent.height-agent.radius),
                    agent.radius*.9f, delta.normalized, delta.magnitude + 0.02f, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(transform)) { blocked = true; break; }
                if (blocked) break;
                agent.Move(delta * (GetComponent<Mismo.Gameplay.Combat.CombatAilment>()?.SpeedMultiplier ?? 1));
                ApplyHits();
            }
        }

        private void ApplyHits()
        {
            if (State != GoblinState.Attack || target == null) return;
            if(attack.kind==CreatureAttackKind.Projectile||StateProgress<attack.damageStartsAt)return;
            Vector3 center = transform.position + Vector3.up * attack.hitHeight + attackDirection * attack.forwardOffset;
            foreach (Collider other in Physics.OverlapBox(center, attack.halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (State != GoblinState.Attack) break;
                DamageReceiver receiver = other.GetComponentInParent<DamageReceiver>();
                if (receiver == null || receiver.transform != target || !HasSight(target)) continue;
                // También se consume el contacto rechazado por parry/iFrames: un intento por ataque.
                if (!hitTargets.Add(receiver)) continue;
                weapon.ApplyTo(receiver.gameObject, other.ClosestPoint(center), attackDirection);
            }
        }

        private void OnDamaged(DamageInfo damage)
        {
            if (health.IsDead || settings == null) return;
            // Projectile damage carries its owner as Source, not the disposable arrow.
            var attacker = damage.Source != null ? damage.Source.GetComponentInParent<PlayerController>() : null;
            if (attacker != null && attacker.gameObject.activeInHierarchy &&
                (attacker.GetComponent<Health>() == null || !attacker.GetComponent<Health>().IsDead))
            {
                SetTarget(attacker.transform);
                lastSeen = target.position;
                // Give the goblin room to reach a distant aggressor without removing escape limits.
                provokedLoseRange = Vector3.Distance(transform.position, target.position) + settings.loseRange;
                provokedLeashRange = Vector3.Distance(home, target.position) + settings.leashRange;
                if (State == GoblinState.Idle || State == GoblinState.Return) Enter(GoblinState.Chase);
            }
            if (combat.UsesPosture) return;
            if (health.IsDead || damage.Amount < settings.staggerDamageThreshold || staggerResistance > 0f) return;
            Stagger(settings.staggerDuration);
        }
        private void Stagger(float seconds)
        {
            if (health.IsDead || settings == null) return;
            // Discard the action: returning from stun must never resume its hit or charge.
            attack = null;
            projectileReleased = true;
            hitTargets.Clear();
            staggerResistance = seconds + settings.staggerResistance;
            Enter(GoblinState.Stagger, seconds);
        }
        private void OnResolved(DamageInfo damage, HitResult result)
        {
            if (settings == null || health.IsDead || result.Outcome != HitOutcome.Hit || result.HealthDamage <= 0 || combat.Broken) return;
            bool comboHit = !damage.Ranged && !damage.Area && damage.Source != null &&
                damage.Source.GetComponent<AttackHitbox>() != null;
            bool armoredAttack = attack != null && attack.resistComboInterrupt &&
                (State == GoblinState.Telegraph || State == GoblinState.Attack);
            if (comboHit && settings.interruptibleByCombos && !settings.isBoss && !armoredAttack)
            {
                // Consecutive hits renew hitstun independently of posture-break immunity.
                // Preserve any longer punish/stun window already in progress.
                float remaining = State == GoblinState.Stagger || State == GoblinState.Recovery ? timer : 0;
                Stagger(Mathf.Max(remaining, Mathf.Max(.05f, settings.comboHitStun)));
                return;
            }
            // Other damage retains the existing heavy-hit recovery interruption.
            if (settings == null || settings is CreatureSettings || settings.isBoss || health.IsDead || combat.Broken || combat.RecoveringFromBreak || staggerResistance > 0 ||
                State != GoblinState.Recovery || result.Outcome != HitOutcome.Hit || result.HealthDamage <= 0 ||
                damage.FeedbackProfile == null || !damage.FeedbackProfile.IsHeavy(damage)) return;
            // Never shorten the punish window by replacing a longer recovery with a flinch.
            Stagger(Mathf.Max(timer, .22f));
        }
        public void OnAttackParried(DamageInfo damage)
        {
            if (!health.IsDead && !combat.Broken && State == GoblinState.Attack)
            {
                Stagger(.2f);
                GetComponent<EnemyEquipment>()?.NotifyParried();
            }
        }
        private void OnDied(DamageInfo damage)
        {
            Enter(GoblinState.Dead);
            hitTargets.Clear();
            agent.enabled = false;
            foreach (Collider item in GetComponentsInChildren<Collider>()) item.enabled = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (settings == null) return;
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, settings.detectionRange);
            if (attack == null) return;
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * 0.85f + transform.forward * attack.forwardOffset,
                transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, attack.halfExtents * 2f);
        }
    }
}
