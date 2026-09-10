using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    // Compatibility bridge for unmigrated abilities and direct legacy combat components.
    // New action clips are resolved from family data before this adapter is consulted.
    internal sealed class LegacyCombatAnimationAdapter
    {
        private readonly BasicSwordCombo combo;
        private readonly SwordParry parry;
        private readonly SwordLunge lunge;
        private readonly SwordSpinAttack spin;
        public LegacyCombatAnimationAdapter(GameObject owner)
        {
            combo=owner.GetComponentInChildren<BasicSwordCombo>();parry=owner.GetComponentInChildren<SwordParry>();
            lunge=owner.GetComponentInChildren<SwordLunge>();spin=owner.GetComponentInChildren<SwordSpinAttack>();
        }
        public void Resolve(AbilityRunner runner,ref CharacterMotion motion,ref float time)
        {
            if(parry!=null && parry.IsWindowOpen){motion=CharacterMotion.Parry;time=1-parry.WindowNormalized;}
            else if(spin!=null && spin.IsActive){motion=CharacterMotion.Spin;time=1-spin.ActiveRemaining/spin.Duration;}
            else if(lunge!=null && lunge.IsActive){motion=CharacterMotion.Lunge;time=lunge.ActiveNormalized;}
            else if(combo!=null && combo.IsActive)
            {motion=(CharacterMotion)((int)CharacterMotion.Attack1+Mathf.Clamp(combo.CurrentStepIndex,0,2));time=combo.CurrentStepNormalized;}
            if(runner==null || !runner.IsBusy || runner.Current.Definition.usesSwordCombo)return;
            switch(runner.Current.Definition.pose)
            {
                case AbilityPose.Lunge:motion=CharacterMotion.Lunge;break;
                case AbilityPose.Spin:motion=CharacterMotion.Spin;break;
                case AbilityPose.Parry:motion=CharacterMotion.Parry;break;
            }
            time=runner.Normalized;
        }
    }
}
