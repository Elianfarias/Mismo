using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatAilment : MonoBehaviour
    {
        float slowUntil,slow=1,poisonUntil,nextPoison,poisonDamage;
        GameObject source;string family;
        public float SpeedMultiplier=>Time.time<slowUntil?slow:1;
        public static void Slow(GameObject target,float multiplier,float duration)
        {
            if(target==null)return;
            var effect=target.GetComponent<CombatAilment>()??target.AddComponent<CombatAilment>();
            effect.slow=Time.time<effect.slowUntil?Mathf.Min(effect.slow,multiplier):multiplier;
            effect.slowUntil=Mathf.Max(effect.slowUntil,Time.time+duration);
        }
        public static void Poison(GameObject target,GameObject source,string family,float damage,float duration)
        {
            if(target==null)return;
            var effect=target.GetComponent<CombatAilment>()??target.AddComponent<CombatAilment>();
            bool fresh=Time.time>=effect.poisonUntil;
            effect.source=source;effect.family=family;effect.poisonDamage=damage;effect.poisonUntil=Time.time+duration;
            if(fresh)effect.nextPoison=Time.time+1;
        }
        void Update()
        {
            if(Time.time<nextPoison||Time.time>poisonUntil||source==null)return;
            nextPoison=Time.time+1;
            GetComponent<IDamageReceiver>()?.ReceiveDamage(new DamageInfo(poisonDamage,source,transform.position,Vector3.zero,AttackIdentity.Next(),0,area:true,parryable:false,weaponFamilyId:family));
        }
        void OnDisable(){slowUntil=poisonUntil=0;source=null;}
    }
}
