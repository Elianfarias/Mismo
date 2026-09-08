using System.Collections;
using Mismo.Gameplay.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.World
{
    [RequireComponent(typeof(Health))]
    public sealed class RegionRespawn : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float delay = 2f;
        private Health health;
        private bool pending;
        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() => health.Died += HandleDeath;
        private void OnDisable() { health.Died -= HandleDeath; StopAllCoroutines(); pending = false; }
        private void HandleDeath(DamageInfo damage)
        {
            if (pending) return;
            pending = true;
            StartCoroutine(Reload());
        }
        private IEnumerator Reload()
        {
            yield return new WaitForSecondsRealtime(delay);
            SceneManager.LoadScene(gameObject.scene.path);
        }
    }
}
