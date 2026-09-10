using UnityEngine;

namespace Mismo.Gameplay.Player.Dash
{
    /// <summary>Origin-independent special slot. Definitions never store per-player effect state.</summary>
    public abstract class SpecialAbilityDefinition : ScriptableObject
    {
        public virtual string DisplayName => "Especial";
        public abstract float Duration { get; }
        public abstract float Cooldown { get; }
        public virtual float InvulnerabilityDuration => 0;
        public virtual bool ControlsMovement => false;
        public virtual bool CanStart(bool grounded) => true;
        public virtual Vector3 EvaluateDisplacement(Vector3 direction,float elapsed,float dt) => Vector3.zero;
        public virtual void Begin(SpecialAbilityExecution execution) { }
        public virtual void Tick(SpecialAbilityExecution execution,float dt) { }
        public virtual void End(SpecialAbilityExecution execution,bool cancelled) { }
    }
    public sealed class SpecialAbilityExecution
    {
        public GameObject Owner { get; }
        public Vector3 Direction { get; }
        public float Elapsed { get; internal set; }
        public object State { get; set; }
        public SpecialAbilityExecution(GameObject owner,Vector3 direction){Owner=owner;Direction=direction;}
    }
}
