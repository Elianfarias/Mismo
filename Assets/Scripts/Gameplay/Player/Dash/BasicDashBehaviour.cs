using UnityEngine;

namespace Mismo.Gameplay.Player.Dash
{
    /// <summary>Dash terrestre rectilíneo del cinturón inicial.</summary>
    [CreateAssetMenu(fileName = "BasicBeltDash", menuName = "Mismo/Player/Basic Belt Dash")]
    public sealed class BasicDashBehaviour : DashBehaviour
    {
        [SerializeField] private DashSettings settings;
        public override float Duration => settings != null ? Mathf.Max(0.01f, settings.Duration) : 0.22f;
        public override float Cooldown => settings != null ? settings.Cooldown : 0.8f;

        /// <summary>Asigna los parámetros del cinturón.</summary>
        public void Configure(DashSettings configuration) => settings = configuration;

        /// <summary>Restringe el cinturón inicial a activaciones desde el suelo.</summary>
        public override bool CanStart(bool grounded) => grounded;

        /// <summary>Limita el último intervalo para conservar la distancia entre tasas de frames.</summary>
        public override Vector3 EvaluateDisplacement(Vector3 direction, float elapsed, float dt)
        {
            float distance = settings != null ? settings.Distance : 5f;
            return direction * (distance / Duration) * Mathf.Clamp(dt, 0f, Duration - elapsed);
        }
    }
}
