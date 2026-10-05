using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterPhaseTwoChecks
    {
        static Vector3 crater;static float originalHeight;static int savedCount;
        static readonly List<string> results=new List<string>();
        static void Check(bool value,string message)
        {if(!value)throw new Exception(message);results.Add("PASS "+message);Debug.Log("PHASE_TWO_CHECK "+message);}
        static IEnumerator Until(Func<bool> ready,float timeout=20)
        {float end=Time.realtimeSinceStartup+timeout;while(!ready()){if(Time.realtimeSinceStartup>end)throw new Exception("Phase two timeout");yield return null;}}
        static void Warp(DragonArcCoordinator arc,Vector3 p)
        {arc.GetComponent<PlayerController>().enabled=false;arc.GetComponent<Movement.PlayerMotor>().ResetPosition(p+Vector3.up*.25f);}
        sealed class FailedSave:IProfileRepository
        {
            public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=null;return ProfileReadResult.Missing;}
            public void Write(string payload)=>throw new IOException("Expected persistent impact failure");
        }
        public static IEnumerator Play(DragonArcCoordinator arc,SoulEaterPhaseOneController boss,Action<string> capture)
        {
            results.Clear();var world=Object.FindFirstObjectByType<ExplorationChunks>();var shield=arc.GetComponent<Invulnerability>();shield.StartWindow(300);
            Check(boss.Settings.enablePhaseTwo&&boss.Settings.flight!=null&&boss.Settings.glide!=null&&boss.Settings.hover!=null&&boss.Settings.aerialBreath!=null,"Second-phase clips are included in the actual boss asset");
            var tree=WorldDestructible.Loaded.FirstOrDefault(p=>p.Kind==WorldAssetKind.Tree&&Vector3.Distance(p.transform.position,arc.Layout.Arena)<25);
            var rock=WorldDestructible.Loaded.FirstOrDefault(p=>p.Kind==WorldAssetKind.Rock&&Vector3.Distance(p.transform.position,arc.Layout.Arena)<28);
            Check(tree!=null&&rock!=null,"Arena contains sparse destructible trees and rocks");
            var repository=typeof(WorldSession).GetField("repository",BindingFlags.NonPublic|BindingFlags.Static);
            object originalRepository=repository.GetValue(null);var p0=rock.transform.position;float h0=world.SurfaceHeight(p0);string before=JsonUtility.ToJson(WorldSession.Current);
            try
            {repository.SetValue(null,new FailedSave());Check(!world.TryDragonImpact(p0,6,1.2f)&&rock.gameObject.activeSelf&&world.SurfaceHeight(p0)==h0&&JsonUtility.ToJson(WorldSession.Current)==before,"Failed save cannot partially deform terrain or destroy resources");}
            finally{repository.SetValue(null,originalRepository);}
            Check(!world.TryDragonImpact(arc.Layout.Altar,6,1.2f),"Altar and access remain protected");
            var corrupt=WorldSession.Current.Copy();corrupt.dragonImpacts.Add(new WorldImpactRecord(new Vector3(float.NaN,0,0),6,1));Check(!corrupt.IsValid(),"Corrupt non-finite impacts are rejected without changing the active save");
            var legacy=JsonUtility.FromJson<WorldSaveData>(before);legacy.dragonImpacts=null;Check(legacy.IsValid(),"Existing saves without destruction data still load");
            boss.ResetEncounter();boss.BeginEncounter(arc.transform);
            var closeCamera=UnityEngine.Camera.main;var closeOrbit=Object.FindFirstObjectByType<Camera.ThirdPersonCamera>();
            var savedPosition=closeCamera.transform.position;var savedRotation=closeCamera.transform.rotation;float savedFov=closeCamera.fieldOfView;
            if(closeOrbit!=null)closeOrbit.enabled=false;
            float framingScale=boss.GetComponent<CapsuleCollider>().radius/1.4f;
            closeCamera.transform.position=boss.transform.TransformPoint(new Vector3(5,2.4f,10)*framingScale);closeCamera.transform.LookAt(boss.transform.TransformPoint(new Vector3(0,1.8f,2)*framingScale));closeCamera.fieldOfView=45;
            yield return null;capture("cuello-patas-en-juego");
            closeCamera.transform.SetPositionAndRotation(savedPosition,savedRotation);closeCamera.fieldOfView=savedFov;if(closeOrbit!=null)closeOrbit.enabled=true;
            int transitions=0;boss.PhaseTwoRequested+=()=>transitions++;
            boss.Health.ApplyDamage(new DamageInfo(boss.Health.Maximum*.51f,arc.gameObject,boss.transform.position,Vector3.forward));float remaining=boss.Health.Current;
            yield return Until(()=>boss.Phase==2);
            Check(boss.Phase==2&&!boss.PhaseOneComplete&&boss.Health.Current==remaining&&transitions==1,"50% transition starts phase two once, preserving remaining health");
            Check(boss.GetComponent<Invulnerability>()?.IsInvulnerable!=true,"Regional encounter no longer shields the boss at 50%");
            Warp(arc,tree.transform.position+Vector3.right);Check(boss.TryStartAerial(SoulEaterAction.Dive),"Dive starts in the live regional encounter");
            var camera=UnityEngine.Camera.main;var orbit=Object.FindFirstObjectByType<Camera.ThirdPersonCamera>();if(orbit!=null)orbit.enabled=false;Vector3 focus=tree.transform.position;camera.transform.position=focus+new Vector3(23,16,-24);camera.transform.LookAt(focus+Vector3.up*8);camera.fieldOfView=55;
            yield return Until(()=>boss.State==SoulEaterState.Ascending&&boss.Progress>.5f);capture("fase2-01-ascenso");
            yield return Until(()=>boss.State==SoulEaterState.AerialAim);crater=boss.ImpactPoint;originalHeight=world.SurfaceHeight(crater);
            Check(boss.Battlefield.MarkerLocked&&boss.Battlefield.MarkerVisible,"Dive warning locks before the descent");
            var marker=boss.GetComponentsInChildren<LineRenderer>().First(l=>l.name=="Attack warning");
            Check(boss.Settings.telegraphPrefab!=null&&marker.sharedMaterial==boss.Settings.telegraphPrefab.sharedMaterial&&Mathf.Approximately(marker.widthMultiplier,boss.Settings.telegraphWidth)&&Vector4.Distance((Vector4)marker.startColor,(Vector4)boss.Settings.lockedTelegraphColor)<.01f,"Warning instantiates the editable prefab and uses configured material, width and locked color (actual="+marker.startColor+", material="+marker.sharedMaterial.name+", width="+marker.widthMultiplier+")");
            Warp(arc,crater+Vector3.right*12);capture("fase2-02-objetivo-fijo");
            yield return Until(()=>boss.State==SoulEaterState.Diving&&boss.Progress>.45f);
            Check(Vector3.Distance(crater,boss.ImpactPoint)<.001f,"Moving after the warning locks cannot move the impact target");capture("fase2-03-picada");
            yield return Until(()=>boss.State==SoulEaterState.ImpactRecovery);
            var dust=boss.GetComponent<SoulEaterEffects>().Dust;int diveDustCount=dust.BurstCount;
            Check(dust.LastKind==SoulEaterDustKind.Dive&&Vector3.Distance(dust.LastPosition,boss.transform.position)<.1f,"La picada emite DustExplosion grande sobre el suelo del cráter");
            yield return new WaitForSeconds(.18f);
            Check(dust.BurstCount==diveDustCount,"La recuperación de picada no repite la explosión de polvo");
            Check(world.SurfaceHeight(crater)<originalHeight-.6f&& !tree.gameObject.activeSelf,"Dive physically lowers terrain and destroys the baited tree");
            Check(boss.Battlefield.FireCount==0&&!boss.Battlefield.MarkerVisible&&boss.Combat.Recovering,"Impact has a clear recovery window and clears its target marker");capture("fase2-04-crater");
            yield return Until(()=>boss.State==SoulEaterState.ImpactRecovery&&boss.Progress>.28f);
            var anim=typeof(SoulEaterPhaseOneController).GetField("animation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(boss);
            var selected=(AnimationClip)typeof(SoulEaterAnimation).GetField("selected",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(anim);
            Check(selected==boss.Settings.idle,"After impact the boss settles into grounded Idle instead of holding the aerial end of Land");
            capture("fase2-04-apoyo");
            var terrainHit=Physics.RaycastAll(crater+Vector3.up*10,Vector3.down,20).FirstOrDefault(h=>h.collider.GetComponentInParent<Health>()==null&&h.collider.GetComponent<MeshCollider>()!=null);
            Check(terrainHit.collider!=null&&terrainHit.point.y<originalHeight-.5f,"Crater mesh collider matches the visible depression");
            var life=arc.GetComponent<Health>();var defense=arc.GetComponent<DefenseWindow>();
            Warp(arc,crater);shield.Cancel();defense.OpenDodge(1);float hp=life.Current;
            boss.Battlefield.Impact(crater);boss.Battlefield.Tick(.15f);
            Check(life.Current==hp,"A timed dodge avoids impact damage");defense.CloseDodge();shield.Cancel();
            boss.Battlefield.Impact(crater);float after=life.Current;boss.Battlefield.Tick(.3f);
            Check(after<hp&&hp-after<=boss.Settings.diveDamage&&life.Current==after,"Direct impact deals bounded damage once per attack");
            life.Heal(life.Maximum);shield.Cancel();Warp(arc,crater+Vector3.right*7.5f);hp=life.Current;
            boss.Battlefield.Impact(crater);Check(life.Current==hp,"Outer shockwave does not hit before reaching the player");boss.Battlefield.Tick(.5f);after=life.Current;boss.Battlefield.Tick(.2f);
            Check(after<hp&&hp-after<=boss.Settings.shockwaveDamage&&life.Current==after,"Expanding outer wave deals a single lower-damage hit");life.Heal(life.Maximum);shield.StartWindow(300);
            int count=WorldSession.Current.dragonImpacts.Count;
            Check(world.TryDragonImpact(crater,6,1.2f)&&WorldSession.Current.dragonImpacts.Count==count&&world.SurfaceHeight(crater)>=originalHeight-1.21f,"Repeated impact is idempotent and cannot dig an inescapable pit");
            Check(world.TryDragonImpact(p0,6,1.2f)&&!rock.gameObject.activeSelf,"Rocks break and their destruction is saved");
            var oldBossPosition=boss.transform.position;
            // The player must not obstruct the exit: boss movement now respects physical contact.
            Warp(arc,crater-Vector3.right*20);
            boss.transform.position=new Vector3(crater.x,world.SurfaceHeight(crater),crater.z);Physics.SyncTransforms();
            var move=typeof(SoulEaterPhaseOneController).GetMethod("MoveGround",BindingFlags.Instance|BindingFlags.NonPublic);
            bool crossed=true;for(int i=0;i<150;i++){Physics.SyncTransforms();if(!(bool)move.Invoke(boss,new object[]{Vector3.right*.05f})){crossed=false;break;}}
            Check(crossed&&boss.transform.position.x>crater.x+7.3f,"Physical movement climbs out across the fresh crater before navigation rebakes (travel="+(boss.transform.position.x-crater.x)+"m)");
            boss.transform.position=oldBossPosition;Physics.SyncTransforms();
            var outside=arc.Layout.Arena+Vector3.forward*(arc.Data.arenaRadius+18);
            if(world.CanDragonImpact(outside,6))Check(world.TryDragonImpact(outside,6,1.2f),"Pursuit impacts can persist outside the original arena");
            savedCount=WorldSession.Current.dragonImpacts.Count;
            Check(WorldSession.Continue()&&WorldSession.Current.dragonImpacts.Count==savedCount,"Continue reads real saved impacts from the isolated world file");
            yield return Until(()=>boss.State==SoulEaterState.Hunting);
            // Keep the human-sized target ahead of the enlarged mouth, outside its physical hurtbox.
            float breathForward=Vector3.ProjectOnPlane(boss.MouthPosition-boss.transform.position,Vector3.up).magnitude+boss.Settings.breathRange*.4f;
            Warp(arc,boss.transform.position+boss.transform.forward*breathForward);Check(boss.TryStartAttack(SoulEaterAction.Breath),"Tracking breath starts in phase two");
            yield return Until(()=>boss.State==SoulEaterState.Active&&boss.Progress>.1f);var initialDirection=boss.LockedDirection;
            var breathCenter=boss.transform.position+boss.transform.forward*breathForward;var breathRight=boss.transform.right;
            float trackingSpeed=boss.Settings.phaseTwoBreathTrackingSpeed,bodySpeed=boss.Settings.breathTurnSpeed;bool bossEnabled=boss.enabled;
            var flame=boss.GetComponent<SoulEaterEffects>().Flame;
            try
            {
                boss.enabled=true;boss.Settings.phaseTwoBreathTrackingSpeed=0;boss.Settings.breathTurnSpeed=180;
                Warp(arc,breathCenter-breathRight*6);Physics.SyncTransforms();
                var beforeAim=boss.LockedDirection;var beforeBody=boss.transform.rotation;boss.Tick(.2f);
                Check((beforeAim-boss.LockedDirection).sqrMagnitude<.000001f&&Quaternion.Angle(beforeBody,boss.transform.rotation)>1,
                    "Zero fire tracking freezes the damage direction even while the body turns");
                Check(flame.Emitting&&Vector3.Angle(flame.transform.forward,boss.LockedDirection)<.05f,
                    "Zero fire tracking also freezes the visible VFX direction");
                boss.Settings.breathTurnSpeed=0;boss.Settings.phaseTwoBreathTrackingSpeed=30;
                Warp(arc,breathCenter+breathRight*6);Physics.SyncTransforms();beforeAim=boss.LockedDirection;beforeBody=boss.transform.rotation;boss.Tick(.02f);
                float slowTurn=Vector3.Angle(beforeAim,boss.LockedDirection);
                Check(slowTurn>.1f&&slowTurn<=.65f,"Phase two tracking is bounded by the configured 30 degrees/second");
                Check(Quaternion.Angle(beforeBody,boss.transform.rotation)<.05f,
                    "Zero body speed holds the dragon still without overriding fire tracking");
                Check(Vector3.Angle(flame.transform.forward,boss.LockedDirection)<.05f,
                    "Slow fire uses the same VFX and damage direction");
                boss.Settings.phaseTwoBreathTrackingSpeed=180;beforeAim=boss.LockedDirection;boss.Tick(.02f);
                Check(Vector3.Angle(beforeAim,boss.LockedDirection)>slowTurn*3&&Vector3.Angle(beforeAim,boss.LockedDirection)<=3.65f,"Increasing tracking speed changes the actual flame direction, not only the body");
            }
            finally{boss.Settings.phaseTwoBreathTrackingSpeed=trackingSpeed;boss.Settings.breathTurnSpeed=bodySpeed;boss.enabled=bossEnabled;}
            Warp(arc,breathCenter-breathRight*6);
            yield return Until(()=>boss.State==SoulEaterState.Active&&boss.Progress>.4f);
            Check(Vector3.Angle(Vector3.ProjectOnPlane(boss.LockedDirection,Vector3.up),Vector3.ProjectOnPlane(arc.transform.position-boss.MouthPosition,Vector3.up))<1,"Phase two follows a leftward player movement while exhaling");
            var leftDirection=boss.LockedDirection;Warp(arc,breathCenter+breathRight*6);
            yield return Until(()=>boss.State==SoulEaterState.Active&&boss.Progress>.8f);
            Check(Vector3.Angle(leftDirection,boss.LockedDirection)>25&&Vector3.Angle(Vector3.ProjectOnPlane(boss.LockedDirection,Vector3.up),Vector3.ProjectOnPlane(arc.transform.position-boss.MouthPosition,Vector3.up))<1&&boss.Battlefield.FireCount>0&&boss.Battlefield.FireCount<=boss.Settings.maximumGroundFires,"Breath reverses with the player and leaves bounded ground flames (turn="+Vector3.Angle(leftDirection,boss.LockedDirection)+", aim error="+Vector3.Angle(Vector3.ProjectOnPlane(boss.LockedDirection,Vector3.up),Vector3.ProjectOnPlane(arc.transform.position-boss.MouthPosition,Vector3.up))+", fire="+boss.Battlefield.FireCount+")");capture("fase2-05-seguimiento");
            float oldDistance=boss.Settings.aerialPassDistance,oldSpeed=boss.Settings.aerialPassSpeed;
            boss.Settings.aerialPassDistance=28;boss.Settings.aerialPassSpeed=14;
            yield return Until(()=>boss.State==SoulEaterState.Hunting);Check(boss.TryStartAerial(SoulEaterAction.AerialBreath),"Aerial fire pass starts with its own readable warning");
            yield return Until(()=>boss.State==SoulEaterState.AerialBreath&&boss.Progress>.1f);
            var passPosition=boss.transform.position;float passProgress=boss.Progress;
            yield return Until(()=>boss.State==SoulEaterState.AerialBreath&&boss.Progress>.5f);
            Check(Mathf.Abs(Vector3.ProjectOnPlane(boss.transform.position-passPosition,Vector3.up).magnitude-28*(boss.Progress-passProgress))<.2f && Mathf.Approximately(boss.Settings.AerialPassDuration,2),"Flight uses configured 28m distance and 14m/s speed in the actual movement");
            Check(boss.GetComponent<SoulEaterEffects>().Flame.Emitting&&boss.Battlefield.FireCount>0,"Low pass emits the existing flame VFX and scorches the ground");capture("fase2-06-pasada");
            yield return Until(()=>boss.State==SoulEaterState.ImpactRecovery);Check(boss.Combat.Recovering,"Aerial pass ends on the ground with a punish window");
            Check(dust.LastKind==SoulEaterDustKind.Landing&&Vector3.Distance(dust.LastPosition,boss.transform.position)<.1f,"El camino de fuego termina con DustExplosion al tocar suelo");boss.Settings.aerialPassDistance=oldDistance;boss.Settings.aerialPassSpeed=oldSpeed;
            yield return Until(()=>boss.State==SoulEaterState.Hunting);boss.Battlefield.Tick(5);Check(boss.Battlefield.FireCount==0,"Temporary fire expires instead of permanently filling the arena");Check(boss.TryStartAerial(SoulEaterAction.Dive),"Subsequent aerial attacks remain available");
            yield return Until(()=>boss.State==SoulEaterState.AerialAim);
            Warp(arc,arc.Layout.Arena+Vector3.forward*(arc.Data.arenaRadius+70));yield return null;yield return null;
            Check(arc.Encounter.IsActive && boss!=null && boss.Target==arc.transform && boss.State!=SoulEaterState.Returning,"Regional boss stays alive and engaged when the player leaves the old leash radius");
            boss.Health.ApplyDamage(new DamageInfo(boss.Health.Current,arc.gameObject,boss.transform.position,Vector3.forward));
            Check(boss.State==SoulEaterState.Dead&&!boss.Battlefield.MarkerVisible&&boss.Battlefield.FireCount==0&&transitions==1,"Death in flight lands safely, clears hazards and does not repeat phase transitions");
            Check(WorldSession.Current.dragonImpacts.Count==savedCount,"Defeating the boss preserves destruction without creating another impact");
            if(orbit!=null)orbit.enabled=true;shield.Cancel();Directory.CreateDirectory("output/dragon-phase-two");File.WriteAllLines("output/dragon-phase-two/checks.txt",results);
        }
        public static void VerifyReload(ExplorationChunks world)
        {
            Check(WorldSession.Current.dragonImpacts.Count==savedCount&&world.SurfaceHeight(crater)<originalHeight-.6f,"Death and scene reload preserve the same physical crater");
            Check(!WorldDestructible.Loaded.Any(p=>p!=null&&WorldImpactRecord.Destroyed(p.transform.position)),"Destroyed props do not return after reloading the scene");
            File.WriteAllLines("output/dragon-phase-two/checks.txt",results);
        }
    }
}
