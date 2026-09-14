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
        private string family; private float focusGain;
        public Action<Component,Vector3> OnImpact;
        public WeaponSkillEffects BasicEffects;
        void Impact(Collider collider,Vector3 point)
        {
            var receiver=collider.GetComponentInParent<IDamageReceiver>();
            var target=receiver as Component;
            float multiplier=BasicEffects!=null?BasicEffects.BasicMultiplier(attackId,point,target):1;
            bool hit=receiver!=null&&receiver.ReceiveDamage(new DamageInfo(damage*multiplier,owner,point,direction,attackId,posture,true,false,origin,weaponFamilyId:family,focusGainOnHit:focusGain));
            if(hit&&target!=null)BasicEffects?.BasicHit(attackId,target);
            OnImpact?.Invoke(hit?target:null,point);
            Destroy(gameObject);enabled=false;
        }
        public static ProjectileInstance Spawn(GameObject owner, Vector3 origin, Vector3 direction, float damage, float speed, float range, float radius, GameObject visual, long id = 0, float postureDamage = -1,string weaponFamilyId=null, float focusGainOnHit=0)
        {
            var go = new GameObject("Arrow"); go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            var projectile = go.AddComponent<ProjectileInstance>();
            projectile.origin=origin; projectile.attackId=id==0?Mismo.Gameplay.Combat.AttackIdentity.Next():id; projectile.posture=postureDamage;
            projectile.owner = owner; projectile.direction = direction.normalized; projectile.damage = damage;
            projectile.family=weaponFamilyId; projectile.focusGain=focusGainOnHit;
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
                if (BelongsToOwner(overlap)) continue;
                Impact(overlap,overlap.ClosestPoint(transform.position));return;
            }
            float travel = Mathf.Min(remaining, speed * dt);
            var hits = Physics.SphereCastAll(transform.position, radius, direction, travel, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (BelongsToOwner(hit.collider)) continue;
                Impact(hit.collider,hit.point);return;
            }
            transform.position += direction * travel; remaining -= travel;
            if (remaining <= 0) { OnImpact?.Invoke(null,transform.position); enabled = false; Destroy(gameObject); }
        }
        bool BelongsToOwner(Collider other)
        {
            if(owner==null)return false;
            var actor=owner.GetComponentInParent<Health>();
            return other.transform.IsChildOf(owner.transform)||(actor!=null&&other.GetComponentInParent<Health>()==actor);
        }
    }
}

