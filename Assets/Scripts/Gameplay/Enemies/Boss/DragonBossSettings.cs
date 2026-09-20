using System;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public enum DragonMotion { Sleep, Scream, idle01, idle02, Walk, Run, attackHand, attackMouth, attackFlame, Defend, takeOff, FlyForward, FlyIdle, FlyGlide, FlyFlame, Land, getHit, Die }
    public enum DragonBossState { Sleeping, Roaring, Hunting, Attacking, Recovering, Defending, TakingOff, Flying, Landing, Staggered, Returning, Dead }

    [CreateAssetMenu(menuName = "Mismo/Enemies/Dragon Boss", fileName = "DragonBoss")]
    public sealed class DragonBossSettings : ScriptableObject
    {
        public string displayName = "FIRYX";
        public Color accent = new Color(1, .25f, .05f);
        [Min(1)] public float health = 1800;
        [Min(1)] public float posture = 350;
        [Min(1)] public float detectionRange = 28, leashRange = 55;
        [Min(.1f)] public float moveSpeed = 4, turnSpeed = 85;
        [Min(.1f)] public float meleeRange = 7, breathRange = 22;
        [Min(0)] public float clawDamage = 24, biteDamage = 32, breathDamage = 12;
        [Range(0, 1)] public float enrageThreshold = .5f;
        [Min(.1f)] public float recovery = 1.5f, breathCooldown = 8, flightCooldown = 22;
        [Min(0)] public float flightHeight = 5, flightDuration = 7;
        [Range(0, 1)] public float defendChance = .12f, biteChance = .45f;
        [Range(0, 1)] public float comboChance = .2f;
        [Tooltip("Una entrada por DragonMotion; también quedan disponibles en el Animator para previsualizar.")]
        public AnimationClip[] clips = new AnimationClip[18];
        public AnimationClip Clip(DragonMotion motion) => clips != null && (int)motion < clips.Length ? clips[(int)motion] : null;
        public float Duration(DragonMotion motion) => Mathf.Max(.15f, Clip(motion) != null ? Clip(motion).length : 1);
    }
}
