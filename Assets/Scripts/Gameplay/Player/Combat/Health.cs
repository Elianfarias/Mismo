using System;
using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>Estado de salud reutilizable; no conoce armas, animaciones ni reglas de invulnerabilidad.</summary>
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float maximum = 100f;
        [SerializeField] private bool resetOnEnable = true;
        private float current;

        public float Maximum => maximum;
        public float Current => current;
        public float Normalized => maximum > 0f ? current / maximum : 0f;
        public bool IsDead => current <= 0f;
        public event Action<float, float> Changed;
        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;

        private void Awake() => current = maximum;
        private void OnEnable()
        {
            if (resetOnEnable) Revive();
        }

        /// <summary>Aplica daño válido y emite una sola muerte por ciclo de vida.</summary>
        public bool ApplyDamage(DamageInfo damage)
        {
            if (IsDead || damage.Amount <= 0f) return false;
            float previous = current;
            current = Mathf.Max(0f, current - damage.Amount);
            Changed?.Invoke(current, maximum);
            Damaged?.Invoke(damage);
            if (current <= 0f) Died?.Invoke(damage);
            return current < previous;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            float previous = current;
            current = Mathf.Min(maximum, current + amount);
            if (!Mathf.Approximately(previous, current)) Changed?.Invoke(current, maximum);
        }

        public void Revive()
        {
            current = maximum;
            Changed?.Invoke(current, maximum);
        }

        public void ConfigureMaximum(float value)
        {
            maximum = Mathf.Max(0.01f, value);
            current = Mathf.Min(current, maximum);
            Changed?.Invoke(current, maximum);
        }
    }
}
