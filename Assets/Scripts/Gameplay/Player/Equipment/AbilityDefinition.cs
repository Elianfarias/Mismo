using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    public enum AbilitySlot { Basic, Q, E, R }
    public enum AbilityPose { None, Lunge, Parry, Spin, Bow }

    [CreateAssetMenu(menuName = "Mismo/Combat/Ability")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public string displayName;
        [TextArea(2, 5)] public string description;
        [Tooltip("Clave estable para traducir el nombre y la descripción en una futura tabla de idiomas.")]
        public string localizationKey;
        public string DisplayName => string.IsNullOrEmpty(localizationKey)?Localization.GameLanguage.Text(displayName):Localization.GameLanguage.Get(localizationKey+".name",displayName);
        public string Description => string.IsNullOrEmpty(localizationKey)?Localization.GameLanguage.Text(description):Localization.GameLanguage.Get(localizationKey+".description",description);
        [Min(0)] public float preparation = .1f;
        [Min(.01f)] public float active = .15f;
        [Min(0)] public float recovery = .2f;
        [Min(0)] public float cooldown = .5f;
        [Min(0)] public float staminaCost;
        [Header("Commitment")]
        public bool chargeable;
        [Min(0)] public float maximumCharge = .9f;
        public AnimationCurve chargeDamageMultiplier = AnimationCurve.Linear(0,1,1,1.7f);
        public AnimationCurve chargePostureMultiplier = AnimationCurve.Linear(0,1,1,2);
        [Range(0,1)] public float preparationMobility = 1;
        public bool interruptible;
        public bool cancelPreparation;
        public bool cancelRecovery;
        [Min(0)] public float focusCost;
        [Tooltip("Focus ganado por enemigo al infligir daño. En áreas se aplica por pulso. Fallos y golpes bloqueados no generan Focus.")]
        [Min(0)] public float focusGainOnHit;
        public AbilityPose pose;
        public bool usesSwordCombo;
        public bool targetsGround;
        public bool aimFromCamera;
        [Min(1)] public float range = 22;
        [SerializeReference] public AbilityAction[] actions = Array.Empty<AbilityAction>();
        public float Duration => Mathf.Max(0, preparation) + Mathf.Max(.01f, active) + Mathf.Max(0, recovery);
    }

    // These objects contain configuration only. Per-cast state belongs to AbilityExecution.
    [Serializable]
    public abstract class AbilityAction
    {
        public virtual void Begin(AbilityExecution cast) { }
        public virtual void Tick(AbilityExecution cast, float dt) { }
        public virtual void End(AbilityExecution cast) { }
    }
}
