using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;

public sealed class EvolutionBeltSpecialProbe : SpecialAbilityDefinition
{
    public override float Duration => .1f;
    public override float Cooldown => 0;
    public override float InvulnerabilityDuration => 1;
}

public static partial class ProgressionImprovementsChecks
{
    static void EvolutionChecks()
    {
        var basic=Data<AbilityDefinition>();basic.name="evolution.basic";basic.usesSwordCombo=true;basic.staminaCost=7;basic.cooldown=0;basic.masteryUsesRequired=1;
        basic.comboSteps=new[]{new ComboStep("cut",.2f,12,0),new ComboStep("heavy",.2f,20,0)};
        basic.masteryModifiers=new[]{new AbilityModifierDefinition{id="charge",behavior=AbilityModifierBehavior.ChargedCut},new AbilityModifierDefinition{id="dodge",behavior=AbilityModifierBehavior.DodgeChain}};
        var parry=Data<AbilityDefinition>();parry.name="evolution.parry";parry.preparation=0;parry.active=.5f;parry.cooldown=0;parry.masteryUsesRequired=1;
        parry.actions=new AbilityAction[]{new ParryAction()};parry.masteryModifiers=new[]{new AbilityModifierDefinition{id="riposte",behavior=AbilityModifierBehavior.ParryRiposte}};
        var lunge=Data<AbilityDefinition>();lunge.name="evolution.lunge";lunge.preparation=0;lunge.active=.4f;lunge.cooldown=0;lunge.masteryUsesRequired=1;
        lunge.actions=new AbilityAction[]{new MeleeAction()};lunge.masteryModifiers=new[]{new AbilityModifierDefinition{id="follow",behavior=AbilityModifierBehavior.LungeFinisher}};
        var step=Data<AbilityDefinition>();step.name="evolution.step";step.preparation=0;step.active=.2f;step.recovery=0;step.cooldown=0;
        step.actions=new AbilityAction[]{new StepAction()};
        var family=Data<WeaponFamilyDefinition>();family.progressionId="evolution.sword";family.abilities=new[]{basic,parry,lunge};family.repertoire=new[]{parry,lunge,step};
        var sword=Data<WeaponDefinition>();sword.Configure("evolution.sword","Sword",.5f);sword.family=family;
        var bow=Data<WeaponDefinition>();bow.Configure("evolution.bow","Bow",.5f);
        var weapons=Data<WeaponSetDefinition>();weapons.primary=sword;weapons.secondary=bow;
        var catalog=Data<ItemCatalog>();catalog.weapons=new[]{sword,bow};
        var store=new Store();var inventory=Player(weapons,catalog,store);var actor=inventory.gameObject;actor.transform.position=new Vector3(300,0,0);
        var loadout=actor.GetComponent<EquipmentLoadout>();var runner=loadout.Runner;var stamina=actor.GetComponent<Stamina>();
        var combo=actor.AddComponent<BasicSwordCombo>();Call(combo,"Awake");Set(runner,"combo",combo);
        var blade=Actor("Evolution hitbox");blade.transform.SetParent(actor.transform,false);blade.AddComponent<BoxCollider>();blade.AddComponent<DamageDealer>();
        var hitbox=blade.AddComponent<AttackHitbox>();Call(hitbox,"Awake");combo.Configure(hitbox);
        var target=Monster("Evolution target",3000,out var targetHealth,out var receiver);target.transform.position=actor.transform.position+Vector3.forward*.7f;
        target.transform.rotation=Quaternion.Euler(0,180,0);target.AddComponent<BoxCollider>().center=Vector3.up;Physics.SyncTransforms();
        var guard=target.GetComponent<DefenseWindow>();DamageInfo lastHit=default;receiver.Resolved+=(damage,result)=>lastHit=damage;
        guard.OpenGuard(10);StartBasic();Impact();inventory.FlushCombatProgress();
        Check(receiver.LastResult.Outcome==HitOutcome.Block&&inventory.Mastery(sword).SkillProgress(basic.Id)==null,"Blocked basics do not train mastery");
        ClearCombat(loadout);guard.CloseGuard();StartBasic();Impact();inventory.FlushCombatProgress();ClearCombat(loadout);
        Check(inventory.Mastery(sword).SkillProgress(basic.Id)?.effectiveUses==1,"A real combo hit trains the always-available M1 skill");
        Check(inventory.Mastery(sword).unlockedAbilities.Count==0&&inventory.AvailableAbilityPoints(sword)==0,"Basic mastery costs no skill-unlock points");
        Check(inventory.TrySelectModifier(sword,-1,"charge"),"Mastered M1 accepts an optional behavior evolution");
        Check(inventory.AbilityBehavior(sword,basic,out var rank)?.behavior==AbilityModifierBehavior.ChargedCut&&rank==0,"Behavior is active immediately on selection at rank zero");
        float beforeStamina=stamina.Current;
        Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero,held:true),"Holding evolved M1 starts preparation");runner.Tick(.35f);
        Check(!runner.Current.Began&&!hitbox.IsAttacking&&stamina.Current==beforeStamina,"Charging does not open a damage window or spend a combo hit early");
        Check(runner.TryGetAnimationFrame(out var chargeFrame)&&chargeFrame.Phase==CombatAnimationPhase.Preparation,"Charged basic exposes a preparation pose");
        Check(runner.Interrupt()&&runner.Current==null&&!hitbox.IsAttacking,"Damage interruption cancels a charged basic");
        ClearCombat(loadout);guard.OpenGuard(10);StartBasic(true);runner.Tick(1);
        Check(runner.Current.Began&&runner.Current.BreaksGuard&&combo.CurrentStepIndex==1,"Full charge opens with the heavy step");
        float hp=targetHealth.Current;Impact();inventory.FlushCombatProgress();
        Check(lastHit.BreaksGuard&&receiver.LastResult.Outcome==HitOutcome.Hit&&targetHealth.Current<hp&&lastHit.PostureDamage>20,"Charged contact breaks the guard and applies increased posture damage");
        Near(stamina.Current,beforeStamina-7,"Charged swing charges stamina exactly once on release");
        ClearCombat(loadout);guard.OpenGuard(10);StartBasic(true);runner.Tick(.03f);runner.SetHeld(false);runner.Tick(.01f);
        Check(combo.CurrentStepIndex==0&&!runner.Current.BreaksGuard,"Early release remains the ordinary opening cut");Impact();Check(receiver.LastResult.Outcome==HitOutcome.Block,"A tap cannot break a guard");ClearCombat(loadout);guard.CloseGuard();
        Check(!basic.chargeable&&basic.comboSteps.Length==2,"Evolutions never mutate the shared basic asset");
        var loaded=Player(weapons,catalog,new Store{json=store.json});
        Check(!loaded.HasSaveProblem&&loaded.Mastery(sword).SkillProgress(basic.Id)?.isBasic==true,"Basic mastery and its selected evolution survive save reload");
        Check(inventory.TrySelectModifier(sword,-1,"dodge"),"Basic can switch to the optional dodge-chain evolution");
        var belt=actor.AddComponent<BeltDash>();belt.Configure(Data<BasicDashBehaviour>());Set(loadout,"belt",belt);
        Check(belt.TryStart(Vector3.forward,true),"Real belt dash starts");belt.Step(1);
        StartBasic();Check(!runner.Current.DodgeChain,"Even a belt dodge cannot activate the weapon chain");ClearCombat(loadout);
        belt.EquipSpecial(Data<EvolutionBeltSpecialProbe>());belt.TickCooldown(2);
        Check(belt.TryStart(Vector3.forward,true),"A different belt special starts");belt.Step(1);
        StartBasic();Check(!runner.Current.DodgeChain,"An invulnerable belt special cannot activate the weapon chain");ClearCombat(loadout);
        inventory.TryGrantVictory(0,new Dictionary<string,int>{{sword.MasteryId,1000}},null);
        Check(inventory.TryUnlockAbility(sword,2)&&inventory.TrySelectAbility(sword,AbilitySlot.R,2),"Weapon evasion can be equipped independently in R");
        CompleteStep();stamina.Tick(false,100);beforeStamina=stamina.Current;
        StartBasic();Check(runner.Current.DodgeChain,"Completing the weapon evasion opens M1 while a different belt special is equipped");runner.Tick(.21f);runner.Tick(.07f);
        Check(combo.CurrentStepIndex==1&&runner.Current!=null,"Dodge chain automatically begins its second authored cut");
        Near(stamina.Current,beforeStamina-14,"Dodge chain pays each actual swing separately");ClearCombat(loadout);
        StartBasic();runner.Tick(.21f);runner.Tick(.07f);Check(runner.Current==null,"The dodge opportunity is consumed by one combo");ClearCombat(loadout);
        CompleteStep();Set(runner,"dodgeFollowupUntil",Time.time-1);StartBasic();Check(!runner.Current.DodgeChain,"Expired dodge opportunities do not alter M1");ClearCombat(loadout);
        Check(runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero),"Weapon evasion starts before cancellation");runner.Tick(.05f);runner.Cancel();
        StartBasic();Check(!runner.Current.DodgeChain,"A canceled weapon evasion does not grant a followup");ClearCombat(loadout);
        CompleteStep();stamina.TrySpend(stamina.Current-7);StartBasic();runner.Tick(.21f);runner.Tick(.07f);
        Check(runner.Current==null&&Mathf.Approximately(stamina.Current,0),"Insufficient stamina stops the automatic second swing");stamina.Tick(false,100);ClearCombat(loadout);
        inventory.TryGrantVictory(0,new Dictionary<string,int>{{sword.MasteryId,400}},null);
        Check(inventory.TryUnlockAbility(sword,0)&&inventory.TryUnlockAbility(sword,1)&&inventory.TrySelectAbility(sword,AbilitySlot.Q,0)&&inventory.TrySelectAbility(sword,AbilitySlot.E,1),"Earned unlocks equip the parry and approach skill");
        inventory.RecordMonsterSkillUse(sword.MasteryId,parry.Id,AttackIdentity.Next());inventory.RecordMonsterSkillUse(sword.MasteryId,lunge.Id,AttackIdentity.Next());inventory.FlushCombatProgress();
        ClearCombat(loadout);Check(inventory.TrySelectModifier(sword,0,"riposte")&&inventory.TrySelectModifier(sword,1,"follow"),"Mastered parry and approach skill accept behavioral modifiers");
        var playerReceiver=actor.AddComponent<DamageReceiver>();Call(playerReceiver,"Awake");
        runner.TryUse(AbilitySlot.Q,Vector3.forward,Vector3.zero);runner.Tick(.01f);
        Check(!runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero),"Parrying empty air cannot open a riposte");runner.Cancel();ClearCombat(loadout);
        runner.TryUse(AbilitySlot.Q,Vector3.forward,Vector3.zero);runner.Tick(.01f);
        var parried=playerReceiver.Resolve(new DamageInfo(8,target,actor.transform.position,Vector3.back,AttackIdentity.Next()));
        Check(parried.Outcome==HitOutcome.PerfectParry||parried.Outcome==HitOutcome.Parry,"An incoming attack is actually parried");
        Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero)&&runner.Current.CounterOpener&&combo.CurrentStepIndex==1,"Confirmed parry opens an immediate heavy M1 riposte");
        ClearCombat(loadout);StartBasic();Check(!runner.Current.CounterOpener,"Riposte opportunity is single-use");ClearCombat(loadout);
        target.transform.position=actor.transform.position+Vector3.forward*20;Physics.SyncTransforms();runner.TryUse(AbilitySlot.E,Vector3.forward,Vector3.zero);runner.Tick(.1f);
        Check(!runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero),"A missed approach skill cannot open a finisher");ClearCombat(loadout);
        target.transform.position=actor.transform.position+Vector3.forward*.7f;Physics.SyncTransforms();runner.TryUse(AbilitySlot.E,Vector3.forward,Vector3.zero);runner.Tick(.1f);
        Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero)&&runner.Current.CounterOpener,"A confirmed monster hit lets the approach skill chain into heavy M1");ClearCombat(loadout);
        CompleteStep();ClearCombat(loadout);Check(inventory.TrySwap()&&inventory.TrySwap(),"Equipment can swap after the evolution checks");StartBasic();Check(!runner.Current.DodgeChain&&!runner.Current.CounterOpener,"Weapon swaps clear contextual opportunities");ClearCombat(loadout);
        Check(inventory.TryClearModifier(sword,-1),"Disabling a basic modifier retains the unevolved attack");StartBasic(true);Check(runner.Current.Began&&!runner.Current.ChargedCombo,"Basic begins normally with no modifier");ClearCombat(loadout);
        AboveHeadVfxChecks(runner,basic,actor);

        void StartBasic(bool held=false)=>Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero,held:held),"Basic starts in evolution fixture");
        void CompleteStep(){Check(runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero),"Weapon evasion starts from its equipped slot");runner.Tick(step.Duration+.01f);}
        void Impact(){runner.Tick(.12f);hitbox.EvaluateImpact();}
    }

    static void AboveHeadVfxChecks(AbilityRunner runner,AbilityDefinition basic,GameObject actor)
    {
        var prefab=Actor("Character charge prefab");prefab.AddComponent<ParticleSystem>();
        var binding=new WeaponVfxDefinition{prefab=prefab,anchor=WeaponVfxAnchor.AboveHead,hand=WeaponVfxHand.Both,faceCamera=true,fullChargeScale=2};
        basic.weaponVfx=new[]{binding};var cast=new AbilityExecution(runner,runner.GetComponent<EquipmentLoadout>().ActiveDefinition,basic,Vector3.forward,Vector3.zero);
        basic.chargeable=true;var vfx=actor.GetComponent<WeaponAbilityVfx>();Call(vfx,"Awake");
        var camera=Actor("VFX viewing camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.rotation=Quaternion.Euler(20,150,0);
        Physics.SyncTransforms();vfx.Begin(cast);var particles=actor.GetComponentsInChildren<ParticleSystem>();
        Check(particles.Length==1,"Above-head binding emits once, without requiring a weapon socket or duplicating both hands");
        var effect=particles[0].transform;
        Check(effect.position.y>actor.GetComponent<CharacterController>().bounds.max.y+.5f,"Above-head effect clears the character height");
        Check(Quaternion.Angle(effect.rotation,camera.transform.rotation)<.01f,"Above-head effect faces the viewing camera");
        Vector3 before=effect.position;actor.transform.position+=Vector3.right*3;Check(Vector3.Distance(effect.position,before+Vector3.right*3)<.001f,"Character effect follows movement");
        vfx.SetCharge(1);Near(effect.localScale.x,2,"Character charge keeps growth feedback");
        vfx.Clear();Check(actor.GetComponentsInChildren<ParticleSystem>().Length==0,"Character effects clean up on cancellation");basic.chargeable=false;
    }
}
