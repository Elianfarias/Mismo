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
        public float damage=7,interval=.2f;
        [Tooltip("Radio de la esfera de impacto, en metros. Aumentarlo amplía el golpe hacia delante, atrás y los lados. Se combina con Forward; no usa el Range de la habilidad.")]
        public float radius=.7f;
        [Tooltip("Distancia desde el personaje hasta el centro de la esfera de impacto, en metros hacia delante. El borde frontal está a Forward + Radius. No cambia el tamaño del área.")]
        public float forward=1;
        public float slow=1,slowSeconds,stunSeconds,bleedDamage,bleedSeconds,pushDistance;
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
                if(!((IDamageReceiver)target).ReceiveDamage(new DamageInfo(damage*c.DamageMultiplier,c.Owner,point,c.Direction,id,weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit,abilityId:c.Definition.Id,abilityUseId:c.AttackId)))continue;
                if(slowSeconds>0)CombatAilment.Slow(target.gameObject,slow,slowSeconds);
                if(stunSeconds>0)target.GetComponent<CombatState>()?.Stagger(stunSeconds);
                if(bleedSeconds>0)CombatAilment.Bleed(target.gameObject,c.Owner,c.WeaponFamilyId,bleedDamage*c.DamageMultiplier,bleedSeconds,c.Definition.Id,c.AttackId);
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
        public override bool IsEvasion => true;
        public override void Begin(AbilityExecution c)
        {
            c.Owner.GetComponent<DefenseWindow>()?.OpenDodge(c.Definition.active);
            c.Owner.GetComponent<WeaponSkillEffects>()?.Empower(c);
        }
        public override void Tick(AbilityExecution c,float dt)=>c.Motor.RequestControlledDisplacement(Vector3.ProjectOnPlane(c.Direction,Vector3.up).normalized*(distance*dt/c.Definition.active),c.Motor.Facing,10);
        public override void End(AbilityExecution c)=>c.Owner.GetComponent<DefenseWindow>()?.CloseDodge();
    }
    [Serializable]
    public sealed class TwoTimesAction : AbilityAction
    {
        public override void Begin(AbilityExecution c)=>c.Owner.GetComponent<WeaponSkillEffects>()?.TwoTimes(c);
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
            arrow.AbilityId=c.Definition.Id;arrow.AbilityUseId=c.AttackId;
            arrow.OnImpact=(target,point)=>{if(target!=null)CombatAilment.Poison(target.gameObject,c.Owner,c.WeaponFamilyId,poisonDamage*c.DamageMultiplier,duration,c.Definition.Id,c.AttackId);};
        }
    }
    [Serializable]
    public sealed class TrapAction : AbilityAction
    {
        [Tooltip("Modelo de la trampa, con HunterTrap y sus mandíbulas articuladas.")]
        public GameObject prefab;
        [Min(.2f)] public float arcHeight=1;
        [Min(1)] public float throwSpeed=8;
        public override void Begin(AbilityExecution c)
        {
            if(c.GroundThrow.HasValue)HunterTrap.Launch(c,prefab,c.GroundThrow.Value);
            else HunterTrap.Spawn(c,prefab);
        }
    }
}
