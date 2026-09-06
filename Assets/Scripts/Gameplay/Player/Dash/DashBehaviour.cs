using UnityEngine;

namespace Mismo.Gameplay.Player.Dash
{
    /// <summary>Contrato sin estado por jugador para la movilidad concedida por un cinturón.</summary>
    public abstract class DashBehaviour : ScriptableObject
    {
        public abstract float Duration { get; }
        public abstract float Cooldown { get; }

        /// <summary>Decide si el estado del suelo permite activar este comportamiento.</summary>
        public abstract bool CanStart(bool grounded);

        /// <summary>Calcula el desplazamiento durante un intervalo de la ejecución.</summary>
        public abstract Vector3 EvaluateDisplacement(Vector3 direction, float elapsed, float dt);
    }
}
