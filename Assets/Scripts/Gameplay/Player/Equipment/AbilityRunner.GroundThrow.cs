using Mismo.Gameplay.Player.Presentation;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    public sealed partial class AbilityRunner
    {
        AbilityDefinition groundAimDefinition;
        WeaponDefinition groundAimWeapon;
        TrapAction groundAimAction;
        GroundAreaAction groundAimArea;
        GroundThrowIndicator groundIndicator;
        public bool IsGroundAiming=>groundAimDefinition!=null;
        public AbilitySlot GroundAimSlot {get;private set;}
        public GroundThrowPath GroundAimPath {get;private set;}
        public static TrapAction GroundThrowAction(AbilityDefinition definition)
        {
            if(definition!=null&&definition.targetsGround&&definition.actions!=null)
                foreach(var action in definition.actions)if(action is TrapAction trap)return trap;
            return null;
        }
        static GroundAreaAction GroundArea(AbilityDefinition definition)
        {
            if(definition?.actions!=null)foreach(var action in definition.actions)if(action is GroundAreaAction area)return area;
            return null;
        }
        public static bool SupportsGroundAim(AbilityDefinition definition)=>definition!=null&&definition.targetsGround&&definition.groundIndicatorMaterial!=null&&(GroundThrowAction(definition)!=null||GroundArea(definition)!=null);
        public bool TryBeginGroundAim(AbilitySlot slot,Ray ray)
        {
            var definition=loadout?.GetAbility(slot);var action=GroundThrowAction(definition);
            if(!SupportsGroundAim(definition)||action!=null&&action.prefab==null||GameplayPause.BlocksInput||
                health!=null&&health.IsDead||loadout.Belt!=null&&loadout.Belt.ControlsMovement||Remaining(definition)>0||
                stamina!=null&&stamina.Current<definition.staminaCost||state.Focus<definition.focusCost)return false;
            // Never queue a targeting gesture: releasing during recovery must not throw later.
            if(Current!=null&&(!CanCancel||slot!=AbilitySlot.Q&&slot!=AbilitySlot.E))return false;
            Cancel();groundAimDefinition=definition;groundAimWeapon=loadout.ActiveDefinition;groundAimAction=action;GroundAimSlot=slot;
            groundAimArea=GroundArea(definition);
            groundIndicator??=new GroundThrowIndicator(gameObject);
            UpdateGroundAim(ray,true);return true;
        }
        public void UpdateGroundAim(Ray ray,bool held)
        {
            if(!IsGroundAiming)return;
            if(GameplayPause.BlocksInput||health!=null&&health.IsDead||loadout.ActiveDefinition!=groundAimWeapon||loadout.GetAbility(GroundAimSlot)!=groundAimDefinition)
            {CancelGroundAim();return;}
            GroundAimPath=GroundThrowTrajectory.Aim(gameObject,ray,groundAimDefinition.range,groundAimAction);
            groundIndicator.Show(GroundAimPath,groundAimDefinition.groundIndicatorMaterial,groundAimAction,groundAimArea?.radius??.65f);
            if(held)return;
            var path=GroundAimPath;var slot=GroundAimSlot;bool thrown=groundAimAction!=null;CancelGroundAim();
            if(path.valid)TryUse(slot,(path.end-path.start).normalized,path.end,path.end,groundThrow:thrown?path:(GroundThrowPath?)null);
        }
        public void CancelGroundAim()
        {groundAimDefinition=null;groundAimWeapon=null;groundAimAction=null;groundAimArea=null;groundIndicator?.Hide();}
        void OnApplicationFocus(bool focused){if(!focused)CancelGroundAim();}
    }
}
