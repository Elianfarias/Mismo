using System;
using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>
    /// Habilidad exclusiva de la espada. El componente es opcional: otras armas pueden
    /// reemplazarlo por sus propias habilidades sin heredar reglas de parry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwordParry : MonoBehaviour
    {
        [Header("Sword parry")]
        [SerializeField, Min(0.01f)] private float windowDuration = 0.16f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.12f;
        [SerializeField, Min(0f)] private float cooldownDuration = 0.45f;
        [SerializeField] private bool requireIncomingDamageDealer = true;
        [SerializeField, Range(0f, 180f)] private float maxIncomingAngle = 180f;

        private float windowRemaining;
        private float recoveryRemaining;
        private float cooldownRemaining;

        public bool IsWindowOpen => windowRemaining > 0f;
        public bool IsRecovering => recoveryRemaining > 0f;
        public bool IsOnCooldown => cooldownRemaining > 0f;
        public bool IsReady => !IsWindowOpen && !IsRecovering && !IsOnCooldown;
        public float WindowDuration => Mathf.Max(0.01f, windowDuration);
        public float WindowRemaining => Mathf.Max(0f, windowRemaining);
        public float WindowNormalized => WindowDuration > 0f ? WindowRemaining / WindowDuration : 0f;
        public float CooldownDuration => Mathf.Max(0f, cooldownDuration);
        public float CooldownRemaining => Mathf.Max(0f, cooldownRemaining);

        public event Action<float> WindowStarted;
        public event Action WindowClosed;
        public event Action<DamageInfo> Parried;
        public event Action<DamageInfo> Rejected;
        public event Action<float> CooldownStarted;
        public event Action CooldownReady;

        /// <summary>Abre la ventana si la espada está disponible.</summary>
        public bool RequestParry()
        {
            if (!IsReady) return false;
            windowRemaining = WindowDuration;
            WindowStarted?.Invoke(WindowDuration);
            return true;
        }

        // Shared ability runner owns costs, cooldown and recovery for configured kits.
        public void OpenWindow(float seconds)
        { windowRemaining = Mathf.Max(.01f, seconds); recoveryRemaining = 0; WindowStarted?.Invoke(windowRemaining); }
        public void Cancel()
        { bool wasOpen = IsWindowOpen; windowRemaining = recoveryRemaining = 0; if (wasOpen) WindowClosed?.Invoke(); }

        /// <summary>Avanza ventana, recuperación y cooldown.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (windowRemaining > 0f)
            {
                windowRemaining = Mathf.Max(0f, windowRemaining - deltaTime);
                if (windowRemaining <= 0f)
                {
                    recoveryRemaining = Mathf.Max(0f, recoveryDuration);
                    WindowClosed?.Invoke();
                }
            }
            else if (recoveryRemaining > 0f)
            {
                recoveryRemaining = Mathf.Max(0f, recoveryRemaining - deltaTime);
            }

            if (cooldownRemaining <= 0f) return;
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
            if (cooldownRemaining <= 0f) CooldownReady?.Invoke();
        }

        /// <summary>
        /// Intercepta un ataque válido durante la ventana; el receptor omite el daño
        /// cuando devuelve true.
        /// </summary>
        public bool TryParry(DamageInfo damage)
        {
            if (!IsWindowOpen || !IsValidAttack(damage))
            {
                if (IsWindowOpen) Rejected?.Invoke(damage);
                return false;
            }

            windowRemaining = 0f;
            recoveryRemaining = Mathf.Max(0f, recoveryDuration);
            WindowClosed?.Invoke();
            cooldownRemaining = CooldownDuration;
            if (cooldownRemaining > 0f) CooldownStarted?.Invoke(cooldownRemaining);
            else CooldownReady?.Invoke();
            Parried?.Invoke(damage);
            damage.Source.GetComponentInParent<IParryResponder>()?.OnAttackParried(damage);
            return true;
        }

        public void ResolveFeedback(DamageInfo damage)
        { windowRemaining=0; recoveryRemaining=RecoveryDuration; WindowClosed?.Invoke(); Parried?.Invoke(damage); }
        private float RecoveryDuration => Mathf.Max(0,recoveryDuration);

        private bool IsValidAttack(DamageInfo damage)
        {
            if (damage.Source == null || damage.Source.transform.root == transform.root) return false;
            if (requireIncomingDamageDealer && damage.Source.GetComponentInParent<DamageDealer>() == null)
                return false;
            if (maxIncomingAngle >= 180f) return true;

            Vector3 toAttacker = damage.Source.transform.position - transform.position;
            var motor = GetComponentInParent<Mismo.Gameplay.Player.Movement.PlayerMotor>();
            Vector3 facing = Vector3.ProjectOnPlane(motor != null ? motor.Facing : transform.forward, Vector3.up);
            Vector3 horizontalAttacker = Vector3.ProjectOnPlane(toAttacker, Vector3.up);
            if (facing.sqrMagnitude <= 0.0001f || horizontalAttacker.sqrMagnitude <= 0.0001f) return true;
            return Vector3.Angle(facing, horizontalAttacker) <= maxIncomingAngle;
        }
    }
}
