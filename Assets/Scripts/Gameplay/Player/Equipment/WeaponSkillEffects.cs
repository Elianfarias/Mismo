using System;
using System.Collections;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    // State belongs to the actor, never to shared ability assets.
    [DisallowMultipleComponent]
    public sealed class WeaponSkillEffects : MonoBehaviour
    {
        EquipmentLoadout loadout;
        Health health;
        Movement.Stamina stamina;
        readonly HashSet<long> basics = new HashSet<long>();
        readonly Queue<long> basicOrder = new Queue<long>();
        int hits, rhythm, sameTargetHits, twoTimes;
        UnityEngine.Object lastTarget;
        float rhythmUntil, empoweredUntil, twoTimesUntil, bucklerReady, bleedPrimedUntil;
        // Tajo sangrante's bleed, copied from its action when it primes the next basic.
        float primedBleedDamage=3,primedBleedSeconds=5;
        // Cuarto corte: consecutive basics that landed on the same target (BasicHit runs once per swing).
        Component streakTarget;int streak;float streakAt;
        float bucklerThrownAt=-100,bucklerAbsentUntil,offhandAbsentUntil;
        float berserkUntil,berserkStart,berserkDamage,berserkSpeed,berserkFocus,berserkStamina=1,berserkArmor=1,berserkLifeSteal;
        public bool BucklerAbsent=>Has(WeaponPassive.Buckler)&&Time.time<bucklerAbsentUntil;
        // The off-hand piece is out of the hand (thrown buckler or thrown axe).
        public bool OffhandAbsent=>BucklerAbsent||Time.time<offhandAbsentUntil;
        public bool Berserking=>Time.time<berserkUntil;
        public float DamageBonus=>Berserking?berserkDamage:0;
        public float ArmorMultiplier=>Berserking?berserkArmor:1;
        public bool BucklerAnimating=>BucklerAbsent&&Time.time-bucklerThrownAt<.5f;
        public float BucklerAnimationProgress=>Mathf.Clamp01((Time.time-bucklerThrownAt)/.5f);
        public float Barrier {get;private set;}
        float barrierUntil;
        AbilityExecution empoweredCast,twoTimesCast,bleedCast,berserkCast,chainCast;
        // Remate's basic: its damage multiplier and how long it waits for the swing to land.
        float chainBoost=1,chainBoostUntil;
        // The basic step in progress was turned into a double strike by Doble filo.
        bool doubleEdgeStep;
        void Awake()
        {
            loadout=GetComponent<EquipmentLoadout>();health=GetComponent<Health>();stamina=GetComponent<Movement.Stamina>();
            if(loadout!=null)loadout.Changed+=ResetEffects;
            if(health!=null)health.Died+=OnDeath;
        }
        void OnDestroy(){if(loadout!=null)loadout.Changed-=ResetEffects;if(health!=null)health.Died-=OnDeath;}
        void OnDeath(DamageInfo damage)=>ResetEffects();
        public void ResetEffects()
        {
            StopAllCoroutines();
            hits=rhythm=sameTargetHits=twoTimes=0;lastTarget=null;ClearStreak();
            rhythmUntil=empoweredUntil=twoTimesUntil=bleedPrimedUntil=chainBoostUntil=0;Barrier=0;basics.Clear();basicOrder.Clear();
            bucklerAbsentUntil=offhandAbsentUntil=berserkUntil=0;
            empoweredCast=twoTimesCast=bleedCast=berserkCast=chainCast=null;doubleEdgeStep=false;
            if(stamina!=null)stamina.CostMultiplier=1;
            // Keep buckler cooldown across equipment changes.
        }
        public bool Has(WeaponPassive passive)
        {
            if(loadout==null)return false;
            for(int i=1;i<=3;i++)if(loadout.GetAbility((AbilitySlot)i)?.passive==passive)return true;
            return false;
        }
        AbilityDefinition Passive(WeaponPassive passive)
        {
            if(loadout==null)return null;
            for(int i=1;i<=3;i++){var ability=loadout.GetAbility((AbilitySlot)i);if(ability?.passive==passive)return ability;}return null;
        }
        float Power(AbilityDefinition ability)=>GetComponent<Inventory.PlayerInventory>()?.AbilityDamageMultiplier(loadout.ActiveDefinition,ability)??1;
        void Credit(Component target,AbilityDefinition ability,long useId,string family=null)
        {
            if(ability!=null)target.GetComponentInParent<ICombatContribution>()?.RecordSkillUse(GetComponent<Inventory.PlayerInventory>(),family??loadout.ActiveDefinition.MasteryId,ability.Id,useId);
        }
        public float SpeedBonus => (Has(WeaponPassive.Rhythm)&&Time.time<rhythmUntil?rhythm*.04f*Power(Passive(WeaponPassive.Rhythm)):0)+(Berserking?berserkSpeed:0)+RisingFurySpeed;
        // Furia creciente: the attack speed keeps growing for every second the mode has lasted.
        float RisingFurySpeed
        {
            get
            {
                var modifier=berserkCast?.Modifier;
                if(!Berserking||modifier==null||modifier.behavior!=AbilityModifierBehavior.RisingFury)return 0;
                return modifier.Amount(berserkCast.ModifierRank)*(Time.time-berserkStart);
            }
        }
        public void Empower(AbilityExecution cast=null){empoweredUntil=Time.time+4;empoweredCast=cast;}
        public void TwoTimes(AbilityExecution cast=null){twoTimes=2;twoTimesUntil=Time.time+5;twoTimesCast=cast;}
        public void PrimeBleed(AbilityExecution cast=null,float damagePerSecond=3,float bleedSeconds=5,float markSeconds=4)
        {bleedPrimedUntil=Time.time+markSeconds;bleedCast=cast;primedBleedDamage=damagePerSecond;primedBleedSeconds=bleedSeconds;}
        public void ThrowOffhand(float seconds)=>offhandAbsentUntil=Time.time+Mathf.Max(0,seconds);
        public void ReturnOffhand()=>offhandAbsentUntil=0;
        public void Berserk(BerserkAction mode,AbilityExecution cast=null)
        {
            // Trained modifiers strengthen every bonus; the armor penalty stays as authored.
            float power=Power(cast?.Definition);berserkCast=cast;
            berserkStart=Time.time;berserkUntil=Time.time+mode.duration;
            berserkDamage=mode.damageBonus*power;berserkSpeed=mode.attackSpeedBonus*power;berserkFocus=mode.focusPerBasic*power;
            berserkStamina=mode.staminaCostMultiplier<1?Mathf.Max(0,1-(1-mode.staminaCostMultiplier)*power):mode.staminaCostMultiplier;
            berserkArmor=mode.armorMultiplier;berserkLifeSteal=Mathf.Min(1,mode.lifeSteal*power);
            GetComponent<CombatState>()?.Reward(0,"MODO BERSERKER");
        }
        void Update()
        {
            if(stamina!=null)stamina.CostMultiplier=Berserking?berserkStamina:1;
        }
        public void BasicDamageDealt(float healthDamage)
        {
            if(Berserking&&berserkLifeSteal>0&&healthDamage>0&&health!=null&&!health.IsDead)health.Heal(healthDamage*berserkLifeSteal);
        }
        // Matanza: every enemy defeated while the Berserker lasts heals a share of the maximum life.
        public void TargetDefeated()
        {
            var modifier=berserkCast?.Modifier;
            if(!Berserking||modifier==null||modifier.behavior!=AbilityModifierBehavior.Slaughter||health==null||health.IsDead)return;
            health.Heal(health.Maximum*modifier.Amount(berserkCast.ModifierRank));
        }
        public void TargetDefeatedOrOpened(Component target=null,long attackId=0)
        {
            var ability=Passive(WeaponPassive.Bloodthirst);
            if(ability==null||health!=null&&health.IsDead)return;
            float share=Mathf.Min(1,ability.passiveValue*Power(ability));
            if(stamina!=null)stamina.Restore(stamina.Maximum*share);
            GetComponent<CombatState>()?.Reward(CombatState.MaximumFocus*share,"SED DE SANGRE");
            // Sed insaciable: a wounded player also recovers life.
            var thirst=Evolution(ability,AbilityModifierBehavior.Insatiable,out int rank);
            if(thirst!=null&&health!=null&&health.Current<health.Maximum*thirst.threshold)health.Heal(health.Maximum*thirst.Amount(rank));
            if(target!=null)Credit(target,ability,attackId!=0?attackId:AttackIdentity.Next());
        }
        // The evolution this ability has chosen, if it is `behavior`; `rank` is its trained rank.
        AbilityModifierDefinition Evolution(AbilityDefinition ability,AbilityModifierBehavior behavior,out int rank)
        {
            rank=0;
            var weapon=loadout!=null?loadout.ActiveDefinition:null;var inventory=GetComponent<Inventory.PlayerInventory>();
            var modifier=ability!=null&&weapon!=null&&inventory!=null?inventory.AbilityBehavior(weapon,ability,out rank):null;
            return modifier!=null&&modifier.behavior==behavior?modifier:null;
        }
        // Corte cruzado: the second axe of a double strike hits harder.
        public float SecondStrikeMultiplier()
        {
            var cross=Evolution(Passive(WeaponPassive.DoubleEdge),AbilityModifierBehavior.CrossCut,out int rank);
            return cross!=null?1+cross.Amount(rank):1;
        }
        // Remate: the basic that follows the landing hits for `multiplier` times its damage, once.
        public void BoostNextBasic(AbilityExecution cast,float multiplier){chainBoost=multiplier;chainBoostUntil=Time.time+1.5f;chainCast=cast;}
        // Rolled when a basic step begins; a thrown off-hand axe cannot join the swing.
        public bool RollDoubleEdge()
        {
            var ability=Passive(WeaponPassive.DoubleEdge);
            doubleEdgeStep=ability!=null&&!OffhandAbsent&&UnityEngine.Random.value<Mathf.Min(1,ability.passiveValue*Power(ability));
            return doubleEdgeStep;
        }
        // The other axe also landed on a target of this step: that extra hit is Doble filo's contribution.
        public void SecondStrikeHit(long attack,Component target)
        {
            if(!doubleEdgeStep||target is DamageReceiver receiver&&receiver.LastResult.HealthDamage<=0)return;
            Credit(target,Passive(WeaponPassive.DoubleEdge),attack);
        }
        public float BasicMultiplier(long attack,Vector3 target,Component receiver=null)
        {
            float value=1;
            if(Has(WeaponPassive.SwordTip)&&Vector3.ProjectOnPlane(target-transform.position,Vector3.up).magnitude>=1.25f)value*=1+.25f*Power(Passive(WeaponPassive.SwordTip));
            if(Has(WeaponPassive.ThirdArrow)&&(hits+1)%3==0)value*=1+.8f*Power(Passive(WeaponPassive.ThirdArrow));
            if(Time.time<empoweredUntil)value*=1+.5f*Power(empoweredCast?.Definition);
            if(Time.time<twoTimesUntil&&twoTimes==1)value*=1+.75f*Power(twoTimesCast?.Definition);
            if(Has(WeaponPassive.Finisher)&&sameTargetHits>=3&&lastTarget==receiver)value*=1+.5f*Power(Passive(WeaponPassive.Finisher));
            var executioner=Passive(WeaponPassive.Verdugo);
            if(executioner!=null&&receiver!=null&&receiver.GetComponent<CombatAilment>()?.Poisoned==true)
            {
                float bonus=executioner.passiveValue*Power(executioner);
                // Ejecución: a bleeding target that is low on life takes the stronger bonus.
                var execution=Evolution(executioner,AbilityModifierBehavior.Execution,out int rank);
                var life=execution!=null?receiver.GetComponent<Health>():null;
                if(life!=null&&life.Current<=life.Maximum*execution.Amount(rank))bonus=Mathf.Max(bonus,execution.bonus);
                value*=1+bonus;
            }
            if(Time.time<chainBoostUntil)value*=chainBoost;
            // Family trait (Hacha sola): heavier basics. Abilities never come through here.
            value*=1+(loadout!=null?loadout.ActiveDefinition?.family?.basicDamageBonus??0:0);
            return value;
        }
        public void BasicHit(long attack,Component target)
        {
            if(target is DamageReceiver receiver&&receiver.LastResult.HealthDamage<=0)return;
            if(!basics.Add(attack))return;
            basicOrder.Enqueue(attack);if(basicOrder.Count>128)basics.Remove(basicOrder.Dequeue());
            if((hits+1)%3==0)Credit(target,Passive(WeaponPassive.ThirdArrow),attack);
            if(Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up).magnitude>=1.25f)Credit(target,Passive(WeaponPassive.SwordTip),attack);
            if(sameTargetHits>=3&&lastTarget==target)Credit(target,Passive(WeaponPassive.Finisher),attack);
            Credit(target,Passive(WeaponPassive.Rhythm),attack);
            if(Time.time<empoweredUntil&&empoweredCast!=null)Credit(target,empoweredCast.Definition,empoweredCast.AttackId,empoweredCast.WeaponFamilyId);
            // Verdugo scaled this hit only if the target was bleeding before it landed.
            if(target.GetComponent<CombatAilment>()?.Poisoned==true)Credit(target,Passive(WeaponPassive.Verdugo),attack);
            if(Berserking&&berserkCast!=null)Credit(target,berserkCast.Definition,berserkCast.AttackId,berserkCast.WeaponFamilyId);
            if(Time.time<chainBoostUntil&&chainCast!=null)Credit(target,chainCast.Definition,chainCast.AttackId,chainCast.WeaponFamilyId);
            hits++;empoweredUntil=0;chainBoostUntil=0;
            if(Time.time<twoTimesUntil&&twoTimes>0)
            {
                if(twoTimesCast!=null)Credit(target,twoTimesCast.Definition,twoTimesCast.AttackId,twoTimesCast.WeaponFamilyId);
                if(twoTimes==2)CombatAilment.Slow(target.gameObject,.5f,2);
                twoTimes--;
            }
            if(Time.time<bleedPrimedUntil)
            {
                var bleed=bleedCast?.Definition;
                // The cast's multiplier already carries weapon level and the ability's trained Potencia.
                float rusty=bleedCast?.Modifier?.behavior==AbilityModifierBehavior.RustyEdge?1+bleedCast.Modifier.Amount(bleedCast.ModifierRank):1;
                float scale=bleedCast!=null?bleedCast.DamageMultiplier:1;
                CombatAilment.Poison(target.gameObject,gameObject,bleedCast?.WeaponFamilyId??loadout?.ActiveDefinition?.MasteryId,primedBleedDamage*scale*rusty,primedBleedSeconds,bleed?.Id,bleedCast?.AttackId??0);
                if(bleedCast!=null)Credit(target,bleed,bleedCast.AttackId,bleedCast.WeaponFamilyId);
                bleedPrimedUntil=0;
            }
            BasicStreak(target,attack);
            var cruel=Passive(WeaponPassive.FiloCruel);
            if(cruel!=null)
            {
                var ailment=target.GetComponent<CombatAilment>();
                float focus=ailment?.Poisoned==true?cruel.passiveValue*Power(cruel):0;
                // Sangre fría: a target whose armor is torn also feeds the Focus.
                int coldRank=0;
                var cold=ailment?.ArmorWeakened==true?Evolution(cruel,AbilityModifierBehavior.ColdBlood,out coldRank):null;
                if(cold!=null)focus=Mathf.Max(focus,cold.Amount(coldRank));
                if(focus>0){GetComponent<CombatState>()?.Reward(focus,"FILO CRUEL");Credit(target,cruel,attack);}
            }
            if(Berserking&&berserkFocus>0)GetComponent<CombatState>()?.Reward(berserkFocus,"BERSERKER");
            if(Time.time>=rhythmUntil)rhythm=0;
            rhythm=Mathf.Min(5,rhythm+1);rhythmUntil=Time.time+2;
            sameTargetHits=lastTarget==target?sameTargetHits+1:1;lastTarget=target;
            if(sameTargetHits>3)sameTargetHits=0;
        }
        void ClearStreak(){streak=0;streakTarget=null;}
        // Basic evolutions that count swings: every Nth basic in a row on the same target either makes it bleed
        // (Cuarto corte) or unleashes extra strikes (Ráfaga). Blocked hits never reach BasicHit.
        void BasicStreak(Component target,long attack)
        {
            var weapon=loadout!=null?loadout.ActiveDefinition:null;var basic=loadout!=null?loadout.GetAbility(AbilitySlot.Basic):null;
            var inventory=GetComponent<Inventory.PlayerInventory>();
            int rank=0;
            var modifier=weapon!=null&&basic!=null&&inventory!=null?inventory.AbilityBehavior(weapon,basic,out rank):null;
            bool bleeds=modifier?.behavior==AbilityModifierBehavior.FourthCut,flurry=modifier?.behavior==AbilityModifierBehavior.Burst;
            if(!bleeds&&!flurry){ClearStreak();return;}
            if(target!=streakTarget||Time.time-streakAt>modifier.streakResetSeconds)streak=0;
            streakTarget=target;streakAt=Time.time;
            if(++streak<Mathf.Max(1,modifier.hitsRequired))return;
            streak=0;
            if(bleeds)
            {
                float damage=modifier.BleedDamage(rank)*inventory.DamageMultiplier(weapon)*Power(basic);
                CombatAilment.Poison(target.gameObject,gameObject,weapon.MasteryId,damage,modifier.BleedSeconds(rank),basic.Id,attack);
                return;
            }
            // Ráfaga: `count` extra strikes, each worth Amount(rank) of the swing.
            var step=GetComponentInChildren<BasicSwordCombo>()?.CurrentStep;
            if(step==null||!(target is IDamageReceiver))return;
            float strike=step.Damage*inventory.DamageMultiplier(weapon)*Power(basic)*modifier.Amount(rank);
            StartCoroutine(Flurry(target,modifier.count,modifier.interval,strike,weapon.MasteryId,basic.Id,attack));
        }
        // Ráfaga: quick extra hits on the target of the swing that completed the count.
        IEnumerator Flurry(Component target,int strikes,float interval,float damage,string family,string abilityId,long useId)
        {
            for(int i=0;i<strikes;i++)
            {
                yield return new WaitForSeconds(interval);
                if(target==null||health!=null&&health.IsDead)yield break;
                Vector3 point=target.transform.position+Vector3.up;
                ((IDamageReceiver)target).ReceiveDamage(new DamageInfo(damage,gameObject,point,(target.transform.position-transform.position).normalized,AttackIdentity.Next(),weaponFamilyId:family,abilityId:abilityId,abilityUseId:useId));
            }
        }
        public bool BucklerAvailable => Has(WeaponPassive.Buckler)&&Time.time>=bucklerReady;
        public bool TryThrowBuckler(Vector3 direction)
        {
            if(!BucklerAvailable)return false;
            bucklerReady=Time.time+8;
            bucklerThrownAt=Time.time;bucklerAbsentUntil=Time.time+5;
            var weapon=loadout.ActiveDefinition;
            var inventory=GetComponent<Inventory.PlayerInventory>();
            var ability=Passive(WeaponPassive.Buckler);
            var projectile=ProjectileInstance.Spawn(gameObject,transform.position+Vector3.up,direction,12*(inventory?.DamageMultiplier(weapon)??1)*Power(ability),18,8,.2f,weapon.SecondaryVisualPrefab,weaponFamilyId:weapon.MasteryId);
            projectile.AbilityId=ability?.Id;projectile.AbilityUseId=AttackIdentity.Next();
            projectile.OnImpact=(target,point)=>BucklerPickup.Spawn(this,point);
            return true;
        }
        public void GainBarrier(float amount,float duration)
        {
            if(health==null||health.IsDead||!Has(WeaponPassive.Buckler))return;
            bucklerAbsentUntil=0;
            Barrier=Mathf.Max(Barrier,amount*Power(Passive(WeaponPassive.Buckler)));barrierUntil=Time.time+duration;
            GetComponent<CombatState>()?.Reward(0,"BROQUEL · BARRERA");
        }
        public float Absorb(float damage,Vector3 direction,GameObject source=null,long attackId=0)
        {
            var motor=GetComponent<Movement.PlayerMotor>();
            if(Has(WeaponPassive.Coverage)&&loadout.Runner.Current?.Definition.usesSwordCombo==true&&Vector3.Dot(motor!=null?motor.Facing:transform.forward,-direction)>.3f)
            {
                if(source!=null)Credit(source.transform,Passive(WeaponPassive.Coverage),attackId);
                damage*=1-Mathf.Min(.8f,.2f*Power(Passive(WeaponPassive.Coverage)));
            }
            if(Time.time>=barrierUntil)Barrier=0;
            float absorbed=Mathf.Min(Barrier,damage);Barrier-=absorbed;return damage-absorbed;
        }
    }

    public sealed class BucklerPickup : MonoBehaviour
    {
        WeaponSkillEffects owner;float expires;
        public static void Spawn(WeaponSkillEffects owner,Vector3 point)
        {
            if(owner==null)return;
            var visual=owner.GetComponent<EquipmentLoadout>()?.ActiveDefinition?.SecondaryVisualPrefab;
            var go=visual!=null?Instantiate(visual):GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Broquel recuperable";
            foreach(var collider in go.GetComponentsInChildren<Collider>())Destroy(collider);
            go.transform.position=point+Vector3.up*.1f;go.transform.rotation=Quaternion.Euler(90,0,0);
            var pickup=go.AddComponent<BucklerPickup>();pickup.owner=owner;pickup.expires=Time.time+5;
        }
        void Update()
        {
            if(owner==null||Time.time>=expires){Destroy(gameObject);return;}
            if(Vector3.Distance(owner.transform.position,transform.position)<1.4f){owner.GainBarrier(25,4);Destroy(gameObject);}
        }
    }
}
