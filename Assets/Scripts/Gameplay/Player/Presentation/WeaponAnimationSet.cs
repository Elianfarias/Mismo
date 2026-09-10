using System;
using Mismo.Gameplay.Player.Equipment;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public enum CombatAnimationPhase { Preparation, Active, Recovery, Combo }
    public enum ActionMaskMode { InheritFamily, FullBody, Custom }

    public readonly struct CombatAnimationFrame
    {
        public readonly AbilityDefinition Ability;
        public readonly CombatAnimationPhase Phase;
        public readonly float Progress;
        public readonly int ComboIndex;
        public readonly long ExecutionId;
        public CombatAnimationFrame(AbilityDefinition ability,CombatAnimationPhase phase,float progress,int comboIndex,long executionId)
        { Ability=ability;Phase=phase;Progress=Mathf.Clamp01(progress);ComboIndex=comboIndex;ExecutionId=executionId; }
    }

    [Serializable]
    public sealed class AbilityAnimationBinding
    {
        public AbilityDefinition ability;
        public AnimationClip clip;
        [Tooltip("Inherit Family: usa la máscara de la familia. Full Body: cuerpo completo. Custom: máscara propia.")]
        public ActionMaskMode maskMode;
        [Tooltip("Sólo se usa en modo Custom. Si falta, se hereda la máscara de la familia.")]
        public AvatarMask customMask;
        public AvatarMask ResolveMask(AvatarMask familyMask) => maskMode==ActionMaskMode.FullBody ? null
            : maskMode==ActionMaskMode.Custom && customMask!=null ? customMask : familyMask;
        [Tooltip("Clips por etapa del combo; no están limitados a tres.")]
        public AnimationClip[] comboClips = Array.Empty<AnimationClip>();
        [Range(0,1)] public float activeStartsAt=.35f;
        [Range(0,1)] public float recoveryStartsAt=.7f;
        [Min(0)] public float blendSeconds=.06f;
        public bool TrySample(CombatAnimationFrame frame,out AnimationClip selected,out float normalized)
        {
            selected=clip; normalized=0;
            if(frame.Phase==CombatAnimationPhase.Combo)
            {
                selected=comboClips!=null && frame.ComboIndex>=0 && frame.ComboIndex<comboClips.Length ? comboClips[frame.ComboIndex] : null;
                normalized=frame.Progress;
            }
            else
            {
                float start=Mathf.Clamp01(activeStartsAt),end=Mathf.Clamp(recoveryStartsAt,start,1);
                normalized=frame.Phase==CombatAnimationPhase.Preparation ? Mathf.Lerp(0,start,frame.Progress)
                    : frame.Phase==CombatAnimationPhase.Active ? Mathf.Lerp(start,end,frame.Progress)
                    : Mathf.Lerp(end,1,frame.Progress);
            }
            return selected!=null;
        }
    }

    [CreateAssetMenu(menuName="Mismo/Player/Weapon Animation Set")]
    public sealed class WeaponAnimationSet : ScriptableObject
    {
        [Tooltip("Opcional: variantes del movimiento común de esta familia.")]
        public AnimatorOverrideController locomotion;
        [Tooltip("Opcional: limita los clips de acción a estos huesos. Sin máscara afectan al cuerpo completo.")]
        public AvatarMask actionMask;
        public AbilityAnimationBinding[] actions=Array.Empty<AbilityAnimationBinding>();
        public AbilityAnimationBinding Find(AbilityDefinition ability)
        {
            if(ability!=null && actions!=null)foreach(var binding in actions)
                if(binding!=null && binding.ability==ability)return binding;
            return null;
        }
    }
}
