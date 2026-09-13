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
        private long attackId;
        private string family; private float focusGain;

        public void Configure(float damageAmount,string weaponFamilyId=null,float focusGainOnHit=0) { amount = Mathf.Max(0f, damageAmount); attackId=AttackIdentity.Next();family=weaponFamilyId;focusGain=focusGainOnHit; }

        public bool ApplyTo(GameObject target, Vector3 hitPoint, Vector3 direction)
        {
            if (target == null || target == gameObject) return false;
            IDamageReceiver receiver = target.GetComponentInParent<IDamageReceiver>();
            if (receiver == null) return false;
            var effects=GetComponentInParent<Mismo.Gameplay.Player.Equipment.WeaponSkillEffects>();
            bool basic=GetComponent<AttackHitbox>()!=null;
            if(attackId==0)attackId=AttackIdentity.Next();
            float multiplier=basic&&effects!=null?effects.BasicMultiplier(attackId,hitPoint,receiver as Component):1;
            bool hit=receiver.ReceiveDamage(new DamageInfo(amount*multiplier, gameObject, hitPoint, direction,attackId,weaponFamilyId:family,focusGainOnHit:focusGain));
            if(hit&&basic&&receiver is Component component)effects?.BasicHit(attackId,component);
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
