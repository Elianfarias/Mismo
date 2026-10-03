using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public enum SoulEaterAction { None, Bite, Tail, Breath }
    public enum SoulEaterState { Dormant, Hunting, Windup, Active, Recovery, SpecialRoar, RetreatJump, ChargeWindup, Charging, Braking, Staggered, PhaseTransition, PhaseTwoReady, Returning, Dead }

    [CreateAssetMenu(menuName = "Mismo/Enemies/SoulEater phase one", fileName = "SoulEater_PhaseOne")]
    public sealed class SoulEaterPhaseOneSettings : ScriptableObject
    {
        public string displayName = "SOUL EATER";
        public Color accent = new Color(.36f, .85f, .16f);
        [Min(1)] public float health = 1400, posture = 220;
        [Min(.1f)] public float movementSpeed = 3.4f, turnSpeed = 65, detectionRange = 22, arenaRadius = 28;
        [Min(.1f)] public float biteRange = 5.1f, tailRange = 4, breathRange = 13;
        [Range(1, 40)] public float breathHalfAngle = 14;
        [Min(.1f)] public float biteWindup = .95f, biteActive = .42f, biteRecovery = 1.25f;
        [Min(.1f)] public float tailWindup = 1.1f, tailActive = .65f, tailRecovery = 1.35f;
        [Min(.1f)] public float breathWindup = 1.5f, breathActive = 2.2f, breathRecovery = 1.7f;
        [Min(0)] public float biteDamage = 22, tailDamage = 19, breathTickDamage = 6, chargeDamage = 30;
        [Min(.05f)] public float breathTickInterval = .4f;
        [Min(0)] public float biteCooldown = 2.6f, tailCooldown = 5, breathCooldown = 8, rearDwellTime = .8f;
        [Min(.1f)] public float decisionPause = .55f, biteRadius = .85f, tailRadius = .55f;
        [Range(0,1)] public float chargeThreshold = .75f, phaseThreshold = .5f;
        [Min(.1f)] public float specialRoar = 1.65f, retreatDistance = 11, jumpHeight = 3, jumpDuration = 1.35f;
        [Min(.1f)] public float chargeWindup = 1, chargeSpeed = 14, chargeDistance = 34, brakeDuration = 1.3f;
        [Min(.1f)] public float phaseRoar = 2.6f, parryRecovery = .7f;
        public AnimationClip idle, walk, run, bite, tail, breath, roar, takeOff, land, hit, die;
        public AnimationClip chargePose, brake;

        public float Windup(SoulEaterAction a) => a == SoulEaterAction.Bite ? biteWindup : a == SoulEaterAction.Tail ? tailWindup : breathWindup;
        public float Active(SoulEaterAction a) => a == SoulEaterAction.Bite ? biteActive : a == SoulEaterAction.Tail ? tailActive : breathActive;
        public float Recovery(SoulEaterAction a) => a == SoulEaterAction.Bite ? biteRecovery : a == SoulEaterAction.Tail ? tailRecovery : breathRecovery;
        public AnimationClip Clip(SoulEaterAction a) => a == SoulEaterAction.Bite ? bite : a == SoulEaterAction.Tail ? tail : breath;
    }
}
