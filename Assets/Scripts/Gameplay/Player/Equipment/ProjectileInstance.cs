using System;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    public sealed class ProjectileInstance : MonoBehaviour
    {
        private GameObject owner;
        private Vector3 direction;
        private float damage, speed, remaining, radius, posture;
        private long attackId;
        private Vector3 origin;
        public static ProjectileInstance Spawn(GameObject owner, Vector3 origin, Vector3 direction, float damage, float speed, float range, float radius, GameObject visual, long id = 0, float postureDamage = -1)
        {
            var go = new GameObject("Arrow"); go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            var projectile = go.AddComponent<ProjectileInstance>();
            projectile.origin=origin; projectile.attackId=id==0?Mismo.Gameplay.Combat.AttackIdentity.Next():id; projectile.posture=postureDamage;
            projectile.owner = owner; projectile.direction = direction.normalized; projectile.damage = damage;
            projectile.speed = Mathf.Max(.1f, speed); projectile.remaining = Mathf.Max(.1f, range); projectile.radius = Mathf.Max(.01f, radius);
            if (visual != null) Instantiate(visual, go.transform, false);
            return projectile;
        }
        private void Update() => Step(Time.deltaTime);
        public void Step(float dt)
        {
            if (!enabled || dt <= 0) return;
            foreach (var overlap in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && overlap.transform.root == owner.transform.root) continue;
                overlap.GetComponentInParent<IDamageReceiver>()?.ReceiveDamage(new DamageInfo(damage, owner, overlap.ClosestPoint(transform.position), direction, attackId, posture, true, false, origin));
                Destroy(gameObject); enabled = false; return;
            }
            float travel = Mathf.Min(remaining, speed * dt);
            var hits = Physics.SphereCastAll(transform.position, radius, direction, travel, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (owner != null && hit.transform.root == owner.transform.root) continue;
                hit.collider.GetComponentInParent<IDamageReceiver>()?.ReceiveDamage(new DamageInfo(damage, owner, hit.point, direction, attackId, posture, true, false, origin));
                Destroy(gameObject); enabled = false; return;
            }
            transform.position += direction * travel; remaining -= travel;
            if (remaining <= 0) { enabled = false; Destroy(gameObject); }
        }
    }
}
