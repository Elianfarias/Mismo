using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    [RequireComponent(typeof(Collider))]
    public sealed class SecretRewardPickup : MonoBehaviour
    {
        [SerializeField, Range(0.01f, 1f)] private float healingFraction = 0.4f;
        public bool Consumed { get; private set; }

        private void OnTriggerEnter(Collider other) => TryCollect(other);
        private void OnTriggerStay(Collider other) => TryCollect(other);

        private void TryCollect(Collider other)
        {
            if (Consumed) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            var health = player.GetComponent<Health>();
            if (health == null || health.IsDead || health.Current >= health.Maximum) return;
            Consumed = true;
            health.Heal(health.Maximum * healingFraction);
            gameObject.SetActive(false);
        }
    }
}
