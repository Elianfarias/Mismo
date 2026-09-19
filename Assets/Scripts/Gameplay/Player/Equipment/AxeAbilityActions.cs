using System;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    // Solo activa el buff de "próximo golpe primado"; el sangrado lo aplica el propio básico (ver WeaponSkillEffects.BasicHit).
    [Serializable]
    public sealed class PrimeBleedAction : AbilityAction
    {
        public override void Begin(AbilityExecution c)=>c.Owner.GetComponent<WeaponSkillEffects>()?.PrimeBleed();
    }

    [Serializable]
    public sealed class ArmorRendStrikeAction : AbilityAction
    {
        public float damage=14,postureDamage=-1,radius=.6f,forward=.8f;
        [Tooltip("Multiplicador de armadura mientras dura el debuff (0.8 = -20%). No acumulable.")]
        public float armorMultiplier=.8f,armorDuration=4;
        public override void Tick(AbilityExecution c,float dt)
        {
            Vector3 origin=c.Owner.transform.position+Vector3.up+c.Direction*forward;
            foreach(var other in Physics.OverlapSphere(origin,radius,~0,QueryTriggerInteraction.Ignore))
            {
                var receiver=other.GetComponentInParent<IDamageReceiver>();
                if(!(receiver is Component target)||target.transform.root==c.Owner.transform.root||!c.HitTargets.Add(target))continue;
                Vector3 point=other.ClosestPoint(origin);
                if(Physics.Linecast(origin,point,out var wall,~0,QueryTriggerInteraction.Ignore)&&wall.transform.root!=c.Owner.transform.root&&wall.collider.GetComponentInParent<IDamageReceiver>()!=receiver)continue;
                if(!receiver.ReceiveDamage(new DamageInfo(damage*c.DamageMultiplier,c.Owner,point,(target.transform.position-c.Owner.transform.position).normalized,c.AttackId,postureDamage,weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit)))continue;
                CombatAilment.WeakenArmor(target.gameObject,armorMultiplier,armorDuration);
            }
        }
    }

    [Serializable]
    public sealed class WeakenedTargetStrikeAction : AbilityAction
    {
        public float damage=18,postureDamage=-1,radius=.6f,forward=.8f;
        [Tooltip("Multiplicador de daño si el objetivo está sangrando o con la armadura reducida.")]
        public float bonusMultiplier=1.3f;
        public override void Tick(AbilityExecution c,float dt)
        {
            Vector3 origin=c.Owner.transform.position+Vector3.up+c.Direction*forward;
            foreach(var other in Physics.OverlapSphere(origin,radius,~0,QueryTriggerInteraction.Ignore))
            {
                var receiver=other.GetComponentInParent<IDamageReceiver>();
                if(!(receiver is Component target)||target.transform.root==c.Owner.transform.root||!c.HitTargets.Add(target))continue;
                Vector3 point=other.ClosestPoint(origin);
                if(Physics.Linecast(origin,point,out var wall,~0,QueryTriggerInteraction.Ignore)&&wall.transform.root!=c.Owner.transform.root&&wall.collider.GetComponentInParent<IDamageReceiver>()!=receiver)continue;
                var ailment=target.GetComponent<CombatAilment>();
                float multiplier=ailment!=null&&(ailment.Poisoned||ailment.ArmorWeakened)?bonusMultiplier:1;
                receiver.ReceiveDamage(new DamageInfo(damage*multiplier*c.DamageMultiplier,c.Owner,point,(target.transform.position-c.Owner.transform.position).normalized,c.AttackId,postureDamage,weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit));
            }
        }
    }
}
