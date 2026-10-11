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
    // Play Mode rules of the Dos hachas evolutions, on the real assets. The arena player gets an in-memory
    // inventory (never the user's save) with two axes equipped, and abilities mastered through the public training API.
    // Basic combos land their hits in AttackHitbox.LateUpdate, so everything that swings advances frame by frame.
    public static class DualAxeEvolutionChecks
    {
        private const string Pending="Mismo.DualAxeEvolutionChecks";
        private static IEnumerator routine;
        private static double deadline;
        private static int lastFrame=-1,count;
        sealed class Memory:IProfileRepository
        {
            public string data;
            public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=data;return data==null?ProfileReadResult.Missing:validate(data)?ProfileReadResult.Loaded:ProfileReadResult.Invalid;}
            public void Write(string value)=>data=value;
        }
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
            deadline=EditorApplication.timeSinceStartup+420;EditorApplication.update+=Step;
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
            Debug.Log("DUAL_EVOLUTION_"+(ok?"PASS ":"FAIL ")+message);EditorApplication.Exit(ok?0:1);
        }
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("DUAL_EVOLUTION_CHECK "+message);}
        private static void Near(float actual,float expected,string message,float tolerance=.02f)=>Check(Mathf.Abs(actual-expected)<tolerance,message+" (expected "+expected+", got "+actual+")");
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void ClearCooldowns(AbilityRunner runner)=>
            ((Dictionary<AbilityDefinition,float>)typeof(AbilityRunner).GetField("readyAt",Private).GetValue(runner)).Clear();

        private static IEnumerator Run()
        {
            var player=Object.FindAnyObjectByType<PlayerController>();Check(player!=null,"Player fixture exists");player.enabled=false;
            // The arena's enemies would stand in the way of swings and thrown axes: switch them off entirely.
            foreach(var goblin in Object.FindObjectsByType<Enemies.EnemyController>())goblin.gameObject.SetActive(false);
            Physics.SyncTransforms();
            var equipment=player.GetComponent<EquipmentLoadout>();var runner=equipment.Runner;
            var playerLife=player.GetComponent<Health>();playerLife.Revive();
            var combat=player.GetComponent<CombatState>();var stamina=player.GetComponent<Movement.Stamina>();
            var effects=player.GetComponent<WeaponSkillEffects>();
            var catalog=Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog");
            var targets=new List<GameObject>();

            void Idle()
            {
                runner.Cancel();equipment.Belt?.Cancel();
                typeof(EquipmentLoadout).GetField("combatUntil",Private).SetValue(equipment,0f);
            }
            void Rest(){ClearCooldowns(runner);stamina.Restore(stamina.Maximum);combat.Reward(100,"TEST");}
            Vector3 front=player.transform.position+Vector3.forward*1.6f+Vector3.up;
            // Old dummies wait far away: a swing hits everything in reach, and overlapping dummies would share its effects.
            void Park(GameObject keep)
            {
                foreach(var go in targets)if(go!=null&&go!=keep)go.transform.position=front+Vector3.back*40;
                Physics.SyncTransforms();
            }
            GameObject NewTarget(out Health life,Vector3? at=null,float maximum=100000)
            {
                Park(null);
                var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.name="Axe target";
                life=go.AddComponent<Health>();life.ConfigureMaximum(maximum);life.Revive();
                go.AddComponent<DamageReceiver>();
                // Facing the player: a back hit would reward the attacker with Focus.
                go.transform.SetPositionAndRotation(at??front,Quaternion.LookRotation(Vector3.back));Physics.SyncTransforms();
                targets.Add(go);return go;
            }
            // Runs an ability from press to the end of its execution, one real frame at a time; leaves the health lost in `lost`.
            float lost=0;
            IEnumerable Swing(AbilitySlot slot,Health subject)
            {
                Rest();float lifeAtStart=subject.Current;
                if(!runner.TryUse(slot,Vector3.forward,player.transform.position))throw new Exception("Cannot use "+slot);
                for(float limit=Time.time+10;runner.Current!=null&&Time.time<limit;){runner.Tick(Time.deltaTime);yield return null;}
                Check(runner.Current==null,"The "+slot+" ability finishes");
                lost=lifeAtStart-subject.Current;
            }
            IEnumerable Wait(float seconds){for(float until=Time.time+seconds;Time.time<until;)yield return null;}

            var inventory=player.gameObject.AddComponent<PlayerInventory>();
            try
            {
                inventory.Initialize(catalog,new Memory());
                Check(inventory.IsReady&&!inventory.HasSaveProblem,"An in-memory inventory starts: "+inventory.Notice);
                Idle();Check(inventory.TryGrantCheatWeapons()&&inventory.TryMaxCheatWeaponMasteries(),"The profile owns every weapon at maximum mastery");
                var axe=Array.Find(catalog.weapons,w=>w!=null&&w.Id=="axe.basic");
                Idle();Check(inventory.TryEquipDefinition(0,axe),"The main axe equips");
                if(equipment.ActiveSlot!=0){Idle();Check(inventory.TrySwap(),"The axe becomes the active weapon");}
                // The second axe goes to the off hand: the composed weapon then belongs to the Dos hachas family.
                var profile=(InventoryProfile)typeof(PlayerInventory).GetField("profile",Private).GetValue(inventory);
                string main=inventory.EquippedId(0);
                var spare=profile.weapons.Find(w=>w.definitionId=="axe.basic"&&w.instanceId!=main&&!w.inChest);
                Check(spare!=null,"A second axe is available");
                Idle();Check(inventory.TryEquipOffhand(0,spare.instanceId),"The second axe equips in the off hand");
                var weapon=equipment.ActiveDefinition;var family=weapon.family;
                Check(family!=null&&family.progressionId=="axe.dual"&&weapon.dualWield,"The active weapon is Dos hachas");
                var basic=family.GetAbility(AbilitySlot.Basic);
                for(int i=0;i<family.SkillCount;i++){Idle();Check(inventory.TryUnlockAbility(weapon,i),"Unlock "+family.Skill(i).Id);}
                AbilityDefinition Skill(string id)=>family.Skill(family.FindSkill(id));
                int Index(AbilityDefinition ability)=>ability==basic?-1:family.FindSkill(ability.Id);
                void Slot(AbilitySlot slot,string id)
                {
                    Idle();if(equipment.GetAbility(slot)?.Id==id)return;
                    Check(inventory.TrySelectAbility(weapon,slot,family.FindSkill(id)),"Slot "+slot+" holds "+id);
                }
                void Train(AbilityDefinition ability,int uses)
                {
                    for(int i=0;i<uses;i++)inventory.RecordMonsterSkillUse(weapon.MasteryId,ability.Id,AttackIdentity.Next());
                    Check(inventory.FlushCombatProgress(),"Training is committed");
                }
                // 25 effective uses master the ability; the chosen modifier then needs 10 more uses per rank.
                void Evolve(AbilityDefinition ability,string modifier,int rank)
                {
                    Idle();var progress=inventory.Mastery(weapon).SkillProgress(ability.Id);
                    if(progress==null||!progress.IsMastered(ability.masteryUsesRequired))Train(ability,ability.masteryUsesRequired);
                    Idle();progress=inventory.Mastery(weapon).SkillProgress(ability.Id);
                    if(progress.selectedModifierId!=modifier)Check(inventory.TrySelectModifier(weapon,Index(ability),modifier),ability.Id+" selects "+modifier);
                    var trained=inventory.Mastery(weapon).SkillProgress(ability.Id).Modifier(modifier);
                    int have=trained.level*10+trained.effectiveUses;if(rank*10>have)Train(ability,rank*10-have);
                    Check(inventory.Mastery(weapon).SkillProgress(ability.Id).Modifier(modifier).level==rank,ability.Id+" "+modifier+" is rank "+rank);
                }
                void ClearModifier(AbilityDefinition ability){Idle();inventory.TryClearModifier(weapon,Index(ability));}
                // Every ability's damage includes the Dos hachas style bonus through DamageMultiplier.
                float Multiplier(AbilityDefinition ability)=>inventory.DamageMultiplier(weapon)*inventory.AbilityDamageMultiplier(weapon,ability);

                // Doble filo: the chance of a double strike comes from its authored value; Corte cruzado hits harder with the second axe.
                var doubleEdge=Skill("DualAxeDoubleEdge");Slot(AbilitySlot.R,doubleEdge.Id);
                float Frequency(){int doubles=0;for(int i=0;i<4000;i++)if(effects.RollDoubleEdge())doubles++;return doubles/4000f;}
                UnityEngine.Random.InitState(2024);
                Near(Frequency(),.5f,"Doble filo starts at 50 %",.04f);
                Near(effects.SecondStrikeMultiplier(),1,"Without Corte cruzado the second strike is plain");
                Evolve(doubleEdge,"cross_cut",0);Near(effects.SecondStrikeMultiplier(),1.2f,"Corte cruzado rank 0 adds 20 % to the second strike");
                Near(Frequency(),.5f,"Corte cruzado leaves the 50 % chance alone",.04f);
                Evolve(doubleEdge,"cross_cut",3);Near(effects.SecondStrikeMultiplier(),1.2333f,"Corte cruzado rank 3 adds 23.3 %",.001f);
                ClearModifier(doubleEdge);

                // Sound: a double strike is two basics, the lead axe's first and the other one's 0.41 s later in step time.
                {
                    var dualCombo=player.GetComponentInChildren<BasicSwordCombo>(true);var basicSounds=equipment.GetAbility(AbilitySlot.Basic).comboStepSfx;
                    var heard=new List<(AudioClip clip,float at)>();float clock=0;
                    void Hear(AudioClip clip,float volume)=>heard.Add((clip,clock));
                    AudioEvents.OnPlayAbilitySFX+=Hear;
                    try
                    {
                        int doubled=-1;
                        for(int attempt=0;attempt<40&&doubled<0;attempt++)
                        {
                            Idle();Rest();heard.Clear();clock=0;
                            runner.TryUse(AbilitySlot.Basic,Vector3.forward,player.transform.position);
                            if(runner.Current!=null&&dualCombo!=null&&dualCombo.CurrentStepIndex>=2)doubled=dualCombo.CurrentStepIndex;
                        }
                        Check(doubled>=2,"A basic rolls a double strike with Doble filo");
                        if(doubled>=2)
                        {
                            Check(runner.FollowUpPending,"The double strike waits for its second axe");
                            for(int i=0;i<30&&runner.FollowUpPending;i++){clock+=.05f;runner.Tick(.05f/Mathf.Max(.01f,runner.Current?.AttackSpeed??1));}
                            var step=basicSounds[doubled];
                            Check(heard.Count==2&&heard[0].clip==step.clip&&heard[1].clip==step.followUpClip&&step.clip!=step.followUpClip,"The double strike sounds both basics, the lead axe first");
                            if(heard.Count==2)Near(heard[1].at-heard[0].at,step.followUpDelay,"The second axe sounds when it cuts",.06f);
                        }
                        Idle();Check(!runner.FollowUpPending,"Cancelling the swing drops its second sound");
                    }
                    finally{AudioEvents.OnPlayAbilitySFX-=Hear;}
                }

                // Sed de sangre: defeating or opening an enemy restores stamina and Focus; Sed insaciable also heals a wounded player.
                var thirst=Skill("DualAxeBloodthirst");Slot(AbilitySlot.E,thirst.Id);
                void Drain(){stamina.TrySpend(stamina.Current);combat.Spend(combat.Focus);}
                float Healed(float lifeShare)
                {
                    playerLife.Revive();playerLife.ApplyDamage(new DamageInfo(playerLife.Maximum*(1-lifeShare),null,player.transform.position,Vector3.forward));
                    float before=playerLife.Current;Drain();effects.TargetDefeatedOrOpened();return (playerLife.Current-before)/playerLife.Maximum;
                }
                Drain();effects.TargetDefeatedOrOpened();
                Near(stamina.Current/stamina.Maximum,.2f,"Sed de sangre restores 20 % of the stamina");Near(combat.Focus/CombatState.MaximumFocus,.2f,"Sed de sangre restores 20 % of the Focus");
                Near(Healed(.3f),0,"Without Sed insaciable nothing heals");
                Evolve(thirst,"insatiable",0);
                Near(Healed(.4f),.05f,"Sed insaciable rank 0 heals 5 % below half the life",.002f);
                Near(stamina.Current/stamina.Maximum,.2f,"Sed insaciable keeps the 20 % of stamina");
                Near(Healed(.8f),0,"Sed insaciable does not heal above half the life");
                Evolve(thirst,"insatiable",3);
                Near(Healed(.4f),.0583f,"Sed insaciable rank 3 heals 5.8 %",.002f);
                playerLife.Revive();
                ClearModifier(thirst);
                // The hit reactions from the healing test would replace the swings below: let them end.
                foreach(var _ in Wait(1.4f))yield return null;
                // No passive stays equipped: Doble filo would roll doubles into the plain basics measured below.
                Slot(AbilitySlot.R,"DualAxeThrow");Slot(AbilitySlot.E,"DualAxeBerserk");

                // Remate: the landing of Hachazo doble is followed at once by a free basic; each rank only adds damage to it.
                var leap=Skill("DualAxeLeap");Slot(AbilitySlot.Q,leap.Id);
                float unit=14*Multiplier(basic);
                var combo=player.GetComponentInChildren<BasicSwordCombo>(true);
                bool chained=false;int chainStep=-1;float staminaAfterPress=0,staminaAtChain=0;
                IEnumerable Landing(Health subject)
                {
                    Rest();float lifeAtStart=subject.Current;
                    Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,player.transform.position),"Hachazo doble starts");
                    staminaAfterPress=stamina.Current;chained=false;chainStep=-1;staminaAtChain=staminaAfterPress;
                    for(float limit=Time.time+10;runner.Current!=null&&Time.time<limit;)
                    {
                        runner.Tick(Time.deltaTime);
                        if(!chained&&runner.Current!=null&&runner.Current.Definition==basic){chained=true;chainStep=combo.CurrentStepIndex;staminaAtChain=stamina.Current;}
                        yield return null;
                    }
                    Check(runner.Current==null,"The landing and its follow-up finish");
                    lost=lifeAtStart-subject.Current;
                }
                var victim=NewTarget(out var victimLife);
                foreach(var _ in Landing(victimLife))yield return null;
                Near(lost,28*Multiplier(leap),"Hachazo doble lands for 200 % of 14");
                Check(!chained,"Without Remate no basic follows the landing");
                Evolve(leap,"landing_strike",0);
                foreach(var _ in Landing(victimLife))yield return null;
                Check(chained&&chainStep==0,"Remate rank 0 chains a basic from the first combo step");
                Near(lost,28*Multiplier(leap)+unit,"Remate rank 0 adds one basic at 100 %",.05f);
                Near(staminaAtChain,staminaAfterPress,"The chained basic costs no stamina");
                Check(runner.Remaining(leap)>0,"The Hachazo doble cooldown keeps running after the chain");
                Evolve(leap,"landing_strike",3);
                foreach(var _ in Landing(victimLife))yield return null;
                Check(chained&&chainStep==0,"Remate rank 3 chains the same single basic");
                Near(lost,28*Multiplier(leap)+unit*1.1667f,"Remate rank 3 makes that basic hit 16.7 % harder",.05f);
                // The boost belongs to the chained basic only: the next plain basic is back to normal.
                foreach(var _ in Swing(AbilitySlot.Basic,victimLife))yield return null;
                Near(lost,unit,"The basic after the chain deals its normal damage",.05f);
                ClearModifier(leap);

                // Ráfaga: every fifth basic in a row unleashes extra strikes; each rank only adds damage to them.
                IEnumerable FlurryRun(float amount)
                {
                    var dummy=NewTarget(out var dummyLife);
                    for(int i=0;i<4;i++)foreach(var _ in Swing(AbilitySlot.Basic,dummyLife))yield return null;
                    float afterFour=dummyLife.Current;
                    foreach(var _ in Wait(.6f))yield return null;
                    Check(Mathf.Approximately(afterFour,dummyLife.Current),"Four basics in a row unleash no flurry");
                    // The first extra strikes land while the fifth swing still plays, so the swing and its flurry are measured together.
                    float beforeFifth=dummyLife.Current;
                    foreach(var _ in Swing(AbilitySlot.Basic,dummyLife))yield return null;
                    foreach(var _ in Wait(1f))yield return null;
                    Near(beforeFifth-dummyLife.Current,unit*(1+2*amount),"The fifth basic deals a normal hit plus 2 extra strikes at "+amount*100+" %",.05f);
                }
                Evolve(basic,"burst",0);foreach(var _ in FlurryRun(.4f))yield return null;
                Evolve(basic,"burst",3);foreach(var _ in FlurryRun(.4667f))yield return null;
                {
                    // Changing target restarts the count.
                    var first=NewTarget(out var firstLife);
                    for(int i=0;i<4;i++)foreach(var _ in Swing(AbilitySlot.Basic,firstLife))yield return null;
                    var second=NewTarget(out var secondLife);
                    foreach(var _ in Swing(AbilitySlot.Basic,secondLife))yield return null;
                    first.transform.position=front;second.transform.position=front+Vector3.back*40;Physics.SyncTransforms();
                    foreach(var _ in Swing(AbilitySlot.Basic,firstLife))yield return null;
                    float afterSwing=firstLife.Current;
                    foreach(var _ in Wait(.8f))yield return null;
                    Check(Mathf.Approximately(afterSwing,firstLife.Current),"Changing target restarts the Ráfaga count");
                }
                ClearModifier(basic);

                // Torbellino: the spin may start over once, with a chance that every rank raises a little.
                var spin=Skill("DualAxeDeathSpin");Slot(AbilitySlot.E,spin.Id);
                var dummySpin=NewTarget(out var spinLife);
                float gap=0;int most=0;
                int Spins(int casts)
                {
                    int repeats=0;gap=0;most=0;
                    for(int i=0;i<casts;i++)
                    {
                        Rest();float lifeAtStart=spinLife.Current;
                        if(!runner.TryUse(AbilitySlot.E,Vector3.forward,player.transform.position))throw new Exception("Cannot use the spin");
                        var cast=runner.Current;
                        for(int tick=0;tick<900&&runner.Current!=null;tick++)runner.Tick(.05f);
                        repeats+=cast.Repeats;most=Mathf.Max(most,cast.Repeats);
                        float expected=(1+cast.Repeats)*2*14*1.3f*Multiplier(spin);
                        gap=Mathf.Max(gap,Mathf.Abs(lifeAtStart-spinLife.Current-expected));
                    }
                    return repeats;
                }
                UnityEngine.Random.InitState(77);
                Check(Spins(60)==0,"Without Torbellino the spin never repeats");Near(gap,0,"Each plain spin deals two passes at 130 % per axe");
                Evolve(spin,"whirlwind",0);
                int repeatTotal=Spins(300);
                Check(repeatTotal>=25&&repeatTotal<=68,"Torbellino rank 0 repeats about 15 % of the spins ("+repeatTotal+" repeats in 300 casts)");
                Check(most==1,"A spin repeats at most once");
                Near(gap,0,"Every repeated spin deals its passes again");
                Evolve(spin,"whirlwind",3);
                repeatTotal=Spins(300);
                Check(repeatTotal>=30&&repeatTotal<=80,"Torbellino rank 3 repeats about 17.5 % of the spins ("+repeatTotal+" repeats in 300 casts)");
                ClearModifier(spin);

                // Hacha errante: the thrown axe jumps to at most 2 other enemies, each hit softer than the last; the rank trims the loss.
                var throwing=Skill("DualAxeThrow");Slot(AbilitySlot.Q,throwing.Id);
                var throwAction=(AxeThrowAction)Array.Find(throwing.actions,a=>a is AxeThrowAction);var throwVisual=throwAction.visualRotation;
                Check(Quaternion.Angle(Quaternion.Euler(throwVisual),Quaternion.Euler(0,-90,0))<1f,"The throw turns the axe model so its blade (+X) faces forward");
                // Each enemy stands a bit short of the asset's reach from the previous one.
                float spacing=Mathf.Max(1,throwing.FindModifier("wandering_axe").distance*.8f);
                IEnumerable Bounce(int rank,float loss)
                {
                    var aim=NewTarget(out var aimLife,front+Vector3.forward*3.4f);
                    var others=new List<Health>();var bodies=new List<GameObject>();
                    for(int i=1;i<=3;i++){bodies.Add(NewTarget(out var life,front+Vector3.forward*3.4f+Vector3.right*(spacing*i)));others.Add(life);}
                    // NewTarget parks the previous dummies: bring the whole line back.
                    aim.transform.position=front+Vector3.forward*3.4f;
                    for(int i=0;i<bodies.Count;i++)bodies[i].transform.position=front+Vector3.forward*3.4f+Vector3.right*(spacing*(i+1));
                    Physics.SyncTransforms();
                    Rest();
                    int cuts=0;void Cut(AudioClip clip,float volume){if(clip!=null&&clip==throwAction.impactSfx)cuts++;}
                    AudioEvents.OnPlayAbilitySFX+=Cut;
                    Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,player.transform.position,front+Vector3.forward*3.4f),"The throw starts");
                    for(int i=0;i<200&&runner.Current!=null;i++)runner.Tick(.05f);
                    // The blade must lead the flight: the model's +X (its edge) points where the axe goes, its flat side is perpendicular.
                    var thrown=Object.FindFirstObjectByType<ProjectileInstance>();
                    Check(thrown!=null&&thrown.transform.childCount>0,"The thrown axe has its model");
                    var whirl=thrown.GetComponent<AudioSource>();
                    Check(throwAction.flightSfx!=null&&whirl!=null&&whirl.loop&&whirl.clip==throwAction.flightSfx&&whirl.spatialBlend>.99f,"The thrown axe whirls in 3D as it flies");
                    var thrownModel=thrown.transform.GetChild(0);
                    Check(Vector3.Dot(thrownModel.right,thrown.transform.forward)>.95f,"The thrown axe flies with the blade forward");
                    Check(Mathf.Abs(Vector3.Dot(thrownModel.forward,thrown.transform.forward))<.1f,"The flat of the thrown blade is not facing forward");
                    // The rebound flight is sampled once while it is in the air.
                    bool sampled=false;
                    for(float until=Time.time+1.6f;Time.time<until;)
                    {
                        var flight=sampled?null:Object.FindFirstObjectByType<ReboundFlight>();
                        if(flight!=null&&flight.Model!=null)
                        {
                            sampled=true;
                            Check(Vector3.Dot(flight.transform.forward,flight.Direction)>.99f,"A rebound flies facing its destination");
                            Check(Quaternion.Angle(flight.Model.localRotation,Quaternion.Euler(throwVisual))<1f,"A rebound turns the model like the throw so the blade leads");
                            Check(flight.GetComponent<AudioSource>()?.clip==throwAction.flightSfx,"A rebound whirls like the throw");
                        }
                        yield return null;
                    }
                    Check(sampled,"A rebound flight was seen in the air");
                    AudioEvents.OnPlayAbilitySFX-=Cut;
                    Check(throwAction.impactSfx!=null&&cuts==3,"The throw and both rebounds sound their cut ("+cuts+")");
                    Check(aimLife.Current<aimLife.Maximum,"The thrown axe hits its target");
                    float throwDamage=14*Multiplier(throwing);
                    Near(others[0].Maximum-others[0].Current,throwDamage*(1-loss),"The first rebound hits "+loss*100+" % softer at rank "+rank,.05f);
                    Near(others[1].Maximum-others[1].Current,throwDamage*(1-loss)*(1-loss),"The second rebound loses the same share again at rank "+rank,.05f);
                    Near(others[2].Maximum-others[2].Current,0,"There is no third rebound at rank "+rank);
                }
                Evolve(throwing,"wandering_axe",0);foreach(var _ in Bounce(0,.3f))yield return null;
                Evolve(throwing,"wandering_axe",3);foreach(var _ in Bounce(3,.25f))yield return null;
                ClearModifier(throwing);

                // Berserker: Matanza heals on kills during the mode, Furia creciente speeds the attacks up over time.
                var berserk=Skill("DualAxeBerserk");Slot(AbilitySlot.Q,berserk.Id);
                void Activate()
                {
                    Rest();Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,player.transform.position),"The Berserker starts");
                    for(int i=0;i<200&&runner.Current!=null;i++)runner.Tick(.05f);
                    Check(effects.Berserking,"The Berserker mode is active");
                }
                // Taking damage plays the hit reaction, which would replace the swing's animation and its blade path: let it end first.
                IEnumerable Hurt()
                {
                    playerLife.Revive();playerLife.ApplyDamage(new DamageInfo(playerLife.Maximum*.6f,null,player.transform.position,Vector3.forward));
                    foreach(var _ in Wait(1.2f))yield return null;
                }
                float healed=0;
                IEnumerable KillOne()
                {
                    var prey=NewTarget(out var preyLife,front,1);float lifeBefore=playerLife.Current;
                    foreach(var _ in Swing(AbilitySlot.Basic,preyLife))yield return null;
                    Check(preyLife.IsDead,"The dummy dies (life "+preyLife.Current+", dummy at "+prey.transform.position+", player at "+player.transform.position+", combo step "+combo.CurrentStepIndex+", slots "+equipment.GetAbility(AbilitySlot.Q)?.Id+"/"+equipment.GetAbility(AbilitySlot.E)?.Id+"/"+equipment.GetAbility(AbilitySlot.R)?.Id+", lost "+lost+")");healed=playerLife.Current-lifeBefore;
                }
                Evolve(berserk,"slaughter",0);
                foreach(var _ in Hurt())yield return null;
                foreach(var _ in KillOne())yield return null;Near(healed,0,"Matanza does not heal outside the Berserker mode");
                Activate();foreach(var _ in Hurt())yield return null;
                foreach(var _ in KillOne())yield return null;
                Near(healed/playerLife.Maximum,.05f,"Matanza rank 0 heals 5 % of the life per kill",.003f);
                Evolve(berserk,"slaughter",3);
                Activate();foreach(var _ in Hurt())yield return null;
                foreach(var _ in KillOne())yield return null;
                Near(healed/playerLife.Maximum,.0583f,"Matanza rank 3 heals 5.8 % of the life per kill",.003f);
                IEnumerable Fury(int rank,float rate)
                {
                    Activate();float start=effects.SpeedBonus;float t0=Time.time;
                    foreach(var _ in Wait(1.2f))yield return null;
                    float gained=effects.SpeedBonus-start,elapsed=Time.time-t0;
                    Near(gained/elapsed,rate,"Furia creciente rank "+rank+" adds "+rate+" attack speed per second",.0012f);
                }
                Evolve(berserk,"rising_fury",0);foreach(var _ in Fury(0,.015f))yield return null;
                Evolve(berserk,"rising_fury",3);foreach(var _ in Fury(3,.0175f))yield return null;

                // HUD timer: the mode reports its seconds left and shows up as a buff tied to its ability.
                var berserkAction=(BerserkAction)Array.Find(berserk.actions,a=>a is BerserkAction);
                effects.ResetEffects();
                Check(!effects.BerserkTimer(berserk,out _,out _),"No Berserker timer before the mode starts");
                Activate();
                Check(effects.BerserkTimer(berserk,out float modeLeft,out float modeTotal),"The Berserker timer runs for its ability");
                Near(modeTotal,berserkAction.duration,"The timer's total is the mode's duration");
                Near(modeLeft,berserkAction.duration,"The timer starts full",.5f);
                Check(!effects.BerserkTimer(Skill("DualAxeDeathSpin"),out _,out _),"Other abilities show no Berserker timer");
                var buffViews=new List<BuffView>();effects.CollectBuffViews(buffViews);
                Check(buffViews.Exists(v=>v.label=="MODO BERSERKER"&&v.ability==berserk&&v.mode&&v.ready&&!v.worldVisible&&v.kind==BuffKind.Damage),"The Berserker appears as a damage buff tied to its ability, without a world symbol");
                foreach(var _ in Wait(1f))yield return null;
                effects.BerserkTimer(berserk,out float later,out _);
                Check(later<modeLeft-.5f,"The timer counts down");
                // Visual: embers, red body pulse, both axe heads lit, rings on the ground and the red screen edge, all growing with the fury.
                var furyVisual=player.GetComponent<BerserkVisual>();
                Check(furyVisual!=null,"The player carries the Berserker visual");
                if(furyVisual!=null)
                {
                    var style=BuffPresentation.Current;
                    for(int i=0;i<4;i++)furyVisual.Tick(.05f);
                    Check(furyVisual.GlowRenderers>=2,"Both axe heads glow during the mode");
                    Check(furyVisual.EmberEmitters==2&&furyVisual.EmberRate>0,"Both axes shed embers during the mode");
                    Check(furyVisual.ScreenEdge>0&&furyVisual.ScreenEdge<=style.screenEdgeMax+1e-4f,"The red screen edge shows, capped by the Inspector");
                    Check(furyVisual.Fury>0&&furyVisual.Fury<.5f,"One second in, the fury has only begun");
                    Near(BerserkVisual.FuryAt(modeTotal,modeTotal),0,"The fury starts at zero");
                    Near(BerserkVisual.FuryAt(0,modeTotal),1,"The fury is full at the end of the mode");
                    Check(BerserkVisual.EmberRateAt(style,1)>BerserkVisual.EmberRateAt(style,0)&&BerserkVisual.RingIntervalAt(style,1)<BerserkVisual.RingIntervalAt(style,0),"Embers and rings grow faster with the fury");
                    int rings=0;for(int i=0;i<30;i++){furyVisual.Tick(.05f);rings=Mathf.Max(rings,furyVisual.ActiveRings);}
                    Check(rings>0,"Rings pulse on the ground");
                    // Sound: the heartbeat follows the red pulse and the embers loop grows with the fury.
                    for(int i=0;i<40;i++)furyVisual.Tick(.05f);
                    Check(furyVisual.HeartbeatsPlayed>0,"The heartbeat sounds with the red pulse");
                    Check(furyVisual.EmbersVolume>0,"The embers crackle during the mode");
                }
                int endsBefore=furyVisual!=null?furyVisual.EndsPlayed:0;
                effects.ResetEffects();buffViews.Clear();effects.CollectBuffViews(buffViews);
                Check(!buffViews.Exists(v=>v.label=="MODO BERSERKER")&&!effects.BerserkTimer(berserk,out _,out _),"The timer and the buff disappear when the mode ends");
                if(furyVisual!=null)
                {
                    for(int i=0;i<10;i++)furyVisual.Tick(.05f);
                    Check(furyVisual.EmberEmitters==0&&furyVisual.GlowRenderers==0&&furyVisual.ScreenEdge==0&&furyVisual.EmberRate==0,"The Berserker visual fades out when the mode ends");
                    Check(furyVisual.EmbersVolume==0&&furyVisual.EndsPlayed==endsBefore,"A mode cut short stops the embers without its end sound");
                }

                // Focus balance: Giro mortal and Modo Berserker cost it, the basic, Hachazo doble and Lanzamiento generate it on hit.
                {
                    var spinCost=Skill("DualAxeDeathSpin");var leapGain=Skill("DualAxeLeap");var throwGain=Skill("DualAxeThrow");
                    Check(spinCost.focusCost>0&&berserk.focusCost>0,"Giro mortal and Modo Berserker cost Focus");
                    Check(basic.focusGainOnHit>0&&leapGain.focusGainOnHit>0&&throwGain.focusGainOnHit>0,"The basic, Hachazo doble and Lanzamiento generate Focus on hit");
                    Check(basic.focusCost==0&&leapGain.focusCost==0&&throwGain.focusCost==0,"The Focus generators cost none");
                    Slot(AbilitySlot.E,spinCost.Id);
                    Rest();combat.Spend(combat.Focus);
                    Check(!runner.TryUse(AbilitySlot.E,Vector3.forward,player.transform.position),"Giro mortal does not start without its Focus");
                    combat.Reward(spinCost.focusCost-1,"TEST");
                    Check(!runner.TryUse(AbilitySlot.E,Vector3.forward,player.transform.position),"Giro mortal does not start one point short");
                    combat.Reward(1,"TEST");
                    Check(runner.TryUse(AbilitySlot.E,Vector3.forward,player.transform.position),"Giro mortal starts with exactly its Focus");
                    Near(combat.Focus,0,"Giro mortal spends its Focus");
                    Idle();
                    Slot(AbilitySlot.Q,leapGain.Id);
                    var focusTarget=NewTarget(out var focusLife);
                    Rest();combat.Spend(combat.Focus);
                    Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,player.transform.position),"Hachazo doble starts without Focus");
                    for(float limit=Time.time+10;runner.Current!=null&&Time.time<limit;){runner.Tick(Time.deltaTime);yield return null;}
                    Check(focusLife.Current<focusLife.Maximum,"Hachazo doble lands on the dummy");
                    Check(combat.Focus>=leapGain.focusGainOnHit-.01f,"Hachazo doble gives its Focus when it lands (got "+combat.Focus+")");
                    Slot(AbilitySlot.Q,berserk.Id);
                    Rest();combat.Spend(combat.Focus);
                    Check(!runner.TryUse(AbilitySlot.Q,Vector3.forward,player.transform.position),"Modo Berserker does not start without its Focus");
                    combat.Reward(berserk.focusCost,"TEST");
                    Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,player.transform.position),"Modo Berserker starts with its Focus");
                    Idle();effects.ResetEffects();
                }

                // Aiming: an object between the camera and the character must not pull the aim behind them, and a point at their
                // back never turns an ability around (the basic of the single axe faced the camera when something got in the way).
                {
                    Vector3 camPos=player.transform.position+new Vector3(0,2,-6);
                    var ray=new Ray(camPos,(player.transform.position+Vector3.up*4f+Vector3.forward*10-camPos).normalized);
                    var between=GameObject.CreatePrimitive(PrimitiveType.Cube);between.transform.position=camPos+ray.direction*2;
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=camPos+ray.direction*20;wall.transform.localScale=new Vector3(8,8,1);
                    Physics.SyncTransforms();
                    Vector3 aimed=WeaponAim.AimPoint(ray,40,player.transform);
                    Check(Vector3.Distance(camPos,aimed)>15f,"The aim ignores what lies between the camera and the character");
                    Object.Destroy(between);
                    var basis=new GameObject("Aim basis").transform;basis.position=camPos;basis.rotation=Quaternion.LookRotation(Vector3.forward);
                    Vector3 chest=player.transform.position+Vector3.up;
                    Vector3 ahead=WeaponAim.Direction(chest,chest+Vector3.forward*3+Vector3.right*.5f,basis);
                    Check(Vector3.Dot(ahead,(Vector3.forward*3+Vector3.right*.5f).normalized)>.999f,"A point in front keeps its exact direction");
                    Vector3 behind=WeaponAim.Direction(chest,chest+Vector3.back*1.5f+Vector3.up*.3f,basis);
                    Check(behind.z>.9f&&behind.y>0,"A point behind the character keeps the camera's heading and the vertical aim");
                    Vector3 beside=WeaponAim.Direction(chest,chest+Vector3.right*2,basis);
                    Check(beside.z>.9f,"A point at the character's side is not followed sideways either");
                    Vector3 onTop=WeaponAim.Direction(chest,chest,basis);
                    Check(onTop.z>.9f,"A point on top of the muzzle falls back to the camera's heading");
                    Object.Destroy(wall);Object.Destroy(basis.gameObject);
                }

                // K toggles the skills page: the first press opens it, the second closes it.
                Idle();foreach(var _ in Wait(.6f))yield return null;
                var panel=player.gameObject.AddComponent<InventoryPanel>();
                var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
                try
                {
                    IEnumerable Press(UnityEngine.InputSystem.Key key)
                    {
                        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
                        yield return null;yield return null;
                        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
                        yield return null;yield return null;
                    }
                    foreach(var _ in Press(UnityEngine.InputSystem.Key.K))yield return null;
                    Check(panel.IsOpen,"K opens the skills panel");
                    foreach(var _ in Press(UnityEngine.InputSystem.Key.K))yield return null;
                    Check(!panel.IsOpen,"A second K closes the skills panel");
                    // P does the same with the character page.
                    foreach(var _ in Press(UnityEngine.InputSystem.Key.P))yield return null;
                    Check(panel.IsOpen,"P opens the character panel");
                    foreach(var _ in Press(UnityEngine.InputSystem.Key.P))yield return null;
                    Check(!panel.IsOpen,"A second P closes the character panel");
                }
                finally{UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);Object.Destroy(panel);}
            }
            finally
            {
                runner.Cancel();
                foreach(var go in targets)if(go!=null)Object.Destroy(go);
            }
        }
    }
}
