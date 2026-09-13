using System;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    [Serializable]
    public sealed class GuardAction : AbilityAction
    {
        public override void Begin(AbilityExecution c)=>c.Owner.GetComponent<DefenseWindow>()?.OpenGuard(c.Definition.active);
        public override void End(AbilityExecution c)=>c.Owner.GetComponent<DefenseWindow>()?.CloseGuard();
    }
    [Serializable]
    public sealed class RepeatedStrikeAction : AbilityAction
    {
        public float damage=7,interval=.2f,radius=.7f,forward=1,slow=1,slowSeconds,stunSeconds,bleedDamage,bleedSeconds,pushDistance;
        public override void Begin(AbilityExecution c){c.ActionTimes[this]=0;Strike(c);}
        public override void Tick(AbilityExecution c,float dt)
        {
            float previous=c.ActionTimes[this],next=previous+dt;c.ActionTimes[this]=next;
            for(float at=(Mathf.Floor(previous/Mathf.Max(.05f,interval))+1)*Mathf.Max(.05f,interval);at<=next&&at<c.Definition.active;at+=Mathf.Max(.05f,interval))Strike(c);
        }
        void Strike(AbilityExecution c)
        {
            long id=AttackIdentity.Next();var seen=new System.Collections.Generic.HashSet<Component>();
            var origin=c.Owner.transform.position+Vector3.up+c.Direction*forward;
            foreach(var collider in Physics.OverlapSphere(origin,radius,~0,QueryTriggerInteraction.Ignore))
            {
                if(!(collider.GetComponentInParent<IDamageReceiver>() is Component target)||target.transform.IsChildOf(c.Owner.transform)||!seen.Add(target))continue;
                Vector3 point=collider.ClosestPoint(origin);
                if(Physics.Linecast(origin,point,out var wall,~0,QueryTriggerInteraction.Ignore)&&!wall.transform.IsChildOf(c.Owner.transform)&&wall.collider.GetComponentInParent<IDamageReceiver>()!=(target as IDamageReceiver))continue;
                if(!((IDamageReceiver)target).ReceiveDamage(new DamageInfo(damage*c.DamageMultiplier,c.Owner,point,c.Direction,id,weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit)))continue;
                if(slowSeconds>0)CombatAilment.Slow(target.gameObject,slow,slowSeconds);
                if(stunSeconds>0)target.GetComponent<CombatState>()?.Stagger(stunSeconds);
                if(bleedSeconds>0)CombatAilment.Poison(target.gameObject,c.Owner,c.WeaponFamilyId,bleedDamage*c.DamageMultiplier,bleedSeconds);
                if(pushDistance>0)
                {
                    var agent=target.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if(agent!=null&&agent.enabled&&agent.isOnNavMesh)agent.Move(Vector3.ProjectOnPlane(c.Direction,Vector3.up).normalized*pushDistance);
                }
            }
        }
    }
    [Serializable]
    public sealed class StepAction : AbilityAction
    {
        public float distance=2;
        public override void Begin(AbilityExecution c)
        {
            c.Owner.GetComponent<DefenseWindow>()?.OpenDodge(c.Definition.active);
            c.Owner.GetComponent<WeaponSkillEffects>()?.Empower();
        }
        public override void Tick(AbilityExecution c,float dt)=>c.Motor.RequestControlledDisplacement(Vector3.ProjectOnPlane(c.Direction,Vector3.up).normalized*(distance*dt/c.Definition.active),c.Motor.Facing,10);
        public override void End(AbilityExecution c)=>c.Owner.GetComponent<DefenseWindow>()?.CloseDodge();
    }
    [Serializable]
    public sealed class TwoTimesAction : AbilityAction
    {
        public override void Begin(AbilityExecution c)=>c.Owner.GetComponent<WeaponSkillEffects>()?.TwoTimes();
    }
    [Serializable]
    public sealed class PoisonArrowAction : AbilityAction
    {
        public GameObject visual;
        public float damage=10,poisonDamage=4,duration=5;
        public override void Begin(AbilityExecution c)
        {
            var origin=WeaponAim.Muzzle(c.Owner);
            var arrow=ProjectileInstance.Spawn(c.Owner,origin,c.AimPoint.HasValue?(c.AimPoint.Value-origin).normalized:c.Direction,damage*c.DamageMultiplier,28,c.Definition.range,.09f,visual,c.AttackId,weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit);
            arrow.OnImpact=(target,point)=>{if(target!=null)CombatAilment.Poison(target.gameObject,c.Owner,c.WeaponFamilyId,poisonDamage*c.DamageMultiplier,duration);};
        }
    }
    [Serializable]
    public sealed class TrapAction : AbilityAction
    {
        public override void Begin(AbilityExecution c)=>HunterTrap.Spawn(c);
    }
    public sealed class HunterTrap : MonoBehaviour
    {
        GameObject owner;string family;float damage,expires,armed;
        public static void Spawn(AbilityExecution c)
        {
            var traps=UnityEngine.Object.FindObjectsByType<HunterTrap>(FindObjectsSortMode.None);
            HunterTrap oldest=null;int count=0;
            foreach(var trap in traps)if(trap.owner==c.Owner){count++;if(oldest==null||trap.expires<oldest.expires)oldest=trap;}
            if(count>=2&&oldest!=null){oldest.enabled=false;Destroy(oldest.gameObject);}
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Trampa de cazador";
            Destroy(go.GetComponent<Collider>());go.transform.position=c.GroundPoint+Vector3.up*.05f;go.transform.localScale=new Vector3(.9f,.05f,.9f);
            var effect=go.AddComponent<HunterTrap>();effect.owner=c.Owner;effect.family=c.WeaponFamilyId;effect.damage=12*c.DamageMultiplier;effect.expires=Time.time+30;effect.armed=Time.time+.4f;
        }
        void Update()
        {
            if(owner==null||Time.time>=expires){Destroy(gameObject);return;}
            if(Time.time<armed)return;
            foreach(var collider in Physics.OverlapSphere(transform.position+Vector3.up*.4f,.65f,~0,QueryTriggerInteraction.Ignore))
            {
                if(!(collider.GetComponentInParent<IDamageReceiver>() is Component target)||target.transform.IsChildOf(owner.transform))continue;
                var receiver=(IDamageReceiver)target;
                if(receiver.ReceiveDamage(new DamageInfo(damage,owner,target.transform.position,Vector3.zero,AttackIdentity.Next(),0,area:true,weaponFamilyId:family)))CombatAilment.Slow(target.gameObject,0,1.5f);
                enabled=false;Destroy(gameObject);return;
            }
        }
    }
}
