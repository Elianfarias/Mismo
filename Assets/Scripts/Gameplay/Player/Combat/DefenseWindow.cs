using UnityEngine;
namespace Mismo.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class DefenseWindow : MonoBehaviour
    {
        float parry,dodge,parryAge,dodgeAge;
        bool dodgeRewarded;
        public bool HasParry => parry > 0;
        public void OpenParry(float duration){parry=duration;parryAge=0;}
        public void CloseParry()=>parry=0;
        public void OpenDodge(float duration){dodge=duration;dodgeAge=0;dodgeRewarded=false;}
        public void CloseDodge()=>dodge=0;
        public HitOutcome Resolve(DamageInfo damage)
        {
            if(parry>0 && damage.Parryable && !damage.Area)
            {
                var motor=GetComponent<Mismo.Gameplay.Player.Movement.PlayerMotor>();
                Vector3 facing=motor!=null?motor.Facing:transform.forward;
                Vector3 incoming=Vector3.ProjectOnPlane(-damage.Direction,Vector3.up);
                if(incoming.sqrMagnitude>.001f&&Vector3.Dot(Vector3.ProjectOnPlane(facing,Vector3.up).normalized,incoming.normalized)>=.1f)
                {parry=0;return parryAge<=CombatRules.Current.perfectParryWindow?HitOutcome.PerfectParry:HitOutcome.Parry;}
            }
            if(dodge>0)
            {
                bool perfect=!dodgeRewarded&&dodgeAge<=CombatRules.Current.perfectDodgeWindow;dodgeRewarded=true;
                return perfect?HitOutcome.PerfectDodge:HitOutcome.Dodge;
            }
            return HitOutcome.Ignored;
        }
        void Update()=>Tick(Time.deltaTime);
        public void Tick(float dt){parry=Mathf.Max(0,parry-dt);dodge=Mathf.Max(0,dodge-dt);parryAge+=dt;dodgeAge+=dt;}
    }
}
