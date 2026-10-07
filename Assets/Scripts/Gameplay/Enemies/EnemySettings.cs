using System;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    [CreateAssetMenu(menuName = "Mismo/Enemies/Enemy Settings")]
    public class EnemySettings : ScriptableObject
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
        [Min(.05f), Tooltip("Reacción breve al alcanzar el límite de interrupciones; luego responde sin pausa adicional.")]
        public float comboResponseStun = .12f;
        [Min(0), Tooltip("Distancia del paso de escape de humanoides tras el límite de interrupciones. Cero lo desactiva.")]
        public float comboBackstepDistance = 2.6f;
        [Min(.1f), Tooltip("Duración del paso de escape; conserva daño recibido y respeta obstáculos.")]
        public float comboBackstepDuration = .28f;
        [Header("Aturdimiento general")]
        [Min(0f)] public float staggerDamageThreshold = 15f;
        [Min(0.05f)] public float staggerDuration = 0.45f;
        [Min(0f)] public float staggerResistance = 0.75f;
        [Min(0.05f)] public float parryStaggerDuration = 1.25f;
        // Legacy fallback fields retain their serialized paths and existing tuning.
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
        [Header("Reacción sonora al recibir daño")]
        [Tooltip("Variantes de voz del enemigo. Complementan el impacto del arma; no se reproducen por veneno, sangrado o quemadura.")]
        public AudioClip[] hitSfx = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float hitSfxVolume = .65f;
    }
}
