using System;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    [Serializable]
    public sealed class HammerImpactAction : AbilityAction
    {
        public float damage=30,radius=2.5f,forward=1,height=.35f,pushDistance=.6f,stunSeconds=.25f;
        [Range(0,360)] public float arcDegrees=360;
        public float postureDamage=-1;
        public float[] hitTimes={0};
        public float[] hitYawAngles=Array.Empty<float>();
        public bool singleHitPerCast;
        public bool groundImpact;
        public AudioClip impactSfx;
        [Range(0,1)] public float impactVolume=.5f;
        public override void Begin(AbilityExecution c){c.ActionTimes[this]=0;HitBetween(c,-1,0);}
        public override void Tick(AbilityExecution c,float dt)
        {
            float previous=c.ActionTimes[this],next=Mathf.Min(previous+dt,c.Definition.active);
            c.ActionTimes[this]=next;HitBetween(c,previous,next);
        }
        void HitBetween(AbilityExecution c,float previous,float next)
        {
            if(hitTimes==null)return;
            for(int i=0;i<hitTimes.Length;i++){float at=hitTimes[i];if(at>=0&&at<c.Definition.active&&at>previous&&at<=next)Strike(c,i);}
        }
        void Strike(AbilityExecution c,int sample)
        {
            long id=AttackIdentity.Next();var seen=new HashSet<Component>();
            Vector3 direction=Vector3.ProjectOnPlane(c.Direction,Vector3.up).normalized;
            if(hitYawAngles!=null&&sample<hitYawAngles.Length)direction=Quaternion.AngleAxis(hitYawAngles[sample],Vector3.up)*direction;
            Vector3 origin=c.Owner.transform.position+Vector3.up*height+direction*forward;
            var feedback=c.Owner.GetComponent<Presentation.CombatFeedback>();
            if(groundImpact)feedback?.NotifyGroundImpact(origin,impactSfx,impactVolume,radius);
            bool sounded=false;int effects=0;
            foreach(var collider in Physics.OverlapSphere(origin,radius,~0,QueryTriggerInteraction.Ignore))
            {
                if(!(collider.GetComponentInParent<IDamageReceiver>() is Component target)||target.transform.root==c.Owner.transform.root||!seen.Add(target))continue;
                if(singleHitPerCast&&c.HitTargets.Contains(target))continue;
                Vector3 point=collider.ClosestPoint(origin);
                Vector3 toTarget=Vector3.ProjectOnPlane(point-c.Owner.transform.position,Vector3.up);
                if(arcDegrees<360 && toTarget.sqrMagnitude>.001f && Vector3.Angle(direction,toTarget)>arcDegrees*.5f)continue;
                Vector3 sightOrigin=c.Owner.transform.position+Vector3.up*height;
                if(Physics.Linecast(sightOrigin,point,out var wall,~0,QueryTriggerInteraction.Ignore)&&wall.transform.root!=c.Owner.transform.root&&wall.collider.GetComponentInParent<IDamageReceiver>()!=(target as IDamageReceiver))continue;
                Vector3 outward=Vector3.ProjectOnPlane(target.transform.position-origin,Vector3.up).normalized;
                if(outward.sqrMagnitude<.001f)outward=direction;
                if(!((IDamageReceiver)target).ReceiveDamage(new DamageInfo(damage*c.DamageMultiplier,c.Owner,point,outward,id,postureDamage,area:true,weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit)))continue;
                if(singleHitPerCast)c.HitTargets.Add(target);
                if(!groundImpact && effects++<4)feedback?.NotifyWeaponImpact(point,7,!sounded?impactSfx:null,impactVolume);
                sounded=true;
                if(stunSeconds>0)target.GetComponent<CombatState>()?.Stagger(stunSeconds);
                var agent=target.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if(pushDistance>0&&agent!=null&&agent.enabled&&agent.isOnNavMesh)agent.Move(outward*pushDistance);
            }
        }
    }
}
