using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        public AbilityDefinition SelectedAbility(WeaponDefinition weapon, AbilitySlot slot)
        {
            if(weapon==null)return null;
            if(slot==AbilitySlot.Basic || weapon.family==null || weapon.overrideFamilyAbilities)return weapon.GetAbility(slot);
            int index=(int)slot-1;
            if(index<0 || index>2)return null;
            var mastery=profile?.progression.Find(weapon.MasteryId);
            var selection=mastery?.equippedAbilities;
            if(selection!=null && selection.Length==3)
            {
                int skill=weapon.family.FindSkill(selection[index]);
                if(skill>=0 && weapon.family.UnlockLevel(skill)<=(mastery?.level??1))return weapon.family.Skill(skill);
            }
            return weapon.family.Skill(index);
        }

        public bool TrySelectAbility(WeaponDefinition weapon, AbilitySlot slot, int skillIndex)
        {
            if(!IsReady || weapon==null || weapon.family==null || weapon.overrideFamilyAbilities || !loadout.CanChangeEquipment ||
                (int)slot<1 || (int)slot>3 || (weapon!=loadout.GetSlot(0) && weapon!=loadout.GetSlot(1)))return false;
            var family=weapon.family;
            var ability=family.Skill(skillIndex);
            if(ability==null || family.UnlockLevel(skillIndex)>(Mastery(weapon)?.level??1))return false;
            var next=profile.Copy();
            var mastery=next.progression.GetOrCreate(weapon.MasteryId);
            var repertoire=new string[family.SkillCount];
            for(int i=0;i<repertoire.Length;i++)repertoire[i]=family.Skill(i)?.Id;
            if(!mastery.TrySelectAbility(repertoire,skillIndex,(int)slot-1,family.masteryLevelsPerUnlock))return false;
            if(!Commit(next,"Habilidades guardadas.",false))return false;
            if(loadout.ActiveDefinition?.MasteryId==weapon.MasteryId)GetComponent<WeaponSkillEffects>()?.ResetEffects();
            return true;
        }
    }
}
