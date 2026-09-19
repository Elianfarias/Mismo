using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [CreateAssetMenu(menuName="Mismo/Progression/Progression Rules", fileName="ProgressionRules")]
    public sealed class ProgressionRules : ScriptableObject
    {
        [Header("Experiencia y puntos por nivel")]
        [Range(2,1000)] public int characterMaxLevel=50;
        [Range(2,1000)] public int masteryMaxLevel=20;
        [Min(1)] public int characterBaseExperience=60, characterExperienceStep=30;
        [Min(1)] public int masteryBaseExperience=40, masteryExperienceStep=20;
        [Min(1)] public int enemyExperience=25, enemyMasteryExperience=20;
        [Min(1)] public int bossExperience=200, bossMasteryExperience=120;
        [Header("Atributos por punto")]
        [Min(0)] public float lifePerPoint=5, armorPerPoint=2;
        [Min(0)] public float attackPerPoint=.02f, masteryDamagePerPoint=.025f, masterySpeedPerPoint=.015f;
        [Min(.01f)] public float armorScale=100;
        [Range(1,2)] public float maximumAttackSpeed=1.4f;
        [Header("Tiers y drops")]
        [Range(0,1)] public float dropChance=.25f;
        public float[] tierWeights={70,25,5,0,0};
        [Min(0)] public float damagePerTier=.08f;
        [Min(0)] public float variantGrowthPerTier=.25f;
        [Header("Coloso")]
        public float colossusDamage=.10f, colossusLife=10, colossusSpeed=-.08f;
        [Header("Duelista")]
        public float duelistSpeed=.10f, duelistArmor=-6;
        [Header("Guardián")]
        public float guardianArmor=8, guardianDamage=-.05f;

        static ProgressionRules fallback;
        public static ProgressionRules Current
        {
            get
            {
                var asset=Mismo.Core.ProjectAssets.Load<ProgressionRules>("ProgressionRules");
                if(asset!=null)return asset;
                if(fallback==null){fallback=CreateInstance<ProgressionRules>();fallback.hideFlags=HideFlags.HideAndDontSave;}
                return fallback;
            }
        }
        public int Needed(int level,bool mastery=false) => (int)System.Math.Min(1000000000L,
            System.Math.Max(1L,(mastery?masteryBaseExperience:characterBaseExperience)+
            (long)System.Math.Max(0,level-1)*System.Math.Max(0,mastery?masteryExperienceStep:characterExperienceStep)));
        public void Grant(ProgressionData p,int experience,System.Collections.Generic.IDictionary<string,int> mastery)
        {
            Advance(ref p.level,ref p.experience,experience,false);
            if(mastery!=null)foreach(var pair in mastery)
            {
                if(string.IsNullOrEmpty(pair.Key)||pair.Value<=0)continue;
                var m=p.GetOrCreate(pair.Key);Advance(ref m.level,ref m.experience,pair.Value,true);
            }
        }
        void Advance(ref int level,ref int xp,int amount,bool mastery)
        {
            int cap=Mathf.Clamp(mastery?masteryMaxLevel:characterMaxLevel,2,1000);
            if(level>=cap)return;
            long available=(long)xp+System.Math.Max(0,amount);
            while(level<cap && available>=Needed(level,mastery)){available-=Needed(level,mastery);level++;}
            xp=level>=cap?0:(int)System.Math.Min(available,1000000000L);
        }
        public int RollTier()
        {
            float total=0;
            if(tierWeights!=null)for(int i=0;i<Mathf.Min(5,tierWeights.Length);i++)total+=Mathf.Max(0,tierWeights[i]);
            if(total<=0)return 1;
            float roll=Random.value*total;
            for(int i=0;i<Mathf.Min(5,tierWeights.Length);i++){roll-=Mathf.Max(0,tierWeights[i]);if(roll<0)return i+1;}
            return 1;
        }
        public WeaponBonuses Bonuses(OwnedWeapon item)
        {
            if(item==null)return default;
            int tier=Mathf.Clamp(item.tier,1,5);
            float scale=1+(tier-1)*Mathf.Max(0,variantGrowthPerTier);
            var b=new WeaponBonuses{damage=(tier-1)*Mathf.Max(0,damagePerTier)};
            switch(item.variant)
            {
                case WeaponVariant.Colossus:b.damage+=colossusDamage*scale;b.life=colossusLife*scale;b.speed=colossusSpeed*scale;break;
                case WeaponVariant.Duelist:b.speed=duelistSpeed*scale;b.armor=duelistArmor*scale;break;
                case WeaponVariant.Guardian:b.armor=guardianArmor*scale;b.damage+=guardianDamage*scale;break;
            }
            return b;
        }
    }
    public struct WeaponBonuses { public float damage,speed,life,armor; }
}
