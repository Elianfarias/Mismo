using System.Collections.Generic;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    public sealed class AbilityExecution
    {
        public readonly AbilityRunner Runner;
        public readonly WeaponDefinition Weapon;
        public readonly AbilityDefinition Definition;
        public Vector3 Direction;
        public readonly Vector3 GroundPoint;
        public readonly HashSet<int> HitTargets = new HashSet<int>();
        public readonly long AttackId = Mismo.Gameplay.Combat.AttackIdentity.Next();
        public Vector3? AimPoint;
        public bool Held;
        public float Charge, ReleasedAt = -1;
        public float Elapsed;
        public bool Began;
        public bool Ended;
        public GameObject Owner => Runner.gameObject;
        public PlayerMotor Motor => Runner.Motor;
        public AbilityExecution(AbilityRunner runner, WeaponDefinition weapon, AbilityDefinition definition, Vector3 direction, Vector3 point)
        { Runner = runner; Weapon = weapon; Definition = definition; Direction = direction; GroundPoint = point; }
    }
}
