using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    public abstract partial class EnemyController
    {
        private void PlayPreparationSound(
            GoblinAttack action)
        {
            if (
                action == null ||
                action.preparationSfx == null ||
                action.preparationSfxVolume <= 0f
            )
                return;

            AudioEvents.RaisePlayAbilitySFX(
                action.preparationSfx,
                action.preparationSfxVolume
            );
        }

        private void PlayExecutionSound(
            GoblinAttack action)
        {
            if (
                action == null ||
                action.executionSfx == null ||
                action.executionSfxVolume <= 0f
            )
                return;

            AudioEvents.RaisePlayAbilitySFX(
                action.executionSfx,
                action.executionSfxVolume
            );
        }

        void ReleaseProjectile()
        {
            if (
                projectileReleased ||
                attack == null ||
                attack.projectileVisual == null ||
                target == null
            )
                return;

            projectileReleased = true;

            var presentation =
                GetComponent<
                    CreatureAnimationDriver
                >();

            presentation?.PrepareRelease(
                attack
            );

            Vector3 origin =
                presentation != null
                    ? presentation.ReleaseOrigin
                    : transform.position +
                      Vector3.up *
                      settings.sightHeight +
                      attackDirection * 2;

            presentation?.ReleaseProp();

            Vector3 direction =
                (aimPoint - origin).normalized;

            var impactSettings = new ProjectileImpactSettings
            {
                impactVfx = attack.projectileImpactVfx,
                impactVfxLifetime = attack.projectileImpactVfxLifetime,
                impactSfx = attack.projectileImpactSfx,
                impactSfxVolume = attack.projectileImpactSfxVolume,
                spawnGroundPool = attack.spawnGroundPool,
                groundPoolPrefab = attack.groundPoolPrefab,
                groundPoolLifetime = attack.groundPoolLifetime,
                groundPoolScale = attack.groundPoolScale,
                groundPoolMinUpDot = attack.groundPoolMinUpDot,
                groundPoolDealsDamage = attack.groundPoolDealsDamage,
                groundPoolDamagePerTick = attack.groundPoolDamagePerTick,
                groundPoolDamageTickInterval = attack.groundPoolDamageTickInterval,
                groundPoolDamageRadius = attack.groundPoolDamageRadius
            };

            var projectile =
                Mismo.Gameplay.Player.Equipment
                    .ProjectileInstance
                    .Spawn(
                        gameObject,
                        origin,
                        direction,
                        attack.damage *
                        (
                            GetComponent<
                                Mismo.Gameplay.Player.World.WorldEnemyIdentity
                            >()?.DamageMultiplier ?? 1
                        ),
                        attack.projectileSpeed,
                        attack.projectileRange,
                        attack.projectileRadius,
                        attack.projectileVisual,
                        impactSettings
                    );

            projectile.name =
                settings.displayName +
                " projectile";
        }

        private void MoveCharge(
            Vector3 displacement)
        {
            int steps =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        displacement.magnitude /
                        0.12f
                    )
                );

            Vector3 delta =
                displacement / steps;

            for (
                int i = 0;
                i < steps &&
                State == EnemyState.Attack;
                i++
            )
            {
                Vector3 from =
                    transform.position;

                if (
                    NavMesh.Raycast(
                        from,
                        from + delta,
                        out NavMeshHit edge,
                        agent.areaMask
                    )
                )
                    break;

                bool blocked = false;

                foreach (
                    RaycastHit hit in
                    Physics.CapsuleCastAll(
                        from +
                        Vector3.up *
                        (agent.radius + .1f),
                        from +
                        Vector3.up *
                        Mathf.Max(
                            agent.radius + .1f,
                            agent.height -
                            agent.radius
                        ),
                        agent.radius * .9f,
                        delta.normalized,
                        delta.magnitude +
                        0.02f,
                        ~0,
                        QueryTriggerInteraction.Ignore
                    )
                )
                {
                    if (
                        !hit.transform.IsChildOf(
                            transform
                        )
                    )
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                    break;

                agent.Move(
                    delta *
                    (
                        GetComponent<
                            Mismo.Gameplay.Combat.CombatAilment
                        >()?.SpeedMultiplier ?? 1
                    )
                );

                ApplyHits();
            }
        }

        private void ApplyHits()
        {
            if (
                State != EnemyState.Attack ||
                target == null
            )
                return;

            if (
                attack.kind ==
                    CreatureAttackKind.Projectile ||
                StateProgress <
                    attack.damageStartsAt
            )
                return;

            Vector3 center =
                transform.position +
                Vector3.up *
                attack.hitHeight +
                attackDirection *
                attack.forwardOffset;

            foreach (
                Collider other in
                Physics.OverlapBox(
                    center,
                    attack.halfExtents,
                    transform.rotation,
                    ~0,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                if (
                    State !=
                    EnemyState.Attack
                )
                    break;

                DamageReceiver receiver =
                    other.GetComponentInParent<
                        DamageReceiver
                    >();

                if (
                    receiver == null ||
                    receiver.transform != target ||
                    !HasSight(target)
                )
                    continue;

                if (
                    !hitTargets.Add(receiver)
                )
                    continue;

                weapon.ApplyTo(
                    receiver.gameObject,
                    other.ClosestPoint(center),
                    attackDirection
                );
            }
        }

        private void OnDamaged(
            DamageInfo damage)
        {
            if (
                health.IsDead ||
                settings == null
            )
                return;

            var attacker =
                damage.Source != null
                    ? damage.Source
                        .GetComponentInParent<
                            PlayerController
                        >()
                    : null;

            if (
                attacker != null &&
                attacker.gameObject
                    .activeInHierarchy &&
                (
                    attacker.GetComponent<
                        Health
                    >() == null ||
                    !attacker
                        .GetComponent<Health>()
                        .IsDead
                )
            )
            {
                SetTarget(attacker.transform);

                lastSeen =
                    target.position;

                provokedLoseRange =
                    Vector3.Distance(
                        transform.position,
                        target.position
                    ) +
                    settings.loseRange;

                provokedLeashRange =
                    Vector3.Distance(
                        home,
                        target.position
                    ) +
                    settings.leashRange;

                if (
                    State == EnemyState.Idle ||
                    State == EnemyState.Return
                )
                {
                    Enter(
                        EnemyState.Chase
                    );
                }
            }

            if (combat.UsesPosture)
                return;

            if (
                health.IsDead ||
                damage.Amount <
                    settings
                        .staggerDamageThreshold ||
                staggerResistance > 0f
            )
                return;

            Stagger(
                settings.staggerDuration
            );
        }

        private void Stagger(
            float seconds)
        {
            if (
                health.IsDead ||
                settings == null
            )
                return;

            CancelBackstep();
            comboResponsePending = false;
            attack = null;

            projectileReleased = true;

            hitTargets.Clear();

            staggerResistance =
                seconds +
                settings.staggerResistance;

            Enter(
                EnemyState.Stagger,
                seconds
            );
        }

        private void OnResolved(
            DamageInfo damage,
            HitResult result)
        {
            if (
                settings == null ||
                health.IsDead ||
                result.Outcome != HitOutcome.Hit ||
                result.HealthDamage <= 0 ||
                combat.Broken
            )
                return;

            bool comboHit =
                !damage.Ranged &&
                !damage.Area &&
                damage.Source != null &&
                damage.Source.GetComponent<
                    AttackHitbox
                >() != null;

            if (
                comboHit &&
                settings.interruptibleByCombos &&
                !settings.isBoss
            )
            {
                if (!TryMiniInterrupt())
                    return;

                // Spend the resistance window responding, instead of waiting out another full hit stun.
                if (combat.InterruptImmune)
                {
                    Stagger(Mathf.Max(.05f, Mathf.Min(settings.comboHitStun, settings.comboResponseStun)));
                    comboResponsePending = true;
                    return;
                }

                float remaining =
                    State ==
                        EnemyState.Stagger ||
                    State ==
                        EnemyState.Recovery
                        ? timer
                        : 0;

                Stagger(
                    Mathf.Max(
                        remaining,
                        Mathf.Max(
                            .05f,
                            settings.comboHitStun
                        )
                    )
                );

                return;
            }

            if (
                settings == null ||
                !AllowsHeavyRecoveryInterrupt ||
                settings.isBoss ||
                health.IsDead ||
                combat.Broken ||
                combat.RecoveringFromBreak ||
                staggerResistance > 0 ||
                State != EnemyState.Recovery ||
                result.Outcome != HitOutcome.Hit ||
                result.HealthDamage <= 0 ||
                damage.FeedbackProfile == null ||
                !damage.FeedbackProfile
                    .IsHeavy(damage)
            )
                return;

            if (TryMiniInterrupt())
            {
                Stagger(
                    Mathf.Max(
                        timer,
                        .22f
                    )
                );
            }
        }

        private bool TryMiniInterrupt()
        {
            if (
                attack != null &&
                !attack.CanBeInterrupted &&
                (
                    State ==
                        EnemyState.Telegraph ||
                    State ==
                        EnemyState.Attack
                )
            )
                return false;

            return combat.TryInterrupt(
                settings.maxConsecutiveInterrupts,
                settings.interruptImmunityDuration
            );
        }

        public void OnAttackParried(
            DamageInfo damage)
        {
            if (
                !health.IsDead &&
                !combat.Broken &&
                State ==
                    EnemyState.Attack
            )
            {
                Stagger(.2f);

                GetComponent<
                    EnemyEquipment
                >()?.NotifyParried();
            }
        }

        private void OnDied(
            DamageInfo damage)
        {
            CancelBackstep();
            comboResponsePending = false;
            Enter(EnemyState.Dead);

            hitTargets.Clear();

            agent.enabled = false;

            foreach (
                Collider item in
                GetComponentsInChildren<
                    Collider
                >()
            )
            {
                item.enabled = false;
            }
        }
    }
}
