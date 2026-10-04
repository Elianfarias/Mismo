using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>Fuente reemplazable de daño manual o por trigger, sin conocer el receptor concreto.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DamageDealer : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float amount = 10f;
        [SerializeField] private bool applyOnTriggerEnter;
        private readonly HashSet<UnityEngine.Object> hitObjects = new HashSet<UnityEngine.Object>();

        public float Amount => amount;
        private long attackId, secondAttackId;
        private string family; private float focusGain;
        private string abilityId;private long abilityUseId;private float postureMultiplier=1;private bool breaksGuard;

        public void Configure(float damageAmount,string weaponFamilyId=null,float focusGainOnHit=0,string skillId=null,long useId=0,float postureScale=1,bool breakGuard=false)
        {amount=Mathf.Max(0f,damageAmount);attackId=AttackIdentity.Next();secondAttackId=AttackIdentity.Next();family=weaponFamilyId;focusGain=focusGainOnHit;abilityId=skillId;abilityUseId=useId;postureMultiplier=postureScale;breaksGuard=breakGuard;}

        /// <param name="secondStrike">La otra arma golpea un objetivo ya alcanzado en este paso: id propio para que el receptor
        /// no lo descarte, sin Focus ni efectos de "otro básico".</param>
        public bool ApplyTo(GameObject target, Vector3 hitPoint, Vector3 direction, bool secondStrike = false)
        {
            if (target == null || target == gameObject) return false;
            IDamageReceiver receiver = target.GetComponentInParent<IDamageReceiver>();
            if (receiver == null) return false;
            var effects=GetComponentInParent<Mismo.Gameplay.Player.Equipment.WeaponSkillEffects>();
            bool basic=GetComponent<AttackHitbox>()!=null;
            if(attackId==0){attackId=AttackIdentity.Next();secondAttackId=AttackIdentity.Next();}
            long id=secondStrike?secondAttackId:attackId;
            float multiplier=basic&&effects!=null?effects.BasicMultiplier(id,hitPoint,receiver as Component):1;
            bool hit=receiver.ReceiveDamage(new DamageInfo(amount*multiplier, gameObject, hitPoint, direction,id,amount*multiplier*.65f*postureMultiplier,weaponFamilyId:family,focusGainOnHit:secondStrike?0:focusGain,abilityId:abilityId,abilityUseId:abilityUseId,breaksGuard:breaksGuard));
            if(hit&&basic&&effects!=null&&receiver is Component component)
            {
                if(!secondStrike)effects.BasicHit(id,component);else effects.SecondStrikeHit(id,component);
                if(receiver is DamageReceiver resolved)effects.BasicDamageDealt(resolved.LastResult.HealthDamage);
            }
            return hit;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!applyOnTriggerEnter || !hitObjects.Add(other.transform.root)) return;
            Vector3 direction = other.transform.position - transform.position;
            ApplyTo(other.gameObject, other.ClosestPoint(transform.position), direction);
        }

        private void OnTriggerExit(Collider other) => hitObjects.Remove(other.transform.root);
    }
}
