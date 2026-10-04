using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public enum SoulEaterAction { None, Bite, Tail, Breath, AerialBreath, Dive }
    public enum SoulEaterState { Dormant, Hunting, Windup, Active, Recovery, SpecialRoar, RetreatJump, ChargeWindup, Charging, Braking, Staggered, PhaseTransition, PhaseTwoReady, Returning, Dead, Ascending, AerialAim, AerialBreath, Diving, Landing, ImpactRecovery }

    [CreateAssetMenu(menuName = "Mismo/Enemies/SoulEater phase one", fileName = "SoulEater_PhaseOne")]
    public sealed class SoulEaterPhaseOneSettings : ScriptableObject
    {
        [Header("Identidad, vida y persecución")]
        public string displayName = "SOUL EATER";
        public Color accent = new Color(.36f, .85f, .16f);
        [Min(1)] public float health = 1400, posture = 220;
        [Tooltip("Velocidad terrestre en metros/segundo y giro en grados/segundo.")]
        [Min(.1f)] public float movementSpeed = 3.4f, turnSpeed = 65;
        [Tooltip("Detección inicial solamente. Un combate iniciado no tiene límite de persecución.")]
        [Min(.1f)] public float detectionRange = 22;
        [HideInInspector] public float arenaRadius = 28; // Retained for existing arena tools; never a combat leash.
        [Tooltip("Escalón máximo que puede subir o bajar. Incluye los escalones de sus cráteres.")]
        [Range(.2f, 2)] public float groundStepHeight = 1.3f;
        [Tooltip("Separación de las suelas respecto al suelo físico, en metros.")]
        [Range(0,.2f)] public float soleClearance = .025f;
        [Header("Navegación A* local")]
        [Min(.5f)] public float pathCellSize = 2;
        [Min(.2f)] public float pathReplanInterval = 1;
        [Range(4,64)] public int pathNodesPerFrame = 16;
        [Min(.05f)] public float decisionPause = .55f;

        [Header("Árboles durante la persecución · ambas fases")]
        [Tooltip("Derriba árboles al caminar o cargar contra ellos. Rocas, construcciones y zonas protegidas siguen usando A*.")]
        public bool crushTrees = true;
        [Tooltip("Distancia adicional delante del cuerpo para iniciar la caída, en metros. No cambia el radio de daño.")]
        [Range(0,2)] public float treeBreakReach = .6f;

        [Header("Carga de persecución · ambas fases")]
        public bool enablePursuitCharge = true;
        [Tooltip("Distancia al jugador que activa la carga para cerrar espacio. No consume el ataque del 75 %.")]
        [Min(1)] public float pursuitChargeTriggerDistance = 28;
        [Tooltip("Anticipación antes de correr. Comparte Charge Speed, daño, fijación de dirección y frenado con la carga especial.")]
        [Min(.1f)] public float pursuitChargeWindup = .75f;
        [Tooltip("Recorrido máximo por carga. Puede volver a cargar si el jugador sigue lejos, después de su enfriamiento.")]
        [Min(1)] public float pursuitChargeMaxDistance = 60;
        [Tooltip("Espera desde que termina la recuperación o se interrumpe la carga de persecución.")]
        [Min(.1f)] public float pursuitChargeCooldown = 4;
        [Tooltip("Margen entre detenerse y volver a caminar, para evitar alternancias al borde del alcance.")]
        [Min(.05f)] public float pursuitStopMargin = .75f;
        [Tooltip("Tiempo sin desplazamiento antes de pasar de caminar a Idle. Filtra pequeñas pausas de navegación.")]
        [Range(.05f,.5f)] public float locomotionStopDelay = .2f;

        [Header("Mordida · segundos, metros y daño")]
        [Min(.1f)] public float biteRange = 10.2f, biteRadius = 1.7f;
        [Min(.05f)] public float biteWindup = .95f, biteActive = .42f, biteRecovery = 1.25f;
        [Min(0)] public float biteDamage = 22, biteCooldown = 2.6f;
        [Min(.1f)] public float parryRecovery = .7f;
        [Range(0,1)] public float biteAimLock = .72f;
        public AnimationClip bite;

        [Header("Coletazo · segundos, metros y daño")]
        [Min(.1f)] public float tailRange = 8, tailRadius = 1.1f;
        [Min(.05f)] public float tailWindup = 1.1f, tailActive = .65f, tailRecovery = 1.35f;
        [Min(0)] public float tailDamage = 19, tailCooldown = 5, rearDwellTime = .8f;
        public AnimationClip tail;

        [Header("Aliento · ambas fases")]
        [Min(.1f)] public float breathRange = 13;
        [Range(1,40)] public float breathHalfAngle = 14;
        [Min(.05f)] public float breathWindup = 1.5f, breathActive = 2.2f, breathRecovery = 1.7f;
        [Min(0)] public float breathTickDamage = 6, breathCooldown = 8;
        [Min(.05f)] public float breathTickInterval = .4f;
        [Tooltip("Avance de la llama en metros/segundo desde la boca.")]
        [Min(1)] public float breathPropagationSpeed = 24;
        [Tooltip("Giro del cuerpo al seguir al jugador, en grados/segundo. Fase 1 fija la dirección al comenzar a emitir; fase 2 sigue durante todo el ataque.")]
        [Min(1)] public float breathTurnSpeed = 90;
        [Tooltip("Giro máximo adicional de la cabeza respecto al cuerpo, en grados.")]
        [Range(0,80)] public float breathHeadYaw = 55;
        [Range(0,60)] public float breathHeadPitch = 35;
        [Tooltip("Inclinación fija del aliento terrestre: 0 es horizontal; positivo baja suavemente. Nunca apunta a los pies del jugador.")]
        [Range(-15,15)] public float breathPitch = 8;
        [Tooltip("Anchura inicial de la llamarada, desde la boca hasta el suelo delante del pecho.")]
        [Min(.1f)] public float breathBaseHalfWidth = 1.8f;
        [Tooltip("Extensión de la base de fuego bajo la boca hacia el pecho, en metros.")]
        [Min(.1f)] public float breathGroundBackreach = 1.5f;
        public AnimationClip breath;
        public Material breathMaterial;

        [Header("Rugido, salto y carga · 75 % y repetición en fase 2")]
        [Range(0,1)] public float chargeThreshold = .75f;
        [Min(.05f)] public float specialRoar = 1.65f, chargeWindup = 1, brakeDuration = 1.3f;
        [Min(.1f)] public float retreatDistance = 11, jumpHeight = 3, jumpDuration = 1.35f;
        [Tooltip("Último fotograma normalizado de despegue usado en el salto corto; evita la postura de vuelo vertical.")]
        [Range(.1f,.7f)] public float retreatTakeOffEnd = .4f;
        [Tooltip("Primer fotograma normalizado de aterrizaje del salto corto; usa el contacto final, no el descenso desde el cielo.")]
        [Range(.6f,.95f)] public float retreatLandStart = .8f;
        [Tooltip("Proporción del salto dedicada a la subida antes de mezclar el contacto de aterrizaje.")]
        [Range(.3f,.7f)] public float retreatPoseSwitch = .5f;
        [Min(.1f)] public float chargeSpeed = 14, chargeDistance = 34;
        [Min(0)] public float chargeDamage = 30;
        [Min(.1f)] public float chargeHitRadius = 2.5f;
        public Vector3 chargeHitOffset = new Vector3(0,2.2f,5.2f);
        [Range(0,1)] public float chargeAimLock = .72f;
        [Range(0,1)] public float brakeTravelFraction = .35f, brakeSpeedFraction = .3f;
        public AnimationClip roar, takeOff, land, chargePose, brake;

        [Header("Impacto al invocarlo · destrucción persistente") ]
        [Range(2,8)] public float arrivalImpactRadius = 8;
        [Range(.2f,1.2f)] public float arrivalCraterDepth = .4f;

        [Header("Transición y ritmo de fase 2")]
        public bool enablePhaseTwo = true;
        [Range(0,1)] public float phaseThreshold = .5f;
        [Min(.1f)] public float phaseRoar = 2.6f, phaseTwoDecisionPause = .35f;
        [Min(.1f)] public float phaseTwoChargeCooldown = 20, aerialCooldown = 25, firstAerialDelay = 3;
        [Min(0)] public float chargeDelayAfterLanding = 5;

        [Header("Ascenso y fijación del objetivo")]
        [Min(.1f)] public float ascentTime = 2, aerialAimTime = 1.5f, flightHeight = 20;
        public AnimationClip flight, hover, glide;

        [Header("Picada, impacto y cráter persistente")]
        [Min(.1f)] public float diveTime = .8f, diveRecovery = 3;
        [Tooltip("Duración de la pose de contacto; después descansa en Idle durante la ventana de castigo.")]
        [Min(.05f)] public float impactSettleTime = .35f;
        [Range(0,1)] public float impactLandPose = .58f;
        [Range(2,8)] public float impactRadius = 6;
        [Min(2)] public float shockwaveRadius = 9;
        [Range(.2f,1.2f)] public float craterDepth = 1.2f;
        [Min(0)] public float diveDamage = 40, shockwaveDamage = 18;
        [Min(.1f)] public float shockwaveTime = .6f;
        [Min(0)] public float shockwaveFadeTime = .1f;
        [Min(.1f)] public float impactDamageHeight = 3;

        [Header("Pasada aérea con fuego · distancia y velocidad independientes")]
        [Tooltip("Recorrido horizontal total en metros, centrado en la posición elegida del jugador.")]
        [Min(2)] public float aerialPassDistance = 20;
        [Tooltip("Velocidad horizontal en metros/segundo. Duración = recorrido / velocidad.")]
        [Min(.1f)] public float aerialPassSpeed = 8.333333f;
        [Min(1)] public float aerialPassHeight = 5;
        [Min(.1f)] public float aerialStripHalfWidth = 3, landingTime = 1.2f;
        [Min(0)] public float aerialFlameAhead = 2;
        [Min(.05f)] public float aerialFirePlacementInterval = .3f;
        public AnimationClip aerialBreath;
        public float AerialPassDuration => aerialPassDistance / Mathf.Max(.1f,aerialPassSpeed);

        [Header("Llamas persistentes de fase 2 · suelo")]
        public GameObject groundFirePrefab;
        [Tooltip("Radio visual del prefab a escala 1; sirve para adaptarlo al radio de daño.")]
        [Min(.1f)] public float groundFirePrefabRadius = 1.5f;
        [Min(0)] public float groundFireDamage = 5;
        [Min(.1f)] public float groundFireLifetime = 4, groundFireInterval = .65f;
        [Min(.1f)] public float groundFireRadius = 2.1f;
        [HideInInspector] public float groundFireHeight = 1.65f; // Legacy procedural fire.
        [Min(.1f)] public float groundFireSpacing = 2.5f, groundFirePlacementInterval = .35f;
        [Range(1,48)] public int maximumGroundFires = 8;
        [HideInInspector] public float groundFireHalfAngle = 40;
        [Min(.1f)] public float groundFireFadeTime = .7f;

        [Header("Telegrafía · asset visual del círculo y de la franja")]
        [Tooltip("Prefab con LineRenderer. Se instancia para avisos y onda expansiva. Puedes reemplazar su material o asignar otro prefab compatible.")]
        public LineRenderer telegraphPrefab;
        [Tooltip("Material opcional que reemplaza el del prefab. El shader debe admitir color de vértices para los colores de aviso.")]
        public Material telegraphMaterial;
        [Range(16,128)] public int telegraphSegments = 64;
        [Min(.005f)] public float telegraphWidth = .18f, shockwaveWidth = .3f, groundFireOutlineWidth = .08f;
        [Min(.01f)] public float telegraphGroundOffset = .12f;
        public Color trackingTelegraphColor = new Color(1,.76f,.14f,.85f);
        public Color lockedTelegraphColor = new Color(1,.22f,.04f,.95f);
        public Color shockwaveColor = new Color(.8f,.65f,.32f,.85f);
        [HideInInspector] public Color groundFireOutlineColor = new Color(.45f,.95f,.06f,.45f);
        // Marker radius intentionally uses impactRadius; the visible warning always matches damage.

        [Header("Animaciones de reposo, movimiento, reacción y muerte")]
        public AnimationClip idle, walk, run, hit, die;

        void OnValidate()
        {
            aerialPassDistance=Mathf.Max(2,aerialPassDistance);aerialPassSpeed=Mathf.Max(.1f,aerialPassSpeed);
            breathPitch=Mathf.Clamp(breathPitch,-15,15);arrivalImpactRadius=Mathf.Clamp(arrivalImpactRadius,2,8);arrivalCraterDepth=Mathf.Clamp(arrivalCraterDepth,.2f,1.2f);
            impactRadius=Mathf.Clamp(impactRadius,2,8);craterDepth=Mathf.Clamp(craterDepth,.2f,1.2f);
            shockwaveRadius=Mathf.Max(impactRadius,shockwaveRadius);
            maximumGroundFires=Mathf.Clamp(maximumGroundFires,1,48);telegraphSegments=Mathf.Clamp(telegraphSegments,16,128);
            phaseThreshold=Mathf.Min(phaseThreshold,chargeThreshold);
        }

        public float Windup(SoulEaterAction a) => a == SoulEaterAction.Bite ? biteWindup : a == SoulEaterAction.Tail ? tailWindup : breathWindup;
        public float Active(SoulEaterAction a) => a == SoulEaterAction.Bite ? biteActive : a == SoulEaterAction.Tail ? tailActive : breathActive;
        public float Recovery(SoulEaterAction a) => a == SoulEaterAction.Bite ? biteRecovery : a == SoulEaterAction.Tail ? tailRecovery : breathRecovery;
        public AnimationClip Clip(SoulEaterAction a) => a == SoulEaterAction.Bite ? bite : a == SoulEaterAction.Tail ? tail : breath;
    }
}
