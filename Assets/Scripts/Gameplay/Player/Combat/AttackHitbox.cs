using System.Collections.Generic;
using UnityEngine;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Equipment;

namespace Mismo.Gameplay.Combat
{
    /// <summary>Consulta el volumen del golpe después del movimiento. El combo es dueño del reloj.</summary>
    [RequireComponent(typeof(Collider), typeof(DamageDealer))]
    [DefaultExecutionOrder(275)]
    public sealed class AttackHitbox : MonoBehaviour
    {
        private readonly HashSet<Object> hitTargets = new HashSet<Object>();
        private DamageDealer damageDealer;
        private PlayerMotor motor;
        private Transform owner;
        private ComboStep step;
        private int currentIndex = -1;
        private float progress, evaluatedProgress;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private bool finishing, damageConfigured, rangedBasic;
        private float damageMultiplier;
        private string family;
        private float focusGain;
        private readonly BladeHistory[] blades = { new BladeHistory(), new BladeHistory() };
        private sealed class BladeHistory { public Transform visual; public Vector3 a,b; public bool valid; }
        private Vector3? bladeContact;
        private Vector3 bladeA,bladeB,bladeMotion;
        private void ResetBlades(){foreach(var blade in blades){blade.valid=false;blade.visual=null;}bladeContact=null;}

        public bool IsAttacking => step != null;
        public bool IsWindowOpen { get; private set; }
        public int CurrentAttackIndex => currentIndex;
        public string CurrentAttackId => step != null ? step.Id : string.Empty;

        private void Awake()
        {
            damageDealer = GetComponent<DamageDealer>();
            // Queries own damage; physical trigger callbacks must never produce a second hit.
            var collider = GetComponent<Collider>();
            collider.isTrigger = true;
            collider.enabled = false;
            motor = GetComponentInParent<PlayerMotor>();
            owner = motor != null ? motor.transform : GetComponentInParent<BasicSwordCombo>()?.transform;
            if (owner == null) owner = transform.parent != null ? transform.parent : transform;
        }

        private Quaternion Facing => motor != null && motor.Facing.sqrMagnitude > .001f
            ? Quaternion.LookRotation(motor.Facing, Vector3.up) : owner.rotation;

        public bool BeginAttack(int index, ComboStep definition)
        {
            if (finishing) EvaluateImpact();
            if (IsAttacking || definition == null || !isActiveAndEnabled) return false;
            ResetBlades();
            step = definition;
            currentIndex = index;
            progress = evaluatedProgress = 0;
            previousPosition = owner.position;
            previousRotation = Facing;
            finishing = damageConfigured = rangedBasic = false;
            IsWindowOpen = false;
            hitTargets.Clear();
            var inventory = GetComponentInParent<Mismo.Gameplay.Player.Equipment.Inventory.PlayerInventory>();
            var loadout = GetComponentInParent<EquipmentLoadout>();
            var weapon = loadout != null ? loadout.ActiveDefinition : null;
            damageMultiplier = inventory != null ? inventory.DamageMultiplier(weapon) : 1;
            family = weapon != null ? weapon.MasteryId : null;
            focusGain = GetComponentInParent<AbilityRunner>()?.Current?.Definition.focusGainOnHit
                ?? loadout?.GetAbility(AbilitySlot.Basic)?.focusGainOnHit ?? 0;
            return true;
        }

        public void SetProgress(float normalized) => progress = Mathf.Max(progress, Mathf.Clamp01(normalized));
        // Preserve the final segment until LateUpdate, after OnAnimatorMove applied root displacement.
        public void CompleteAttack() { progress = 1; finishing = true; }
        public void CancelAttack()
        {
            ResetBlades();
            step = null;
            currentIndex = -1;
            IsWindowOpen = finishing = false;
            hitTargets.Clear();
        }
        private void OnDisable() => CancelAttack();
        private void LateUpdate() => EvaluateImpact();

        /// <summary>Explicit simulation boundary, also usable by deterministic callers/tests.</summary>
        public void EvaluateImpact()
        {
            if (step == null) return;
            Vector3 position = owner.position;
            Quaternion rotation = Facing;
            transform.SetPositionAndRotation(position + rotation * Vector3.Scale(step.Center, owner.lossyScale), rotation);
            float advance = progress - evaluatedProgress;
            bool intersects = advance > 0 && step.ImpactEnd > step.ImpactStart &&
                progress >= step.ImpactStart && evaluatedProgress < step.ImpactEnd;
            bladeContact=null;
            if (intersects && !rangedBasic)
            {
                if (!damageConfigured)
                {
                    rangedBasic = GetComponentInParent<WeaponSkillEffects>()?.TryThrowBuckler(motor != null ? motor.Facing : owner.forward) == true;
                    damageDealer.Configure(step.Damage * damageMultiplier, family, focusGain);
                    damageConfigured = true;
                }
                if (!rangedBasic)
                {
                    // Clip the path to the active portion, including windows crossed in a single frame.
                    float from = Mathf.Clamp01((step.ImpactStart - evaluatedProgress) / advance);
                    float to = Mathf.Clamp01((step.ImpactEnd - evaluatedProgress) / advance);
                    Physics.SyncTransforms();
                    if(step.Shape==ComboHitShape.Blade)SweepBlades(from,to,true);
                    else Sweep(Vector3.Lerp(previousPosition, position, from), Quaternion.Slerp(previousRotation, rotation, from),
                        Vector3.Lerp(previousPosition, position, to), Quaternion.Slerp(previousRotation, rotation, to));
                }
            }
            if(step!=null&&step.Shape==ComboHitShape.Blade&&(!intersects||rangedBasic))SweepBlades(0,1,false);
            if (step == null) return; // A parry can cancel this attack from a damage callback.
            IsWindowOpen = !rangedBasic && progress >= step.ImpactStart && progress < step.ImpactEnd;
            previousPosition = position;
            previousRotation = rotation;
            evaluatedProgress = progress;
            if (finishing) CancelAttack();
        }

        private void SweepBlades(float from,float to,bool detect)
        {
            var presentation=GetComponentInParent<WeaponPresentation>();
            var weapon=GetComponentInParent<EquipmentLoadout>()?.ActiveDefinition;
            var mainProfile=weapon!=null?weapon.poseProfile:null;
            for(int hand=0;hand<2&&step!=null;hand++)
            {
                var history=blades[hand];
                var off=weapon!=null?weapon.equippedOffhand:null;
                var profile=hand==1&&off!=null?off.poseProfile:mainProfile;
                bool legacySecond=hand==1&&off==null;
                var visual=presentation!=null?(hand==0?presentation.ActiveVisual:weapon!=null&&weapon.dualWield?presentation.ActiveSecondVisual:null):null;
                if(visual==null||profile==null||!visual.gameObject.activeInHierarchy){history.valid=false;continue;}
                Vector3 a=visual.TransformPoint(legacySecond?profile.secondaryTrailBase:profile.trailBase);
                Vector3 b=visual.TransformPoint(legacySecond?profile.secondaryTrailTip:profile.trailTip);
                bool same=history.valid&&history.visual==visual;
                Vector3 oldA=same?history.a:a,oldB=same?history.b:b;
                if(detect)
                {
                    float radius=step.BladeRadius;
                    Vector3 startA=Vector3.Lerp(oldA,a,from),startB=Vector3.Lerp(oldB,b,from);
                    Vector3 endA=Vector3.Lerp(oldA,a,to),endB=Vector3.Lerp(oldB,b,to);
                    float distance=Mathf.Max(Vector3.Distance(startA,endA),Vector3.Distance(startB,endB));
                    // Dense swept capsules follow both ends of the actual rendered blade.
                    int count=Mathf.Clamp(Mathf.CeilToInt(distance/(radius*.5f)),1,256);
                    for(int i=0;i<=count&&step!=null;i++)
                    {
                        float t=(float)i/count;Vector3 x=Vector3.Lerp(startA,endA,t),y=Vector3.Lerp(startB,endB,t);
                        bladeA=x;bladeB=y;bladeMotion=((endA-startA)+(endB-startB))*.5f;
                        bladeContact=(x+y)*.5f;transform.position=bladeContact.Value;
                        foreach(var other in Physics.OverlapCapsule(x,y,radius,~0,QueryTriggerInteraction.Ignore))ApplyHit(other);
                    }
                }
                history.a=a;history.b=b;history.visual=visual;history.valid=true;
            }
        }

        private void Sweep(Vector3 from, Quaternion fromRotation, Vector3 to, Quaternion toRotation)
        {
            Vector3 scale = owner.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 offset = Vector3.Scale(step.Center, scale);
            Vector3 extents = Vector3.Scale(step.Size, scale) * .5f;
            float radius = step.Radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            int samples = Mathf.Max(1, Mathf.CeilToInt(Quaternion.Angle(fromRotation, toRotation) / 5));
            Vector3 start = from + fromRotation * offset;
            Query(start, fromRotation, extents, radius);
            for (int i = 1; i <= samples && step != null; i++)
            {
                float t = (float)i / samples;
                Quaternion orientation = Quaternion.Slerp(fromRotation, toRotation, t);
                Vector3 end = Vector3.Lerp(from, to, t) + orientation * offset;
                Vector3 delta = end - start;
                if (delta.sqrMagnitude > .0000001f)
                {
                    RaycastHit[] hits = step.Shape == ComboHitShape.Sphere
                        ? Physics.SphereCastAll(start, radius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)
                        : Physics.BoxCastAll(start, extents, delta.normalized, orientation, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var hit in hits) ApplyHit(hit.collider);
                }
                if (step == null) return;
                Query(end, orientation, extents, radius);
                start = end;
            }
        }

        private void Query(Vector3 center, Quaternion rotation, Vector3 extents, float radius)
        {
            Collider[] overlaps = step.Shape == ComboHitShape.Sphere
                ? Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore)
                : Physics.OverlapBox(center, extents, rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (var other in overlaps) ApplyHit(other);
        }

        private void ApplyHit(Collider other)
        {
            if (step == null || other == null || other.transform == owner || other.transform.IsChildOf(owner)) return;
            if (!(other.GetComponentInParent<IDamageReceiver>() is Component receiver)) return;
            if (!hitTargets.Add(receiver)) return;
            Vector3 direction=bladeContact.HasValue&&bladeMotion.sqrMagnitude>.000001f?bladeMotion:other.transform.position-owner.position;
            Vector3 point=bladeContact.HasValue?FindBladeContact(other,bladeA,bladeB,direction):other.ClosestPoint(owner.position+Facing*step.Center);
            damageDealer.ApplyTo(other.gameObject,point,direction);
        }

        public static Vector3 FindBladeContact(Collider other,Vector3 a,Vector3 b,Vector3 direction)
        {
            // Minimize distance from the blade segment to this particular hurtbox.
            float lo=0,hi=1;
            for(int i=0;i<18;i++)
            {
                float left=Mathf.Lerp(lo,hi,1f/3),right=Mathf.Lerp(lo,hi,2f/3);
                Vector3 x=Vector3.Lerp(a,b,left),y=Vector3.Lerp(a,b,right);
                if((other.ClosestPoint(x)-x).sqrMagnitude<(other.ClosestPoint(y)-y).sqrMagnitude)hi=right;else lo=left;
            }
            Vector3 blade=Vector3.Lerp(a,b,(lo+hi)*.5f),point=other.ClosestPoint(blade);
            // If the sampled blade is already inside, recover the entry surface along its motion.
            if((point-blade).sqrMagnitude<.0000001f&&direction.sqrMagnitude>.000001f)
            {
                Vector3 forward=direction.normalized;float reach=other.bounds.size.magnitude+Vector3.Distance(a,b)+1;
                if(other.Raycast(new Ray(blade-forward*reach,forward),out RaycastHit hit,reach*2))point=hit.point;
            }
            return point;
        }

        private void OnDrawGizmosSelected()
        {
            if (step == null || owner == null) return;
            if(step.Shape==ComboHitShape.Blade)
            {
                Gizmos.color=Color.cyan;
                foreach(var blade in blades)if(blade.valid){Gizmos.DrawLine(blade.a,blade.b);Gizmos.DrawWireSphere(blade.a,step.BladeRadius);Gizmos.DrawWireSphere(blade.b,step.BladeRadius);}
                return;
            }
            Gizmos.color = IsWindowOpen ? Color.red : Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(owner.position, Facing, owner.lossyScale);
            if (step.Shape == ComboHitShape.Sphere) Gizmos.DrawWireSphere(step.Center, step.Radius);
            else Gizmos.DrawWireCube(step.Center, step.Size);
        }
    }
}
