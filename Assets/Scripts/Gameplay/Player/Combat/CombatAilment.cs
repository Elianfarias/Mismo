using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatAilment : MonoBehaviour
    {
        float slowUntil,slow=1,poisonUntil,nextPoison,poisonDamage;
        float armorDebuffUntil,armorDebuffMultiplier=1,baseArmor;
        GameObject source;string family;
        public float SpeedMultiplier=>Time.time<slowUntil?slow:1;
        public bool Poisoned=>Time.time<poisonUntil;
        public bool ArmorWeakened=>Time.time<armorDebuffUntil;
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
        // No acumulable: la reaplicación reemplaza el multiplicador y renueva la duración, no los suma.
        public static void WeakenArmor(GameObject target,float multiplier,float duration)
        {
            if(target==null)return;
            var effect=target.GetComponent<CombatAilment>()??target.AddComponent<CombatAilment>();
            effect.armorDebuffMultiplier=multiplier;
            effect.armorDebuffUntil=Time.time+duration;
        }
        /// <summary>Armadura base del actor (jugadores usan PlayerInventory.Armor en su lugar); configurada una vez al spawnear.</summary>
        public void ConfigureArmor(float value)=>baseArmor=value;
        public float IncomingDamageMultiplier
        {
            get
            {
                if(baseArmor<=0)return 1;
                float effective=baseArmor*(ArmorWeakened?armorDebuffMultiplier:1);
                float scale=Mathf.Max(.01f,CombatRules.Current.armorScale);
                return scale/(scale+effective);
            }
        }
        void Update()
        {
            if(Time.time<nextPoison||Time.time>poisonUntil||source==null)return;
            nextPoison=Time.time+1;
            GetComponent<IDamageReceiver>()?.ReceiveDamage(new DamageInfo(poisonDamage,source,transform.position,Vector3.zero,AttackIdentity.Next(),0,area:true,parryable:false,weaponFamilyId:family));
        }
        void OnDisable(){slowUntil=poisonUntil=armorDebuffUntil=0;source=null;}
    }
}
