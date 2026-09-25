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
        [Tooltip("Familia base al usar este objeto solo. Las combinaciones con otra arma o escudo usan Dual Sword Family o Sword Shield Family.")]
        public WeaponFamilyDefinition family;
        [Tooltip("Vacío hereda el feedback de la familia. Asignar una copia para editar sólo esta arma.")]
        public Presentation.CombatFeedbackProfile feedbackOverride;
        public Presentation.CombatFeedbackProfile FeedbackProfile => feedbackOverride != null ? feedbackOverride : family != null ? family.feedback : null;
        public bool isShield;
        public WeaponFamilyDefinition dualSwordFamily;
        public WeaponFamilyDefinition swordShieldFamily;
        [System.NonSerialized] public float styleDamageMultiplier=1;
        [System.NonSerialized] public float styleSpeedBonus;
        [Tooltip("Activar sólo para armas con habilidades distintas de su familia.")]
        public bool overrideFamilyAbilities;
        public GameObject visualPrefab;
        [Tooltip("Una sola pieza sostenida con ambas manos. La postura de manos la define la animación; no activa IK.")]
        public bool isTwoHanded;
        [Tooltip("Compatibilidad con pares visuales antiguos. El inventario determina las dos manos según los objetos equipados.")]
        public bool dualWield;
        [System.NonSerialized] public WeaponDefinition equippedOffhand;
        [Tooltip("Visual de pares antiguos sin inventario. El taller usa una segunda WeaponDefinition para previsualizar.")]
        public GameObject secondaryVisualPrefab;
        [Tooltip("Agarre de ESTE objeto cuando se equipa en la mano secundaria.")]
        public WeaponAttachmentPose secondaryEquipped = new WeaponAttachmentPose { anchor = WeaponAnchor.LeftHand };
        [Tooltip("Posición guardada de ESTE objeto cuando ocupa la mano secundaria.")]
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
        [Tooltip("Ícono del arma en el selector de Tab de la HUD. Vacío usa el ícono predeterminado de espada/arco. Se puede cambiar durante Play.")]
        public Texture2D hudIcon;
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
        public string DisplayName => Localization.GameLanguage.Text(displayName);
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
