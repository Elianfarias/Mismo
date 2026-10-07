using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    public enum AbilitySlot { Basic, Q, E, R }
    public enum AbilityPose { None, Lunge, Parry, Spin, Bow }
    // Serialized by index: append new passives at the end.
    public enum WeaponPassive { None, ThirdArrow, SwordTip, Rhythm, Finisher, Coverage, Buckler, FiloCruel, Verdugo, Bloodthirst, DoubleEdge }
    public enum ComboOrder { Sequential, AlternateHands }

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
        [Tooltip("Magnitud de la pasiva. Verdugo: daño extra de los básicos contra objetivos sangrando (0,2 = 20 %). Filo cruel: Focus por básico contra objetivos sangrando. Doble filo: probabilidad de golpe doble (0,5 = 50 %). Sed de sangre: fracción de estamina y Focus máximos que restaura (0,2 = 20 %). Potencia la multiplica por rango.")]
        public float passiveValue;
        public bool IsPassive => passive != WeaponPassive.None;
        // Passives whose magnitude is authored in passiveValue; the rest still use their coded constants.
        public bool UsesPassiveValue => passive == WeaponPassive.Verdugo || passive == WeaponPassive.FiloCruel ||
            passive == WeaponPassive.DoubleEdge || passive == WeaponPassive.Bloodthirst;
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
        [Tooltip("Coste de estamina por ejecución; en combos se cobra por cada golpe que comienza, no al encolarlo. Con reactivación se cobra sólo en la primera pulsación, igual que el Focus.")]
        [Min(0)] public float staminaCost;
        [Header("Reactivación")]
        [InspectorName("Etapas por pulsación")]
        [Tooltip("Cada elemento es una pulsación, como la Q de Riven: volver a pulsar ejecuta la etapa siguiente con sus propios tiempos y el clip de combo del mismo índice en las animaciones de la familia. Focus y estamina se cobran en la primera; el cooldown empieza en la última o al vencer la ventana. Vacío o un solo elemento = una ejecución normal. No se combina con combos de espada ni con carga.")]
        public AbilityRecastStage[] recastStages = Array.Empty<AbilityRecastStage>();
        [InspectorName("Ventana para volver a pulsar (segundos)")]
        [Tooltip("Corre desde que termina la animación de una pulsación. Si vence sin volver a pulsar, la habilidad se reinicia y empieza el cooldown.")]
        [Min(.1f)] public float recastWindow = 4;
        public int RecastCount => !usesSwordCombo && !chargeable && recastStages != null && recastStages.Length > 1 ? recastStages.Length : 0;
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
        [Header("VFX en el arma")]
        [Tooltip("Prefabs visuales por fase de ejecución. Preparación permanece mientras se carga; Al ejecutar crea un efecto breve al soltar. Vacío = sin VFX.")]
        public WeaponVfxDefinition[] weaponVfx = Array.Empty<WeaponVfxDefinition>();
        [Header("Presentación y acciones")]
        public AbilityPose pose;
        public bool usesSwordCombo;
        [InspectorName("Golpes del combo")]
        [Tooltip("La duración controla la velocidad del clip; las ventanas se expresan de 0 a 100 %. El estado de ejecución pertenece a cada personaje.")]
        public Mismo.Gameplay.Combat.ComboStep[] comboSteps = Array.Empty<Mismo.Gameplay.Combat.ComboStep>();
        [InspectorName("Orden de los golpes")]
        [Tooltip("Secuencial: cada golpe lleva al siguiente y la cadena termina en el último. Alternar manos: 0 derecha, 1 izquierda, 2 doble que abre la derecha, 3 doble que abre la izquierda; se repite mientras se encadene y los dobles salen con Doble filo.")]
        public ComboOrder comboOrder;
        public bool targetsGround;
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
        [Tooltip("Valor de la evolución en rango 0, ya activa. Su significado depende del comportamiento (daño extra, reducción de armadura, probabilidad, curación, segundos...). Cada rango suma Amount por rango.")]
        public float amount;
        [Tooltip("Se suma a Amount por cada rango entrenado.")]
        public float amountPerRank;
        [Tooltip("Cuarto corte: golpes básicos seguidos al mismo objetivo que aplican el sangrado.")]
        [Min(1)] public int hitsRequired=4;
        [Tooltip("Cuarto corte: segundos sin golpear que reinician la cuenta.")]
        [Min(.1f)] public float streakResetSeconds=3;
        [Tooltip("Daño por segundo del sangrado que aplica esta evolución, antes del multiplicador del arma.")]
        [Min(0)] public float bleedDamagePerSecond=3;
        [Tooltip("Duración del sangrado en rango 0.")]
        [Min(0)] public float bleedSeconds=3;
        [Tooltip("Segundos de sangrado que suma cada rango entrenado.")]
        [Min(0)] public float bleedSecondsPerRank=1;
        [Tooltip("Torbellino: cuántas veces seguidas puede repetirse el giro.")]
        [Min(1)] public int maxRepeats=3;
        [Tooltip("Hacha errante: distancia máxima del siguiente enemigo, en metros.")]
        [Min(.5f)] public float distance=6;
        [Tooltip("Daño de sangrado por segundo que suma cada rango entrenado (Cuarto corte, Carne viva).")]
        [Min(0)] public float bleedDamagePerRank;
        [Tooltip("Cantidad fija de la evolución: golpes extra de Ráfaga o rebotes máximos de Hacha errante.")]
        [Min(1)] public int count=2;
        [Tooltip("Ráfaga: segundos entre un golpe extra y el siguiente.")]
        [Min(.02f)] public float interval=.12f;
        [Tooltip("Valor fijo de la evolución que no crece con el rango: bono de Ejecución (0,4 = +40 %).")]
        [Min(0)] public float bonus=.4f;
        [Tooltip("Sed insaciable: la vida por debajo de la cual cura (0,5 = 50 % de la vida máxima).")]
        [Range(0,1)] public float threshold=.5f;
        [Tooltip("Barrido: cuánto más ancha es el área del golpe (2 = el doble de radio).")]
        [Min(1)] public float width=2;
        // Every evolution is active at rank 0; each rank only improves its numbers: value = base + perRank × rank.
        public float Amount(int rank)=>amount+amountPerRank*Mathf.Max(0,rank);
        public float BleedSeconds(int rank)=>bleedSeconds+bleedSecondsPerRank*Mathf.Max(0,rank);
        public float BleedDamage(int rank)=>bleedDamagePerSecond+bleedDamagePerRank*Mathf.Max(0,rank);
        [Tooltip("VFX adicionales a los de la habilidad. Se reproducen si este modificador está elegido y tiene al menos un rango entrenado.")]
        public WeaponVfxDefinition[] weaponVfx = Array.Empty<WeaponVfxDefinition>();
    }

    // Serialized by index: append new behaviors at the end.
    public enum AbilityModifierBehavior { None, ChargedCut, DodgeChain, ParryRiposte, LungeFinisher, FourthCut, DeepRend, RawFlesh, RustyEdge, Escalation, Shatter,
        LandingStrike, Whirlwind, Burst, WanderingAxe, Slaughter, RisingFury,
        Reopen, Sweep, ColdBlood, Execution, CrossCut, Insatiable }

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

    // Tiempos de una pulsación de una habilidad con reactivación; reemplazan Preparation, Active y Recovery de la habilidad.
    [Serializable]
    public sealed class AbilityRecastStage
    {
        [Min(0)] public float preparation;
        [Min(.01f)] public float active = .15f;
        [Min(0)] public float recovery;
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
        // Only evasive weapon actions can open weapon followups; the belt is independent.
        public virtual bool IsEvasion => false;
        public virtual void Begin(AbilityExecution cast) { }
        public virtual void Tick(AbilityExecution cast, float dt) { }
        public virtual void End(AbilityExecution cast) { }
    }
}
