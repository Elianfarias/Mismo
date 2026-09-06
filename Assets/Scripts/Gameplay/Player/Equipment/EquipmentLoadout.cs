using Mismo.Gameplay.Player.Dash;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    /// <summary>Compone el equipamiento actual sin exponer detalles de sus implementaciones.</summary>
    public sealed class EquipmentLoadout : MonoBehaviour
    {
        [SerializeField] private EquippedWeapon weapon;
        [SerializeField] private BeltDash belt;

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
        }

        public void Configure(EquippedWeapon equippedWeapon, BeltDash equippedBelt)
        {
            weapon = equippedWeapon;
            belt = equippedBelt;
        }
    }
}
