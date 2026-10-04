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
        public readonly float DamageMultiplier;
        public float AttackSpeed {get;private set;}
        readonly bool offensive;
        public readonly AbilityModifierDefinition Modifier;
        public readonly int ModifierRank;
        public bool DodgeChain, CounterOpener;
        public int OpeningStep;
        public bool ChargedCombo=>Definition.usesSwordCombo&&Modifier?.behavior==AbilityModifierBehavior.ChargedCut;
        public bool Chargeable=>Definition.chargeable||ChargedCombo;
        public float MaximumCharge=>ChargedCombo?Mathf.Max(.25f,Modifier.chargeSeconds-ModifierRank*.05f):Definition.maximumCharge;
        public bool BreaksGuard=>ChargedCombo&&Charge>=.95f;
        public float PostureMultiplier=>ChargedCombo?Mathf.Lerp(1,Mathf.Max(1,Modifier.chargedPostureMultiplier),Charge):1;
        public readonly string WeaponFamilyId;
        public Vector3 Direction;
        public readonly Vector3 GroundPoint;
        public readonly HashSet<UnityEngine.Object> HitTargets = new HashSet<UnityEngine.Object>();
        public readonly Dictionary<AbilityAction,float> ActionTimes=new Dictionary<AbilityAction,float>();
        public readonly List<Collider> IgnoredColliders=new List<Collider>();
        public readonly long AttackId = Mismo.Gameplay.Combat.AttackIdentity.Next();
        public Vector3? AimPoint;
        public bool Held;
        public float Charge, ReleasedAt = -1;
        public float Elapsed;
        public bool Began;
        public bool Ended;
        public bool MovementBlocked;
        public GameObject Owner => Runner.gameObject;
        public PlayerMotor Motor => Runner.Motor;
        public AbilityExecution(AbilityRunner runner, WeaponDefinition weapon, AbilityDefinition definition, Vector3 direction, Vector3 point)
        {
            Runner = runner; Weapon = weapon; Definition = definition; Direction = direction; GroundPoint = point;
            var inventory=runner.GetComponent<Inventory.PlayerInventory>();
            int rank=0;Modifier=inventory!=null?inventory.AbilityBehavior(weapon,definition,out rank):null;ModifierRank=rank;
            DamageMultiplier=inventory!=null?inventory.DamageMultiplier(weapon)*inventory.AbilityDamageMultiplier(weapon,definition):1;
            offensive=definition.usesSwordCombo||definition.actions!=null&&System.Array.Exists(definition.actions,a=>a is MeleeAction||a is ProjectileAction||a is PoisonArrowAction||a is RepeatedStrikeAction||a is GroundAreaAction||a is ArmorRendStrikeAction||a is FuriousComboAction||a is AxeThrowAction);
            RefreshAttackSpeed();
            WeaponFamilyId=weapon!=null?weapon.MasteryId:null;
        }
        // Combos re-read it per step: a looping chain would otherwise keep an expired buff.
        public void RefreshAttackSpeed()
        {
            var inventory=Runner.GetComponent<Inventory.PlayerInventory>();
            AttackSpeed=offensive&&inventory!=null?inventory.AttackSpeed(Weapon):1;
        }
    }
}
