using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mismo.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class DamageReceiver : MonoBehaviour, IDamageReceiver
    {
        [SerializeField] Health health;
        [SerializeField] Invulnerability invulnerability;
        [SerializeField] SwordParry swordParry;
        [SerializeField, Min(0)] float invulnerabilityAfterHit=.18f;
        CombatState state;DefenseWindow defense;
        readonly HashSet<long> resolved=new HashSet<long>();readonly Queue<long> order=new Queue<long>();
        public Health Health=>health;
        public Invulnerability Invulnerability=>invulnerability;
        public SwordParry SwordParry=>swordParry;
        public HitResult LastResult {get;private set;}
        public event Action<DamageInfo,HitResult> Resolved;
        void Awake()
        {
            if(health==null)health=GetComponent<Health>();
            if(invulnerability==null)invulnerability=GetComponent<Invulnerability>();
            if(swordParry==null)swordParry=GetComponentInChildren<SwordParry>();
            state=GetComponent<CombatState>()??gameObject.AddComponent<CombatState>();
            defense=GetComponent<DefenseWindow>()??gameObject.AddComponent<DefenseWindow>();
        }
        public bool ReceiveDamage(DamageInfo damage)=>Resolve(damage).Outcome==HitOutcome.Hit;
        public HitResult Resolve(DamageInfo damage)
        {
            if(health==null||health.IsDead||damage.Amount<=0||damage.Source!=null&&damage.Source.transform.root==transform.root)return new HitResult(HitOutcome.Ignored);
            if(damage.AttackId!=0)
            {
                if(!resolved.Add(damage.AttackId))return new HitResult(HitOutcome.Ignored);
                order.Enqueue(damage.AttackId);if(order.Count>256)resolved.Remove(order.Dequeue());
            }
            GetComponent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>()?.MarkCombat();
            damage.Source?.GetComponentInParent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>()?.MarkCombat();
            var rules=CombatRules.Current;
            var attacker=damage.Source!=null?damage.Source.GetComponentInParent<CombatState>():null;
            var outcome=defense.Resolve(damage);
            if(outcome==HitOutcome.Block){state.Reward(0,"BLOQUEO");return Publish(damage,new HitResult(outcome));}
            if(outcome==HitOutcome.Parry||outcome==HitOutcome.PerfectParry)
            {
                bool perfect=outcome==HitOutcome.PerfectParry;
                bool attackerWasBroken = attacker != null && attacker.Broken;
                attacker?.DamagePosture(perfect?rules.perfectParryPosture:rules.parryPosture);
                if (Application.isPlaying && attacker != null && !attackerWasBroken && attacker.Broken)
                {
                    var profile = GetComponent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>()?.ActiveDefinition?.FeedbackProfile;
                    if (profile != null) Mismo.Gameplay.Player.Presentation.CombatImpactPool.Instance.Play(profile.postureBreak, attacker.transform.position + Vector3.up, -damage.Direction);
                }
                if(perfect){state.Reward(rules.perfectFocus,"PARRY PERFECTO");CombatTimeFeedback.PerfectDefense();}
                else state.Reward(0,"PARRY");
                swordParry?.ResolveFeedback(damage);
                var defendingPlayer=GetComponent<Mismo.Gameplay.Player.Equipment.Inventory.PlayerInventory>();
                var defendingWeapon=GetComponent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>()?.ActiveDefinition;
                if(defendingPlayer!=null&&defendingWeapon!=null)
                    damage.Source?.GetComponentInParent<ICombatContribution>()?.RecordDefense(defendingPlayer,defendingWeapon.MasteryId);
                damage.Source?.GetComponentInParent<IParryResponder>()?.OnAttackParried(damage);
                return Publish(damage,new HitResult(outcome));
            }
            if(outcome==HitOutcome.Dodge||outcome==HitOutcome.PerfectDodge)
            {
                if(outcome==HitOutcome.PerfectDodge){state.Reward(rules.perfectFocus,"ESQUIVA PERFECTA");CombatTimeFeedback.PerfectDodge();}
                return Publish(damage,new HitResult(outcome));
            }
            // Compatibility for old scenes that open the sword's legacy window directly.
            if(!defense.HasParry&&swordParry!=null&&swordParry.TryParry(damage))
            {state.Reward(0,"PARRY");return Publish(damage,new HitResult(HitOutcome.Parry));}
            if(invulnerability!=null&&invulnerability.IsInvulnerable)return Publish(damage,new HitResult(HitOutcome.Invulnerable));
            bool opening=state.Opening,back=false;float multiplier=1;
            if(state.UsesPosture&&!damage.Area)
            {
                Vector3 incoming=Vector3.ProjectOnPlane(-damage.Direction,Vector3.up).normalized;
                float dot=Vector3.Dot(transform.forward,incoming);back=incoming.sqrMagnitude>.001f&&dot<-.5f;
                multiplier=back?rules.backDamage:dot>.5f?rules.frontalDamage:1;
            }
            if(damage.Ranged&&!damage.Area)multiplier*=rules.RangedMultiplier(Vector3.Distance(damage.Origin,damage.HitPoint));
            float amount=damage.Amount*multiplier;
            var inventory=GetComponent<Mismo.Gameplay.Player.Equipment.Inventory.PlayerInventory>();
            if(inventory!=null&&inventory.IsReady)amount*=inventory.IncomingDamageMultiplier;
            else{var ailment=GetComponent<CombatAilment>();if(ailment!=null)amount*=ailment.IncomingDamageMultiplier;}
            var skillEffects=GetComponent<Mismo.Gameplay.Player.Equipment.WeaponSkillEffects>();
            if(skillEffects!=null)amount=skillEffects.Absorb(amount,damage.Direction);
            float previousHealth=health.Current;
            health.ApplyDamage(new DamageInfo(amount,damage.Source,damage.HitPoint,damage.Direction,damage.AttackId));
            float healthDamage=previousHealth-health.Current;
            bool wasBroken = state.Broken;
            float posture=state.DamagePosture(damage.PostureDamage*(back?rules.backPosture:1)*(opening?rules.openingPosture:1));
            if(healthDamage>0 && damage.FocusGainOnHit>0)attacker?.Reward(damage.FocusGainOnHit,"IMPACTO");
            if(attacker!=null&&(back||opening))attacker.Reward(back?rules.backFocus:rules.openingFocus,back?"ESPALDA":"APERTURA");
            GetComponent<Mismo.Gameplay.Player.Equipment.AbilityRunner>()?.Interrupt();
            if(invulnerability!=null)invulnerability.StartWindow(invulnerabilityAfterHit);
            return Publish(damage,new HitResult(HitOutcome.Hit,healthDamage,posture,back,!wasBroken && state.Broken));
        }
        HitResult Publish(DamageInfo damage,HitResult result)
        {
            LastResult=result;
            if (Application.isPlaying) Mismo.Gameplay.Player.Presentation.CombatImpactPool.Confirm(this, damage, result);
            Resolved?.Invoke(damage,result);return result;
        }
    }
}
