using System;
using System.Collections.Generic;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public enum CharacterAttribute { Life, Attack, Armor, Stamina }
    public enum MasteryAttribute { Damage, Speed, Cooldown }
    public enum WeaponVariant { Balanced, Colossus, Duelist, Guardian }

    [Serializable]
    public sealed class MasteryProgress
    {
        public string familyId;
        public int level = 1, experience, damagePoints, speedPoints, cooldownPoints;
        public string[] equippedAbilities;
        public List<string> unlockedAbilities = new List<string>();
        public List<AbilityProgress> abilityProgress = new List<AbilityProgress>();
        public int legacyAbilityCredit;
        public double damageExperienceRemainder;
        // Milestones are 3, 6, 9...; interval 1 still excludes the starting level.
        public int EarnedAbilityPoints(int interval=3) => interval<=1?Math.Max(0,level-1):level/interval;
        public int AbilityPoints(int interval=3) => Math.Max(0,EarnedAbilityPoints(interval)+legacyAbilityCredit-(unlockedAbilities?.Count??0));
        public bool IsUnlocked(string id) => !string.IsNullOrEmpty(id) && unlockedAbilities?.Contains(id)==true;
        public bool TryUnlockAbility(string[] repertoire,int index,int interval=3)
        {
            if(repertoire==null||index<0||index>=repertoire.Length||!ValidId(repertoire[index])||IsUnlocked(repertoire[index])||AbilityPoints(interval)<=0)return false;
            if(unlockedAbilities==null)unlockedAbilities=new List<string>();
            unlockedAbilities.Add(repertoire[index]);return true;
        }
        public AbilityProgress SkillProgress(string id) => abilityProgress?.Find(p=>p.abilityId==id);
        public AbilityProgress GetSkillProgress(string id,bool basic=false)
        {
            if(abilityProgress==null)abilityProgress=new List<AbilityProgress>();
            var progress=SkillProgress(id);
            if(progress==null){progress=new AbilityProgress{abilityId=id,isBasic=basic};abilityProgress.Add(progress);}
            return progress;
        }
        // Used only when importing a save created before free-choice unlocks.
        public static int AbilityUnlockLevel(int index,int interval=3) => index<3?1:(index-2)*Math.Max(1,interval);
        public void MigrateAbilities(string[] repertoire,int oldInterval=3,int newInterval=3)
        {
            unlockedAbilities=new List<string>();
            for(int i=0;i<repertoire.Length;i++)
                if(ValidId(repertoire[i])&&level>=AbilityUnlockLevel(i,oldInterval))unlockedAbilities.Add(repertoire[i]);
            if(equippedAbilities==null||equippedAbilities.Length==0)
            {
                equippedAbilities=new string[3];
                for(int i=0;i<Math.Min(3,repertoire.Length);i++)equippedAbilities[i]=repertoire[i];
            }
            legacyAbilityCredit=Math.Max(0,unlockedAbilities.Count-EarnedAbilityPoints(newInterval));
        }
        public bool TrySelectAbility(string[] repertoire,int abilityIndex,int slot,int interval=1)
        {
            if(repertoire==null||abilityIndex<0||abilityIndex>=repertoire.Length||slot<0||slot>=3||!IsUnlocked(repertoire[abilityIndex]))return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var id in repertoire)if(string.IsNullOrEmpty(id)||id.Length>100||!ids.Add(id))return false;
            var selected=equippedAbilities==null||equippedAbilities.Length==0?new string[3]:(string[])equippedAbilities.Clone();
            if(selected.Length!=3)return false;
            for(int i=0;i<3;i++)
            {
                if(string.IsNullOrEmpty(selected[i]))continue;
                int found=Array.IndexOf(repertoire,selected[i]);
                if(found<0||!IsUnlocked(selected[i]))return false;
            }
            int existing=Array.IndexOf(selected,repertoire[abilityIndex]);
            if(existing==slot)return false;
            if(existing>=0)selected[existing]=selected[slot];
            selected[slot]=repertoire[abilityIndex];
            equippedAbilities=selected;return true;
        }
        public int Available => level - 1 - damagePoints - speedPoints - cooldownPoints;
        public MasteryProgress Copy()
        {
            var copy=(MasteryProgress)MemberwiseClone();
            copy.equippedAbilities=equippedAbilities==null?null:(string[])equippedAbilities.Clone();
            copy.unlockedAbilities=unlockedAbilities==null?null:new List<string>(unlockedAbilities);
            copy.abilityProgress=abilityProgress==null?null:abilityProgress.ConvertAll(p=>p.Copy());
            return copy;
        }
        bool ValidAbilities()
        {
            if(equippedAbilities==null||equippedAbilities.Length==0)return true; // Saves from before ability selection.
            if(equippedAbilities.Length!=3)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var id in equippedAbilities)
                if(!string.IsNullOrEmpty(id)&&(!ValidId(id)||!ids.Add(id)))return false;
            return true;
        }
        internal static bool ValidId(string id)=>!string.IsNullOrWhiteSpace(id)&&id.Length<=100;
        bool ValidSkillProgress()
        {
            if(legacyAbilityCredit<0||legacyAbilityCredit>256||double.IsNaN(damageExperienceRemainder)||double.IsInfinity(damageExperienceRemainder)||damageExperienceRemainder<0||damageExperienceRemainder>=1)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            if(unlockedAbilities!=null){if(unlockedAbilities.Count>256)return false;foreach(var id in unlockedAbilities)if(!ValidId(id)||!ids.Add(id))return false;}
            ids.Clear();
            if(abilityProgress!=null){if(abilityProgress.Count>256)return false;foreach(var p in abilityProgress)if(p==null||!p.IsValid()||!p.isBasic&&!IsUnlocked(p.abilityId)||!ids.Add(p.abilityId))return false;}
            return true;
        }
        public bool IsValid() => !string.IsNullOrEmpty(familyId) && familyId.Length <= 100 &&
            level >= 1 && level <= 1000 && experience >= 0 && experience <= 1000000000 &&
            damagePoints >= 0 && speedPoints >= 0 && cooldownPoints >= 0 && damagePoints <= 999 && speedPoints <= 999 && cooldownPoints <= 999 && Available >= 0 && ValidAbilities() && ValidSkillProgress();
    }

    [Serializable]
    public sealed class AbilityProgress
    {
        public string abilityId, selectedModifierId;
        public bool isBasic;
        public int effectiveUses;
        public List<AbilityModifierProgress> modifiers=new List<AbilityModifierProgress>();
        public bool IsMastered(int requiredUses)=>effectiveUses>=Math.Max(1,requiredUses);
        public AbilityModifierProgress Modifier(string id)=>modifiers?.Find(m=>m.modifierId==id);
        public void RecordUse(int requiredUses,int modifierMaxLevel,int usesPerLevel)
        {
            bool mastered=IsMastered(requiredUses);
            effectiveUses=Math.Min(1000000000,effectiveUses+1);
            // The hit that achieves mastery never also trains a modifier.
            var modifier=Modifier(selectedModifierId);
            if(!mastered||modifier==null||modifier.level>=modifierMaxLevel)return;
            modifier.effectiveUses++;
            if(modifier.effectiveUses>=Math.Max(1,usesPerLevel))
            {modifier.effectiveUses=0;modifier.level++;}
        }
        public bool TrySelectModifier(string id,int requiredUses)
        {
            if(!IsMastered(requiredUses)||!MasteryProgress.ValidId(id)||id==selectedModifierId)return false;
            if(modifiers==null)modifiers=new List<AbilityModifierProgress>();
            if(Modifier(id)==null){if(modifiers.Count>=64)return false;modifiers.Add(new AbilityModifierProgress{modifierId=id});}
            selectedModifierId=id;return true;
        }
        public AbilityProgress Copy()
        {
            var copy=(AbilityProgress)MemberwiseClone();
            copy.modifiers=modifiers==null?null:modifiers.ConvertAll(m=>m.Copy());return copy;
        }
        public bool IsValid()
        {
            if(!MasteryProgress.ValidId(abilityId)||effectiveUses<0||effectiveUses>1000000000||modifiers?.Count>64)return false;
            var ids=new HashSet<string>();
            if(modifiers!=null)foreach(var m in modifiers)if(m==null||!MasteryProgress.ValidId(m.modifierId)||m.level<0||m.level>100||m.effectiveUses<0||m.effectiveUses>1000000||!ids.Add(m.modifierId))return false;
            return string.IsNullOrEmpty(selectedModifierId)||ids.Contains(selectedModifierId);
        }
    }
    [Serializable]
    public sealed class AbilityModifierProgress
    {
        public string modifierId;
        public int level,effectiveUses;
        public AbilityModifierProgress Copy()=>(AbilityModifierProgress)MemberwiseClone();
    }

    [Serializable]
    public sealed class ProgressionData
    {
        public int level = 1, experience, lifePoints, attackPoints, armorPoints, staminaPoints;
        // Schema 7 only. Kept to read and refund points when upgrading the save.
        public int cooldownPoints;
        public List<MasteryProgress> masteries = new List<MasteryProgress>();
        public int Available => level - 1 - lifePoints - attackPoints - armorPoints - staminaPoints - cooldownPoints;
        public MasteryProgress Find(string family) => masteries.Find(m => m.familyId == family);
        public MasteryProgress GetOrCreate(string family)
        {
            var value = Find(family);
            if (value == null) { value = new MasteryProgress { familyId = family }; masteries.Add(value); }
            return value;
        }
        public ProgressionData Copy()
        {
            var copy = new ProgressionData { level=level, experience=experience, lifePoints=lifePoints,
                attackPoints=attackPoints, armorPoints=armorPoints, staminaPoints=staminaPoints, cooldownPoints=cooldownPoints };
            foreach (var mastery in masteries) copy.masteries.Add(mastery.Copy());
            return copy;
        }
        public bool IsValid()
        {
            if (level < 1 || level > 1000 || experience < 0 || experience > 1000000000 ||
                lifePoints < 0 || attackPoints < 0 || armorPoints < 0 || lifePoints > 999 ||
                attackPoints > 999 || armorPoints > 999 || staminaPoints<0||staminaPoints>999||cooldownPoints<0||cooldownPoints>999|| Available < 0 || masteries == null || masteries.Count > 256) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in masteries) if (m == null || !m.IsValid() || !ids.Add(m.familyId)) return false;
            return true;
        }
    }
}
