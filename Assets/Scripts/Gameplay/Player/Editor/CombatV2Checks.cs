using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class CombatV2Checks
    {
        const string Pending="Mismo.CombatV2Checks";
        static IEnumerator routine;static double deadline;static int frame=-1,count;
        public static void RunBatch(){EditorSceneManager.OpenScene(GoblinPrototypeTool.EliteScenePath);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Resume(){if(!SessionState.GetBool(Pending,false))return;deadline=EditorApplication.timeSinceStartup+140;EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
            if(!Application.isPlaying||Time.frameCount<10||frame==Time.frameCount)return;frame=Time.frameCount;
            try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,count+" checks");}catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool ok,string message){SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Debug.Log("COMBAT_V2_"+(ok?"OK ":"FAILED ")+message);EditorApplication.Exit(ok?0:1);}
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("COMBAT_V2_CHECK "+message);}
        static int Arrows()=>Object.FindObjectsByType<ProjectileInstance>(FindObjectsSortMode.None).Length;
        static IEnumerator Run()
        {
            var player=Object.FindFirstObjectByType<PlayerController>();player.enabled=false;
            var runner=player.GetComponent<AbilityRunner>();var loadout=player.GetComponent<EquipmentLoadout>();var motor=player.GetComponent<PlayerMotor>();
            var playerState=player.GetComponent<CombatState>();var receiver=player.GetComponent<DamageReceiver>();var defense=player.GetComponent<DefenseWindow>();
            string feedback=null;playerState.Rewarded+=value=>feedback=value;
            var enemy=Object.FindFirstObjectByType<GoblinController>();enemy.enabled=false;
            var dummy=GameObject.CreatePrimitive(PrimitiveType.Capsule);dummy.transform.position=new Vector3(50,1,50);
            var health=dummy.AddComponent<Health>();health.ConfigureMaximum(1000);health.Revive();var target=dummy.AddComponent<DamageReceiver>();var state=dummy.GetComponent<CombatState>();state.ConfigurePosture(100);
            var front=new DamageInfo(20,player.gameObject,dummy.transform.position,Vector3.back,AttackIdentity.Next());
            Check(Mathf.Abs(target.Resolve(front).HealthDamage-17)<.01f,"Front damage reduction");float hp=health.Current;
            Check(target.Resolve(front).Outcome==HitOutcome.Ignored&&health.Current==hp,"Same attack resolves once");
            Check(Mathf.Abs(target.Resolve(new DamageInfo(20,player.gameObject,dummy.transform.position,Vector3.forward,AttackIdentity.Next())).HealthDamage-24)<.01f,"Back damage bonus");
            Check(playerState.Focus==8,"Back hit generates Focus");
            Check(Mathf.Abs(target.Resolve(new DamageInfo(20,player.gameObject,dummy.transform.position,Vector3.down,AttackIdentity.Next(),10,true,true)).HealthDamage-20)<.01f,"Area damage has no arbitrary back or range bonus");
            float near=target.Resolve(new DamageInfo(8,player.gameObject,dummy.transform.position,Vector3.back,AttackIdentity.Next(),0,true,false,dummy.transform.position+Vector3.forward)).HealthDamage;
            float far=target.Resolve(new DamageInfo(8,player.gameObject,dummy.transform.position,Vector3.back,AttackIdentity.Next(),0,true,false,dummy.transform.position+Vector3.forward*18)).HealthDamage;
            float capped=target.Resolve(new DamageInfo(8,player.gameObject,dummy.transform.position,Vector3.back,AttackIdentity.Next(),0,true,false,dummy.transform.position+Vector3.forward*100)).HealthDamage;
            Check(far>near*1.25f&&Mathf.Abs(far-capped)<.001f,"Ranged damage penalizes close range and caps distant bonus");
            state.Recovering=true;float before=state.Posture;
            target.Resolve(new DamageInfo(1,player.gameObject,dummy.transform.position,Vector3.right,AttackIdentity.Next(),10));
            Check(Mathf.Abs(before-state.Posture-14)<.01f,"Recovery increases Posture damage");state.Recovering=false;
            playerState.ResetCombat();motor.Face(Vector3.forward);receiver.Invulnerability.Cancel();
            defense.OpenParry(.16f);
            var incoming=new DamageInfo(10,dummy,player.transform.position,Vector3.back,AttackIdentity.Next());
            Check(receiver.Resolve(incoming).Outcome==HitOutcome.PerfectParry,"Precise frontal parry");
            Check(feedback=="PARRY PERFECTO"&&Time.timeScale==0f,"Perfect parry confirms success and freezes time");
            float frozenAt=Time.unscaledTime, gameTime=Time.time, fixedStep=Time.fixedDeltaTime;
            while(Time.unscaledTime-frozenAt<.1f)yield return null;
            Check(Time.timeScale==0f&&Mathf.Abs(Time.time-gameTime)<.02f,"Perfect defense visibly stops gameplay for at least 100 ms");
            while(Time.unscaledTime-frozenAt<.25f)yield return null;
            Check(Mathf.Approximately(Time.timeScale,CombatTimeFeedback.SlowScale),"Freeze transitions to sustained slow motion");
            while(Time.unscaledTime-frozenAt<.9f)yield return null;
            Check(Mathf.Approximately(Time.timeScale,1f)&&Mathf.Approximately(Time.fixedDeltaTime,fixedStep),"Perfect defense restores game and physics time");
            Check(playerState.Focus==20,"Perfect parry rewards Focus");receiver.Resolve(incoming);Check(playerState.Focus==20,"No repeated parry reward");
            defense.OpenParry(.16f);defense.Tick(.11f);
            Check(receiver.Resolve(new DamageInfo(10,dummy,player.transform.position,Vector3.back,AttackIdentity.Next())).Outcome==HitOutcome.Parry,"Late valid parry is not perfect");
            Check(feedback=="PARRY","Normal parry has visible confirmation");
            defense.OpenDodge(.12f);float focus=playerState.Focus;
            Check(playerState.Focus==focus,"Dashing alone does not earn Focus");
            Check(receiver.Resolve(new DamageInfo(10,dummy,player.transform.position,Vector3.back,AttackIdentity.Next())).Outcome==HitOutcome.PerfectDodge,"Threat contact during precise dodge");
            Check(Time.timeScale==0f,"A subsequent perfect dodge also freezes time without the old cooldown");
            Check(playerState.Focus==focus+20,"Perfect dodge reward");
            receiver.Resolve(new DamageInfo(10,dummy,player.transform.position,Vector3.back,AttackIdentity.Next()));Check(playerState.Focus==focus+20,"One perfect reward per dodge");
            defense.CloseDodge();receiver.Invulnerability.StartWindow(.2f);
            Check(receiver.Resolve(new DamageInfo(10,dummy,player.transform.position,Vector3.back,AttackIdentity.Next())).Outcome==HitOutcome.Invulnerable,"Hit immunity is not perfect dodge");
            receiver.Invulnerability.Cancel();
            if(!loadout.ActiveDefinition.isBow)loadout.TrySwap();playerState.ResetCombat();
            Check(loadout.ActiveDefinition.isBow,"Bow equipped for charge tests");
            Check(loadout.ActiveDefinition.GetAbility(AbilitySlot.Q).focusCost==20,"Power Shot data requires 20 Focus");
            Check(!runner.TryUse(AbilitySlot.Q,Vector3.forward,Vector3.zero),"Power Shot requires earned Focus");
            Check(loadout.ActiveDefinition.GetAbility(AbilitySlot.Basic).preparation>=.4f&&loadout.ActiveDefinition.GetAbility(AbilitySlot.Basic).cooldown>=.85f,"Basic has perceptible tension and slower cadence");
            Check(((ProjectileAction)loadout.ActiveDefinition.GetAbility(AbilitySlot.Basic).actions[0]).damage==8,"Basic damage reduced to 8 before modifiers");
            Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero,null,true),"Held bow basic starts");int arrows=Arrows();
            runner.Tick(.1f);Check(Arrows()==arrows,"No instant ranged damage");runner.Tick(.45f);
            Check(Arrows()==arrows&&runner.Current.Charge>0&&runner.Mobility<1,"Held charge delays release and reduces mobility");
            runner.SetHeld(false);runner.Tick(.01f);Check(Arrows()==arrows+1,"Releasing creates one arrow");
            Check(!loadout.TrySwap(),"Cannot swap during ranged recovery");runner.Tick(.6f);
            Check(runner.TryUse(AbilitySlot.E,Vector3.forward,Vector3.zero),"Retreat starts preparation");arrows=Arrows();runner.Tick(.44f);
            Check(Arrows()==arrows&&!runner.IsMoving,"Retreat has no arrow or retreat movement before 0.45 seconds");
            receiver.Resolve(new DamageInfo(1,dummy,player.transform.position,Vector3.back,AttackIdentity.Next()));
            Check(!runner.IsBusy&&Arrows()==arrows,"A hit interrupts Retreat before release");
            Check(runner.Remaining(loadout.ActiveDefinition.GetAbility(AbilitySlot.E))>0,"Interruption does not refund cooldown");
            playerState.Reward(20,"TEST");Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,Vector3.zero),"Earned Focus enables Power Shot");
            Check(playerState.Focus==0,"Power Shot spends Focus once");runner.Tick(.7f);runner.Tick(.5f);
            Check(loadout.TrySwap(),"Swap unlocks after recovery");
            Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero),"Sword combo starts");runner.Tick(.2f);
            runner.TryUse(AbilitySlot.E,Vector3.forward,Vector3.zero);runner.Tick(.09f);
            Check(runner.Current!=null&&runner.Current.Definition==loadout.ActiveDefinition.GetAbility(AbilitySlot.E),"Buffered parry branches from sword combo");runner.Cancel();
            enemy.enabled=true;var enemyState=enemy.GetComponent<CombatState>();enemyState.DamagePosture(10000);
            Check(enemyState.Broken&&enemy.State==GoblinState.Stagger,"Posture Break drives Elite vulnerability");
            Check(enemyState.DamagePosture(50)==0,"Break cannot be extended by repeated Posture hits");
            enemyState.Tick(2.1f);Check(!enemyState.Broken&&enemyState.PostureNormalized==1,"Posture resets after break");
            enemy.enabled=false;enemy.enabled=true;
            motor.ResetPosition(enemy.transform.position+Vector3.forward*4);Physics.SyncTransforms();enemy.SetTarget(player.transform);
            if(!loadout.ActiveDefinition.isBow)loadout.TrySwap();
            enemy.enabled=false;
            float ready=Time.time+Mathf.Max(.5f,runner.Remaining(loadout.ActiveDefinition.GetAbility(AbilitySlot.Basic))+.01f);while(Time.time<ready)yield return null;
            enemy.enabled=true;enemy.SetTarget(player.transform);
            Check(runner.TryUse(AbilitySlot.Basic,Vector3.back,Vector3.zero,null,true),"Ranged preparation actually starts before AI response");runner.Tick(.2f);enemy.Tick(.8f);
            Check(enemy.State==GoblinState.Telegraph&&enemy.CurrentAttack==enemy.Settings.charge,"Elite answers visible ranged preparation with a telegraphed charge");
            runner.Cancel();
            enemy.enabled=false;
            float chargeReady=Time.time+Mathf.Max(.6f,runner.Remaining(loadout.ActiveDefinition.GetAbility(AbilitySlot.Basic))+.01f);while(Time.time<chargeReady)yield return null;
            Check(runner.TryUse(AbilitySlot.Basic,Vector3.back,Vector3.zero,null,true),"Another charged basic starts after cooldown");
            int beforeFull=Arrows();runner.Tick(1.25f);
            Check(Arrows()==beforeFull+1&&runner.Current!=null&&runner.Current.Charge==1,"Maximum charge releases automatically once");runner.Tick(.4f);
            Check(loadout.TrySwap(),"Return to sword after full charge");
            Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero),"Sword starts for dash transition");runner.Tick(.29f);
            motor.Tick(Vector3.zero,false,false,false,.03f);
            Check(runner.CanCancel&&runner.TryDash(Vector3.right)&&loadout.Belt.IsActive&&!runner.IsBusy,"Dash cancels sword only in branch window");loadout.Belt.Cancel();
            playerState.Reward(7,"TEST");float keptFocus=playerState.Focus;
            player.GetComponent<Health>().Heal(100);
            Check(playerState.Focus==keptFocus,"Healing to full does not erase earned Focus");
            float finish=Time.unscaledTime+.2f;while(Time.unscaledTime<finish)yield return null;
            Check(Mathf.Approximately(Time.timeScale,1),"Perfect dodge slow motion restores time");
        }
    }
}
