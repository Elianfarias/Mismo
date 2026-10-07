using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    /// <summary>One ground trap; the ability owns the prefab reference, this instance owns its lifetime.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(HunterTrapVisual))]
    public sealed class HunterTrap : MonoBehaviour
    {
        GameObject owner;
        string family, abilityId;
        long useId, placementOrder;
        static long nextPlacementOrder;
        float damage, expires, armed, triggeredAt;
        bool initialized, triggered;
        GroundThrowPath flight;
        float flightElapsed;
        public bool IsFlying {get;private set;}
        HunterTrapVisual visual;
        public bool Triggered => triggered;
        public bool Waiting => initialized && !triggered && isActiveAndEnabled;
        public bool IsArmed => Waiting && !IsFlying && Time.time >= armed;

        public static HunterTrap Launch(AbilityExecution cast,GameObject prefab,GroundThrowPath path)
        {
            if(!path.valid)return null;
            var trap=Spawn(cast,prefab);if(trap==null)return null;
            trap.flight=path;trap.flightElapsed=0;trap.IsFlying=true;
            trap.armed=trap.expires=float.PositiveInfinity;
            trap.transform.SetPositionAndRotation(path.start,path.LandingRotation(cast.Direction));
            return trap;
        }

        public static HunterTrap Spawn(AbilityExecution cast, GameObject prefab = null)
        {
            if (cast == null || cast.Owner == null) return null;
            if (prefab == null && cast.Definition.actions != null)
                foreach (var action in cast.Definition.actions) if (action is TrapAction trapAction && trapAction.prefab != null) { prefab = trapAction.prefab; break; }
            if (prefab == null || prefab.GetComponent<HunterTrap>() == null)
            { Debug.LogError("La habilidad Trampa de cazador necesita su prefab HunterTrap.", cast.Owner); return null; }
            HunterTrap oldest = null; int count = 0;
            foreach (var trap in FindObjectsByType<HunterTrap>())
                if (trap.Waiting && trap.owner == cast.Owner)
                { count++; if (oldest == null || trap.placementOrder < oldest.placementOrder) oldest = trap; }
            if (count >= 2 && oldest != null) oldest.Remove();

            Vector3 point = cast.GroundPoint, up = Vector3.up;
            float nearest = float.MaxValue;
            // Ground points can lie on slopes; ignore actors when finding the supporting surface.
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * .8f, Vector3.down, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<Health>() == null && !hit.transform.IsChildOf(cast.Owner.transform) &&
                    hit.distance < nearest && hit.normal.y > .4f)
                { nearest = hit.distance; point = hit.point; up = hit.normal; }
            var forward = Vector3.ProjectOnPlane(cast.Direction, up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            var go = Instantiate(prefab, point + up * .006f, Quaternion.LookRotation(forward, up));
            go.name = "Trampa de cazador";
            var instance = go.GetComponent<HunterTrap>();
            instance.owner = cast.Owner; instance.family = cast.WeaponFamilyId;
            instance.damage = 12 * cast.DamageMultiplier; instance.expires = Time.time + 30; instance.armed = Time.time + .4f;
            instance.abilityId = cast.Definition.Id; instance.useId = cast.AttackId; instance.initialized = true;
            instance.placementOrder = ++nextPlacementOrder;
            instance.visual = go.GetComponent<HunterTrapVisual>(); instance.visual.SetClosure(0);
            return instance;
        }
        void Update()
        {
            if (!initialized) return; // The art prefab can be previewed without spawning gameplay.
            if (owner == null) { Remove(); return; }
            if(IsFlying){AdvanceFlight(Time.deltaTime);return;}
            if (triggered)
            {
                float elapsed = Time.time - triggeredAt;
                visual.SetClosure(1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / .12f), 3));
                if (elapsed >= 1.5f) Remove();
                return;
            }
            if (Time.time >= expires) { Remove(); return; }
            if (Time.time < armed) return;
            foreach (var collider in Physics.OverlapSphere(transform.position + transform.up * .45f, .65f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!(collider.GetComponentInParent<IDamageReceiver>() is Component target) || target.transform.IsChildOf(owner.transform)) continue;
                var targetHealth = target.GetComponent<Health>();
                if (targetHealth != null && targetHealth.IsDead) continue;
                // Mark consumed before damage callbacks: one trigger, even with several body colliders.
                triggered = true; triggeredAt = Time.time;
                bool hit = ((IDamageReceiver)target).ReceiveDamage(new DamageInfo(damage, owner, target.transform.position,
                    Vector3.zero, AttackIdentity.Next(), 0, area: true, weaponFamilyId: family, abilityId: abilityId, abilityUseId: useId));
                if (hit && (targetHealth == null || !targetHealth.IsDead)) CombatAilment.Slow(target.gameObject, 0, 1.5f);
                return;
            }
        }
        void AdvanceFlight(float dt)
        {
            if(dt<=0)return;
            float from=Mathf.Clamp01(flightElapsed/flight.duration);
            flightElapsed+=dt;float to=Mathf.Clamp01(flightElapsed/flight.duration);
            int steps=Mathf.Max(1,Mathf.CeilToInt((to-from)*GroundThrowTrajectory.Segments));
            Vector3 previous=flight.Sample(from);
            for(int i=1;i<=steps;i++)
            {
                Vector3 next=flight.Sample(Mathf.Lerp(from,to,i/(float)steps));
                if(GroundThrowTrajectory.Blocked(owner,previous,next,flight.end,out _)){Remove();return;}
                previous=next;
            }
            transform.position=flight.Sample(to);
            if(to<1)return;
            if(!GroundThrowTrajectory.Supported(owner,flight.end,flight.normal)){Remove();return;}
            IsFlying=false;armed=Time.time+.4f;expires=Time.time+30;
        }
        void Remove() { initialized = false; enabled = false; gameObject.SetActive(false); Destroy(gameObject); }
    }
}
