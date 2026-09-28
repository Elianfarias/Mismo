using System;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public enum BossAttackId
    {
        FrontSlash,
        OverheadSmash,
        StraightCharge
    }

    public enum BossPatternId
    {
        SlashOnly,
        SmashOnly,
        SlashThenSmash,
        ChargeOnly
    }

    /// <summary>Datos de un ataque del primer boss. No contiene estado runtime.</summary>
    [Serializable]
    public sealed class BossAttackDefinition
    {
        public BossAttackId id = BossAttackId.FrontSlash;
        public string label = "Tajo frontal";
        [Min(0f)] public float minRange;
        [Min(0.1f)] public float maxRange = 2f;
        [Min(0.05f)] public float windup = 0.65f;
        [Min(0.02f)] public float active = 0.15f;
        [Min(0.05f)] public float recovery = 0.65f;
        [Min(0f)] public float cooldown = 1.5f;
        [Min(0f)] public float damage = 12f;
        [Min(0f)] public float travel;
        public Vector3 halfExtents = new Vector3(1f, 0.7f, 0.9f);
        [Min(0f)] public float forwardOffset = 1f;
        public EnemyAttackAnimation animation = new EnemyAttackAnimation();

        public float MinRange => Mathf.Max(0f, minRange);
        public float MaxRange => Mathf.Max(MinRange, maxRange);
        public float Windup => Mathf.Max(0.05f, windup);
        public float Active => Mathf.Max(0.02f, active);
        public float Recovery => Mathf.Max(0.05f, recovery);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public float Damage => Mathf.Max(0f, damage);
        public float Travel => Mathf.Max(0f, travel);
        public Vector3 HalfExtents => new Vector3(Mathf.Max(0.05f, halfExtents.x), Mathf.Max(0.05f, halfExtents.y), Mathf.Max(0.05f, halfExtents.z));
        public float ForwardOffset => Mathf.Max(0f, forwardOffset);
        public string CooldownKey => "boss.attack." + id;
    }

    /// <summary>Secuencia corta reconocible. El máximo de ataques se mantiene en dos.</summary>
    [Serializable]
    public sealed class BossPatternDefinition
    {
        public BossPatternId id = BossPatternId.SlashOnly;
        public BossAttackId[] attacks = { BossAttackId.FrontSlash };
        [Min(0f)] public float normalWeight = 1f;
        [Min(0f)] public float aggressiveWeight = 1f;
        [Min(0f)] public float finalPause = 0.6f;
        public string chainSignal = "";

        public float Weight(bool aggressive) => Mathf.Max(0f, aggressive ? aggressiveWeight : normalWeight);
    }

    [CreateAssetMenu(fileName = "FirstBoss", menuName = "Mismo/Enemies/First Boss Settings")]
    public sealed class BossSettings : ScriptableObject
    {
        [Header("Encounter")]
        [Min(1f)] public float health = 300f;
        [Min(0f)] public float armor = 10f;
        [Min(1f)] public float detectionRange = 12f;
        [Min(1f)] public float loseRange = 18f;
        [Min(1f)] public float leashRange = 22f;
        [Min(0.1f)] public float memoryDuration = 3f;
        [Min(0.1f)] public float speed = 3.2f;
        [Min(0.1f)] public float positioningSpeed = 1.6f;
        [Min(0f)] public float decisionPause = 0.45f;
        [Min(0f)] public float aggressionHealthThreshold = 0.5f;
        [Min(0f)] public float aggressiveDecisionPause = 0.25f;
        [Min(0.1f)] public float sameHeightTolerance = 1.2f;
        [Min(0.1f)] public float attackDecisionInterval = 0.2f;

        [Header("Stagger")]
        [Min(0f)] public float staggerDamageThreshold = 18f;
        [Min(0.05f)] public float staggerDuration = 0.45f;
        [Min(0f)] public float staggerResistance = 2f;
        [Min(0.05f)] public float parryStaggerDuration = 1.25f;

        [Header("Attacks")]
        public BossAttackDefinition frontSlash = new BossAttackDefinition
        {
            id = BossAttackId.FrontSlash, label = "TAJO", minRange = 0f, maxRange = 2f,
            windup = 0.65f, active = 0.15f, recovery = 0.65f, cooldown = 1.5f,
            damage = 12f, halfExtents = new Vector3(1f, 0.7f, 0.9f), forwardOffset = 1f
        };

        public BossAttackDefinition overheadSmash = new BossAttackDefinition
        {
            id = BossAttackId.OverheadSmash, label = "GOLPE", minRange = 0f, maxRange = 2.5f,
            windup = 0.95f, active = 0.18f, recovery = 1.15f, cooldown = 3f,
            damage = 18f, halfExtents = new Vector3(0.55f, 0.7f, 1.15f), forwardOffset = 1.2f
        };

        public BossAttackDefinition straightCharge = new BossAttackDefinition
        {
            id = BossAttackId.StraightCharge, label = "CARGA", minRange = 3f, maxRange = 6f,
            windup = 1f, active = 0.45f, recovery = 1.25f, cooldown = 6f,
            damage = 16f, travel = 4f, halfExtents = new Vector3(0.55f, 0.7f, 0.65f), forwardOffset = 0.7f
        };

        [Header("Patterns")]
        public BossPatternDefinition[] patterns =
        {
            new BossPatternDefinition { id = BossPatternId.SlashOnly, attacks = new[] { BossAttackId.FrontSlash }, normalWeight = 35f, aggressiveWeight = 25f, finalPause = 0.6f, chainSignal = "" },
            new BossPatternDefinition { id = BossPatternId.SmashOnly, attacks = new[] { BossAttackId.OverheadSmash }, normalWeight = 25f, aggressiveWeight = 20f, finalPause = 0.6f, chainSignal = "" },
            new BossPatternDefinition { id = BossPatternId.SlashThenSmash, attacks = new[] { BossAttackId.FrontSlash, BossAttackId.OverheadSmash }, normalWeight = 40f, aggressiveWeight = 55f, finalPause = 0.6f, chainSignal = "CADENA" },
            new BossPatternDefinition { id = BossPatternId.ChargeOnly, attacks = new[] { BossAttackId.StraightCharge }, normalWeight = 100f, aggressiveWeight = 100f, finalPause = 0.6f, chainSignal = "" }
        };

        public BossAttackDefinition GetAttack(BossAttackId id)
        {
            switch (id)
            {
                case BossAttackId.OverheadSmash: return overheadSmash;
                case BossAttackId.StraightCharge: return straightCharge;
                default: return frontSlash;
            }
        }
    }
}
