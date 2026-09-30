using System;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    [Serializable]
    public sealed class ProjectileImpactSettings
    {
        public GameObject impactVfx;

        [Min(0f)]
        public float impactVfxLifetime = 3f;

        public AudioClip impactSfx;

        [Range(0f, 1f)]
        public float impactSfxVolume = 1f;

        public bool spawnGroundPool;
        public GameObject groundPoolPrefab;

        [Min(0.1f)]
        public float groundPoolLifetime = 5f;

        [Min(0.01f)]
        public float groundPoolScale = 1f;

        [Range(0f, 1f)]
        public float groundPoolMinUpDot = 0.7f;

        public bool groundPoolDealsDamage;
        public float groundPoolDamagePerTick;
        public float groundPoolDamageTickInterval = 0.5f;
        public float groundPoolDamageRadius = 1.25f;
    }

    public sealed class ProjectileInstance : MonoBehaviour
    {
        private GameObject owner;
        private Vector3 direction;
        private float damage, speed, remaining, radius, posture;
        private long attackId;
        private Vector3 origin;
        private string family; private float focusGain;
        private ProjectileImpactSettings impactSettings;
        public Action<Component,Vector3> OnImpact;
        public WeaponSkillEffects BasicEffects;
        void Impact(Collider collider,Vector3 point,Vector3 normal)
        {
            SpawnImpactEffects(point,normal);
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
            return SpawnCore(owner,origin,direction,damage,speed,range,radius,visual,null,id,postureDamage,weaponFamilyId,focusGainOnHit);
        }

        public static ProjectileInstance Spawn(GameObject owner, Vector3 origin, Vector3 direction, float damage, float speed, float range, float radius, GameObject visual, ProjectileImpactSettings impactSettings, long id = 0, float postureDamage = -1,string weaponFamilyId=null, float focusGainOnHit=0)
        {
            return SpawnCore(owner,origin,direction,damage,speed,range,radius,visual,impactSettings,id,postureDamage,weaponFamilyId,focusGainOnHit);
        }

        private static ProjectileInstance SpawnCore(GameObject owner, Vector3 origin, Vector3 direction, float damage, float speed, float range, float radius, GameObject visual, ProjectileImpactSettings impactSettings, long id, float postureDamage, string weaponFamilyId, float focusGainOnHit)
        {
            var go = new GameObject("Arrow"); go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            var projectile = go.AddComponent<ProjectileInstance>();
            projectile.origin=origin; projectile.attackId=id==0?Mismo.Gameplay.Combat.AttackIdentity.Next():id; projectile.posture=postureDamage;
            projectile.owner = owner; projectile.direction = direction.normalized; projectile.damage = damage;
            projectile.family=weaponFamilyId; projectile.focusGain=focusGainOnHit; projectile.impactSettings=impactSettings;
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
                Vector3 point = overlap.ClosestPoint(transform.position);
                Vector3 normal = transform.position - point;
                if (normal.sqrMagnitude <= 0.0001f) normal = -direction;
                Impact(overlap,point,normal);return;
            }
            float travel = Mathf.Min(remaining, speed * dt);
            var hits = Physics.SphereCastAll(transform.position, radius, direction, travel, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (BelongsToOwner(hit.collider)) continue;
                Impact(hit.collider,hit.point,hit.normal);return;
            }
            transform.position += direction * travel; remaining -= travel;
            if (remaining <= 0) { OnImpact?.Invoke(null,transform.position); enabled = false; Destroy(gameObject); }
        }

        private void SpawnImpactEffects(Vector3 point, Vector3 normal)
        {
            if (impactSettings == null) return;

            Vector3 surfaceNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up,surfaceNormal);

            if (impactSettings.impactVfx != null)
            {
                var impactVfx = Instantiate(impactSettings.impactVfx,point + surfaceNormal * 0.002f,surfaceRotation);
                if (impactSettings.impactVfxLifetime > 0f)
                    Destroy(impactVfx,impactSettings.impactVfxLifetime);
            }

            AudioRuntime.PlayWorldSFX(impactSettings.impactSfx,point,impactSettings.impactSfxVolume);

            if (!impactSettings.spawnGroundPool || impactSettings.groundPoolPrefab == null)
                return;

            float upDot = Vector3.Dot(surfaceNormal,Vector3.up);
            if (upDot < Mathf.Clamp01(impactSettings.groundPoolMinUpDot))
                return;

            var groundPool = Instantiate(impactSettings.groundPoolPrefab,point + surfaceNormal * 0.004f,surfaceRotation);
            groundPool.transform.localScale *= Mathf.Max(0.01f,impactSettings.groundPoolScale);
            float poolLifetime = Mathf.Max(0.1f,impactSettings.groundPoolLifetime);

            if (impactSettings.groundPoolDealsDamage && impactSettings.groundPoolDamagePerTick > 0f)
            {
                var damagePool = groundPool.GetComponent<GroundDamagePool>() ?? groundPool.AddComponent<GroundDamagePool>();
                damagePool.Initialize(owner,impactSettings.groundPoolDamageRadius,poolLifetime,impactSettings.groundPoolDamageTickInterval,impactSettings.groundPoolDamagePerTick,groundPool.transform.position, family,focusGain);
            }

            Destroy(groundPool,poolLifetime);
        }

        bool BelongsToOwner(Collider other)
        {
            if(owner==null)return false;
            var actor=owner.GetComponentInParent<Health>();
            return other.transform.IsChildOf(owner.transform)||(actor!=null&&other.GetComponentInParent<Health>()==actor);
        }
    }

    public sealed class GroundDamagePool : MonoBehaviour
    {
        private GameObject owner;
        private float radius, duration, interval, damage, age, nextTick;
        private Vector3 origin;
        private string family;
        private float focusGain;
        private bool initialized;

        public void Initialize(GameObject source, float damageRadius, float lifetime, float tickInterval, float damagePerTick, Vector3 damageOrigin, string weaponFamilyId = null, float focusGainOnHit = 0f)
        {
            owner = source;
            radius = Mathf.Max(0.01f,damageRadius);
            duration = Mathf.Max(0.1f,lifetime);
            interval = Mathf.Max(0.05f,tickInterval);
            damage = Mathf.Max(0f,damagePerTick);
            origin = damageOrigin;
            family = weaponFamilyId;
            focusGain = focusGainOnHit;
            age = 0f;
            nextTick = 0f;
            initialized = damage > 0f;
        }

        private void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (!enabled || !initialized || dt <= 0f) return;

            age += dt;
            while (nextTick <= age && nextTick < duration)
            {
                Pulse();
                nextTick += interval;
            }
        }

        private void Pulse()
        {
            long attackId = AttackIdentity.Next();
            var visited = new System.Collections.Generic.HashSet<UnityEngine.Object>();
            Vector3 queryOrigin = transform.position + transform.up * 0.7f;

            foreach (var other in Physics.OverlapSphere(queryOrigin,radius,~0,QueryTriggerInteraction.Ignore))
            {
                var receiver = other.GetComponentInParent<IDamageReceiver>();
                if (!(receiver is Component target) || !visited.Add(target)) continue;
                if (owner != null && target.transform.root == owner.transform.root) continue;

                Vector3 point = other.ClosestPoint(queryOrigin);
                receiver.ReceiveDamage(new DamageInfo(damage,owner,point,-transform.up,attackId,damage * 0.5f,true,true,origin,false,family,focusGain));
            }
        }
    }
}

