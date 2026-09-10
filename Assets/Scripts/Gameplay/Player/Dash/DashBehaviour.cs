using UnityEngine;

namespace Mismo.Gameplay.Player.Dash
{
    /// <summary>Contrato sin estado por jugador para la movilidad concedida por un cinturón.</summary>
    public abstract class DashBehaviour : SpecialAbilityDefinition
    {
        public override string DisplayName => "Dash";
        public override bool ControlsMovement => true;

        /// <summary>Decide si el estado del suelo permite activar este comportamiento.</summary>
        public abstract override bool CanStart(bool grounded);

        /// <summary>Calcula el desplazamiento durante un intervalo de la ejecución.</summary>
        public abstract override Vector3 EvaluateDisplacement(Vector3 direction, float elapsed, float dt);
    }
}
