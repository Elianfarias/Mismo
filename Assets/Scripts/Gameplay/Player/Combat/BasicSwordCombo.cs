using System;
using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>Parámetros de una etapa de la cadena básica de espada.</summary>
    [Serializable]
    public sealed class ComboStep
    {
        [SerializeField] private string id = "slash";
        [SerializeField, Min(0.01f)] private float duration = 0.38f;
        [SerializeField, Min(0f)] private float inputWindowStart = 0.14f;
        [SerializeField, Min(0f)] private float inputWindowEnd = 0.3f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.06f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.16f;

        public string Id => string.IsNullOrEmpty(id) ? "slash" : id;
        public float Duration => Mathf.Max(0.01f, duration);
        public float InputWindowStart => Mathf.Clamp(inputWindowStart, 0f, Duration);
        public float InputWindowEnd => Mathf.Clamp(Mathf.Max(inputWindowStart, inputWindowEnd), InputWindowStart, Duration);
        public float TransitionDuration => Mathf.Max(0f, transitionDuration);
        public float RecoveryDuration => Mathf.Max(0f, recoveryDuration);
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
        [SerializeField] private ComboStep[] steps =
        {
            new ComboStep(),
            new ComboStep(),
            new ComboStep()
        };

        private Phase phase;
        private int currentStep = -1;
        private float phaseElapsed;
        private bool queuedNext;

        public bool IsActive => phase == Phase.Active || phase == Phase.Transition;
        public bool IsRecovering => phase == Phase.Recovery;
        public bool CanQueue => phase == Phase.Active && CurrentStep != null &&
            phaseElapsed >= CurrentStep.InputWindowStart && phaseElapsed <= CurrentStep.InputWindowEnd;
        public int CurrentStepIndex => currentStep;
        public string CurrentStepId => CurrentStep != null ? CurrentStep.Id : string.Empty;
        public float CurrentStepElapsed => phaseElapsed;
        public float CurrentStepNormalized => CurrentStep != null ? Mathf.Clamp01(phaseElapsed / CurrentStep.Duration) : 0f;
        public event Action<int, string> AttackStarted;
        public event Action<int, string> AttackFinished;
        public event Action<int, string> AttackQueued;

        private ComboStep CurrentStep => currentStep >= 0 && steps != null && currentStep < steps.Length ? steps[currentStep] : null;

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
        public bool RequestAttack()
        {
            if (phase == Phase.Idle) return BeginStep(0);
            if (!CanQueue || queuedNext || currentStep + 1 >= StepCount) return false;
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
                    if (phaseElapsed < CurrentStep.Duration) return;
                    FinishCurrentStep();
                    break;
                case Phase.Transition:
                    if (phaseElapsed < CurrentStep.TransitionDuration) return;
                    BeginStep(currentStep + 1);
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

        private int StepCount => steps != null ? steps.Length : 0;

        private bool BeginStep(int index)
        {
            if (hitbox == null || steps == null || index < 0 || index >= steps.Length || steps[index] == null ||
                hitbox.WindowCount <= index || !hitbox.BeginAttack(index)) return false;
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
            hitbox?.CancelAttack();
            AttackFinished?.Invoke(currentStep, CurrentStep.Id);
            if (queuedNext && currentStep + 1 < StepCount)
            {
                phase = Phase.Transition;
                phaseElapsed = 0f;
                if (CurrentStep.TransitionDuration <= 0f) BeginStep(currentStep + 1);
                return;
            }
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
