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
                if(skill>=0 && mastery.IsUnlocked(selection[index]))return weapon.family.Skill(skill);
            }
            return null;
        }

        public bool TrySelectAbility(WeaponDefinition weapon, AbilitySlot slot, int skillIndex)
        {
            if(!IsReady || weapon==null || weapon.family==null || weapon.overrideFamilyAbilities || !loadout.CanChangeEquipment ||
                (int)slot<1 || (int)slot>3 || (weapon!=loadout.GetSlot(0) && weapon!=loadout.GetSlot(1)))return false;
            var family=weapon.family;
            var ability=family.Skill(skillIndex);
            if(ability==null || !IsAbilityUnlocked(weapon,skillIndex))return false;
            var next=profile.Copy();
            var mastery=next.progression.GetOrCreate(weapon.MasteryId);
            var repertoire=new string[family.SkillCount];
            for(int i=0;i<repertoire.Length;i++)repertoire[i]=family.Skill(i)?.Id;
            if(!mastery.TrySelectAbility(repertoire,skillIndex,(int)slot-1,family.masteryLevelsPerUnlock))return false;
            if(!Commit(next,"Habilidades guardadas.",false))return false;
            if(loadout.ActiveDefinition?.MasteryId==weapon.MasteryId)GetComponent<WeaponSkillEffects>()?.ResetEffects();
            return true;
        }

        public AbilityDefinition MasteryAbility(WeaponDefinition weapon,int index)=>index==-1?weapon?.GetAbility(AbilitySlot.Basic):weapon?.family?.Skill(index);
        public bool IsAbilityUnlocked(WeaponDefinition weapon,int index)=>index==-1?MasteryAbility(weapon,index)!=null:weapon?.family!=null &&
            profile?.progression.Find(weapon.MasteryId)?.IsUnlocked(weapon.family.Skill(index)?.Id)==true;
        public int AvailableAbilityPoints(WeaponDefinition weapon)=>Mastery(weapon)?.AbilityPoints(Rules.masteryLevelsPerAbilityPoint)??0;
        public AbilityDefinition ProgressAbility(string familyId,string abilityId)
        {
            if(!IsReady)return null;
            var family=FindFamily(familyId);
            var basic=family?.GetAbility(AbilitySlot.Basic);
            return basic!=null&&basic.Id==abilityId?basic:family?.Skill(family.FindSkill(abilityId));
        }
        static string[] Repertoire(WeaponFamilyDefinition family)
        {
            var ids=new string[family.SkillCount];for(int i=0;i<ids.Length;i++)ids[i]=family.Skill(i)?.Id;return ids;
        }
        public bool TryUnlockAbility(WeaponDefinition weapon,int index)
        {
            if(!CanManage||weapon?.family==null||weapon.overrideFamilyAbilities||weapon!=loadout.GetSlot(0)&&weapon!=loadout.GetSlot(1))return false;
            var next=profile.Copy();var mastery=next.progression.GetOrCreate(weapon.MasteryId);
            if(!mastery.TryUnlockAbility(Repertoire(weapon.family),index,Rules.masteryLevelsPerAbilityPoint))return false;
            return Commit(next,"Habilidad desbloqueada. Arrastrala a Q, E o R.",false);
        }
        public bool TrySelectModifier(WeaponDefinition weapon,int skillIndex,string modifierId)
        {
            if(!CanManage||!IsAbilityUnlocked(weapon,skillIndex)||weapon!=loadout.GetSlot(0)&&weapon!=loadout.GetSlot(1))return false;
            var ability=MasteryAbility(weapon,skillIndex);
            if(ability.FindModifier(modifierId)==null||ability.FindModifier(modifierId).retired)return false;
            // Credit earlier hits to the modifier that was active when they occurred.
            if(!FlushCombatProgress())return false;
            var next=profile.Copy();var progress=next.progression.GetOrCreate(weapon.MasteryId).GetSkillProgress(ability.Id,skillIndex==-1);
            if(!progress.TrySelectModifier(modifierId,ability.masteryUsesRequired))return false;
            return Commit(next,"Modificador seleccionado. Entrenalo contra monstruos.",false);
        }
        public bool TryClearModifier(WeaponDefinition weapon,int skillIndex)
        {
            if(!CanManage||!IsAbilityUnlocked(weapon,skillIndex)||weapon!=loadout.GetSlot(0)&&weapon!=loadout.GetSlot(1))return false;
            if(!FlushCombatProgress())return false;
            var next=profile.Copy();var p=next.progression.Find(weapon.MasteryId)?.SkillProgress(MasteryAbility(weapon,skillIndex).Id);
            if(p==null||string.IsNullOrEmpty(p.selectedModifierId))return false;
            p.selectedModifierId=null;return Commit(next,"Modificador desactivado. Conservás su progreso.",false);
        }
        public float CooldownReduction(WeaponDefinition weapon)=>Rules.CooldownReduction(weapon==null?0:profile?.progression.Find(weapon.MasteryId)?.cooldownPoints??0);
        AbilityModifierDefinition TrainedModifier(WeaponDefinition weapon,AbilityDefinition ability,out int rank)
        {
            rank=0;if(weapon==null||ability==null)return null;
            var p=profile?.progression.Find(weapon.MasteryId)?.SkillProgress(ability.Id);
            if(p?.IsMastered(ability.masteryUsesRequired)!=true)return null;
            var modifier=ability.FindModifier(p.selectedModifierId);if(modifier==null||modifier.retired)return null;
            rank=Mathf.Clamp(p.Modifier(modifier.id)?.level??0,0,modifier.maxLevel);return modifier;
        }
        public float AbilityDamageMultiplier(WeaponDefinition weapon,AbilityDefinition ability)
        {
            var modifier=TrainedModifier(weapon,ability,out int rank);
            return Mathf.Max(.1f,1+(modifier?.damagePerLevel??0)*rank);
        }
        public AbilityModifierDefinition AbilityVisualModifier(WeaponDefinition weapon,AbilityDefinition ability)
        {
            var modifier=TrainedModifier(weapon,ability,out int rank);
            return rank>0||modifier!=null&&modifier.behavior!=AbilityModifierBehavior.None?modifier:null;
        }
        public AbilityModifierDefinition AbilityBehavior(WeaponDefinition weapon,AbilityDefinition ability,out int rank)
        {
            var modifier=TrainedModifier(weapon,ability,out rank);
            return modifier!=null&&modifier.behavior!=AbilityModifierBehavior.None?modifier:null;
        }
        public float AbilityCooldown(WeaponDefinition weapon,AbilityDefinition ability,bool basic=false)
        {
            if(ability==null)return 0;
            if(basic)return Mathf.Max(0,ability.cooldown)/AttackSpeed(weapon);
            var modifier=TrainedModifier(weapon,ability,out int rank);
            float reduction=Mathf.Clamp01((modifier?.cooldownReductionPerLevel??0)*rank);
            return Mathf.Max(0,ability.cooldown)*Mathf.Max(.2f,(1-CooldownReduction(weapon))*(1-reduction));
        }
        void MigrateAbilityUnlocks(InventoryProfile value)
        {
            if(value.version>=7)return;
            value.UpgradeFromVersionOne();
            // Earlier versions implicitly had the first three skills for every family, even without an entry.
            var families=new System.Collections.Generic.HashSet<WeaponFamilyDefinition>();
            foreach(var weapon in catalog.weapons)
            {if(weapon.family!=null)families.Add(weapon.family);if(weapon.dualSwordFamily!=null)families.Add(weapon.dualSwordFamily);if(weapon.swordShieldFamily!=null)families.Add(weapon.swordShieldFamily);}
            foreach(var family in families)value.progression.GetOrCreate(family.progressionId).MigrateAbilities(Repertoire(family),family.masteryLevelsPerUnlock,Rules.masteryLevelsPerAbilityPoint);
        }
        bool ValidateAbilityProgress(MasteryProgress mastery,WeaponFamilyDefinition family,int version)
        {
            if(family==null)return (mastery.unlockedAbilities?.Count??0)==0&&(mastery.abilityProgress?.Count??0)==0;
            int earned=version<8?mastery.level-1:mastery.EarnedAbilityPoints(Rules.masteryLevelsPerAbilityPoint);
            if((mastery.unlockedAbilities?.Count??0)>earned+mastery.legacyAbilityCredit)return false;
            if(mastery.unlockedAbilities!=null)foreach(var id in mastery.unlockedAbilities)if(family.FindSkill(id)<0)return false;
            if(mastery.abilityProgress!=null)foreach(var progress in mastery.abilityProgress)
            {
                int index=family.FindSkill(progress.abilityId);
                var ability=progress.isBasic?family.GetAbility(AbilitySlot.Basic):family.Skill(index);
                if(ability==null||ability.Id!=progress.abilityId)return false;
                if(progress.modifiers!=null)foreach(var m in progress.modifiers)
                {
                    var definition=ability.FindModifier(m.modifierId);
                    // Balance tuning must not invalidate an otherwise sound inventory save.
                    // Runtime clamps effects to the current cap and rechecks the mastery threshold.
                    if(definition==null)return false;
                }
            }
            return true;
        }
    }
}
