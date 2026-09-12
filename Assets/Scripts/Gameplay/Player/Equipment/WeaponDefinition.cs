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
        public WeaponFamilyDefinition family;
        [Tooltip("Activar sólo para armas con habilidades distintas de su familia.")]
        public bool overrideFamilyAbilities;
        public GameObject visualPrefab;
        [Tooltip("Una sola pieza sostenida con ambas manos. La postura de manos la define la animación; no activa IK.")]
        public bool isTwoHanded;
        [Tooltip("Muestra dos piezas visuales para esta única arma del inventario.")]
        public bool dualWield;
        [Tooltip("Segunda pieza. Si está vacío, se reutiliza Visual Prefab.")]
        public GameObject secondaryVisualPrefab;
        public WeaponAttachmentPose secondaryEquipped = new WeaponAttachmentPose { anchor = WeaponAnchor.LeftHand };
        public WeaponAttachmentPose secondaryHolstered = new WeaponAttachmentPose { anchor = WeaponAnchor.Character, offset = new Vector3(-.2f, 1.2f, -.25f) };
        public GameObject SecondaryVisualPrefab => dualWield ? (secondaryVisualPrefab != null ? secondaryVisualPrefab : visualPrefab) : null;
        public WeaponPoseProfile poseProfile;
        public bool isBow;
        [TextArea] public string inventoryDescription;
        public bool canDiscard = true;
        [Range(1,12)] public int gridWidth = 1;
        [Range(1,12)] public int gridHeight = 3;
        public bool canRotate = true;
        public Sprite inventoryIcon;
        public Vector3 inventoryPreviewRotation;
        public bool canSell = true;
        [Min(0)] public int sellValue = 10;
        public Vector3 handRotation;
        public Vector3 backRotation = new Vector3(0, 0, 35);
        public Vector3 handOffset;
        public Vector3 backOffset = new Vector3(0, 1.2f, -.25f);
        public AbilityDefinition GetAbility(AbilitySlot slot) => family!=null && !overrideFamilyAbilities ? family.GetAbility(slot)
            : abilities != null && (int)slot >= 0 && (int)slot < abilities.Length ? abilities[(int)slot] : null;

        public string Id => id;
        public string DisplayName => displayName;
        public string MasteryId => family!=null && !string.IsNullOrEmpty(family.progressionId) ? family.progressionId : Id;
        public float BasicAttackCooldown => Mathf.Max(0.01f, basicAttackCooldown);

        public void Configure(string weaponId, string name, float attackCooldown)
        {
            id = weaponId;
            displayName = name;
            basicAttackCooldown = attackCooldown;
        }
    }
}
