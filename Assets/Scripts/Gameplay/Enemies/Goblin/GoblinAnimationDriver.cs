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
        public Animator Animator => animator;
        public void Configure(Animator value) => animator=value;
        private void Awake(){goblin=GetComponent<GoblinController>();agent=GetComponent<NavMeshAgent>();health=GetComponent<Health>();}
        private void OnEnable(){if(health!=null)health.Damaged+=OnHit;}
        private void OnDisable(){if(health!=null)health.Damaged-=OnHit;}
        private void OnHit(DamageInfo _) => hitAt=Time.time;
        private void Update()
        {
            if(animator==null||goblin==null)return;
            int motion=0;float time=0;float speed=agent!=null&&agent.enabled?agent.velocity.magnitude:0;
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
            animator.SetInteger("Motion",motion);animator.SetFloat("ActionTime",Mathf.Clamp01(time));
            animator.SetFloat("PlaybackRate",Mathf.Clamp(speed/(motion==2?3.2f:1.6f),.7f,1.5f));
        }
    }
}
