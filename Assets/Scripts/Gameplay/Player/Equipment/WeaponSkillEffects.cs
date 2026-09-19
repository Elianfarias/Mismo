using System;
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
        readonly HashSet<long> basics = new HashSet<long>();
        readonly Queue<long> basicOrder = new Queue<long>();
        int hits, rhythm, sameTargetHits, twoTimes;
        UnityEngine.Object lastTarget;
        float rhythmUntil, empoweredUntil, twoTimesUntil, bucklerReady, bleedPrimedUntil;
        float bucklerThrownAt=-100,bucklerAbsentUntil;
        public bool BucklerAbsent=>Has(WeaponPassive.Buckler)&&Time.time<bucklerAbsentUntil;
        public bool BucklerAnimating=>BucklerAbsent&&Time.time-bucklerThrownAt<.5f;
        public float BucklerAnimationProgress=>Mathf.Clamp01((Time.time-bucklerThrownAt)/.5f);
        public float Barrier {get;private set;}
        float barrierUntil;
        void Awake()
        {
            loadout=GetComponent<EquipmentLoadout>();health=GetComponent<Health>();
            if(loadout!=null)loadout.Changed+=ResetEffects;
            if(health!=null)health.Died+=OnDeath;
        }
        void OnDestroy(){if(loadout!=null)loadout.Changed-=ResetEffects;if(health!=null)health.Died-=OnDeath;}
        void OnDeath(DamageInfo damage)=>ResetEffects();
        public void ResetEffects()
        {
            hits=rhythm=sameTargetHits=twoTimes=0;lastTarget=null;
            rhythmUntil=empoweredUntil=twoTimesUntil=bleedPrimedUntil=0;Barrier=0;basics.Clear();basicOrder.Clear();
            bucklerAbsentUntil=0;
            // Keep buckler cooldown across equipment changes.
        }
        public bool Has(WeaponPassive passive)
        {
            if(loadout==null)return false;
            for(int i=1;i<=3;i++)if(loadout.GetAbility((AbilitySlot)i)?.passive==passive)return true;
            return false;
        }
        public float SpeedBonus => Has(WeaponPassive.Rhythm)&&Time.time<rhythmUntil?rhythm*.04f:0;
        public void Empower()=>empoweredUntil=Time.time+4;
        public void TwoTimes(){twoTimes=2;twoTimesUntil=Time.time+5;}
        public void PrimeBleed()=>bleedPrimedUntil=Time.time+4;
        public float BasicMultiplier(long attack,Vector3 target,Component receiver=null)
        {
            float value=1;
            if(Has(WeaponPassive.SwordTip)&&Vector3.ProjectOnPlane(target-transform.position,Vector3.up).magnitude>=1.25f)value*=1.25f;
            if(Has(WeaponPassive.ThirdArrow)&&(hits+1)%3==0)value*=1.8f;
            if(Time.time<empoweredUntil)value*=1.5f;
            if(Time.time<twoTimesUntil&&twoTimes==1)value*=1.75f;
            if(Has(WeaponPassive.Finisher)&&sameTargetHits>=3&&lastTarget==receiver)value*=1.5f;
            if(Has(WeaponPassive.Verdugo)&&receiver!=null&&receiver.GetComponent<CombatAilment>()?.Poisoned==true)value*=1.2f;
            return value;
        }
        public void BasicHit(long attack,Component target)
        {
            if(!basics.Add(attack))return;
            basicOrder.Enqueue(attack);if(basicOrder.Count>128)basics.Remove(basicOrder.Dequeue());
            hits++;empoweredUntil=0;
            if(Time.time<twoTimesUntil&&twoTimes>0)
            {
                if(twoTimes==2)CombatAilment.Slow(target.gameObject,.5f,2);
                twoTimes--;
            }
            if(Time.time<bleedPrimedUntil)
            {
                CombatAilment.Poison(target.gameObject,gameObject,loadout?.ActiveDefinition?.MasteryId,1,5);
                bleedPrimedUntil=0;
            }
            if(Has(WeaponPassive.FiloCruel)&&target.GetComponent<CombatAilment>()?.Poisoned==true)
                GetComponent<CombatState>()?.Reward(1,"FILO CRUEL");
            if(Time.time>=rhythmUntil)rhythm=0;
            rhythm=Mathf.Min(5,rhythm+1);rhythmUntil=Time.time+2;
            sameTargetHits=lastTarget==target?sameTargetHits+1:1;lastTarget=target;
            if(sameTargetHits>3)sameTargetHits=0;
        }
        public bool BucklerAvailable => Has(WeaponPassive.Buckler)&&Time.time>=bucklerReady;
        public bool TryThrowBuckler(Vector3 direction)
        {
            if(!BucklerAvailable)return false;
            bucklerReady=Time.time+8;
            bucklerThrownAt=Time.time;bucklerAbsentUntil=Time.time+5;
            var weapon=loadout.ActiveDefinition;
            var inventory=GetComponent<Inventory.PlayerInventory>();
            var projectile=ProjectileInstance.Spawn(gameObject,transform.position+Vector3.up,direction,12*(inventory?.DamageMultiplier(weapon)??1),18,8,.2f,weapon.SecondaryVisualPrefab,weaponFamilyId:weapon.MasteryId);
            projectile.BasicEffects=this;
            projectile.OnImpact=(target,point)=>BucklerPickup.Spawn(this,point);
            return true;
        }
        public void GainBarrier(float amount,float duration)
        {
            if(health==null||health.IsDead||!Has(WeaponPassive.Buckler))return;
            bucklerAbsentUntil=0;
            Barrier=Mathf.Max(Barrier,amount);barrierUntil=Time.time+duration;
            GetComponent<CombatState>()?.Reward(0,"BROQUEL · BARRERA");
        }
        public float Absorb(float damage,Vector3 direction)
        {
            var motor=GetComponent<Movement.PlayerMotor>();
            if(Has(WeaponPassive.Coverage)&&loadout.Runner.Current?.Definition.usesSwordCombo==true&&Vector3.Dot(motor!=null?motor.Facing:transform.forward,-direction)>.3f)damage*=.8f;
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
