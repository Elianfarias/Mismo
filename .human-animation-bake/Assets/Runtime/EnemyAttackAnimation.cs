using System;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public enum EnemyAttackPhase { Preparation, Active, Recovery }

    /// <summary>Presentation data embedded in each attack's settings asset; never owns combat time.</summary>
    [Serializable]
    public sealed class EnemyAttackAnimation
    {
        [Tooltip("Clip completo del ataque. Vacío conserva la animación del Animator existente.")]
        public AnimationClip clip;
        [HideInInspector] public AnimationClip compatibleClip;
        [HideInInspector] public AnimationClip compatibleSource;
        public AnimationClip PlaybackClip => compatibleSource==clip && compatibleClip!=null ? compatibleClip : clip;
        [Tooltip("Opcional. Sin máscara, el clip afecta al cuerpo completo.")]
        public AvatarMask mask;
        [Tooltip("Inicio del golpe dentro del clip (0 a 1). La preparación reproduce desde 0 hasta este punto.")]
        [Range(0f, 1f)] public float activeStartsAt = .32f;
        [Tooltip("Inicio de recuperación dentro del clip (0 a 1). Debe ser posterior al inicio del golpe.")]
        [Range(0f, 1f)] public float recoveryStartsAt = .59f;
        [Min(0f)] public float blendSeconds = .06f;

        public float Sample(EnemyAttackPhase phase, float progress)
        {
            float start = Mathf.Clamp01(activeStartsAt);
            float end = Mathf.Clamp(recoveryStartsAt, start, 1f);
            progress = Mathf.Clamp01(progress);
            switch (phase)
            {
                case EnemyAttackPhase.Preparation: return Mathf.Lerp(0f, start, progress);
                case EnemyAttackPhase.Active: return Mathf.Lerp(start, end, progress);
                default: return Mathf.Lerp(end, 1f, progress);
            }
        }
    }
}
