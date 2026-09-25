using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    public enum AbilitySlot { Basic, Q, E, R }
    public enum AbilityPose { None, Lunge, Parry, Spin, Bow }
    public enum WeaponPassive { None, ThirdArrow, SwordTip, Rhythm, Finisher, Coverage, Buckler, FiloCruel, Verdugo }

    [CreateAssetMenu(menuName = "Mismo/Combat/Ability")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public string displayName;
        [Tooltip("Imagen para el HUD y el menú de habilidades. Vacío conserva el ícono predeterminado.")]
        public Texture2D icon;
        [Tooltip("ID persistente para guardar la selección; no cambiar después de publicar.")]
        public string abilityId;
        [Tooltip("Reutiliza la animación de otra habilidad de esta familia, sin copiar su comportamiento.")]
        public AbilityDefinition animationSource;
        public WeaponPassive passive;
        public bool IsPassive => passive != WeaponPassive.None;
        public string Id => string.IsNullOrEmpty(abilityId) ? name : abilityId;
        [TextArea(2, 5)] public string description;
        [Tooltip("Clave estable para traducir el nombre y la descripción en una futura tabla de idiomas.")]
        public string localizationKey;
        public string DisplayName => string.IsNullOrEmpty(localizationKey)?Localization.GameLanguage.Text(displayName):Localization.GameLanguage.Get(localizationKey+".name",displayName);
        public string Description => string.IsNullOrEmpty(localizationKey)?Localization.GameLanguage.Text(description):Localization.GameLanguage.Get(localizationKey+".description",description);
        [Min(0)] public float preparation = .1f;
        [Min(.01f)] public float active = .15f;
        [Min(0)] public float recovery = .2f;
        [Min(0)] public float cooldown = .5f;
        [Tooltip("Coste de estamina por ejecución; en combos se cobra por cada golpe que comienza, no al encolarlo.")]
        [Min(0)] public float staminaCost;
        [Header("Commitment")]
        public bool chargeable;
        [Min(0)] public float maximumCharge = .9f;
        public AnimationCurve chargeDamageMultiplier = AnimationCurve.Linear(0,1,1,1.7f);
        public AnimationCurve chargePostureMultiplier = AnimationCurve.Linear(0,1,1,2);
        [Range(0,1)] public float preparationMobility = 1;
        [Range(0,1)] public float activeMobility = 1;
        public bool interruptible;
        [Tooltip("El daño recibido no corta la animación de esta habilidad. No afecta al daño ni a la postura.")]
        public bool unstoppable;
        public bool cancelPreparation;
        public bool cancelRecovery;
        [Min(0)] public float focusCost;
        [Tooltip("Focus ganado por enemigo al infligir daño. En áreas se aplica por pulso. Fallos y golpes bloqueados no generan Focus.")]
        [Min(0)] public float focusGainOnHit;
        [Header("Sonido de la habilidad")]
        [Tooltip("Opcional: sonido al comenzar a preparar o tensar la habilidad. Se detiene al ejecutar o cancelar. No se usa en combos sin preparación.")]
        public AudioClip preparationSfx;
        [Range(0f, 1f)] public float preparationSfxVolume = 1f;
        [Tooltip("Repetir mientras se prepara o mantiene la carga. Desactivado reproduce el clip una sola vez.")]
        public bool loopPreparationSfx;
        [Tooltip("SFX al ejecutar la fase activa. En ataques cargados suena al soltar; en combos, una vez por golpe. Vacío = sin sonido.")]
        public AudioClip executionSfx;
        [Range(0f, 1f)] public float executionSfxVolume = 1f;
        [Tooltip("Un sonido por golpe del combo, en orden: Element 0 = primero, Element 1 = segundo, etc. Un elemento sin clip usa Execution Sfx. Volumen 0 silencia ese golpe.")]
        public ComboStepSound[] comboStepSfx = Array.Empty<ComboStepSound>();
        [Header("Presentación y acciones")]
        public AbilityPose pose;
        public bool usesSwordCombo;
        [InspectorName("Golpes del combo")]
        [Tooltip("La duración controla la velocidad del clip; las ventanas se expresan de 0 a 100 %. El estado de ejecución pertenece a cada personaje.")]
        public Mismo.Gameplay.Combat.ComboStep[] comboSteps = Array.Empty<Mismo.Gameplay.Combat.ComboStep>();
        public bool targetsGround;
        public bool aimFromCamera;
        [Tooltip("Alcance de apuntado y proyectiles. Los golpes cuerpo a cuerpo configuran su área en Actions (Radius y Forward), o en Golpes del combo.")]
        [Min(1)] public float range = 22;
        [SerializeReference] public AbilityAction[] actions = Array.Empty<AbilityAction>();
        public float Duration => Mathf.Max(0, preparation) + Mathf.Max(.01f, active) + Mathf.Max(0, recovery);
    }

    [Serializable]
    public sealed class ComboStepSound
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
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
