using System;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public enum CreatureAttackKind { Melee, Projectile }
    [Serializable]
    public sealed class GoblinAttack
    {
        public string label = "Golpe";
        [Min(0.1f)] public float range = 1.9f;
        [Min(0.05f)] public float windup = 0.65f;
        [Min(0.02f)] public float active = 0.18f;
        [Min(0.05f)] public float recovery = 0.85f;
        [Min(0f)] public float damage = 12f;
        [Min(0f)] public float travel = 0f;
        public Vector3 halfExtents = new Vector3(0.65f, 0.65f, 0.8f);
        [Min(0f)] public float forwardOffset = 0.9f;
        public EnemyAttackAnimation animation = new EnemyAttackAnimation();
        [Header("Optional generic creature action")]
        public bool enabled=true;
        [Min(0)] public float minimumRange;
        [Min(0)] public float cooldown=3,weight=1;
        public CreatureAttackKind kind;
        public float hitHeight=.85f;
        [Range(0,1)] public float damageStartsAt;
        public AnimationClip preparationClip,recoveryClip;
        [Min(0)] public float preparationDuration;
        [Range(0,1)] public float preparationEndNormalized=1;
        public bool loopActiveAnimation;
        public GameObject projectileVisual;
        public string projectileSocket="Rock_Carry";
        [Min(0)] public float grabAt=.833333f;
        [Min(.1f)] public float projectileSpeed=12,projectileRange=30,projectileRadius=.6f;
    }

    [CreateAssetMenu(menuName = "Mismo/Enemies/Goblin Settings")]
    public class GoblinSettings : ScriptableObject
    {
        [Min(1f)] public float health = 80f;
        [Min(1f)] public float detectionRange = 12f;
        [Min(1f)] public float loseRange = 18f;
        [Min(1f)] public float leashRange = 22f;
        [Min(0.1f)] public float memoryDuration = 3f;
        [Min(0.1f)] public float speed = 3.2f;
        [Min(0.1f)] public float positioningSpeed = 1.6f;
        [Min(0f)] public float decisionPause = 0.65f;
        [Min(0f)] public float chargeCooldown = 5f;
        [Min(0f)] public float staggerDamageThreshold = 15f;
        [Min(0.05f)] public float staggerDuration = 0.45f;
        [Min(0f)] public float staggerResistance = 0.75f;
        [Min(0.05f)] public float parryStaggerDuration = 1.25f;
        public GoblinAttack slash = new GoblinAttack();
        public GoblinAttack charge = new GoblinAttack
        {
            label = "Carga", range = 5f, windup = 1f, active = 0.4f,
            recovery = 1.2f, damage = 20f, travel = 3.4f,
            halfExtents = new Vector3(0.5f, 0.65f, 0.65f), forwardOffset = 0.7f
        };
        [Header("Generic creatures (empty list preserves goblin behavior)")]
        public string displayName="Goblin";
        public bool isBoss;
        [Min(1)] public float posture=70;
        [Min(.1f)] public float sightHeight=.9f,allowedHeightDifference=1.2f;
        [Min(0)] public float preferredRange=1.7f;
        public GoblinAttack[] attacks=Array.Empty<GoblinAttack>();
    }
}
