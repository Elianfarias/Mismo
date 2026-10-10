using Mismo.Gameplay.Combat;
using UnityEngine;
namespace Mismo.Gameplay.Enemies
{
    public sealed partial class SoulEaterPhaseOneController
    {
        bool phaseTwo,nextDive=true;
        float nextAerial,nextRepeatCharge,nextFire;
        Vector3 airStart,airTop,impactPoint,passStart,passEnd;
        Quaternion visualRest;
        SoulEaterBattlefield battlefield;
        public int Phase=>phaseTwo?2:1;
        public SoulEaterBattlefield Battlefield=>battlefield;
        public Vector3 ImpactPoint=>impactPoint;
        bool Airborne=>State==SoulEaterState.Ascending||State==SoulEaterState.AerialAim||State==SoulEaterState.AerialBreath||State==SoulEaterState.Diving||State==SoulEaterState.Landing;
        float DecisionPause=>phaseTwo?settings.phaseTwoDecisionPause:settings.decisionPause;
        void InitializePhaseTwo()
        {
            visualRest=rig.transform.localRotation;
            battlefield=GetComponent<SoulEaterBattlefield>()??gameObject.AddComponent<SoulEaterBattlefield>();
            battlefield.Initialize(this,effects?.Flame?.Material);
        }
        void ClearPhaseTwo()
        {phaseTwo=false;nextDive=true;nextAerial=nextRepeatCharge=nextFire=0;battlefield?.Clear();if(rig!=null)rig.transform.localRotation=visualRest;}
        public bool TryCheatPhaseTwo()
        {
            if(!initialized || !isActiveAndEnabled || health.IsDead || !settings.enablePhaseTwo || phaseTwo ||
                State==SoulEaterState.PhaseTransition || target==null || !IsFightingPlayer(target))return false;
            // Bypass attack damage modifiers: the one-hit cheat must never turn this into a kill.
            float remaining=health.Maximum*Mathf.Clamp(settings.phaseThreshold,.01f,.99f);
            if(health.Current>remaining)health.ApplyDamage(new DamageInfo(health.Current-remaining,null,transform.position,Vector3.zero,postureDamage:0));
            pendingStagger=false;ChargeUsed=true;battlefield?.Clear();rig.transform.localRotation=visualRest;
            LandSafely();Enter(SoulEaterState.PhaseTransition,settings.phaseRoar);effects?.Cue(SoulEaterCue.Roar);
            return true;
        }
        void BeginPhaseTwo()
        {
            phaseTwo=true;nextAerial=clock+settings.firstAerialDelay;nextRepeatCharge=clock+settings.phaseTwoChargeCooldown;
            Enter(SoulEaterState.Hunting,DecisionPause);
        }
        bool PhaseTwoDecision()
        {
            if(!phaseTwo)return false;
            if(clock>=nextAerial)
            {if(TryStartAerial(nextDive?SoulEaterAction.Dive:SoulEaterAction.AerialBreath)){nextDive=!nextDive;return true;}}
            if(clock>=nextRepeatCharge)
            {nextRepeatCharge=clock+settings.phaseTwoChargeCooldown;Enter(SoulEaterState.SpecialRoar,settings.specialRoar);effects?.Cue(SoulEaterCue.Roar);return true;}
            return false;
        }
        Vector3 SafeAirTarget(Vector3 targetPoint)
        {
            var point=targetPoint;point.y=battlefield.GroundY(point);return point;
        }
        public bool TryStartAerial(SoulEaterAction action)
        {
            if(!initialized || !phaseTwo || State!=SoulEaterState.Hunting || target==null ||
                (action!=SoulEaterAction.Dive&&action!=SoulEaterAction.AerialBreath))return false;
            battlefield.Clear();Action=action;airStart=transform.position;impactPoint=SafeAirTarget(target.position);
            var forward=Planar(target.position-transform.position).normalized;if(forward.sqrMagnitude<.1f)forward=transform.forward;
            passStart=SafeAirTarget(impactPoint-forward*settings.aerialPassDistance*.5f);passEnd=SafeAirTarget(impactPoint+forward*settings.aerialPassDistance*.5f);
            airTop=(action==SoulEaterAction.Dive?airStart:passStart)+Vector3.up*settings.flightHeight;
            if(action==SoulEaterAction.AerialBreath)transform.rotation=Quaternion.LookRotation((passEnd-passStart).normalized);
            DisableNavigation();Enter(SoulEaterState.Ascending,settings.ascentTime,true);effects?.Cue(SoulEaterCue.Jump);return true;
        }
        void AerialStep(float dt)
        {
            if(State==SoulEaterState.Ascending)
            {
                transform.position=Vector3.Lerp(airStart,airTop,Mathf.SmoothStep(0,1,Progress));
                if(Action==SoulEaterAction.Dive){impactPoint=SafeAirTarget(target.position);battlefield.Circle(impactPoint,settings.impactRadius,false);}
                else battlefield.Strip(passStart,passEnd,settings.aerialStripHalfWidth,false);
                if(elapsed>=duration)
                {
                    if(Action==SoulEaterAction.Dive)battlefield.Circle(impactPoint,settings.impactRadius,true);
                    else battlefield.Strip(passStart,passEnd,settings.aerialStripHalfWidth,true);
                    Enter(SoulEaterState.AerialAim,settings.aerialAimTime,true);
                    if(Action==SoulEaterAction.AerialBreath)effects?.Cue(SoulEaterCue.Inhale);
                }
            }
            else if(State==SoulEaterState.AerialAim && elapsed>=duration)
            {
                nextTick=clock;nextFire=clock;
                if(Action==SoulEaterAction.Dive)
                {var d=Planar(impactPoint-transform.position);if(d.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(d);Enter(SoulEaterState.Diving,settings.diveTime,true);}
                else Enter(SoulEaterState.AerialBreath,settings.AerialPassDuration,true);
            }
            else if(State==SoulEaterState.Diving)
            {
                transform.position=Vector3.Lerp(airTop,impactPoint,Progress*Progress);
                if(elapsed>=duration)
                {
                    battlefield.Impact(impactPoint);impactPoint.y=battlefield.GroundY(impactPoint);transform.position=impactPoint;
                    effects?.DustImpact(impactPoint,SoulEaterDustKind.Dive);Enter(SoulEaterState.ImpactRecovery,settings.diveRecovery);pendingStagger=false;
                }
            }
            else if(State==SoulEaterState.AerialBreath)
            {
                // A single low pass, followed immediately by a vulnerable landing.
                var p=Vector3.Lerp(passStart,passEnd,Progress);p.y=battlefield.GroundY(p)+Mathf.Lerp(settings.flightHeight,settings.aerialPassHeight,Mathf.Clamp01(Progress*3));transform.position=p;
                if(elapsed>=duration){airStart=transform.position;impactPoint=passEnd;impactPoint.y=battlefield.GroundY(impactPoint);battlefield.HideMarker();Enter(SoulEaterState.Landing,settings.landingTime,true);}
            }
            else if(State==SoulEaterState.Landing)
            {
                transform.position=Vector3.Lerp(airStart,impactPoint,Mathf.SmoothStep(0,1,Progress));
                if(elapsed>=duration){transform.position=impactPoint;effects?.DustImpact(impactPoint,SoulEaterDustKind.Landing);Enter(SoulEaterState.ImpactRecovery,settings.diveRecovery);pendingStagger=false;}
            }
            else if(State==SoulEaterState.ImpactRecovery && elapsed>=duration)
            {
                RestoreNavigation();nextAerial=clock+settings.aerialCooldown;nextRepeatCharge=Mathf.Max(nextRepeatCharge,clock+settings.chargeDelayAfterLanding);Enter(SoulEaterState.Hunting,DecisionPause);
            }
        }
        void AerialPresentation(float dt)
        {
            if(State!=SoulEaterState.AerialBreath)return;
            Vector3 point=transform.position+transform.forward*settings.aerialFlameAhead;point.y=battlefield.GroundY(point);
            aim=(point-MouthPosition).normalized;float reach=Vector3.Distance(MouthPosition,point)+1;
            effects?.Breath(MouthPosition,aim,reach,settings.breathHalfAngle,dt);
            if(clock>=nextTick){nextTick=clock+settings.breathTickInterval;BreathHit(reach);}
            if(clock>=nextFire){nextFire=clock+settings.aerialFirePlacementInterval;battlefield.AddFire(point);}
        }
        void LeaveGroundFire(float flameReach)
        {
            if(!phaseTwo || clock<nextFire || flameReach<1)return;
            nextFire=clock+settings.groundFirePlacementInterval;
            // Follow the flame's actual endpoint, clamped to the player distance, rather than a fixed sweep.
            float reach=Mathf.Min(flameReach,Vector3.Distance(MouthPosition,TargetPoint));
            var point=MouthPosition+aim*reach;point.y=battlefield.GroundY(point);battlefield.AddFire(point);
        }
        void AerialPose(ref AnimationClip clip,ref float p)
        {
            if(State==SoulEaterState.Ascending){clip=Progress<.25f?settings.takeOff:settings.flight;p=Progress<.25f?Progress/.25f:Loop(clip);}
            else if(State==SoulEaterState.AerialAim){clip=settings.hover;p=Loop(clip);}
            else if(State==SoulEaterState.Diving){clip=settings.glide;p=Loop(clip);}
            else if(State==SoulEaterState.AerialBreath){clip=settings.aerialBreath!=null?settings.aerialBreath:settings.flight;p=Mathf.Lerp(.2f,.8f,Progress);}
            else if(State==SoulEaterState.Landing){clip=settings.land;p=Mathf.Lerp(.1f,.75f,Progress);}
            else if(State==SoulEaterState.ImpactRecovery)
            {
                // Land's last frames return to an aerial pose. Never hold those after impact.
                clip=elapsed<settings.impactSettleTime?settings.land:settings.idle;
                p=elapsed<settings.impactSettleTime?settings.impactLandPose:Loop(clip);
            }
        }
    }
}
