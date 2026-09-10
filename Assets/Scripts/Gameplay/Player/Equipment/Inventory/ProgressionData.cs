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
        public int Available => level - 1 - damagePoints - speedPoints;
        public MasteryProgress Copy() => (MasteryProgress)MemberwiseClone();
        public bool IsValid() => !string.IsNullOrEmpty(familyId) && familyId.Length <= 100 &&
            level >= 1 && level <= 1000 && experience >= 0 && experience <= 1000000000 &&
            damagePoints >= 0 && speedPoints >= 0 && damagePoints <= 999 && speedPoints <= 999 && Available >= 0;
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
