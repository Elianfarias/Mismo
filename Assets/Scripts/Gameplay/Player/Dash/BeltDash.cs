using UnityEngine;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Cooldowns;

namespace Mismo.Gameplay.Player.Dash
{
    /// <summary>Conserva la ejecución y el cooldown del cinturón de este personaje.</summary>
    public sealed class BeltDash : MonoBehaviour, IBelt
    {
        [SerializeField] private DashBehaviour behaviour;
        private DashBehaviour runningBehaviour;
        private Vector3 direction;
        private float elapsed;
        private readonly CooldownBook cooldowns = new CooldownBook();
        private const string CooldownId = "belt.dash";
        public event System.Action<float> CooldownStarted;
        public event System.Action CooldownReady;
        public bool IsActive { get; private set; }
        public float CooldownRemaining => cooldowns.Remaining(CooldownId);
        public float CooldownDuration => behaviour != null ? behaviour.Cooldown : 0f;

        /// <summary>Asigna el comportamiento del cinturón sin exponer su implementación al controlador.</summary>
        public void Configure(DashBehaviour configuration) => behaviour = configuration;

        /// <summary>Reduce el cooldown independientemente de la stamina.</summary>
        public void TickCooldown(float dt)
        {
            float before = CooldownRemaining;
            cooldowns.Tick(dt);
            if (before > 0f && CooldownRemaining <= 0f) CooldownReady?.Invoke();
        }

        /// <summary>Inicia un dash disponible si el comportamiento del cinturón permite activarlo.</summary>
        public bool CanStart(bool grounded) => behaviour != null && !IsActive && cooldowns.IsReady(CooldownId) && behaviour.CanStart(grounded);
        public bool TryStart(Vector3 requestedDirection, bool grounded)
        {
            if (behaviour == null || IsActive || !cooldowns.IsReady(CooldownId) || !behaviour.CanStart(grounded)) return false;
            direction = requestedDirection.normalized;
            runningBehaviour = behaviour;
            elapsed = 0f;
            if (!cooldowns.TryStart(CooldownId, behaviour.Cooldown)) return false;
            CooldownStarted?.Invoke(behaviour.Cooldown);
            IsActive = true;
            GetComponent<Mismo.Gameplay.Combat.Invulnerability>()?.StartWindow(behaviour.InvulnerabilityDuration);
            GetComponent<Mismo.Gameplay.Combat.DefenseWindow>()?.OpenDodge(behaviour.InvulnerabilityDuration);
            return true;
        }

        /// <summary>Entrega el desplazamiento especial y avanza la ejecución.</summary>
        public Vector3 Step(float dt)
        {
            if (!IsActive) return Vector3.zero;
            Vector3 displacement = runningBehaviour.EvaluateDisplacement(direction, elapsed, dt);
            elapsed += dt;
            if (elapsed >= runningBehaviour.Duration) IsActive = false;
            return displacement;
        }

        /// <summary>Interrumpe el desplazamiento al encontrar un obstáculo, conservando el cooldown.</summary>
        public void Cancel() { IsActive = false; GetComponent<Mismo.Gameplay.Combat.DefenseWindow>()?.CloseDodge(); }
    }
}
