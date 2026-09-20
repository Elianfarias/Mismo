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
    public struct ComboHit
    {
        [Tooltip("Momento del golpe dentro de la fase activa (0 = inicio, 1 = final).")]
        [Range(0,1)] public float at;
        [Tooltip("Multiplicador del daño base de la acción para este golpe.")]
        public float multiplier;
    }

    // Como RepeatedStrikeAction pero con los golpes en momentos explícitos y con multiplicador propio;
    // el aturdimiento sólo se aplica al enemigo que recibió todos los golpes del combo.
    [Serializable]
    public sealed class FuriousComboAction : AbilityAction
    {
        public float damage=15,postureDamage=-1,radius=.7f,forward=1,stunSeconds=.8f;
        public ComboHit[] hits={new ComboHit{at=.105f,multiplier=1},new ComboHit{at=.559f,multiplier=1.2f},new ComboHit{at=.923f,multiplier=1.5f}};
        // Estado por ejecución: Unity no serializa Dictionary, así que no ensucia el asset.
        readonly System.Collections.Generic.Dictionary<Component,int> landed=new System.Collections.Generic.Dictionary<Component,int>();
        public override void Begin(AbilityExecution c){c.ActionTimes[this]=0;landed.Clear();}
        public override void Tick(AbilityExecution c,float dt)
        {
            float previous=c.ActionTimes[this],next=previous+dt;c.ActionTimes[this]=next;
            if(hits==null)return;
            foreach(var hit in hits)
            {
                float at=hit.at*c.Definition.active;
                if(previous<at&&at<=next)Strike(c,hit.multiplier);
            }
        }
        void Strike(AbilityExecution c,float multiplier)
        {
            long id=AttackIdentity.Next();var seen=new System.Collections.Generic.HashSet<Component>();
            var origin=c.Owner.transform.position+Vector3.up+c.Direction*forward;
            foreach(var collider in Physics.OverlapSphere(origin,radius,~0,QueryTriggerInteraction.Ignore))
            {
                if(!(collider.GetComponentInParent<IDamageReceiver>() is Component target)||target.transform.IsChildOf(c.Owner.transform)||!seen.Add(target))continue;
                Vector3 point=collider.ClosestPoint(origin);
                if(Physics.Linecast(origin,point,out var wall,~0,QueryTriggerInteraction.Ignore)&&!wall.transform.IsChildOf(c.Owner.transform)&&wall.collider.GetComponentInParent<IDamageReceiver>()!=(target as IDamageReceiver))continue;
                if(!((IDamageReceiver)target).ReceiveDamage(new DamageInfo(damage*multiplier*c.DamageMultiplier,c.Owner,point,c.Direction,id,postureDamage,weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit)))continue;
                landed.TryGetValue(target,out int count);
                landed[target]=++count;
                if(stunSeconds>0&&count>=hits.Length)target.GetComponent<CombatState>()?.Stagger(stunSeconds);
            }
        }
    }
}
