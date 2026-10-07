using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    public sealed partial class AbilityRunner
    {
        float dodgeFollowupUntil,openingUntil;
        string dodgeFamily,openingFamily;
        AbilityExecution openingSource;

        // Equipment changes, death and disabling also end open recast chains and start their cooldown.
        void ResetOpportunities()
        {dodgeFollowupUntil=openingUntil=0;dodgeFamily=openingFamily=null;openingSource=null;CloseRecasts();}

        bool CanFollowup(WeaponDefinition weapon)=>weapon!=null&&weapon.MasteryId==openingFamily&&Time.time<openingUntil&&
            (Current==null||Current==openingSource);

        void ConfigureOpening(AbilityExecution cast,bool followup)
        {
            if(!cast.Definition.usesSwordCombo)return;
            cast.CounterOpener=followup;
            cast.OpeningStep=followup?Mathf.Max(0,cast.Definition.comboSteps.Length-1):0;
            cast.DodgeChain=!followup&&cast.Modifier?.behavior==AbilityModifierBehavior.DodgeChain&&
                cast.WeaponFamilyId==dodgeFamily&&Time.time<dodgeFollowupUntil;
        }

        bool StartCombo(AbilityExecution cast,bool free=false)
        {
            if(combo==null||!combo.RequestAttack(cast.Definition,cast.OpeningStep,cast.DodgeChain,free))return false;
            cast.Began=true;return true;
        }

        void ConsumeOpening(AbilityExecution cast)
        {
            if(cast.CounterOpener){openingUntil=0;openingSource=null;}
            if(cast.DodgeChain)dodgeFollowupUntil=0;
        }

        void OnWeaponEvasionCompleted(AbilityExecution cast)
        {
            var inventory=GetComponent<Inventory.PlayerInventory>();var weapon=loadout?.ActiveDefinition;
            if(weapon==null||cast.WeaponFamilyId!=weapon.MasteryId)return;
            int rank=0;var modifier=inventory!=null?inventory.AbilityBehavior(weapon,loadout.GetAbility(AbilitySlot.Basic),out rank):null;
            if(modifier?.behavior!=AbilityModifierBehavior.DodgeChain)return;
            dodgeFamily=weapon.MasteryId;dodgeFollowupUntil=Time.time+modifier.followupWindow+rank*modifier.windowPerRank;
            state?.Reward(0,"CADENA EVASIVA · M1");
        }

        public void OnDefenseResolved(HitOutcome outcome)
        {
            if(outcome!=HitOutcome.Parry&&outcome!=HitOutcome.PerfectParry)return;
            var cast=Current;
            if(cast==null||!cast.Began||cast.Ended||cast.Modifier?.behavior!=AbilityModifierBehavior.ParryRiposte)return;
            OpenFollowup(cast,"CONTRAATAQUE · M1");
        }

        public void OnMonsterAbilityHit(string family,string abilityId,long useId)
        {
            var cast=Current;
            if(cast==null||!cast.Began||cast.Ended||cast.UseId!=useId||cast.WeaponFamilyId!=family||cast.Definition.Id!=abilityId||
                cast.Modifier?.behavior!=AbilityModifierBehavior.LungeFinisher)return;
            OpenFollowup(cast,"REMATE · M1");
        }

        void OpenFollowup(AbilityExecution cast,string label)
        {
            openingFamily=cast.WeaponFamilyId;openingSource=cast;
            openingUntil=Time.time+cast.Modifier.followupWindow+cast.ModifierRank*cast.Modifier.windowPerRank;
            state?.Reward(0,label);
        }
    }
}
