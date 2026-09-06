using UnityEngine;

namespace Mismo.Gameplay.Player.Movement
{
    /// <summary>
    /// Stores the tunable values used by the player's sprint stamina.
    /// </summary>
    [CreateAssetMenu(fileName = "DefaultStaminaSettings", menuName = "Mismo/Player/Stamina Settings")]
    public sealed class StaminaSettings : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float maximum = 100f;
        [SerializeField, Min(0f)] private float sprintCostPerSecond = 20f;
        [SerializeField, Min(0f)] private float regenerationPerSecond = 25f;
        [SerializeField, Min(0f)] private float regenerationDelay = 0.75f;
        [SerializeField, Min(0f)] private float sprintRestartThreshold = 15f;

        /// <summary>
        /// Gets the maximum stamina available to the player.
        /// </summary>
        public float Maximum => maximum;

        /// <summary>
        /// Gets the amount of stamina consumed per second while sprinting.
        /// </summary>
        public float SprintCostPerSecond => sprintCostPerSecond;

        /// <summary>
        /// Gets the amount of stamina restored per second.
        /// </summary>
        public float RegenerationPerSecond => regenerationPerSecond;

        /// <summary>
        /// Gets the delay before stamina regeneration starts.
        /// </summary>
        public float RegenerationDelay => regenerationDelay;

        /// <summary>
        /// Gets the stamina required to resume sprinting after exhaustion.
        /// </summary>
        public float SprintRestartThreshold => sprintRestartThreshold;

        /// <summary>Evita umbrales de recuperación imposibles y capacidad nula.</summary>
        private void OnValidate()
        {
            maximum = Mathf.Max(0.01f, maximum);
            sprintRestartThreshold = Mathf.Clamp(sprintRestartThreshold, 0.01f, maximum);
        }
    }
}
