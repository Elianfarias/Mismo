using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    public enum AbilitySlot { Basic, Q, E, R }
    public enum AbilityPose { None, Lunge, Parry, Spin, Bow }
    public enum WeaponPassive { None, ThirdArrow, SwordTip, Rhythm, Finisher, Coverage, Buckler }

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
        [Tooltip("Tercer impacto: segundos sin acertar un básico antes de perder las cargas. El tiempo se detiene durante la pausa.")]
        [Min(.1f)] public float thirdArrowResetSeconds=5;
        public bool IsPassive => passive != WeaponPassive.None;
        public string Id => string.IsNullOrEmpty(abilityId) ? name : abilityId;
        [Header("Maestría de habilidad")]
        [Tooltip("Usos efectivos contra monstruos necesarios para habilitar modificadores. Un uso cuenta una vez aunque alcance varios blancos o haga varios pulsos.")]
        [Min(1)] public int masteryUsesRequired=25;
        [Tooltip("Opciones de diseño. Vacío = sin modificadores disponibles; no se inventan efectos automáticamente.")]
        public AbilityModifierDefinition[] masteryModifiers=Array.Empty<AbilityModifierDefinition>();
        public AbilityModifierDefinition FindModifier(string id)=>string.IsNullOrEmpty(id)?null:Array.Find(masteryModifiers??Array.Empty<AbilityModifierDefinition>(),m=>m!=null&&m.id==id);
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
        [Header("VFX en el arma")]
        [Tooltip("Prefabs visuales por fase de ejecución. Preparación permanece mientras se carga; Al ejecutar crea un efecto breve al soltar. Vacío = sin VFX.")]
        public WeaponVfxDefinition[] weaponVfx = Array.Empty<WeaponVfxDefinition>();
        [Header("Presentación y acciones")]
        public AbilityPose pose;
        public bool usesSwordCombo;
        [InspectorName("Golpes del combo")]
        [Tooltip("La duración controla la velocidad del clip; las ventanas se expresan de 0 a 100 %. El estado de ejecución pertenece a cada personaje.")]
        public Mismo.Gameplay.Combat.ComboStep[] comboSteps = Array.Empty<Mismo.Gameplay.Combat.ComboStep>();
        public bool targetsGround;
        [Tooltip("Material del indicador al mantener la tecla. Trampas muestran trayectoria; áreas muestran su radio real.")]
        public Material groundIndicatorMaterial;
        public bool aimFromCamera;
        [Tooltip("Alcance de apuntado y proyectiles. Los golpes cuerpo a cuerpo configuran su área en Actions (Radius y Forward), o en Golpes del combo.")]
        [Min(1)] public float range = 22;
        [SerializeReference] public AbilityAction[] actions = Array.Empty<AbilityAction>();
        public float Duration => Mathf.Max(0, preparation) + Mathf.Max(.01f, active) + Mathf.Max(0, recovery);
    }

    [Serializable]
    public sealed class AbilityModifierDefinition
    {
        [Tooltip("ID persistente dentro de esta habilidad.")] public string id;
        public string displayName;
        [TextArea] public string description;
        [Range(1,100)] public int maxLevel=3;
        [Range(1,1000000)] public int effectiveUsesPerLevel=10;
        [Tooltip("Bonificación por rango entrenado. Rango 0 no aplica efectos.")]
        public float damagePerLevel;
        [Range(0,.2f)] public float cooldownReductionPerLevel;
        [Tooltip("Evolución jugable, activa al elegirla después de dominar la habilidad. Los campos numéricos anteriores se conservan para los modificadores antiguos.")]
        public AbilityModifierBehavior behavior;
        [Tooltip("Conserva el ID y progreso de un modificador antiguo, pero deja de ofrecerlo o aplicarlo.")]
        public bool retired;
        [Min(.1f)] public float chargeSeconds=.85f;
        [Min(1)] public float chargedPostureMultiplier=3;
        [Min(.1f)] public float followupWindow=1.4f;
        [Tooltip("Cada rango entrenado amplía la ventana para encadenar y reduce la carga en 0,05 segundos.")]
        [Min(0)] public float windowPerRank=.15f;
        [Tooltip("VFX adicionales a los de la habilidad. Se reproducen si este modificador está elegido y tiene al menos un rango entrenado.")]
        public WeaponVfxDefinition[] weaponVfx = Array.Empty<WeaponVfxDefinition>();
    }

    public enum AbilityModifierBehavior { None, ChargedCut, DodgeChain, ParryRiposte, LungeFinisher }

    public enum WeaponVfxPhase { Preparation, Execution, Active }
    public enum WeaponVfxHand { Main, Offhand, Both }
    public enum WeaponVfxAnchor { Weapon, Character, AboveHead }

    [Serializable]
    public sealed class WeaponVfxDefinition
    {
        [Tooltip("Prefab visual, normalmente con Particle Systems. Guardarlo en Assets/Art/Prefabs. No debe contener lógica de combate.")]
        public GameObject prefab;
        [Tooltip("Preparation: mientras prepara/carga. Execution: una vez al ejecutar/soltar. Active: durante la fase activa.")]
        public WeaponVfxPhase phase;
        public WeaponVfxHand hand;
        [Tooltip("Weapon usa el socket del arma. Character usa la raíz del personaje. AboveHead coloca el efecto por encima de su altura, visible desde cualquier lado.")]
        public WeaponVfxAnchor anchor;
        [Tooltip("Orienta el efecto hacia la cámara principal sin moverla. Útil para auras sobre el personaje.")]
        public bool faceCamera;
        [Tooltip("ID de un Weapon Vfx Socket del modelo. Vacío usa la raíz del modelo. Si el ID no existe, no se emite el efecto.")]
        public string socketId;
        public Vector3 localOffset, localRotation;
        public Vector3 localScale = Vector3.one;
        [Tooltip("Sólo Preparation: multiplica la escala al alcanzar la carga máxima. 1 conserva el tamaño. No modifica daño ni tiempos.")]
        [Min(.01f)] public float fullChargeScale = 1;
        [Tooltip("Sólo Execution: duración del efecto, en segundos de juego. Permanece unido al arma; cancelar o cambiar de arma lo retira.")]
        [Min(.01f)] public float lifetime = 1;
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
        // Only evasive weapon actions can open weapon followups; the belt is independent.
        public virtual bool IsEvasion => false;
        public virtual void Begin(AbilityExecution cast) { }
        public virtual void Tick(AbilityExecution cast, float dt) { }
        public virtual void End(AbilityExecution cast) { }
    }
}
