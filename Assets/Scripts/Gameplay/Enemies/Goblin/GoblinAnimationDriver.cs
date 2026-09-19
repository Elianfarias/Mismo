using Mismo.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class GoblinAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private GoblinController goblin;
        private NavMeshAgent agent;
        private Health health;
        private float hitAt=-10;
        private AnimationClip hitClip;
        private AvatarMask hitMask;
        private const float HitDuration = .36f;
        private readonly EnemyActionPlayback playback = new EnemyActionPlayback();
        public AnimationClip ActionClip => playback.ActionClip;
        public Animator Animator => animator;
        public void Configure(Animator value) => animator=value;
        private void Awake(){goblin=GetComponent<GoblinController>();agent=GetComponent<NavMeshAgent>();health=GetComponent<Health>();if(animator==null)animator=GetComponentInChildren<Animator>();hitClip=Mismo.Core.ProjectAssets.Load<AnimationClip>("CombatPresentation/Human_Goblin_CombatDamage01");hitMask=Mismo.Core.ProjectAssets.Load<AvatarMask>("CombatPresentation/GoblinUpperBody");}
        private void OnEnable(){if(health!=null)health.Damaged+=OnHit;}
        private void OnDisable(){if(health!=null)health.Damaged-=OnHit;hitAt=-10;playback.Dispose();}
        private void OnHit(DamageInfo _) => hitAt=Time.time;
        private void Update()
        {
            if(animator==null||goblin==null){playback.Dispose();return;}
            int motion=0;float time=0;float speed=agent!=null&&agent.enabled&&agent.isOnNavMesh?agent.velocity.magnitude:0;
            switch(goblin.State)
            {
                // One authored attack is mapped to the existing AI clocks, preserving damage timing.
                case GoblinState.Telegraph: motion=3;time=.32f*goblin.StateProgress;break;
                case GoblinState.Attack: motion=3;time=Mathf.Lerp(.32f,.59f,goblin.StateProgress);break;
                case GoblinState.Recovery: motion=3;time=Mathf.Lerp(.59f,1,goblin.StateProgress);break;
                case GoblinState.Stagger: motion=4;time=Mathf.Min(1,(Time.time-hitAt)/.416667f);if(time>=1)time=Mathf.Lerp(.2f,1,goblin.StateProgress);break;
                case GoblinState.Dead: motion=4;time=1;break;
                default:
                    if(Time.time-hitAt<.416667f){motion=4;time=(Time.time-hitAt)/.416667f;}
                    else if(speed>.1f)motion=speed>2.1f?2:1;
                    break;
            }
            bool attacking=goblin.State==GoblinState.Telegraph||goblin.State==GoblinState.Attack||goblin.State==GoblinState.Recovery;
            bool reacting = health != null && !health.IsDead && Time.time - hitAt < HitDuration;
            var phase=goblin.State==GoblinState.Telegraph?EnemyAttackPhase.Preparation
                :goblin.State==GoblinState.Attack?EnemyAttackPhase.Active:EnemyAttackPhase.Recovery;
            playback.Tick(animator,attacking?goblin.CurrentAttack?.animation:null,phase,
                goblin.StateProgress,motion,time,speed,Time.deltaTime,
                reacting ? hitClip : null, Mathf.Clamp01((Time.time-hitAt)/HitDuration), hitMask);
        }
    }
}
