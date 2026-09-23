using System;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public enum CombatCue { None, Impact, Heavy, PostureBreak, Block, Parry }

    [Serializable]
    public sealed class CombatFeedbackCue
    {
        [Tooltip("Secuencia Feel para cámara y pantalla; se reproduce sólo en contactos confirmados cercanos al jugador.")]
        public MoreMountains.Feedbacks.MMF_Player feel;
        public GameObject prefab;
        [Min(.01f)] public float scale = .35f;
        public Vector3 rotation;
        [Tooltip("Color para materiales Mismo/Combat Impact; no modifica el material compartido.")]
        public Color tint = Color.white;
        public AudioClip sound;
        [Range(0, 1)] public float volume = .7f;
        [Range(0, .08f)] public float hitStop;
        [Min(.1f)] public float maximumLifetime = 3;
    }

    [CreateAssetMenu(menuName = "Mismo/Combat/Feedback profile")]
    public sealed class CombatFeedbackProfile : ScriptableObject
    {
        [Tooltip("Se evalúa el daño de postura solicitado, antes de bonificaciones de espalda/apertura.")]
        [Min(1)] public float heavyPostureThreshold = 15;
        public CombatFeedbackCue impact = new CombatFeedbackCue();
        public CombatFeedbackCue heavy = new CombatFeedbackCue { scale = .5f, hitStop = .035f };
        public CombatFeedbackCue postureBreak = new CombatFeedbackCue { scale = .65f, hitStop = .055f };
        public CombatFeedbackCue block = new CombatFeedbackCue { scale = .3f };
        public CombatFeedbackCue parry = new CombatFeedbackCue { scale = .5f };

        public bool IsHeavy(DamageInfo damage) => !damage.Ranged && damage.PostureDamage >= heavyPostureThreshold;
        public CombatCue Select(DamageInfo damage, HitResult result)
        {
            if (result.Outcome == HitOutcome.Block) return CombatCue.Block;
            if (result.Outcome == HitOutcome.Parry || result.Outcome == HitOutcome.PerfectParry) return CombatCue.Parry;
            if (result.Outcome != HitOutcome.Hit) return CombatCue.None;
            if (result.PostureBroken) return CombatCue.PostureBreak;
            if (result.HealthDamage <= 0) return CombatCue.None;
            return IsHeavy(damage) ? CombatCue.Heavy : CombatCue.Impact;
        }
        public CombatFeedbackCue Get(CombatCue cue) => cue == CombatCue.Impact ? impact : cue == CombatCue.Heavy ? heavy :
            cue == CombatCue.PostureBreak ? postureBreak : cue == CombatCue.Block ? block : cue == CombatCue.Parry ? parry : null;
    }
}
