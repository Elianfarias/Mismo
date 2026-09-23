using System;
using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    public enum ComboHitShape { Box, Sphere, Blade }

    /// <summary>Parámetros de una etapa de la cadena básica de espada.</summary>
    [Serializable]
    public sealed class ComboStep
    {
        [SerializeField] private string id = "slash";
        [SerializeField, InspectorName("Duración (segundos)"), Min(.01f)] private float duration = .8f;
        [SerializeField, InspectorName("Encadenar desde (%)"), Range(0,100)] private float inputStartPercent = 35;
        [SerializeField, InspectorName("Encadenar hasta (%)"), Range(0,100)] private float inputEndPercent = 80;
        [SerializeField, InspectorName("Permitir otra habilidad desde (%)"), Range(0,100)] private float branchPercent = 65;
        [SerializeField, InspectorName("Transición (segundos)"), Min(0)] private float transitionDuration = .06f;
        [SerializeField, InspectorName("Recuperación (segundos)"), Min(0)] private float recoveryDuration = .16f;
        [SerializeField, InspectorName("Impacto desde (%)"), Range(0,100)] private float impactStartPercent = 35;
        [SerializeField, InspectorName("Impacto hasta (%)"), Range(0,100)] private float impactEndPercent = 65;
        [SerializeField, InspectorName("Daño"), Min(0)] private float damage = 10;
        [SerializeField, InspectorName("Forma del impacto")] private ComboHitShape shape;
        [SerializeField, InspectorName("Centro (respecto al personaje)")] private Vector3 center = new Vector3(0,1,.65f);
        [SerializeField, InspectorName("Tamaño de la caja")] private Vector3 size = new Vector3(.9f,1.4f,1.4f);
        [SerializeField, InspectorName("Radio de la esfera"), Min(.01f)] private float radius = .7f;
        public ComboStep() { }
        public ComboStep(string stepId, float seconds, float hitDamage, float recoverySeconds = .16f)
        { id = stepId; duration = seconds; damage = hitDamage; recoveryDuration = recoverySeconds; }
        public string Id => string.IsNullOrEmpty(id) ? "slash" : id;
        public float Duration => Mathf.Max(.01f,duration);
        public float InputWindowStart => Mathf.Clamp01(inputStartPercent / 100) * Duration;
        public float InputWindowEnd => Mathf.Max(InputWindowStart,Mathf.Clamp01(inputEndPercent / 100) * Duration);
        public float BranchProgress => Mathf.Clamp01(branchPercent / 100);
        public float TransitionDuration => Mathf.Max(0,transitionDuration);
        public float RecoveryDuration => Mathf.Max(0,recoveryDuration);
        public float ImpactStart => Mathf.Clamp01(impactStartPercent / 100);
        public float ImpactEnd => Mathf.Max(ImpactStart,Mathf.Clamp01(impactEndPercent / 100));
        public float Damage => Mathf.Max(0,damage);
        public ComboHitShape Shape => shape;
        public Vector3 Center => center;
        public Vector3 Size => new Vector3(Mathf.Max(.01f,size.x),Mathf.Max(.01f,size.y),Mathf.Max(.01f,size.z));
        [SerializeField, InspectorName("Grosor de contacto de la hoja"), Min(.005f)] private float bladeRadius = .035f;
        public float BladeRadius => Mathf.Max(.005f,bladeRadius);
        public float Radius => Mathf.Max(.01f,radius);

    }

    /// <summary>
    /// Máquina de estados de un combo básico. Solo coordina etapas y tiempos; los impactos
    /// continúan siendo responsabilidad de <see cref="AttackHitbox"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BasicSwordCombo : MonoBehaviour
    {
        private enum Phase
        {
            Idle,
            Active,
            Transition,
            Recovery
        }

        [SerializeField] private AttackHitbox hitbox;
        private ComboStep[] steps = Array.Empty<ComboStep>();

        private Phase phase;
        private int currentStep = -1;
        private float phaseElapsed;
        private bool queuedNext;
        private Mismo.Gameplay.Player.Movement.Stamina stamina;
        private float staminaCost;

        public bool CanBranch => (phase == Phase.Active && currentStep < StepCount-1 && CurrentStepNormalized >= CurrentStep.BranchProgress) || phase == Phase.Transition;
        public bool IsActive => phase == Phase.Active || phase == Phase.Transition;
        public bool IsRecovering => phase == Phase.Recovery;
        public bool CanQueue => phase == Phase.Active && CurrentStep != null &&
            phaseElapsed >= CurrentStep.InputWindowStart && phaseElapsed <= CurrentStep.InputWindowEnd;
        public int CurrentStepIndex => currentStep;
        public string CurrentStepId => CurrentStep != null ? CurrentStep.Id : string.Empty;
        public float CurrentStepElapsed => phaseElapsed;
        public float CurrentStepNormalized => CurrentStep != null ? (phase == Phase.Active ? Mathf.Clamp01(phaseElapsed / CurrentStep.Duration) : 1f) : 0f;
        public event Action<int, string> AttackStarted;
        public event Action<int, string> AttackFinished;
        public event Action<int, string> AttackQueued;

        public ComboStep CurrentStep => currentStep >= 0 && steps != null && currentStep < steps.Length ? steps[currentStep] : null;

        private void Awake()
        {
            if (hitbox == null) hitbox = GetComponentInChildren<AttackHitbox>();
            phase = Phase.Idle;
        }

        /// <summary>Asigna el hitbox que ejecutará las ventanas de impacto de cada etapa.</summary>
        public void Configure(AttackHitbox attackHitbox) => hitbox = attackHitbox;

        /// <summary>
        /// Recibe una pulsación de ataque. En la ventana configurada la conserva para enlazar
        /// la siguiente etapa; fuera de ella no altera el estado actual.
        /// </summary>
        public bool RequestAttack(Mismo.Gameplay.Player.Equipment.AbilityDefinition definition = null)
        {
            if (phase == Phase.Idle)
            {
                if (definition == null) definition = GetComponentInParent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>()?.GetAbility(Mismo.Gameplay.Player.Equipment.AbilitySlot.Basic);
                if (definition == null || !definition.usesSwordCombo || definition.comboSteps == null || definition.comboSteps.Length == 0 || Array.Exists(definition.comboSteps, step => step == null)) return false;
                stamina=GetComponentInParent<Mismo.Gameplay.Player.Movement.Stamina>();
                staminaCost=Mathf.Max(0,definition.staminaCost);
                steps = definition.comboSteps;
                return BeginStep(0);
            }
            if (!CanQueue || queuedNext || currentStep + 1 >= StepCount || stamina!=null&&stamina.Current<staminaCost) return false;
            queuedNext = true;
            AttackQueued?.Invoke(currentStep + 1, steps[currentStep + 1].Id);
            return true;
        }

        /// <summary>Avanza la etapa actual; se puede llamar desde Update o desde un coordinador.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || phase == Phase.Idle) return;
            phaseElapsed += deltaTime;
            switch (phase)
            {
                case Phase.Active:
                    hitbox?.SetProgress(CurrentStepNormalized);
                    if (phaseElapsed < CurrentStep.Duration) return;
                    FinishCurrentStep();
                    break;
                case Phase.Transition:
                    if (phaseElapsed < CurrentStep.TransitionDuration) return;
                    if(!BeginStep(currentStep + 1))Recover();
                    break;
                case Phase.Recovery:
                    if (phaseElapsed < CurrentStep.RecoveryDuration) return;
                    phase = Phase.Idle;
                    currentStep = -1;
                    phaseElapsed = 0f;
                    break;
            }
        }

        /// <summary>Cancela la ejecución y cierra la ventana activa sin reiniciar el hitbox externo.</summary>
        public void Cancel()
        {
            if (phase == Phase.Idle) return;
            hitbox?.CancelAttack();
            if (currentStep >= 0 && CurrentStep != null) AttackFinished?.Invoke(currentStep, CurrentStep.Id);
            phase = Phase.Idle;
            currentStep = -1;
            phaseElapsed = 0f;
            queuedNext = false;
        }

        private void OnDisable() => Cancel();

        private int StepCount => steps != null ? steps.Length : 0;

        private bool BeginStep(int index)
        {
            if(stamina!=null&&stamina.Current<staminaCost)return false;
            if (hitbox == null || steps == null || index < 0 || index >= steps.Length || steps[index] == null ||
                !hitbox.BeginAttack(index, steps[index])) return false;
            if(stamina!=null&&!stamina.TrySpend(staminaCost)){hitbox.CancelAttack();return false;}
            phase = Phase.Active;
            currentStep = index;
            phaseElapsed = 0f;
            queuedNext = false;
            AttackStarted?.Invoke(index, steps[index].Id);
            return true;
        }

        private void FinishCurrentStep()
        {
            if (CurrentStep == null)
            {
                Cancel();
                return;
            }
            hitbox?.CompleteAttack();
            AttackFinished?.Invoke(currentStep, CurrentStep.Id);
            if (queuedNext && currentStep + 1 < StepCount)
            {
                phase = Phase.Transition;
                phaseElapsed = 0f;
                if (CurrentStep.TransitionDuration <= 0f && !BeginStep(currentStep + 1))Recover();
                return;
            }
            Recover();
        }

        private void Recover()
        {
            phase = Phase.Recovery;
            phaseElapsed = 0f;
            queuedNext = false;
            if (CurrentStep.RecoveryDuration <= 0f)
            {
                phase = Phase.Idle;
                currentStep = -1;
            }
        }
    }
}
