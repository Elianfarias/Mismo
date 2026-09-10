using Mismo.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class BossAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private BossController boss;
        private NavMeshAgent agent;
        private Health health;
        private float hitAt = -10f;
        private float staggerAt = -10f;
        private readonly EnemyActionPlayback playback = new EnemyActionPlayback();
        public AnimationClip ActionClip => playback.ActionClip;
        public Animator Animator => animator;
        public void Configure(Animator value) => animator = value;
        private void Awake()
        {
            boss=GetComponent<BossController>();agent=GetComponent<NavMeshAgent>();health=GetComponent<Health>();
            if(animator==null)animator=GetComponentInChildren<Animator>();
        }
        private void OnEnable()
        {
            if(health!=null)health.Damaged+=OnHit;
            if(boss!=null)boss.StateChanged+=OnState;
        }
        private void OnDisable()
        {
            if(health!=null)health.Damaged-=OnHit;
            if(boss!=null)boss.StateChanged-=OnState;
            playback.Dispose();
        }
        private void OnHit(DamageInfo _) => hitAt=Time.time;
        private void OnState(BossState state){if(state==BossState.Stagger)staggerAt=Time.time;}
        private void Update()
        {
            if(animator==null||boss==null){playback.Dispose();return;}
            float speed=agent!=null&&agent.enabled&&agent.isOnNavMesh?agent.velocity.magnitude:0f;
            float time=0;int motion=0;
            switch(boss.State)
            {
                case BossState.Telegraph:motion=3;time=.32f*boss.StateProgress;break;
                case BossState.Attack:motion=3;time=Mathf.Lerp(.32f,.59f,boss.StateProgress);break;
                case BossState.Recovery:motion=3;time=Mathf.Lerp(.59f,1,boss.StateProgress);break;
                case BossState.Stagger:motion=4;time=(Time.time-Mathf.Max(hitAt,staggerAt))/.416667f;break;
                case BossState.Dead:motion=4;time=1;break;
                default:
                    if(Time.time-hitAt<.416667f){motion=4;time=(Time.time-hitAt)/.416667f;}
                    else if(speed>.1f)motion=speed>2.1f?2:1;
                    break;
            }
            bool attacking=boss.State==BossState.Telegraph||boss.State==BossState.Attack||boss.State==BossState.Recovery;
            var phase=boss.State==BossState.Telegraph?EnemyAttackPhase.Preparation
                :boss.State==BossState.Attack?EnemyAttackPhase.Active:EnemyAttackPhase.Recovery;
            playback.Tick(animator,attacking?boss.CurrentAttack?.animation:null,phase,
                boss.StateProgress,motion,time,speed,Time.deltaTime);
        }
    }
}
