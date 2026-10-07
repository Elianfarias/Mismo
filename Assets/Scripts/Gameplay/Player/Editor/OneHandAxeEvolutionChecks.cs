using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Play Mode rules of the Hacha sola evolutions, on the real axe assets. The arena player gets an in-memory
    // inventory (never the user's save) whose abilities are mastered through the public training API.
    public static class OneHandAxeEvolutionChecks
    {
        private const string Pending="Mismo.OneHandAxeEvolutionChecks";
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
            deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update+=Step;
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
            Debug.Log("AXE_EVOLUTION_"+(ok?"PASS ":"FAIL ")+message);EditorApplication.Exit(ok?0:1);
        }
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("AXE_EVOLUTION_CHECK "+message);}
        private static void Near(float actual,float expected,string message)=>Check(Mathf.Abs(actual-expected)<.02f,message+" (expected "+expected+", got "+actual+")");
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void ClearCooldowns(AbilityRunner runner)=>
            ((Dictionary<AbilityDefinition,float>)typeof(AbilityRunner).GetField("readyAt",Private).GetValue(runner)).Clear();
        // Bleed state has no public getters: damage per second and seconds left on the target.
        private static (float damage,float seconds) Bleed(CombatAilment ailment)
        {
            foreach(var entry in (System.Collections.IList)typeof(CombatAilment).GetField("effects",Private).GetValue(ailment))
            {
                var type=entry.GetType();
                if((StatusEffectType)type.GetField("type").GetValue(entry)==StatusEffectType.Bleed)
                    return ((float)type.GetField("damage").GetValue(entry),(float)type.GetField("until").GetValue(entry)-Time.time);
            }
            return (0,0);
        }

        private static IEnumerator Run()
        {
            var player=Object.FindAnyObjectByType<PlayerController>();Check(player!=null,"Player fixture exists");player.enabled=false;
            foreach(var goblin in Object.FindObjectsByType<Enemies.EnemyController>())goblin.enabled=false;
            var equipment=player.GetComponent<EquipmentLoadout>();var runner=equipment.Runner;
            player.GetComponent<Health>().Revive();
            var combat=player.GetComponent<CombatState>();var stamina=player.GetComponent<Movement.Stamina>();
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
                foreach(var go in targets)if(go!=null&&go!=keep)go.transform.position=front+Vector3.forward*40;
                Physics.SyncTransforms();
            }
            void Bring(GameObject go){Park(go);go.transform.position=front;Physics.SyncTransforms();}
            GameObject NewTarget(out Health life,out CombatAilment ailment,float armor=0)
            {
                Park(null);
                var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.name="Axe target";
                life=go.AddComponent<Health>();life.ConfigureMaximum(100000);life.Revive();
                go.AddComponent<DamageReceiver>();
                ailment=go.AddComponent<CombatAilment>();ailment.ConfigureArmor(armor);
                // Facing the player: a back hit would reward the attacker with Focus.
                go.transform.SetPositionAndRotation(front,Quaternion.LookRotation(Vector3.back));Physics.SyncTransforms();
                targets.Add(go);return go;
            }
            // Runs an ability from press to the end of its execution and returns the health the target lost.
            float Use(AbilitySlot slot,Health subject)
            {
                Rest();float lifeAtStart=subject.Current;
                if(!runner.TryUse(slot,Vector3.forward,player.transform.position))throw new Exception("Cannot use "+slot);
                for(int i=0;i<400&&runner.Current!=null;i++)runner.Tick(.05f);
                return lifeAtStart-subject.Current;
            }

            var inventory=player.gameObject.AddComponent<PlayerInventory>();
            var storage=new Memory();
            try
            {
                inventory.Initialize(catalog,storage);
                Check(inventory.IsReady&&!inventory.HasSaveProblem,"An in-memory inventory starts: "+inventory.Notice);
                Idle();Check(inventory.TryGrantCheatWeapons()&&inventory.TryMaxCheatWeaponMasteries(),"The profile owns every weapon at maximum mastery");
                var axe=Array.Find(catalog.weapons,w=>w!=null&&w.Id=="axe.basic");
                Idle();Check(inventory.TryEquipDefinition(0,axe),"The axe equips");
                if(equipment.ActiveSlot!=0){Idle();Check(inventory.TrySwap(),"The axe becomes the active weapon");}
                var family=axe.family;Check(family!=null&&equipment.ActiveDefinition==axe,"The active weapon is the single axe");
                var basic=family.GetAbility(AbilitySlot.Basic);
                for(int i=0;i<family.SkillCount;i++){Idle();Check(inventory.TryUnlockAbility(axe,i),"Unlock "+family.Skill(i).Id);}
                AbilityDefinition Skill(string id)=>family.Skill(family.FindSkill(id));
                int Index(AbilityDefinition ability)=>ability==basic?-1:family.FindSkill(ability.Id);
                void Slot(AbilitySlot slot,string id)
                {
                    Idle();if(equipment.GetAbility(slot)?.Id==id)return;
                    Check(inventory.TrySelectAbility(axe,slot,family.FindSkill(id)),"Slot "+slot+" holds "+id);
                }
                void Train(AbilityDefinition ability,int uses)
                {
                    for(int i=0;i<uses;i++)inventory.RecordMonsterSkillUse(axe.MasteryId,ability.Id,AttackIdentity.Next());
                    Check(inventory.FlushCombatProgress(),"Training is committed");
                }
                // 25 effective uses master the ability; the chosen modifier then needs 10 more uses per rank.
                void Evolve(AbilityDefinition ability,string modifier,int rank)
                {
                    Idle();var progress=inventory.Mastery(axe).SkillProgress(ability.Id);
                    if(progress==null||!progress.IsMastered(ability.masteryUsesRequired))Train(ability,ability.masteryUsesRequired);
                    Idle();progress=inventory.Mastery(axe).SkillProgress(ability.Id);
                    if(progress.selectedModifierId!=modifier)Check(inventory.TrySelectModifier(axe,Index(ability),modifier),ability.Id+" selects "+modifier);
                    var trained=inventory.Mastery(axe).SkillProgress(ability.Id).Modifier(modifier);
                    int have=trained.level*10+trained.effectiveUses;if(rank*10>have)Train(ability,rank*10-have);
                    Check(inventory.Mastery(axe).SkillProgress(ability.Id).Modifier(modifier).level==rank,ability.Id+" "+modifier+" is rank "+rank);
                }
                void ClearModifier(AbilityDefinition ability){Idle();inventory.TryClearModifier(axe,Index(ability));}
                float Multiplier(AbilityDefinition ability)=>inventory.DamageMultiplier(axe)*inventory.AbilityDamageMultiplier(axe,ability);

                // Family trait: only the basic is heavier.
                Near(family.basicDamageBonus,.15f,"The family trait is +15 %");
                var target=NewTarget(out var life,out _);
                Near(Use(AbilitySlot.Basic,life),14*1.15f*Multiplier(basic),"The basic hits for 14 +15 %");
                var swing=Skill("AxeForwardSwing");Slot(AbilitySlot.Q,swing.Id);
                Near(Use(AbilitySlot.Q,life),20*Multiplier(swing),"Hachazo does not get the basic's +15 %");

                // Hachazo: Reabrir lengthens a bleed that is already running; Barrido widens the swing.
                var main=NewTarget(out var mainLife,out var mainAilment);
                Vector3 swingOrigin=player.transform.position+Vector3.up+Vector3.forward;
                Vector3 sidePlace=swingOrigin+Vector3.right*1.5f+Vector3.back*.3f;
                var side=NewTarget(out var sideLife,out _);
                void Arrange(){main.transform.position=front;side.transform.position=sidePlace;Physics.SyncTransforms();}
                (float front,float side) Swipe()
                {
                    Arrange();Rest();float mainBefore=mainLife.Current,sideBefore=sideLife.Current;
                    Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,player.transform.position),"Hachazo starts");
                    for(int i=0;i<400&&runner.Current!=null;i++)runner.Tick(.05f);
                    return (mainBefore-mainLife.Current,sideBefore-sideLife.Current);
                }
                var plainSwipe=Swipe();
                Near(plainSwipe.front,20*Multiplier(swing),"Hachazo hits the target in front for 20");Near(plainSwipe.side,0,"Without Barrido the side target is out of reach");
                Evolve(swing,"sweep",0);
                var sweep=Swipe();
                Near(sweep.front,20*Multiplier(swing),"Barrido keeps the full damage in front");Near(sweep.side,20*Multiplier(swing)*.6f,"Barrido rank 0: the sides take 60 %");
                Evolve(swing,"sweep",3);
                sweep=Swipe();Near(sweep.side,20*Multiplier(swing)*.7f,"Barrido rank 3: the sides take 70 %");
                ClearModifier(swing);
                side.transform.position=front+Vector3.forward*40;Physics.SyncTransforms();
                // Seconds of bleed left on the target in front after one swing; -1 when it does not bleed.
                float BleedAfterSwipe(bool bleeding)
                {
                    mainAilment.enabled=false;mainAilment.enabled=true;
                    if(bleeding)CombatAilment.Bleed(main,player.gameObject,axe.MasteryId,.01f,10);
                    main.transform.position=front;Physics.SyncTransforms();Rest();
                    Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,player.transform.position),"Hachazo starts");
                    for(int i=0;i<400&&runner.Current!=null;i++)runner.Tick(.05f);
                    return mainAilment.IsActive(StatusEffectType.Bleed)?Bleed(mainAilment).seconds:-1;
                }
                Near(BleedAfterSwipe(true),10,"Without Reabrir the bleed keeps its time");
                Evolve(swing,"reopen",0);
                Near(BleedAfterSwipe(true),13,"Reabrir rank 0 adds 3 s to a bleed");
                Near(BleedAfterSwipe(false),-1,"Reabrir does not make a healthy target bleed");
                Evolve(swing,"reopen",3);
                Near(BleedAfterSwipe(true),13.5f,"Reabrir rank 3 adds 3.5 s");
                ClearModifier(swing);

                // The bleed rule: the strongest bleed wins and a weaker one never overrides or extends it.
                var bare=new GameObject("Bleed rule target");targets.Add(bare);
                CombatAilment.Bleed(bare,player.gameObject,axe.MasteryId,5,10);
                var rule=bare.GetComponent<CombatAilment>();
                var state=Bleed(rule);Near(state.damage,5,"A bleed starts at 5 per second");Near(state.seconds,10,"A bleed starts for 10 s");
                CombatAilment.Bleed(bare,player.gameObject,axe.MasteryId,3,20);
                state=Bleed(rule);Near(state.damage,5,"A weaker bleed does not replace it");Near(state.seconds,10,"A weaker bleed does not extend it");
                CombatAilment.Bleed(bare,player.gameObject,axe.MasteryId,5,15);
                state=Bleed(rule);Near(state.damage,5,"An equal bleed keeps the damage");Near(state.seconds,15,"An equal bleed renews a longer duration");
                CombatAilment.Bleed(bare,player.gameObject,axe.MasteryId,5,2);
                state=Bleed(rule);Near(state.seconds,15,"An equal bleed never shortens it");
                CombatAilment.Bleed(bare,player.gameObject,axe.MasteryId,9,4);
                state=Bleed(rule);Near(state.damage,9,"A stronger bleed replaces it");Near(state.seconds,4,"A stronger bleed brings its own duration");

                // Cuarto corte: active from rank 0; each rank only adds damage to the bleed.
                Evolve(basic,"fourth_cut",0);
                var first=NewTarget(out var firstLife,out var firstAilment);
                for(int i=0;i<3;i++)Use(AbilitySlot.Basic,firstLife);
                Check(!firstAilment.IsActive(StatusEffectType.Bleed),"Three basics in a row do not bleed");
                Use(AbilitySlot.Basic,firstLife);
                Check(firstAilment.IsActive(StatusEffectType.Bleed),"The fourth basic in a row makes the target bleed");
                state=Bleed(firstAilment);Near(state.damage,3*Multiplier(basic),"Cuarto corte bleeds 3 per second at rank 0");Near(state.seconds,3,"Cuarto corte bleeds 3 s");
                var swapped=NewTarget(out var swappedLife,out var swappedAilment);
                for(int i=0;i<3;i++)Use(AbilitySlot.Basic,swappedLife);
                var other=NewTarget(out var otherLife,out _);
                Use(AbilitySlot.Basic,otherLife);
                Bring(swapped);Use(AbilitySlot.Basic,swappedLife);
                Check(!swappedAilment.IsActive(StatusEffectType.Bleed),"Changing target restarts the count");
                var slow=NewTarget(out var slowLife,out var slowAilment);
                for(int i=0;i<3;i++)Use(AbilitySlot.Basic,slowLife);
                float until=Time.time+3.3f;while(Time.time<until)yield return null;
                Use(AbilitySlot.Basic,slowLife);
                Check(!slowAilment.IsActive(StatusEffectType.Bleed),"Three seconds without hitting restart the count");
                Evolve(basic,"fourth_cut",3);
                var ranked=NewTarget(out var rankedLife,out var rankedAilment);
                for(int i=0;i<4;i++)Use(AbilitySlot.Basic,rankedLife);
                state=Bleed(rankedAilment);Near(state.damage,3.5f*Multiplier(basic),"Cuarto corte bleeds 3.5 per second at rank 3");Near(state.seconds,3,"Cuarto corte still bleeds 3 s at rank 3");
                ClearModifier(basic);

                // Tajo sangrante: 3 per second, and Filo oxidado.
                var cut=Skill("AxeBleedingCut");Slot(AbilitySlot.E,cut.Id);
                CombatAilment Prime(out Health lifeOut)
                {
                    var go=NewTarget(out lifeOut,out var ailment);
                    Rest();Check(runner.TryUse(AbilitySlot.E,Vector3.forward,player.transform.position),"Tajo sangrante primes the next basic");
                    for(int i=0;i<400&&runner.Current!=null;i++)runner.Tick(.05f);
                    Use(AbilitySlot.Basic,lifeOut);return ailment;
                }
                var marked=Prime(out _);Check(marked.IsActive(StatusEffectType.Bleed),"The marked basic applies the bleed");
                state=Bleed(marked);Near(state.damage,3*Multiplier(cut),"Tajo sangrante bleeds 3 per second");Near(state.seconds,5,"Tajo sangrante bleeds 5 s");
                Evolve(cut,"rusty_edge",0);
                state=Bleed(Prime(out _));Near(state.damage,3*Multiplier(cut)*1.5f,"Filo oxidado rank 0 adds 50 %");
                Evolve(cut,"rusty_edge",3);
                state=Bleed(Prime(out _));Near(state.damage,3*Multiplier(cut)*1.75f,"Filo oxidado rank 3 adds 75 %");
                ClearModifier(cut);

                // Desgarre: 125 % of the basic, armor loss, and its two evolutions.
                var rend=Skill("AxeArmorRend");Slot(AbilitySlot.E,rend.Id);float scale=CombatRules.Current.armorScale;
                float Armored(float lost)=>scale/(scale+50*(1-lost));
                var plain=NewTarget(out var plainLife,out var plainAilment,50);float incoming=plainAilment.IncomingDamageMultiplier;
                Near(Use(AbilitySlot.E,plainLife),17.5f*Multiplier(rend)*incoming,"Desgarre hits for 125 % of the basic");
                Near(plainAilment.IncomingDamageMultiplier,Armored(.3f),"Desgarre removes 30 % of the armor");
                Evolve(rend,"deep_rend",0);
                var deep=NewTarget(out var deepLife,out var deepAilment,50);Use(AbilitySlot.E,deepLife);
                Near(deepAilment.IncomingDamageMultiplier,Armored(.39f),"Desgarro profundo rank 0 removes 39 %");
                Evolve(rend,"deep_rend",3);
                deep=NewTarget(out deepLife,out deepAilment,50);Use(AbilitySlot.E,deepLife);
                Near(deepAilment.IncomingDamageMultiplier,Armored(.45f),"Desgarro profundo rank 3 removes 45 %");
                Check(!deepAilment.IsActive(StatusEffectType.Bleed),"Desgarro profundo does not bleed");
                Evolve(rend,"raw_flesh",0);
                var flesh=NewTarget(out var fleshLife,out var fleshAilment,50);Use(AbilitySlot.E,fleshLife);
                Check(fleshAilment.IsActive(StatusEffectType.Bleed),"Carne viva makes the target bleed");
                state=Bleed(fleshAilment);Near(state.damage,3*Multiplier(rend),"Carne viva bleeds 3 per second at rank 0");Near(state.seconds,3,"Carne viva bleeds 3 s");
                Near(fleshAilment.IncomingDamageMultiplier,Armored(.3f),"Carne viva keeps the 30 % armor loss");
                Evolve(rend,"raw_flesh",3);
                flesh=NewTarget(out fleshLife,out fleshAilment,50);Use(AbilitySlot.E,fleshLife);
                state=Bleed(fleshAilment);Near(state.damage,3.5f*Multiplier(rend),"Carne viva bleeds 3.5 per second at rank 3");Near(state.seconds,3,"Carne viva still bleeds 3 s at rank 3");
                ClearModifier(rend);

                // Combo furioso: Escalada and Quebranto.
                var combo=Skill("AxeFuriousCombo");Slot(AbilitySlot.Q,combo.Id);
                float[] Chain(Health lifeTarget,Transform body,Func<int,bool> reachable=null)
                {
                    var damage=new float[3];Rest();Vector3 home=body.position;
                    for(int stage=0;stage<3;stage++)
                    {
                        body.position=reachable==null||reachable(stage)?home:home+Vector3.forward*30;Physics.SyncTransforms();
                        float lifeBefore=lifeTarget.Current;
                        Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,player.transform.position),"Press "+(stage+1)+" of the combo starts");
                        for(int i=0;i<400&&runner.Current!=null;i++)runner.Tick(.05f);
                        damage[stage]=lifeBefore-lifeTarget.Current;
                    }
                    body.position=home;Physics.SyncTransforms();return damage;
                }
                var victim=NewTarget(out var victimLife,out _);float unit=14*Multiplier(combo);
                var hits=Chain(victimLife,victim.transform);
                for(int i=0;i<3;i++)Near(hits[i],unit*1.35f,"Combo furioso strike "+(i+1)+" deals 135 % of 14");
                Evolve(combo,"escalation",0);
                hits=Chain(victimLife,victim.transform);
                Near(hits[0],unit*1.35f,"Escalada rank 0: the first strike is plain");Near(hits[1],unit*1.45f,"Escalada rank 0: +10 % after one landed strike");Near(hits[2],unit*1.55f,"Escalada rank 0: +20 % after two");
                Evolve(combo,"escalation",3);
                hits=Chain(victimLife,victim.transform);
                Near(hits[0],unit*1.35f,"Escalada rank 3: the first strike is plain");Near(hits[1],unit*1.5f,"Escalada rank 3: +15 % after one landed strike");Near(hits[2],unit*1.65f,"Escalada rank 3: +30 % after two");
                hits=Chain(victimLife,victim.transform,stage=>stage>0);
                Near(hits[0],0,"The first strike of the chain misses");Near(hits[1],unit*1.35f,"A miss gives no Escalada");Near(hits[2],unit*1.5f,"Only landed strikes count");
                ClearModifier(combo);
                Evolve(combo,"shatter",0);
                var breaks=new List<float>();var broken=NewTarget(out var brokenLife,out _);broken.GetComponent<CombatState>().PostureBroken+=breaks.Add;
                Chain(brokenLife,broken.transform);
                Check(breaks.Count==1,"Only the third strike breaks posture");Near(breaks[0],1.3f,"Quebranto rank 0 breaks posture for 1.3 s");
                Evolve(combo,"shatter",3);breaks.Clear();
                Chain(brokenLife,broken.transform);
                Check(breaks.Count==1,"Quebranto keeps one break per chain");Near(breaks[0],1.45f,"Quebranto rank 3 breaks posture for 1.45 s");
                ClearModifier(combo);

                // Passives: Verdugo and Filo cruel read their authored value; Ejecución and Sangre fría add a condition.
                var executioner=Skill("AxeExecutioner");var cruel=Skill("AxeCruelEdge");
                Slot(AbilitySlot.R,executioner.Id);Slot(AbilitySlot.E,cruel.Id);
                Near(executioner.passiveValue,.2f,"Verdugo is authored at 20 %");Near(cruel.passiveValue,3,"Filo cruel is authored at 3 Focus");
                // A bleeding dummy that only has `lifeShare` of its life left.
                float Bleeding(float lifeShare=1)
                {
                    var go=NewTarget(out var lifeBleeding,out _);CombatAilment.Bleed(go,player.gameObject,axe.MasteryId,.01f,30);
                    if(lifeShare<1)lifeBleeding.ApplyDamage(new DamageInfo(lifeBleeding.Maximum*(1-lifeShare),null,go.transform.position,Vector3.forward));
                    return Use(AbilitySlot.Basic,lifeBleeding);
                }
                var calm=NewTarget(out var calmLife,out _);
                Near(Use(AbilitySlot.Basic,calmLife),14*1.15f*Multiplier(basic),"Verdugo does nothing against a target that does not bleed");
                Near(Bleeding(),14*1.15f*1.2f*Multiplier(basic),"Verdugo adds 20 % against a bleeding target");
                Near(Bleeding(.25f),14*1.15f*1.2f*Multiplier(basic),"Without Ejecución a weak target gets the same 20 %");
                Evolve(executioner,"execution",0);
                Near(Bleeding(.5f),14*1.15f*1.2f*Multiplier(basic),"Ejecución: above 30 % of the life the bonus stays 20 %");
                Near(Bleeding(.25f),14*1.15f*1.4f*Multiplier(basic),"Ejecución rank 0: below 30 % of the life the bonus is 40 %");
                Near(Bleeding(.33f),14*1.15f*1.2f*Multiplier(basic),"Ejecución rank 0: 33 % of the life is still above the threshold");
                Evolve(executioner,"execution",3);
                Near(Bleeding(.33f),14*1.15f*1.4f*Multiplier(basic),"Ejecución rank 3: the threshold grows to 35 %");
                Near(Bleeding(.4f),14*1.15f*1.2f*Multiplier(basic),"Ejecución rank 3: 40 % of the life is still above it");
                ClearModifier(executioner);
                float Focus(bool bleeding,bool torn=false)
                {
                    var go=NewTarget(out var lifeFocus,out _);if(bleeding)CombatAilment.Bleed(go,player.gameObject,axe.MasteryId,.01f,30);
                    if(torn)CombatAilment.WeakenArmor(go,.7f,30);
                    Rest();combat.Spend(combat.Focus);combat.Reward(40,"TEST");float focusBefore=combat.Focus;
                    Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,player.transform.position),"A basic starts");
                    for(int i=0;i<400&&runner.Current!=null;i++)runner.Tick(.05f);
                    return combat.Focus-focusBefore;
                }
                float plainFocus=Focus(false);
                Near(Focus(true)-plainFocus,3,"Filo cruel gives 3 Focus against a bleeding target");
                Near(Focus(false,true)-plainFocus,0,"Without Sangre fría a torn armor gives no Focus");
                Evolve(cruel,"cold_blood",0);
                Near(Focus(false,true)-plainFocus,3,"Sangre fría rank 0: 3 Focus against torn armor");
                Near(Focus(true)-plainFocus,3,"Sangre fría keeps the 3 Focus against a bleeding target");
                Near(Focus(false)-plainFocus,0,"Sangre fría gives nothing against an intact target");
                Evolve(cruel,"cold_blood",3);
                Near(Focus(false,true)-plainFocus,3.5f,"Sangre fría rank 3: 3.5 Focus against torn armor");
                ClearModifier(cruel);
            }
            finally
            {
                runner.Cancel();
                foreach(var go in targets)if(go!=null)Object.Destroy(go);
            }
        }
    }
}
