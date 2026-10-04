using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        readonly Dictionary<string,double> pendingWeaponDamage=new Dictionary<string,double>();
        readonly Dictionary<string,Dictionary<string,int>> pendingSkillUses=new Dictionary<string,Dictionary<string,int>>();
        readonly HashSet<(string family,string ability,long use)> creditedSkillUses=new HashSet<(string,string,long)>();
        readonly Queue<(string family,string ability,long use)> skillUseOrder=new Queue<(string,string,long)>();
        float combatProgressSaveAt;

        // Called by monster reward receivers only, after a confirmed hit/defense. Never on casting.
        public void RecordMonsterDamage(string family,float healthDamage,string abilityId=null,long useId=0)
        {
            if(!IsReady||!HasFamily(family)||float.IsNaN(healthDamage)||float.IsInfinity(healthDamage)||healthDamage<=0)return;
            pendingWeaponDamage.TryGetValue(family,out double old);
            pendingWeaponDamage[family]=old+healthDamage;
            GetComponent<AbilityRunner>()?.OnMonsterAbilityHit(family,abilityId,useId);
            RecordMonsterSkillUse(family,abilityId,useId);
        }
        public void RecordMonsterSkillUse(string family,string abilityId,long useId)
        {
            if(!IsReady||useId==0||string.IsNullOrEmpty(abilityId))return;
            bool basic=FindFamily(family)?.GetAbility(AbilitySlot.Basic)?.Id==abilityId;
            if(!basic&&profile.progression.Find(family)?.IsUnlocked(abilityId)!=true)return;
            var key=(family,abilityId,useId);if(!creditedSkillUses.Add(key))return;
            skillUseOrder.Enqueue(key);
            if(skillUseOrder.Count>4096)creditedSkillUses.Remove(skillUseOrder.Dequeue());
            if(!pendingSkillUses.TryGetValue(family,out var skills))pendingSkillUses[family]=skills=new Dictionary<string,int>();
            skills.TryGetValue(abilityId,out int old);skills[abilityId]=Mathf.Min(1000000,old+1);
        }
        void ApplyPendingCombatProgress(ProgressionData next)
        {
            foreach(var pair in pendingWeaponDamage)Rules.GrantDamage(next.GetOrCreate(pair.Key),pair.Value);
            foreach(var family in pendingSkillUses)
            {
                var definition=FindFamily(family.Key);if(definition==null)continue;
                var mastery=next.GetOrCreate(family.Key);
                foreach(var skill in family.Value)
                {
                    bool basic=definition.GetAbility(AbilitySlot.Basic)?.Id==skill.Key;
                    int index=definition.FindSkill(skill.Key);if(!basic&&(index<0||!mastery.IsUnlocked(skill.Key)))continue;
                    var ability=basic?definition.GetAbility(AbilitySlot.Basic):definition.Skill(index);var progress=mastery.GetSkillProgress(skill.Key,basic);
                    var modifier=ability.FindModifier(progress.selectedModifierId);
                    for(int i=0;i<skill.Value;i++)progress.RecordUse(ability.masteryUsesRequired,modifier?.maxLevel??0,modifier?.effectiveUsesPerLevel??1);
                }
            }
        }
        void ClearPendingCombatProgress(){pendingWeaponDamage.Clear();pendingSkillUses.Clear();}
        public bool FlushCombatProgress()
        {
            if(!IsReady||pendingWeaponDamage.Count==0&&pendingSkillUses.Count==0)return true;
            return Commit(profile.Copy(),"",false);
        }
        void TickCombatProgress()
        {
            if(Time.unscaledTime<combatProgressSaveAt)return;
            combatProgressSaveAt=Time.unscaledTime+1;
            FlushCombatProgress();
        }
    }
}
