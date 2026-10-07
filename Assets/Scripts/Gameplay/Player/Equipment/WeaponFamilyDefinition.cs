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
        [Tooltip("Habilidades que el jugador puede desbloquear en cualquier orden. El básico permanece fuera de esta lista.")]
        public AbilityDefinition[] repertoire = new AbilityDefinition[0];
        [Tooltip("Intervalo histórico; se usa exclusivamente para migrar guardados anteriores a los desbloqueos elegidos.")]
        [Min(1)] public int masteryLevelsPerUnlock = 3;
        public int SkillCount => repertoire != null && repertoire.Length > 0 ? repertoire.Length : Mathf.Max(0, (abilities?.Length ?? 0)-1);
        public AbilityDefinition Skill(int index) => index < 0 || index >= SkillCount ? null :
            repertoire != null && repertoire.Length > 0 ? repertoire[index] : abilities[index+1];
        public int UnlockLevel(int index) => Inventory.MasteryProgress.AbilityUnlockLevel(index,masteryLevelsPerUnlock);
        public int FindSkill(string id)
        {
            for(int i=0;i<SkillCount;i++)if(Skill(i)!=null && Skill(i).Id==id)return i;
            return -1;
        }
        public WeaponAnimationSet animations;
        public CombatFeedbackProfile feedback;
        [Header("Rasgo de familia")]
        [InspectorName("Daño adicional de los básicos")]
        [Tooltip("0,15 = +15 %. Sólo afecta al ataque básico de esta familia; las habilidades conservan su daño.")]
        [Range(0,1)] public float basicDamageBonus;
        public AbilityDefinition GetAbility(AbilitySlot slot)
        {
            int index=(int)slot;
            return abilities!=null && index>=0 && index<abilities.Length ? abilities[index] : null;
        }
    }
}
