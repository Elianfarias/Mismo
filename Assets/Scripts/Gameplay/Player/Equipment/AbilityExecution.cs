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
        public readonly float DamageMultiplier, AttackSpeed;
        public readonly string WeaponFamilyId;
        public Vector3 Direction;
        public readonly Vector3 GroundPoint;
        public readonly HashSet<EntityId> HitTargets = new HashSet<EntityId>();
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
        {
            Runner = runner; Weapon = weapon; Definition = definition; Direction = direction; GroundPoint = point;
            var inventory=runner.GetComponent<Inventory.PlayerInventory>();
            DamageMultiplier=inventory!=null?inventory.DamageMultiplier(weapon):1;
            bool offensive=definition.usesSwordCombo||definition.actions!=null&&System.Array.Exists(definition.actions,a=>a is MeleeAction||a is ProjectileAction||a is GroundAreaAction);
            AttackSpeed=offensive&&inventory!=null?inventory.AttackSpeed(weapon):1;
            WeaponFamilyId=weapon!=null?weapon.MasteryId:null;
        }
    }
}
