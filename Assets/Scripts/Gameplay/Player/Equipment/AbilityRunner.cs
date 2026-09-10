using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;
namespace Mismo.Gameplay.Player.Equipment
{
    [DisallowMultipleComponent]
    public sealed class AbilityRunner : MonoBehaviour
    {
        readonly Dictionary<AbilityDefinition,float> readyAt=new Dictionary<AbilityDefinition,float>();
        BasicSwordCombo combo;Health health;Stamina stamina;CombatState state;EquipmentLoadout loadout;
        AbilitySlot? pending;Vector3 pendingDirection,pendingPoint;Vector3? pendingAim;float pendingUntil;bool pendingDash;
        public AbilityExecution Current {get;private set;}
        public PlayerMotor Motor {get;private set;}
        public SwordParry Parry {get;private set;}
        public bool IsBusy=>Current!=null;
        public bool IsMoving=>Current!=null&&Current.Began&&!Current.Ended&&System.Array.Exists(Current.Definition.actions,a=>a is MoveCasterAction);
        public float Mobility=>Current!=null&&!Current.Began?Current.Definition.preparationMobility:1;
        public float Normalized=>Current==null?0:Mathf.Clamp01(Current.Elapsed/(Current.Definition.Duration+(Current.Definition.chargeable?Current.Definition.maximumCharge:0)));
        public event System.Action<AbilityDefinition> Started;
        // Read-only presentation snapshot. Gameplay remains the owner of all clocks.
        public bool TryGetAnimationFrame(out Presentation.CombatAnimationFrame frame)
        {
            frame=default;
            var cast=Current;if(cast==null)return false;
            var definition=cast.Definition;
            if(definition.usesSwordCombo)
            {
                if(combo==null || !combo.IsActive)return false;
                frame=new Presentation.CombatAnimationFrame(definition,Presentation.CombatAnimationPhase.Combo,combo.CurrentStepNormalized,combo.CurrentStepIndex,cast.AttackId);
            }
            else
            {
                float start=cast.ReleasedAt>=0?cast.ReleasedAt:Mathf.Max(0,definition.preparation);
                var phase=!cast.Began?Presentation.CombatAnimationPhase.Preparation:!cast.Ended?Presentation.CombatAnimationPhase.Active:Presentation.CombatAnimationPhase.Recovery;
                float progress=!cast.Began?cast.Elapsed/Mathf.Max(.001f,definition.preparation)
                    :!cast.Ended?(cast.Elapsed-start)/Mathf.Max(.01f,definition.active)
                    :(cast.Elapsed-start-Mathf.Max(.01f,definition.active))/Mathf.Max(.001f,definition.recovery);
                frame=new Presentation.CombatAnimationFrame(definition,phase,progress,-1,cast.AttackId);
            }
            return true;
        }
        public void Initialize(EquipmentLoadout equipment)
        {
            loadout=equipment;Motor=GetComponent<PlayerMotor>();stamina=GetComponent<Stamina>();health=GetComponent<Health>();
            state=GetComponent<CombatState>()??gameObject.AddComponent<CombatState>();
            combo=GetComponentInChildren<BasicSwordCombo>(true);Parry=GetComponentInChildren<SwordParry>(true);
        }
        public float Remaining(AbilityDefinition definition)=>definition!=null&&readyAt.TryGetValue(definition,out float time)?Mathf.Max(0,time-Time.time):0;
        public bool CanCancel
        {
            get
            {
                if(Current==null)return true;
                if(Current.Definition.usesSwordCombo)return combo!=null&&combo.CanBranch;
                return !Current.Began?Current.Definition.cancelPreparation:Current.Ended&&Current.Definition.cancelRecovery;
            }
        }
        public bool TryUse(AbilitySlot slot,Vector3 direction,Vector3 groundPoint,Vector3? aimPoint=null,bool held=false)
        {
            var weapon=loadout.ActiveDefinition;var definition=weapon!=null?weapon.GetAbility(slot):null;
            if(definition==null||health!=null&&health.IsDead||loadout.Belt!=null&&loadout.Belt.ControlsMovement)return false;
            if(Remaining(definition)>0&&!(Current!=null&&definition.usesSwordCombo&&Current.Definition==definition))return false;
            if(stamina!=null&&stamina.Current<definition.staminaCost||state.Focus<definition.focusCost)return false;
            if(Current!=null)
            {
                if(definition.usesSwordCombo&&Current.Definition==definition&&combo!=null&&combo.RequestAttack())return true;
                if(!CanCancel||slot!=AbilitySlot.Q&&slot!=AbilitySlot.E)
                {
                    pending=slot;pendingDirection=direction;pendingPoint=groundPoint;pendingAim=aimPoint;pendingDash=false;pendingUntil=Time.time+.14f;return false;
                }
                Cancel();
            }
            if(definition.usesSwordCombo&&(combo==null||!combo.RequestAttack()))return false;
            stamina?.TrySpend(definition.staminaCost);state.Spend(definition.focusCost);
            Current=new AbilityExecution(this,weapon,definition,direction.sqrMagnitude>.001f?direction.normalized:Motor.Facing,groundPoint){AimPoint=aimPoint,Held=held};
            readyAt[definition]=Time.time+definition.cooldown/(slot==AbilitySlot.Basic?Current.AttackSpeed:1);loadout.MarkCombat();Started?.Invoke(definition);return true;
        }
        public bool TryDash(Vector3 direction)
        {
            var belt=loadout.BeltComponent;
            if(health!=null&&health.IsDead||belt==null||!belt.CanStart(Motor.IsGrounded))return false;
            if(!CanCancel){pendingDash=true;pending=null;pendingDirection=direction;pendingUntil=Time.time+.14f;return false;}
            Cancel();return belt.TryStart(direction,Motor.IsGrounded);
        }
        public void SetHeld(bool held){if(Current!=null&&Current.Definition.chargeable)Current.Held=held;}
        public void Tick(float dt)
        {
            Parry?.Tick(dt);
            if(health!=null&&health.IsDead){Cancel();return;}
            Advance(dt);
            if(Time.time>pendingUntil){pending=null;pendingDash=false;}
            if(pendingDash&&CanCancel){Vector3 direction=pendingDirection;pendingDash=false;TryDash(direction);}
            else if(pending.HasValue&&(Current==null||CanCancel&&(pending==AbilitySlot.Q||pending==AbilitySlot.E)||pending==AbilitySlot.Basic&&combo!=null&&combo.CanQueue))
            {var slot=pending.Value;pending=null;TryUse(slot,pendingDirection,pendingPoint,pendingAim);}
        }
        void Advance(float dt)
        {
            if(Current==null||dt<=0)return;
            var c=Current;var d=c.Definition;dt*=c.AttackSpeed;c.Elapsed+=dt;
            if(d.usesSwordCombo){combo.Tick(dt);if(!combo.IsActive&&!combo.IsRecovering)Current=null;return;}
            float start=Mathf.Max(0,d.preparation);
            if(d.chargeable)
            {
                if(!c.Began)
                {
                    c.Charge=Mathf.Clamp01((c.Elapsed-start)/Mathf.Max(.01f,d.maximumCharge-start));
                    if(c.Elapsed<start||c.Held&&c.Elapsed<d.maximumCharge)return;
                    c.ReleasedAt=Mathf.Max(start,Mathf.Min(c.Elapsed,d.maximumCharge));
                }
                start=c.ReleasedAt;
            }
            float end=start+Mathf.Max(.01f,d.active);
            if(!c.Began&&c.Elapsed>=start){c.Began=true;foreach(var action in d.actions)action?.Begin(c);}
            float activeDt=Mathf.Max(0,Mathf.Min(c.Elapsed,end)-Mathf.Max(c.Elapsed-dt,start));
            if(c.Began&&!c.Ended&&activeDt>0)foreach(var action in d.actions)action?.Tick(c,activeDt);
            if(c.Began&&!c.Ended&&c.Elapsed>=end)EndActions(c);
            if(c.Elapsed>=end+d.recovery)Current=null;
        }
        static void EndActions(AbilityExecution c){c.Ended=true;foreach(var action in c.Definition.actions)action?.End(c);}
        public bool Interrupt()
        {
            if(Current==null||Current.Began||!Current.Definition.interruptible)return false;
            state.Reward(0,"INTERRUMPIDO");Cancel();return true;
        }
        public void Cancel()
        {
            pending=null;pendingDash=false;
            if(Current==null)return;
            if(Current.Definition.usesSwordCombo)combo?.Cancel();
            if(Current.Began&&!Current.Ended)EndActions(Current);
            Parry?.Cancel();GetComponent<DefenseWindow>()?.CloseParry();Motor?.ClearControlledMovement();Current=null;
        }
        void OnDisable()=>Cancel();
    }
}
