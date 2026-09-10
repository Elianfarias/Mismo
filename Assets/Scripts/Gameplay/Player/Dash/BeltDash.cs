using UnityEngine;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Cooldowns;

namespace Mismo.Gameplay.Player.Dash
{
    /// <summary>Conserva la ejecución y el cooldown del cinturón de este personaje.</summary>
    public sealed class BeltDash : MonoBehaviour, IBelt
    {
        [SerializeField] private DashBehaviour behaviour;
        [SerializeField] private SpecialAbilityDefinition specialAbility;
        private SpecialAbilityDefinition runningBehaviour;
        private SpecialAbilityExecution execution;
        public SpecialAbilityDefinition Definition => specialAbility!=null?specialAbility:behaviour;
        public string DisplayName => Definition!=null?Definition.DisplayName:"Especial";
        public bool ControlsMovement => IsActive&&runningBehaviour!=null&&runningBehaviour.ControlsMovement;
        private Vector3 direction;
        private float elapsed;
        private readonly CooldownBook cooldowns = new CooldownBook();
        private const string CooldownId = "belt.dash";
        public event System.Action<float> CooldownStarted;
        public event System.Action CooldownReady;
        public bool IsActive { get; private set; }
        public float CooldownRemaining => cooldowns.Remaining(CooldownId);
        public float CooldownDuration => Definition != null ? Definition.Cooldown : 0f;

        /// <summary>Asigna el comportamiento del cinturón sin exponer su implementación al controlador.</summary>
        public void Configure(DashBehaviour configuration) { Cancel();behaviour=configuration;specialAbility=null; }
        public void EquipSpecial(SpecialAbilityDefinition configuration) { Cancel();specialAbility=configuration; }
        private void OnDisable()=>Cancel();

        /// <summary>Reduce el cooldown independientemente de la stamina.</summary>
        public void TickCooldown(float dt)
        {
            var health=GetComponent<Mismo.Gameplay.Combat.Health>();
            if(health!=null&&health.IsDead)Cancel();
            float before = CooldownRemaining;
            cooldowns.Tick(dt);
            if (before > 0f && CooldownRemaining <= 0f) CooldownReady?.Invoke();
        }

        /// <summary>Inicia un dash disponible si el comportamiento del cinturón permite activarlo.</summary>
        public bool CanStart(bool grounded) => Definition != null && !IsActive && cooldowns.IsReady(CooldownId) && Definition.CanStart(grounded);
        public bool TryStart(Vector3 requestedDirection, bool grounded)
        {
            if (!CanStart(grounded)) return false;
            direction = requestedDirection.normalized;
            runningBehaviour = Definition;
            elapsed = 0f;
            if (!cooldowns.TryStart(CooldownId, runningBehaviour.Cooldown)) return false;
            CooldownStarted?.Invoke(runningBehaviour.Cooldown);
            IsActive = true;
            execution=new SpecialAbilityExecution(gameObject,direction);runningBehaviour.Begin(execution);
            GetComponent<Mismo.Gameplay.Combat.Invulnerability>()?.StartWindow(runningBehaviour.InvulnerabilityDuration);
            if(runningBehaviour.InvulnerabilityDuration>0)GetComponent<Mismo.Gameplay.Combat.DefenseWindow>()?.OpenDodge(runningBehaviour.InvulnerabilityDuration);
            return true;
        }

        /// <summary>Entrega el desplazamiento especial y avanza la ejecución.</summary>
        public Vector3 Step(float dt)
        {
            if (!IsActive) return Vector3.zero;
            Vector3 displacement = runningBehaviour.EvaluateDisplacement(direction, elapsed, dt);
            runningBehaviour.Tick(execution,Mathf.Min(dt,Mathf.Max(0,runningBehaviour.Duration-elapsed)));
            elapsed += dt;
            execution.Elapsed=elapsed;
            if (elapsed >= runningBehaviour.Duration) Finish(false);
            return displacement;
        }

        /// <summary>Interrumpe el desplazamiento al encontrar un obstáculo, conservando el cooldown.</summary>
        public void Cancel() => Finish(true);
        private void Finish(bool cancelled)
        {
            if(!IsActive)return;
            IsActive=false;runningBehaviour.End(execution,cancelled);execution=null;
            GetComponent<Mismo.Gameplay.Combat.DefenseWindow>()?.CloseDodge();
        }
    }
}
