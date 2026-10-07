using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    // Evolutions that chain one ability into another or replay part of it.
    public sealed partial class AbilityRunner
    {
        // Hachazo doble's Remate: once the landing connects, a free basic follows at once.
        // The evolution's amount (100 % at rank 0, more per rank) scales the damage of that basic.
        void ChainBasic(AbilityExecution from)
        {
            from.Chain=false;
            var weapon=loadout.ActiveDefinition;var basic=loadout.GetAbility(AbilitySlot.Basic);
            if(from!=Current||basic==null||!basic.usesSwordCombo||combo==null)return;
            // The landing is over: end it quietly (its cooldown already runs) so the basic can start.
            Cancel();
            var evolution=from.Modifier;
            GetComponent<WeaponSkillEffects>()?.BoostNextBasic(from,evolution!=null?evolution.Amount(from.ModifierRank):1);
            var cast=new AbilityExecution(this,weapon,basic,from.Direction,from.GroundPoint){Began=false};
            Current=cast;
            if(!StartCombo(cast,true)){Current=null;return;}
            ConsumeOpening(cast);
            weaponVfx.Begin(cast);
            PlayExecutionSound(basic,combo.CurrentStepIndex);
            var inventory=GetComponent<Inventory.PlayerInventory>();
            float cooldown=inventory!=null?inventory.AbilityCooldown(weapon,basic,true):basic.cooldown/cast.AttackSpeed;
            cooldownDurations[basic]=cooldown;readyAt[basic]=Time.time+cooldown;
            loadout.MarkCombat();Started?.Invoke(basic);
        }

        // Giro mortal's Torbellino: when the active phase ends it may start over, free of cost and cooldown.
        // The strikes fire again and the animation replays the same window of the clip.
        bool RepeatActive(AbilityExecution c,float start)
        {
            var modifier=c.Modifier;
            if(modifier==null||modifier.behavior!=AbilityModifierBehavior.Whirlwind||c.Repeats>=modifier.maxRepeats)return false;
            if(Random.value>=modifier.Amount(c.ModifierRank))return false;
            c.Repeats++;
            c.Elapsed=start;
            c.HitTargets.Clear();c.ActionTimes.Clear();
            foreach(var action in c.Definition.actions)action?.Begin(c);
            weaponVfx.Release();
            PlayExecutionSound(c.Definition,c.RecastStage);
            state?.Reward(0,"GIRO EXTRA");
            return true;
        }
    }
}
