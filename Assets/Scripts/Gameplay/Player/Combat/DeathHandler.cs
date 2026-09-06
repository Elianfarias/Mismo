using UnityEngine;
using UnityEngine.Events;

namespace Mismo.Gameplay.Combat
{
    /// <summary>Reacción de muerte separada de la salud para permitir respawn, animación o loot.</summary>
    public sealed class DeathHandler : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private bool disableCollidersOnDeath;
        [SerializeField] private UnityEvent onDeath = new UnityEvent();

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (health != null) health.Died += HandleDeath;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= HandleDeath;
        }

        private void HandleDeath(DamageInfo damage)
        {
            if (disableCollidersOnDeath)
                foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            onDeath?.Invoke();
        }
    }
}
