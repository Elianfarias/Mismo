using System;
using System.Collections.Generic;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public enum CharacterAttribute { Life, Attack, Armor }
    public enum MasteryAttribute { Damage, Speed }
    public enum WeaponVariant { Balanced, Colossus, Duelist, Guardian }

    [Serializable]
    public sealed class MasteryProgress
    {
        public string familyId;
        public int level = 1, experience, damagePoints, speedPoints;
        public string[] equippedAbilities;
        public static int AbilityUnlockLevel(int index,int interval=3) => index<3?1:(index-2)*Math.Max(1,interval);
        public bool TrySelectAbility(string[] repertoire,int abilityIndex,int slot,int interval=3)
        {
            if(repertoire==null||repertoire.Length<3||abilityIndex<0||abilityIndex>=repertoire.Length||slot<0||slot>=3||
                level<AbilityUnlockLevel(abilityIndex,interval))return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var id in repertoire)if(string.IsNullOrEmpty(id)||id.Length>100||!ids.Add(id))return false;
            var selected=equippedAbilities==null||equippedAbilities.Length==0?new[]{repertoire[0],repertoire[1],repertoire[2]}:(string[])equippedAbilities.Clone();
            if(selected.Length!=3)return false;
            for(int i=0;i<3;i++)
            {
                int found=Array.IndexOf(repertoire,selected[i]);
                if(found<0||level<AbilityUnlockLevel(found,interval))return false;
            }
            int existing=Array.IndexOf(selected,repertoire[abilityIndex]);
            if(existing==slot)return false;
            if(existing>=0)selected[existing]=selected[slot];
            selected[slot]=repertoire[abilityIndex];
            equippedAbilities=selected;return true;
        }
        public int Available => level - 1 - damagePoints - speedPoints;
        public MasteryProgress Copy()
        {
            var copy=(MasteryProgress)MemberwiseClone();
            copy.equippedAbilities=equippedAbilities==null?null:(string[])equippedAbilities.Clone();
            return copy;
        }
        bool ValidAbilities()
        {
            if(equippedAbilities==null||equippedAbilities.Length==0)return true; // Saves from before ability selection.
            if(equippedAbilities.Length!=3)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var id in equippedAbilities)
                if(string.IsNullOrEmpty(id)||id.Length>100||!ids.Add(id))return false;
            return true;
        }
        public bool IsValid() => !string.IsNullOrEmpty(familyId) && familyId.Length <= 100 &&
            level >= 1 && level <= 1000 && experience >= 0 && experience <= 1000000000 &&
            damagePoints >= 0 && speedPoints >= 0 && damagePoints <= 999 && speedPoints <= 999 && Available >= 0 && ValidAbilities();
    }

    [Serializable]
    public sealed class ProgressionData
    {
        public int level = 1, experience, lifePoints, attackPoints, armorPoints;
        public List<MasteryProgress> masteries = new List<MasteryProgress>();
        public int Available => level - 1 - lifePoints - attackPoints - armorPoints;
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
                attackPoints=attackPoints, armorPoints=armorPoints };
            foreach (var mastery in masteries) copy.masteries.Add(mastery.Copy());
            return copy;
        }
        public bool IsValid()
        {
            if (level < 1 || level > 1000 || experience < 0 || experience > 1000000000 ||
                lifePoints < 0 || attackPoints < 0 || armorPoints < 0 || lifePoints > 999 ||
                attackPoints > 999 || armorPoints > 999 || Available < 0 || masteries == null || masteries.Count > 256) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in masteries) if (m == null || !m.IsValid() || !ids.Add(m.familyId)) return false;
            return true;
        }
    }
}
