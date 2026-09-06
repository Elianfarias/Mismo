using UnityEngine;

namespace Mismo.Gameplay.Player.Movement
{
    /// <summary>
    /// Solicitud de un desplazamiento especial que el motor consume en su siguiente tick.
    /// Una solicitud dura un solo frame y no modifica el Transform directamente.
    /// </summary>
    public readonly struct ControlledMovementRequest
    {
        public Vector3 Displacement { get; }
        public Vector3 Facing { get; }
        public int Priority { get; }
        public bool BlocksJump { get; }

        public ControlledMovementRequest(Vector3 displacement, Vector3 facing, int priority, bool blocksJump)
        {
            Displacement = displacement;
            Facing = facing;
            Priority = priority;
            BlocksJump = blocksJump;
        }
    }
}
