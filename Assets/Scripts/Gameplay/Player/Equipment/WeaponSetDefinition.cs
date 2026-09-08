using UnityEngine;
namespace Mismo.Gameplay.Player.Equipment
{
    [CreateAssetMenu(menuName = "Mismo/Combat/Starting Weapons")]
    public sealed class WeaponSetDefinition : ScriptableObject
    {
        public WeaponDefinition primary;
        public WeaponDefinition secondary;
    }
}
