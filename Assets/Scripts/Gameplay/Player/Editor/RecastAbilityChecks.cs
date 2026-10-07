using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Play Mode rules of recast abilities (one stage per press), with throwaway assets on the arena player.
    public static class RecastAbilityChecks
    {
        private const string Pending="Mismo.RecastAbilityChecks";
        private static IEnumerator routine;
        private static double deadline;
        private static int lastFrame=-1,count;
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/GoblinEliteArena.unity");
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Isolate()
        {
            if(!SessionState.GetBool(Pending,false))return;
            foreach(var respawn in Object.FindObjectsByType<World.RegionRespawn>())Object.DestroyImmediate(respawn);
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if(!SessionState.GetBool(Pending,false))return;
            deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Step;
        }
        private static void Step()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
            // With the console's Error Pause on, an unrelated scene error (e.g. a missing prefab) would freeze the run.
            if(Application.isPlaying&&EditorApplication.isPaused)EditorApplication.isPaused=false;
            if(!Application.isPlaying || Time.frameCount<10 || lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;
            try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,count+" checks");}
            catch(Exception error){Finish(false,error.ToString());}
        }
        private static void Finish(bool ok,string message)
        {
            (routine as IDisposable)?.Dispose();SessionState.SetBool(Pending,false);EditorApplication.update-=Step;
            Debug.Log("RECAST_ABILITY_"+(ok?"PASS ":"FAIL ")+message);EditorApplication.Exit(ok?0:1);
        }
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("RECAST_CHECK "+message);}
        private static void ClearCooldowns(AbilityRunner runner)=>
            ((Dictionary<AbilityDefinition,float>)typeof(AbilityRunner).GetField("readyAt",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(runner)).Clear();
        private static AbilityRecastStage Stage()=>new AbilityRecastStage{preparation=.1f,active=.2f,recovery=.1f};
        private static IEnumerator Run()
        {
            var player=Object.FindAnyObjectByType<PlayerController>();Check(player!=null,"Player fixture exists");player.enabled=false;
            foreach(var goblin in Object.FindObjectsByType<Enemies.EnemyController>())goblin.enabled=false;
            var equipment=player.GetComponent<EquipmentLoadout>();equipment.Runner.Cancel();equipment.Belt?.Cancel();player.GetComponent<Health>().Revive();
            float until=Time.time+7;while(equipment.InCombat && Time.time<until)yield return null;
            var runner=equipment.Runner;var combat=player.GetComponent<CombatState>();var stamina=player.GetComponent<Movement.Stamina>();
            var catalog=Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog");
            var sword=Array.Find(catalog.weapons,w=>w.Id=="sword.basic");
            var family=ScriptableObject.CreateInstance<WeaponFamilyDefinition>();
            var set=ScriptableObject.CreateInstance<WeaponAnimationSet>();
            var weapon=ScriptableObject.CreateInstance<WeaponDefinition>();
            var single=ScriptableObject.CreateInstance<AbilityDefinition>();
            var recast=ScriptableObject.CreateInstance<AbilityDefinition>();
            var clips=new[]{new AnimationClip{name="Recast stage 1"},new AnimationClip{name="Recast stage 2"},new AnimationClip{name="Recast stage 3"}};
            var target=GameObject.CreatePrimitive(PrimitiveType.Capsule);target.name="Recast target";
            single.abilityId="recast.single";single.preparation=.2f;single.active=.3f;single.recovery=.4f;single.cooldown=0;single.pose=AbilityPose.None;
            recast.abilityId="recast.test";recast.cooldown=5;recast.focusCost=10;recast.staminaCost=5;recast.pose=AbilityPose.None;
            recast.recastStages=new[]{Stage(),Stage(),Stage()};recast.recastWindow=1;
            recast.actions=new AbilityAction[]{new RecastStrikeAction{damage=1,postureDamage=0,radius=1,forward=1,
                strikes=new[]{new RecastStrike{at=.5f,multiplier=1},new RecastStrike{at=.5f,multiplier=1},new RecastStrike{at=.5f,multiplier=1,breakPostureSeconds=2}}}};
            var binding=new AbilityAnimationBinding{ability=recast,comboClips=clips,blendSeconds=0};
            set.actions=new[]{binding};family.animations=set;family.abilities=new[]{single,recast};
            weapon.family=family;weapon.overrideFamilyAbilities=true;weapon.abilities=new[]{single,recast,null,null};weapon.visualPrefab=sword.visualPrefab;
            var old0=equipment.GetSlot(0);
            try
            {
                Check(equipment.TryEquip(0,weapon),"Equip a weapon with a recast ability");
                if(equipment.ActiveSlot!=0)Check(equipment.TrySwap(),"Select the test weapon");
                var health=target.AddComponent<Health>();target.AddComponent<DamageReceiver>();
                var posture=target.GetComponent<CombatState>();posture.ConfigurePosture(50);
                // Facing the player: a back hit would reward the attacker with Focus and hide a second payment.
                target.transform.SetPositionAndRotation(player.transform.position+Vector3.forward*1.6f+Vector3.up,Quaternion.LookRotation(Vector3.back));Physics.SyncTransforms();
                Vector3 point=player.transform.position;

                combat.Reward(100,"TEST");float focus=combat.Focus,energy=stamina!=null?stamina.Current:0;
                Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,point),"First press starts the chain");
                var first=runner.Current;
                Check(first!=null&&first.Definition==recast&&first.RecastStage==0&&first.UseId==first.AttackId,"First press runs stage 1 and opens a use");
                Check(Mathf.Abs(first.Preparation-.1f)<.001f&&Mathf.Abs(first.Duration-.4f)<.001f,"Stage timings replace the ability timings");
                Check(Mathf.Abs(focus-combat.Focus-10)<.001f&&(stamina==null||stamina.Current<energy),"First press pays Focus and stamina");
                Check(runner.Remaining(recast)==0&&runner.NextRecastStage(recast)==1,"Cooldown waits while the chain is open");
                Check(runner.TryGetAnimationFrame(out var frame)&&frame.Phase==CombatAnimationPhase.Combo&&frame.ComboIndex==0,"Stage 1 shows as combo step 0");
                Check(binding.TrySample(frame,out var selected,out _)&&selected==clips[0],"Stage 1 plays its own combo clip");

                focus=combat.Focus;energy=stamina!=null?stamina.Current:0;float life=health.Current;
                Check(!runner.TryUse(AbilitySlot.Q,Vector3.forward,point),"A press during stage 1 waits for it");
                runner.Tick(.45f);
                var second=runner.Current;
                Check(health.Current<life&&!posture.Broken,"Stage 1 strikes without breaking posture");
                Check(second!=null&&second.RecastStage==1&&second.UseId==first.UseId&&second.AttackId!=first.AttackId,"The buffered press starts stage 2 in the same tick and keeps the use");
                Check(combat.Focus>=focus-.001f&&(stamina==null||stamina.Current>=energy-.001f),"Later presses are free");
                Check(runner.TryGetAnimationFrame(out frame)&&frame.ComboIndex==1&&binding.TrySample(frame,out selected,out _)&&selected==clips[1],"Stage 2 plays the second combo clip");

                life=health.Current;runner.Tick(.45f);
                Check(runner.Current==null&&runner.NextRecastStage(recast)==2&&runner.RecastWindow(recast)>0&&runner.RecastWindow(recast)<1,"Stage 2 ends and its window starts");
                Check(health.Current<life&&!posture.Broken,"Stage 2 strikes without breaking posture");
                runner.Tick(.3f);
                Check(runner.NextRecastStage(recast)==2&&runner.Remaining(recast)==0,"The window keeps the chain open");
                life=health.Current;
                Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,point)&&runner.Current.RecastStage==2,"Third press runs stage 3");
                Check(runner.NextRecastStage(recast)==0&&runner.CooldownDuration(recast)>0&&Mathf.Abs(runner.Remaining(recast)-runner.CooldownDuration(recast))<.01f,"The third press closes the chain and starts the cooldown");
                runner.Tick(.45f);
                Check(health.Current<life&&posture.Broken&&posture.Posture==0,"The third strike breaks posture and empties its bar");

                ClearCooldowns(runner);combat.Reward(100,"TEST");
                Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,point)&&runner.Current.RecastStage==0,"A new chain starts after the cooldown");
                runner.Tick(.45f);runner.Tick(.4f);
                Check(runner.NextRecastStage(recast)==1,"The chain survives part of its window");
                runner.Tick(.7f);
                Check(runner.NextRecastStage(recast)==0&&runner.Remaining(recast)>0,"An expired window resets the chain and starts the cooldown");

                if(equipment.SecondaryDefinition!=null)
                {
                    ClearCooldowns(runner);combat.Reward(100,"TEST");
                    Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,point),"Another chain starts");runner.Tick(.45f);
                    Check(runner.NextRecastStage(recast)==1,"The chain is open between presses");
                    Check(equipment.TrySwap()&&runner.NextRecastStage(recast)==0&&runner.Remaining(recast)>0,"Changing weapons closes the chain and starts the cooldown");
                    Check(equipment.TrySwap(),"Back to the test weapon");
                }

                ClearCooldowns(runner);
                Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,point),"A single-press ability still starts");
                var cast=runner.Current;
                Check(cast.RecastStage==-1&&cast.UseId==cast.AttackId&&Mathf.Approximately(cast.Preparation,.2f)&&Mathf.Approximately(cast.Active,.3f)&&Mathf.Approximately(cast.Recovery,.4f),"Single-press abilities keep their own timings and use id");
                Check(runner.TryGetAnimationFrame(out frame)&&frame.Phase==CombatAnimationPhase.Preparation&&frame.ComboIndex==-1,"Single-press abilities keep phase frames");
                runner.Cancel();
                Check(runner.NextRecastStage(single)==0,"Single-press abilities never open a chain");

                until=Time.time+7;while(equipment.InCombat && Time.time<until)yield return null;
                Check(equipment.TryEquip(0,old0),"Return to the original weapon");yield return null;
            }
            finally
            {
                equipment.Runner.Cancel();
                Object.Destroy(target);Object.Destroy(weapon);Object.Destroy(family);Object.Destroy(set);Object.Destroy(single);Object.Destroy(recast);
                foreach(var clip in clips)Object.Destroy(clip);
            }
        }
    }
}
