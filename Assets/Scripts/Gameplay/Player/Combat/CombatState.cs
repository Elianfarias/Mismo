using System;
using UnityEngine;
namespace Mismo.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatState : MonoBehaviour
    {
        [SerializeField] float maximumPosture=70, regeneration=9, regenerationDelay=3, breakDuration=2;
        public bool UsesPosture {get;private set;}
        public bool Recovering {get;set;}
        public float Focus {get;private set;}
        public float Posture {get;private set;}
        public float PostureNormalized => maximumPosture>0?Posture/maximumPosture:0;
        public bool Broken => brokenRemaining>0;
        public bool Opening => Recovering||Broken;
        public event Action<float> PostureBroken;
        public event Action<string> Rewarded;
        float sinceHit,brokenRemaining;
        float breakRecoveryDuration, breakRecoveryRemaining;
        public bool RecoveringFromBreak => breakRecoveryRemaining > 0;
        int consecutiveInterrupts;
        float interruptImmunityRemaining;
        public bool InterruptImmune => interruptImmunityRemaining > 0;
        // Call only for an eligible mini-interruption, after resolving posture damage.
        // Posture breaks and parries must never pass through this gate.
        public bool TryInterrupt(int maximumConsecutive, float immunityDuration)
        {
            if (Broken || InterruptImmune || health != null && health.IsDead) return false;
            if (++consecutiveInterrupts >= Mathf.Max(1, maximumConsecutive))
            {
                consecutiveInterrupts = 0;
                interruptImmunityRemaining = Mathf.Max(0, immunityDuration);
            }
            return true;
        }
        public void ConfigureBreakRecovery(float seconds) => breakRecoveryDuration = Mathf.Max(0, seconds);
        Health health;
        bool wasDead;
        void Awake(){health=GetComponent<Health>();if(health!=null)health.Changed+=LifeChanged;}
        void OnDestroy(){if(health!=null)health.Changed-=LifeChanged;}
        void LifeChanged(float value,float max){if(value<=0){wasDead=true;Focus=0;}else if(wasDead){wasDead=false;ResetCombat();}}
        public void ConfigurePosture(float maximum){UsesPosture=true;maximumPosture=maximum;ResetCombat();}
        public void ResetCombat(){Posture=maximumPosture;Focus=0;sinceHit=brokenRemaining=breakRecoveryRemaining=interruptImmunityRemaining=0;consecutiveInterrupts=0;Recovering=false;}
        public void Stagger(float duration)
        {
            if(health!=null&&health.IsDead)return;
            brokenRemaining=Mathf.Max(brokenRemaining,duration);PostureBroken?.Invoke(duration);
        }
        public float DamagePosture(float amount)
        {
            if(!UsesPosture||Broken||RecoveringFromBreak||amount<=0||health!=null&&health.IsDead)return 0;
            sinceHit=0;float applied=Mathf.Min(Posture,amount);Posture-=applied;
            if(Posture<=0){brokenRemaining=breakDuration;PostureBroken?.Invoke(breakDuration);}
            return applied;
        }
        public void Reward(float amount,string reason){if(health!=null&&health.IsDead)return;Focus=Mathf.Clamp(Focus+amount,0,100);Rewarded?.Invoke(reason);}
        public bool Spend(float amount){if(Focus<amount)return false;Focus-=amount;return true;}
        void Update()=>Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (dt <= 0) return;
            if(health!=null&&health.IsDead)return;
            interruptImmunityRemaining=Mathf.Max(0,interruptImmunityRemaining-dt);
            if(Broken){brokenRemaining=Mathf.Max(0,brokenRemaining-dt);if(!Broken){Posture=maximumPosture;breakRecoveryRemaining=breakRecoveryDuration;}return;}
            breakRecoveryRemaining=Mathf.Max(0,breakRecoveryRemaining-dt);
            sinceHit+=dt;if(UsesPosture&&sinceHit>=regenerationDelay)Posture=Mathf.Min(maximumPosture,Posture+regeneration*dt);
        }
    }
}
