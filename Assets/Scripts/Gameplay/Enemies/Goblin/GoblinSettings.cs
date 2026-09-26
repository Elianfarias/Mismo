using System;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public enum CreatureAttackKind { Melee, Projectile }
    [Serializable]
    public sealed class GoblinAttack
    {
        public string label = "Golpe";
        [Tooltip("Este ataque resiste la interrupción de combos durante preparación y ejecución. La rotura de postura y el parry siguen funcionando.")]
        public bool resistComboInterrupt;
        public bool CanBeInterrupted { get => !resistComboInterrupt; set => resistComboInterrupt = !value; }
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
        public AudioClip preparationSfx;
        [Range(0f, 1f)] public float preparationSfxVolume = 1f;
        public AudioClip executionSfx;
        [Range(0f, 1f)] public float executionSfxVolume = 1f;
        [Header("Projectile Impact")]

        [Tooltip("VFX que se instancia cuando el proyectil impacta.")]
        public GameObject projectileImpactVfx;

        [Min(0f)]
        [Tooltip("Tiempo antes de destruir automáticamente el VFX de impacto. 0 = no destruir automáticamente.")]
        public float projectileImpactVfxLifetime = 3f;

        [Tooltip("Sonido que se reproduce cuando impacta el proyectil.")]
        public AudioClip projectileImpactSfx;

        [Range(0f, 1f)]
        public float projectileImpactSfxVolume = 1f;


        [Header("Ground Pool")]

        [Tooltip("Si está activo, el proyectil puede dejar un charco cuando impacta contra una superficie suficientemente horizontal.")]
        public bool spawnGroundPool;

        [Tooltip("Prefab visual/lógico del charco.")]
        public GameObject groundPoolPrefab;

        [Min(0.1f)]
        [Tooltip("Tiempo que permanece el charco.")]
        public float groundPoolLifetime = 5f;

        [Min(0.01f)]
        [Tooltip("Multiplicador de escala del prefab del charco.")]
        public float groundPoolScale = 1f;

        [Range(0f, 1f)]
        [Tooltip("Qué tan horizontal debe ser la superficie para permitir el charco. 1 = completamente plana.")]
        public float groundPoolMinUpDot = 0.7f;

        [Header("Ground Pool Damage")]

        [Tooltip("Si está activo, el charco aplica daño periódico a los objetivos dentro de su radio.")]
        public bool groundPoolDealsDamage;

        [Min(0f)]
        [Tooltip("Daño de cada tick. 0 desactiva el daño aunque la opción esté activa.")]
        public float groundPoolDamagePerTick;

        [Min(0.05f)]
        [Tooltip("Tiempo entre ticks de daño.")]
        public float groundPoolDamageTickInterval = 0.5f;

        [Min(0.01f)]
        [Tooltip("Radio de búsqueda de objetivos para cada tick.")]
        public float groundPoolDamageRadius = 1.25f;
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
        [Header("Interrupción por combos")]
        [Tooltip("Los impactos confirmados de un combo pueden cancelar el ataque, sujetos a resistencia temporal. No se aplica a jefes.")]
        public bool interruptibleByCombos = true;
        [Min(.05f), Tooltip("Segundos sin actuar después de cada impacto del combo.")]
        public float comboHitStun = .55f;
        [Min(1), Tooltip("Mini-interrupciones permitidas antes de activar inmunidad temporal.")]
        public int maxConsecutiveInterrupts = 2;
        [Min(0), Tooltip("Segundos de inmunidad a mini-interrupciones desde la última permitida. No bloquea daño, postura, rotura ni parry.")]
        public float interruptImmunityDuration = 1.5f;
        [Header("Aturdimiento general")]
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
