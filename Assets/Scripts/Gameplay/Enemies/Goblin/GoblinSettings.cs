using System;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
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
    }

    [CreateAssetMenu(menuName = "Mismo/Enemies/Goblin Settings")]
    public sealed class GoblinSettings : ScriptableObject
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
    }
}
