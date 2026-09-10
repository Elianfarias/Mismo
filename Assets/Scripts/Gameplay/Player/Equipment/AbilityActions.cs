using System;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    [Serializable]
    public sealed class MoveCasterAction : AbilityAction
    {
        public float distance = 3;
        public override void Tick(AbilityExecution c, float dt)
        {
            Vector3 direction = Vector3.ProjectOnPlane(c.Direction, Vector3.up).normalized;
            c.Motor.RequestControlledDisplacement(direction * (distance * dt / Mathf.Max(.01f, c.Definition.active)), direction, 10);
        }
    }

    [Serializable]
    public sealed class MeleeAction : AbilityAction
    {
        public float damage = 18;
        public float postureDamage = -1;
        public float radius = .6f;
        public float forward = .8f;
        public override void Tick(AbilityExecution c, float dt)
        {
            Vector3 origin = c.Owner.transform.position + Vector3.up + c.Direction * forward;
            foreach (var other in Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var receiver = other.GetComponentInParent<IDamageReceiver>();
                if (!(receiver is Component target) || target.transform.root == c.Owner.transform.root || !c.HitTargets.Add(target.GetInstanceID())) continue;
                Vector3 point = other.ClosestPoint(origin);
                if (Physics.Linecast(origin, point, out var wall, ~0, QueryTriggerInteraction.Ignore) && wall.transform.root != c.Owner.transform.root && wall.collider.GetComponentInParent<IDamageReceiver>() != receiver) continue;
                receiver.ReceiveDamage(new DamageInfo(damage*c.DamageMultiplier, c.Owner, other.ClosestPoint(origin), (target.transform.position-c.Owner.transform.position).normalized, c.AttackId, postureDamage, weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit));
            }
        }
    }

    [Serializable]
    public sealed class ParryAction : AbilityAction
    {
        public override void Begin(AbilityExecution c) { c.Runner.Parry?.OpenWindow(c.Definition.active); c.Owner.GetComponent<DefenseWindow>()?.OpenParry(c.Definition.active); }
        public override void End(AbilityExecution c) { c.Runner.Parry?.Cancel(); c.Owner.GetComponent<DefenseWindow>()?.CloseParry(); }
    }

    [Serializable]
    public sealed class ProjectileAction : AbilityAction
    {
        public GameObject visual;
        public float damage = 14;
        public float postureDamage = -1;
        public float speed = 28;
        public float radius = .09f;
        public override void Begin(AbilityExecution c)
        {
            Vector3 origin = WeaponAim.Muzzle(c.Owner);
            ProjectileInstance.Spawn(c.Owner, origin, c.AimPoint.HasValue ? (c.AimPoint.Value - origin).normalized : c.Direction, damage * c.DamageMultiplier * (c.Definition.chargeable ? c.Definition.chargeDamageMultiplier.Evaluate(c.Charge) : 1), speed, c.Definition.range, radius, visual, c.AttackId, (postureDamage < 0 ? damage*.8f : postureDamage) * (c.Definition.chargeable ? c.Definition.chargePostureMultiplier.Evaluate(c.Charge) : 1), c.WeaponFamilyId,c.Definition.focusGainOnHit);
        }
    }

    [Serializable]
    public sealed class GroundAreaAction : AbilityAction
    {
        public float radius = 2.5f;
        public float duration = 3;
        public float interval = .6f;
        public float damage = 7;
        public GameObject fallingVisual;
        [Min(1)] public int arrowsPerVolley = 9;
        [Min(.5f)] public float fallHeight = 5;
        [Min(.1f)] public float fallSpeed = 12;
        public override void Begin(AbilityExecution c) => AreaInstance.Spawn(c.Owner, c.GroundPoint, radius, duration, interval, damage*c.DamageMultiplier,
            fallingVisual, arrowsPerVolley, fallHeight, fallSpeed,c.WeaponFamilyId,c.Definition.focusGainOnHit);
    }
}

