using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    /// <summary>Datos del arma, separados de la instancia que el jugador tiene equipada.</summary>
    [CreateAssetMenu(fileName = "BasicSword", menuName = "Mismo/Player/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string id = "sword.basic";
        [SerializeField] private string displayName = "Espada básica";
        [SerializeField, Min(0.01f)] private float basicAttackCooldown = 0.45f;
        public AbilityDefinition[] abilities = new AbilityDefinition[4];
        public GameObject visualPrefab;
        public bool isBow;
        public Vector3 handRotation;
        public Vector3 backRotation = new Vector3(0, 0, 35);
        public Vector3 handOffset;
        public Vector3 backOffset = new Vector3(0, 1.2f, -.25f);
        public AbilityDefinition GetAbility(AbilitySlot slot) => abilities != null && (int)slot < abilities.Length ? abilities[(int)slot] : null;

        public string Id => id;
        public string DisplayName => displayName;
        public float BasicAttackCooldown => Mathf.Max(0.01f, basicAttackCooldown);

        public void Configure(string weaponId, string name, float attackCooldown)
        {
            id = weaponId;
            displayName = name;
            basicAttackCooldown = attackCooldown;
        }
    }
}
