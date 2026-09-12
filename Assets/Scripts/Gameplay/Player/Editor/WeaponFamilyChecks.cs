using System;
using System.Collections;
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
    public static class WeaponFamilyChecks
    {
        private const string Pending="Mismo.WeaponFamilyChecks";
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
            if(!Application.isPlaying || Time.frameCount<10 || lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;
            try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,count+" checks");}
            catch(Exception error){Finish(false,error.ToString());}
        }
        private static void Finish(bool ok,string message)
        {
            (routine as IDisposable)?.Dispose();SessionState.SetBool(Pending,false);EditorApplication.update-=Step;
            Debug.Log("WEAPON_FAMILY_"+(ok?"PASS ":"FAIL ")+message);EditorApplication.Exit(ok?0:1);
        }
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("FAMILY_CHECK "+message);}
        private static IEnumerator Run()
        {
            var player=Object.FindAnyObjectByType<PlayerController>();Check(player!=null,"Player fixture exists");player.enabled=false;
            foreach(var goblin in Object.FindObjectsByType<Enemies.GoblinController>())goblin.enabled=false;
            var equipment=player.GetComponent<EquipmentLoadout>();equipment.Runner.Cancel();equipment.Belt?.Cancel();player.GetComponent<Health>().Revive();
            float until=Time.time+7;while(equipment.InCombat && Time.time<until)yield return null;
            var catalog=Resources.Load<ItemCatalog>("ItemCatalog");
            var sword=Array.Find(catalog.weapons,w=>w.Id=="sword.basic");var bow=Array.Find(catalog.weapons,w=>w.Id=="bow.basic");
            Check(sword.family!=null && catalog.bossReward.family==sword.family,"Sword and boss reward share one family");
            Check(bow.family!=null && bow.family!=sword.family,"Bow has an independent family");
            Check(sword.GetAbility(AbilitySlot.Q)==catalog.bossReward.GetAbility(AbilitySlot.Q),"Shared family resolves the same ability");
            var family=ScriptableObject.CreateInstance<WeaponFamilyDefinition>();
            var set=ScriptableObject.CreateInstance<WeaponAnimationSet>();
            var definition=ScriptableObject.CreateInstance<WeaponDefinition>();
            var ability=ScriptableObject.CreateInstance<AbilityDefinition>();
            var clip=new AnimationClip {name="Arbitrary action outside legacy enum"};
            var driver=player.GetComponent<PlayerAnimationDriver>();
            var probe=new GameObject("AnimationProbe");probe.transform.SetParent(driver.Animator.transform,false);
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("AnimationProbe",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Linear(0,2,1,3));
            ability.preparation=.2f;ability.active=.3f;ability.recovery=.4f;ability.cooldown=0;ability.pose=AbilityPose.None;
            family.abilities=new[]{ability};family.animations=set;
            var binding=new AbilityAnimationBinding{ability=ability,clip=clip,activeStartsAt=.25f,recoveryStartsAt=.75f,blendSeconds=0};
            set.actions=new[]{binding};definition.family=family;definition.visualPrefab=sword.visualPrefab;
            var old0=equipment.GetSlot(0);
            try
            {
                Check(equipment.TryEquip(0,definition),"Equip a weapon with an arbitrary action");
                if(equipment.ActiveSlot!=0)Check(equipment.TrySwap(),"Select test weapon");
                var runner=equipment.Runner;
                Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero),"Unmapped legacy action starts normally");
                runner.Tick(.1f);
                Check(runner.TryGetAnimationFrame(out var frame) && frame.Phase==CombatAnimationPhase.Preparation && Mathf.Abs(frame.Progress-.5f)<.001f,"Preparation comes from gameplay clock");
                Check(binding.TrySample(frame,out var selected,out var time) && selected==clip && Mathf.Abs(time-.125f)<.001f,"Phase maps to configured clip time");
                yield return null;yield return null;
                Check(driver!=null && driver.ActionClip==clip,"Character plays a family clip without a new Animator state");
                Check(Mathf.Abs(probe.transform.localPosition.x-2.125f)<.03f,"Playable evaluates the configured clip on the character hierarchy");
                Check(Mathf.Abs(runner.Current.Elapsed-.1f)<.001f,"Animation does not advance gameplay");
                runner.Tick(.2f);
                Check(runner.TryGetAnimationFrame(out frame) && frame.Phase==CombatAnimationPhase.Active,"Active phase is exposed");
                runner.Tick(.3f);
                Check(runner.TryGetAnimationFrame(out frame) && frame.Phase==CombatAnimationPhase.Recovery,"Recovery phase is exposed");
                runner.Cancel();yield return null;yield return null;
                Check(driver.ActionClip==null,"Cancellation clears the action clip");
                ability.chargeable=true;ability.maximumCharge=1;
                Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero,null,true),"Charged action starts");runner.Tick(.5f);
                Check(runner.TryGetAnimationFrame(out frame) && frame.Phase==CombatAnimationPhase.Preparation && frame.Progress==1,"Holding charge keeps preparation at its final pose");
                runner.SetHeld(false);runner.Tick(.01f);
                Check(runner.TryGetAnimationFrame(out frame) && frame.Phase==CombatAnimationPhase.Active && frame.Progress<.001f,"Release starts active phase from the actual release time");
                runner.Cancel();
                binding.comboClips=new[]{clip,clip,clip,clip,clip};
                Check(binding.TrySample(new CombatAnimationFrame(ability,CombatAnimationPhase.Combo,.4f,4,1),out selected,out time) && selected==clip,"Animation mapping supports a fifth combo step");
                Check(!binding.TrySample(new CombatAnimationFrame(ability,CombatAnimationPhase.Combo,.4f,9,1),out selected,out time),"Missing combo clip is not silently clamped to the third attack");
                driver.enabled=false;driver.enabled=true;yield return null;yield return null;
                Check(driver.Animator!=null,"Playback survives component disable and re-enable");
                until=Time.time+7;while(equipment.InCombat && Time.time<until)yield return null;
                Check(equipment.TryEquip(0,old0),"Return to the real sword");yield return null;yield return null;
            }
            finally
            {
                equipment.Runner.Cancel();
                Object.Destroy(definition);Object.Destroy(family);Object.Destroy(set);Object.Destroy(ability);Object.Destroy(clip);
                Object.Destroy(probe);
            }
        }
    }
}
