using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    [DefaultExecutionOrder(100),DisallowMultipleComponent]
    public sealed class CreatureAnimationDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] EnemyGroundSupport groundSupport=new EnemyGroundSupport();
        Transform visual;
        Vector3 groundedLocalPosition;
        readonly EnemyActionPlayback playback=new EnemyActionPlayback();
        readonly EnemyAttackAnimation sample=new EnemyAttackAnimation{activeStartsAt=0,recoveryStartsAt=1,blendSeconds=.025f};
        GoblinController controller;
        NavMeshAgent agent;
        GameObject held;
        Transform socket;
        GoblinAttack current;
        bool released;
        public Animator Animator=>animator;
        public Vector3 ReleaseOrigin=>socket!=null?socket.position:transform.position+Vector3.up*3+transform.forward*2;
        public void Configure(Animator value)=>animator=value;
        public void PrepareRelease(GoblinAttack action)
        {
            if(animator==null||action?.animation.PlaybackClip==null)return;
            // Resolve the authored socket at the release frame, not the previous rendered frame.
            action.animation.PlaybackClip.SampleAnimation(animator.gameObject,Mathf.Max(0,action.windup-action.preparationDuration));
            socket=animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==action.projectileSocket);
        }
        void Awake()
        {
            controller=GetComponent<GoblinController>();agent=GetComponent<NavMeshAgent>();
            if(animator==null)animator=GetComponentInChildren<Animator>();
            visual=transform.Find("Visual");
            if(visual!=null)groundedLocalPosition=visual.localPosition;
        }
        void LateUpdate()
        {
            if(visual==null||controller==null||controller.State==GoblinState.Dead)return;
            // Creature prefabs already align the model's soles with the actor origin.
            // Restore that authored offset so terrain corrections never accumulate.
            visual.localPosition=groundedLocalPosition;
            groundSupport.Apply(transform,visual,1f,visualAlreadyGrounded:true);
        }
        public void ReleaseProp(){released=true;if(held!=null)Destroy(held);held=null;}
        void OnDisable(){ReleaseProp();playback.Dispose();current=null;}
        void Update()
        {
            if(controller==null||animator==null)return;
            bool acting=controller.State==GoblinState.Telegraph||controller.State==GoblinState.Attack||controller.State==GoblinState.Recovery;
            var action=acting?controller.CurrentAttack:null;
            if(action!=current){ReleaseProp();current=action;released=false;socket=action==null?null:animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==action.projectileSocket);}
            if(!acting){ReleaseProp();current=null;}
            float speed=agent!=null&&agent.enabled&&agent.isOnNavMesh?agent.velocity.magnitude:0;
            if(action==null)
            {playback.Tick(animator,null,EnemyAttackPhase.Active,0,speed>.1f?(speed>2.1f?2:1):0,0,speed,Time.deltaTime);return;}
            float elapsed=controller.AttackElapsed;
            if(action.kind==CreatureAttackKind.Projectile&&socket!=null&&!released&&held==null&&elapsed>=action.grabAt&&controller.State==GoblinState.Telegraph)
            {held=Instantiate(action.projectileVisual,socket,false);held.name="Held rock";}
            AnimationClip clip=action.animation.PlaybackClip;float normalized=0;
            if(action.preparationClip!=null&&elapsed<action.preparationDuration)
            {clip=action.preparationClip;normalized=elapsed/Mathf.Max(.01f,action.preparationDuration)*action.preparationEndNormalized;}
            else if(action.loopActiveAnimation)
            {
                if(controller.State==GoblinState.Telegraph||controller.State==GoblinState.Recovery)
                {clip=action.preparationClip;normalized=controller.State==GoblinState.Telegraph?action.preparationEndNormalized:Mathf.Lerp(action.preparationEndNormalized,1,controller.StateProgress);}
                else normalized=clip!=null?Mathf.Repeat((elapsed-action.windup)/Mathf.Max(.01f,clip.length),1):0;
            }
            else if(clip!=null)
            {
                float mainTime=elapsed-action.preparationDuration;
                if(action.recoveryClip!=null&&mainTime>=clip.length)
                {mainTime-=clip.length;clip=action.recoveryClip;}
                normalized=mainTime/Mathf.Max(.01f,clip.length);
            }
            sample.clip=clip;sample.blendSeconds=action.animation.blendSeconds;
            playback.Tick(animator,sample,EnemyAttackPhase.Active,Mathf.Clamp01(normalized),0,0,speed,Time.deltaTime);
        }
    }
}
