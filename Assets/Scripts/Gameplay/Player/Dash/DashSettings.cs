using UnityEngine;

namespace Mismo.Gameplay.Player.Dash
{
    /// <summary>
    /// Stores the tunable values for the initial belt dash.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicDashSettings", menuName = "Mismo/Player/Basic Dash Settings")]
    public sealed class DashSettings : ScriptableObject
    {
        [SerializeField, Min(0f)] private float distance = 5f;
        [SerializeField, Min(0.01f)] private float duration = 0.22f;
        [SerializeField, Min(0f)] private float cooldown = 0.8f;

        /// <summary>
        /// Gets the total distance travelled by the dash in metres.
        /// </summary>
        public float Distance => distance;

        /// <summary>
        /// Gets the duration of the dash in seconds.
        /// </summary>
        public float Duration => duration;

        /// <summary>
        /// Gets the cooldown that starts after a dash begins.
        /// </summary>
        public float Cooldown => cooldown;

        /// <summary>Conserva distancia y tiempos válidos al editar el asset.</summary>
        private void OnValidate()
        {
            distance = Mathf.Max(0f, distance);
            duration = Mathf.Max(0.01f, duration);
            cooldown = Mathf.Max(0f, cooldown);
        }
    }
}
