using UnityEngine;

namespace Mismo.Gameplay.Player.Movement
{
    /// <summary>Administra la stamina de una instancia de jugador.</summary>
    public sealed class Stamina : MonoBehaviour
    {
        [SerializeField] private StaminaSettings settings;
        private float recoveryDelay;
        private bool exhausted;
        public float Current { get; private set; }
        public float Normalized => settings == null ? 0f : Current / settings.Maximum;

        /// <summary>Asigna la configuración al construir el personaje.</summary>
        public void Configure(StaminaSettings configuration) => settings = configuration;

        /// <summary>Consume un coste puntual como una habilidad de arma.</summary>
        public bool TrySpend(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (amount <= 0f) return true;
            if (settings == null || Current < amount) return false;
            Current -= amount;
            recoveryDelay = settings.RegenerationDelay;
            exhausted = Current <= 0f;
            return true;
        }

        /// <summary>Inicializa el recurso de esta instancia.</summary>
        private void Awake() => Current = settings != null ? settings.Maximum : 0f;

        /// <summary>Resuelve consumo o regeneración y devuelve si puede mantenerse el sprint.</summary>
        public bool Tick(bool requested, float dt)
        {
            if (settings == null) return false;
            Current = Mathf.Clamp(Current, 0f, settings.Maximum);
            if (exhausted && Current >= Mathf.Max(0.01f, settings.SprintRestartThreshold)) exhausted = false;
            bool sprint = requested && !exhausted && Current > 0f;
            if (sprint)
            {
                Current = Mathf.Max(0f, Current - settings.SprintCostPerSecond * dt);
                recoveryDelay = settings.RegenerationDelay;
                exhausted = Current <= 0f;
                return !exhausted;
            }
            float regenerationTime = Mathf.Max(0f, dt - recoveryDelay);
            recoveryDelay = Mathf.Max(0f, recoveryDelay - dt);
            Current = Mathf.Min(settings.Maximum, Current + settings.RegenerationPerSecond * regenerationTime);
            return false;
        }
    }
}
