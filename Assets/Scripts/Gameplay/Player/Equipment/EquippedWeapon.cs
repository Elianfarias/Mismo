using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    /// <summary>Instancia equipada que expone solo los datos necesarios al combate.</summary>
    public sealed class EquippedWeapon : MonoBehaviour, IWeapon
    {
        [SerializeField] private WeaponDefinition definition;

        public string Id => definition != null ? definition.Id : "weapon.unconfigured";
        public string DisplayName => definition != null ? definition.DisplayName : "Arma sin configurar";
        public float BasicAttackCooldown => definition != null ? definition.BasicAttackCooldown : 0.45f;
        public WeaponDefinition Definition => definition;

        public void Configure(WeaponDefinition configuration) => definition = configuration;
    }
}
