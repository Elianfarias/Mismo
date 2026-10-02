using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public enum ImpTactic { Approach, Strafe, Retreat, Reposition, Hold }

    /// <summary>Ranged skirmisher. Attacks still use the shared telegraph, damage and recovery pipeline.</summary>
    public sealed class ImpController : HumanoidEnemyController
    {
        [Header("Posicionamiento del Imp")]
        [Min(.2f), Tooltip("Margen alrededor de Preferred Range en los datos del enemigo.")]
        public float rangeTolerance = 1.5f;
        [Min(.1f)] public float reactionDelay = .3f;
        [Min(.1f)] public float retreatSpeed = 5.5f;
        [Min(.1f)] public float retreatDuration = .65f;
        [Min(.1f)] public float retreatCooldown = 3f;
        [Min(.2f)] public float strafeStep = 2f;

        public ImpTactic Tactic { get; private set; }
        public Vector3 TacticalDestination => destination;
        float reaction, retreatLeft, retreatWait, reconsider;
        bool leaveMelee, hasDestination;
        int side;
        Vector3 destination;

        float NearRange => Mathf.Max(.5f, PreferredRange - rangeTolerance);
        float FarRange => PreferredRange + rangeTolerance;

        bool HasRangedAction
        {
            get
            {
                if (!AttackSelection.HasActions(Settings)) return false;
                foreach (var action in Settings.attacks)
                    if (action != null && action.enabled && action.weight > 0 &&
                        action.kind == CreatureAttackKind.Projectile && action.projectileVisual != null) return true;
                return false;
            }
        }

        protected override void ResetTactics()
        {
            reaction = reactionDelay;
            retreatLeft = retreatWait = reconsider = 0;
            leaveMelee = hasDestination = false;
            side = (GetEntityId().GetHashCode() & 1) == 0 ? 1 : -1;
            Tactic = ImpTactic.Hold;
        }

        protected override void AttackFinished(GoblinAttack completed)
        {
            side *= -1;
            hasDestination = false;
            reconsider = 0;
            reaction = reactionDelay;
            leaveMelee = completed != null && completed.kind != CreatureAttackKind.Projectile;
        }

        protected override void ComboResponseFinished()
        {
            // The quick step already bought space. Take a firing/melee decision immediately,
            // preserving attack cooldowns and avoiding a second retreat chained onto it.
            reaction = retreatLeft = reconsider = 0;
            retreatWait = retreatCooldown;
            hasDestination = leaveMelee = false;
        }

        protected override void PrepareDecision(float dt, float distance, bool sight)
        {
            reaction = Mathf.Max(0, reaction - dt);
            retreatLeft = Mathf.Max(0, retreatLeft - dt);
            retreatWait = Mathf.Max(0, retreatWait - dt);
            reconsider -= dt;
            if (!HasRangedAction) return;

            // Commit briefly to a destination. Only loss of sight or finishing a retreat can break it early.
            if (reconsider > 0 &&
                (Tactic != ImpTactic.Retreat || retreatLeft > 0) &&
                (Tactic == ImpTactic.Reposition || sight)) return;
            hasDestination = false;
            reconsider = .35f;
            Vector3 anchor = sight ? Target.position : LastKnownTargetPosition;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - anchor, Vector3.up).normalized;
            if (away.sqrMagnitude < .001f) away = -transform.forward;
            Vector3 tangent = Vector3.Cross(Vector3.up, away) * side;

            if (!sight)
            {
                Tactic = ImpTactic.Reposition;
                // Seek a nearby angle using remembered position; do not follow an unseen target through walls.
                hasDestination = TrySideStep(anchor, away, tangent, false);
                return;
            }

            if (distance < NearRange && (retreatLeft > 0 || retreatWait <= 0))
            {
                float step = Mathf.Min(2.5f, Mathf.Max(.8f, PreferredRange - distance));
                hasDestination = TryTacticalDestination(transform.position + away * step, true, false, out destination) ||
                    TryTacticalDestination(transform.position + (away + tangent * .6f).normalized * step, true, false, out destination) ||
                    TryTacticalDestination(transform.position + (away - tangent * .6f).normalized * step, true, false, out destination);
                if (hasDestination)
                {
                    if (retreatLeft <= 0)
                    {
                        retreatLeft = retreatDuration;
                        retreatWait = retreatDuration + retreatCooldown;
                    }
                    Tactic = ImpTactic.Retreat;
                    return;
                }
            }
            leaveMelee = false;
            if (distance > FarRange)
            {
                Tactic = ImpTactic.Approach;
                // Stop at a firing band instead of using the player's position as the destination.
                hasDestination = TryTacticalDestination(anchor + away * PreferredRange, false, false, out destination);
                return;
            }
            if (distance < NearRange)
            {
                // The player caught up, or our retreat budget ran out. Give them a punishable window.
                Tactic = ImpTactic.Hold;
                return;
            }
            Tactic = ImpTactic.Strafe;
            hasDestination = TrySideStep(anchor, away, tangent, true);
        }

        bool TrySideStep(Vector3 anchor, Vector3 away, Vector3 tangent, bool requireSight)
        {
            float radius = Mathf.Max(.5f, PreferredRange);
            Vector3 first = anchor + (away * radius + tangent * strafeStep).normalized * radius;
            first.y = transform.position.y;
            if (TryTacticalDestination(first, true, requireSight, out destination)) return true;
            Vector3 second = anchor + (away * radius - tangent * strafeStep).normalized * radius;
            second.y = transform.position.y;
            if (!TryTacticalDestination(second, true, requireSight, out destination)) return false;
            side *= -1;
            return true;
        }

        protected override GoblinAttack ChooseAttack(float distance, bool pressure)
        {
            if (!HasRangedAction)
                return AttackSelection.HasActions(Settings)
                    ? AttackSelection.SelectWeighted(Settings, distance)
                    : AttackSelection.SelectLegacyMelee(Settings, distance, pressure);
            if (reaction > 0) return null;
            // A melee strike buys room, but we still finish its recovery before disengaging.
            var melee = AttackSelection.SelectWeighted(Settings, distance, IsMelee);
            if (melee != null && !(leaveMelee && retreatLeft > 0)) return melee;
            if (Tactic == ImpTactic.Retreat || distance > FarRange) return null;
            return AttackSelection.SelectWeighted(Settings, distance, IsProjectile);
        }

        static bool IsMelee(GoblinAttack action) => action.kind != CreatureAttackKind.Projectile;
        static bool IsProjectile(GoblinAttack action) => action.kind == CreatureAttackKind.Projectile;

        protected override bool MoveTactically(Vector3 offset, float distance, bool sight)
        {
            if (!HasRangedAction) return false;
            if (hasDestination)
            {
                PositionAt(destination, Tactic == ImpTactic.Retreat ? retreatSpeed :
                    Tactic == ImpTactic.Approach ? Settings.speed : Settings.positioningSpeed);
                return true;
            }
            if (!sight || Tactic == ImpTactic.Approach) return false;
            HoldPosition();
            return true;
        }
    }
}
