using Mismo.Gameplay.Player.Dash;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    /// <summary>Compone el equipamiento actual sin exponer detalles de sus implementaciones.</summary>
    public sealed class EquipmentLoadout : MonoBehaviour
    {
        [SerializeField] private EquippedWeapon weapon;
        [SerializeField] private BeltDash belt;
        [SerializeField] private WeaponSetDefinition startingWeapons;
        [SerializeField, Min(0)] private float combatGraceSeconds = 6f;
        private readonly WeaponDefinition[] slots = new WeaponDefinition[2];
        private int activeSlot;
        private float combatUntil;
        private AbilityRunner runner;
        public WeaponDefinition ActiveDefinition => slots[activeSlot];
        public WeaponDefinition SecondaryDefinition => slots[1 - activeSlot];
        public AbilityRunner Runner => runner;
        public bool InCombat => Time.time < combatUntil;
        public event System.Action Changed;

        public IWeapon Weapon => weapon;
        public IBelt Belt => belt;
        public EquippedWeapon WeaponComponent => weapon;
        public BeltDash BeltComponent => belt;

        /// <summary>Resuelve las dos ranuras mínimas en el mismo objeto del jugador.</summary>
        public void Initialize()
        {
            if (weapon == null) weapon = GetComponent<EquippedWeapon>();
            if (weapon == null) weapon = gameObject.AddComponent<EquippedWeapon>();
            if (belt == null) belt = GetComponent<BeltDash>();
            if (runner != null) return;
            if (startingWeapons == null) startingWeapons = Resources.Load<WeaponSetDefinition>("StartingWeapons");
            slots[0] = startingWeapons != null ? startingWeapons.primary : weapon.Definition;
            slots[1] = startingWeapons != null ? startingWeapons.secondary : null;
            if (slots[0] != null) weapon.Configure(slots[0]);
            runner = GetComponent<AbilityRunner>() ?? gameObject.AddComponent<AbilityRunner>();
            runner.Initialize(this);
            if (startingWeapons != null && GetComponent<WeaponPresentation>() == null) gameObject.AddComponent<WeaponPresentation>();
        }

        public void MarkCombat() => combatUntil = Time.time + combatGraceSeconds;
        public bool TrySwap()
        {
            var health = GetComponent<Mismo.Gameplay.Combat.Health>();
            if (SecondaryDefinition == null || runner == null || runner.IsBusy || belt != null && belt.IsActive || health != null && health.IsDead) return false;
            activeSlot = 1 - activeSlot; weapon.Configure(ActiveDefinition); Changed?.Invoke(); return true;
        }
        public bool TryEquip(int slot, WeaponDefinition definition)
        {
            var health = GetComponent<Mismo.Gameplay.Combat.Health>();
            if (slot < 0 || slot > 1 || definition == null || InCombat || runner != null && runner.IsBusy || belt != null && belt.IsActive || health != null && health.IsDead) return false;
            slots[slot] = definition;
            if (slot == activeSlot) weapon.Configure(definition);
            Changed?.Invoke(); return true;
        }

        public void Configure(EquippedWeapon equippedWeapon, BeltDash equippedBelt)
        {
            weapon = equippedWeapon;
            belt = equippedBelt;
        }
    }
}
