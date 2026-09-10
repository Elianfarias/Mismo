using UnityEngine;
using Mismo.Gameplay.Player.Presentation;

namespace Mismo.Gameplay.Player.Equipment
{
    [CreateAssetMenu(menuName = "Mismo/Player/Weapon Family")]
    public sealed class WeaponFamilyDefinition : ScriptableObject
    {
        [Tooltip("Identificador persistente de maestría. No cambiar después de publicar guardados.")]
        public string progressionId;
        [Tooltip("Nombre visible para el jugador. Independiente del identificador de guardado.")]
        public string displayName;
        public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName :
            progressionId=="sword.onehand" || name=="OneHandSword" ? "Arma a una mano" :
            progressionId=="bow" || name=="Bow" ? "Arco" : "Familia de arma";
        public AbilityDefinition[] abilities = new AbilityDefinition[4];
        public WeaponAnimationSet animations;
        public AbilityDefinition GetAbility(AbilitySlot slot)
        {
            int index=(int)slot;
            return abilities!=null && index>=0 && index<abilities.Length ? abilities[index] : null;
        }
    }
}
