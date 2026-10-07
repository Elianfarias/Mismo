using System.Collections.Generic;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>One instance per target; each ailment owns its timer and damage attribution.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatAilment : MonoBehaviour
    {
        sealed class DamageOverTime
        {
            public StatusEffectType type;
            public float until, nextTick, damage;
            public GameObject source;
            public string family, abilityId;
            public long abilityUseId;
        }
        readonly List<DamageOverTime> effects = new List<DamageOverTime>();
        float slowUntil, slow = 1;
        Health health;
        StatusEffectVisual visual;
        public float SpeedMultiplier => Time.time < slowUntil ? slow : 1;
        public bool IsActive(StatusEffectType type) => effects.Exists(e => e.type == type && Time.time <= e.until && e.source != null);

        void Awake() => health = GetComponent<Health>();
        void OnEnable() { if (health != null) health.Died += Died; }
        void Died(DamageInfo damage) => ClearAll();

        public static void Slow(GameObject target, float multiplier, float duration)
        {
            if (target == null) return;
            var effect = target.GetComponent<CombatAilment>() ?? target.AddComponent<CombatAilment>();
            effect.slow = Time.time < effect.slowUntil ? Mathf.Min(effect.slow, multiplier) : multiplier;
            effect.slowUntil = Mathf.Max(effect.slowUntil, Time.time + duration);
        }
        public static void Poison(GameObject target, GameObject source, string family, float damage, float duration, string abilityId = null, long abilityUseId = 0) =>
            Apply(target, source, StatusEffectType.Poison, family, damage, duration, abilityId, abilityUseId);
        public static void Bleed(GameObject target, GameObject source, string family, float damage, float duration, string abilityId = null, long abilityUseId = 0) =>
            Apply(target, source, StatusEffectType.Bleed, family, damage, duration, abilityId, abilityUseId);
        public static void Burn(GameObject target, GameObject source, string family, float damage, float duration, string abilityId = null, long abilityUseId = 0) =>
            Apply(target, source, StatusEffectType.Burn, family, damage, duration, abilityId, abilityUseId);

        public static void Apply(GameObject target, GameObject source, StatusEffectType type, string family, float damage, float duration, string abilityId = null, long abilityUseId = 0)
        {
            if (target == null || source == null || !target.activeInHierarchy || type == StatusEffectType.None ||
                damage <= 0 || duration <= 0 || float.IsNaN(damage) || float.IsInfinity(damage) || float.IsNaN(duration) || float.IsInfinity(duration)) return;
            var health = target.GetComponent<Health>();
            if (health != null && health.IsDead) return;
            var owner = target.GetComponent<CombatAilment>() ?? target.AddComponent<CombatAilment>();
            if (!owner.enabled) return;
            var entry = owner.effects.Find(e => e.type == type);
            if (entry == null) { entry = new DamageOverTime { type = type, nextTick = Time.time + 1 }; owner.effects.Add(entry); }
            else if (Time.time > entry.until) entry.nextTick = Time.time + 1;
            // Reapplying refreshes one timer, preserving its next tick; different states coexist.
            entry.until = Mathf.Max(entry.until, Time.time + duration);
            entry.source = source; entry.family = family; entry.damage = damage;
            entry.abilityId = abilityId; entry.abilityUseId = abilityUseId;
            if (type == StatusEffectType.Poison)
            {
                owner.visual = target.GetComponent<StatusEffectVisual>() ?? target.AddComponent<StatusEffectVisual>();
                owner.visual.PlayPoison();
            }
        }
        void Update() => Tick(Time.time);
        void Tick(float now)
        {
            if (health != null && health.IsDead) { ClearAll(); return; }
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var entry = effects[i];
                // Include a tick at the exact end even when the frame arrives just after expiry.
                int budget = 8;
                while (entry.source != null && entry.nextTick <= now && entry.nextTick <= entry.until + .0001f && budget-- > 0)
                {
                    entry.nextTick += 1;
                    GetComponent<IDamageReceiver>()?.ReceiveDamage(new DamageInfo(entry.damage, entry.source, transform.position,
                        Vector3.zero, AttackIdentity.Next(), 0, area: true, parryable: false, weaponFamilyId: entry.family,
                        abilityId: entry.abilityId, abilityUseId: entry.abilityUseId, statusEffect: entry.type));
                    if (!isActiveAndEnabled || health != null && health.IsDead) return; // Death callbacks can clear the list.
                }
                if (entry.source == null || now >= entry.until && entry.nextTick > entry.until)
                {
                    effects.RemoveAt(i);
                    if (entry.type == StatusEffectType.Poison) visual?.Stop();
                }
            }
        }
        public void Clear(StatusEffectType type)
        {
            effects.RemoveAll(e => e.type == type);
            if (type == StatusEffectType.Poison) visual?.Stop();
        }
        public void ClearAll()
        {
            effects.Clear(); slowUntil = 0; slow = 1; visual?.Stop();
        }
        void OnDisable() { if (health != null) health.Died -= Died; ClearAll(); }
    }
}
