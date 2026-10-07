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
        private static string arrivingScene;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetArrival() => arrivingScene = null;
        private void Start()
        {
            // sceneLoaded has restored the saved position; Awake has applied the chosen skin.
            if (arrivingScene != gameObject.scene.path) return;
            arrivingScene = null;
            GetComponent<Presentation.PlayerArrivalVfx>()?.Play();
        }
        private Health health;
        private bool pending;
        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() => health.Died += HandleDeath;
        private void OnDisable() { health.Died -= HandleDeath; StopAllCoroutines(); pending = false; }
        private void HandleDeath(DamageInfo damage)
        {
            if (pending) return;
            pending = true;
            WorldSession.Respawn();
            StartCoroutine(Reload());
        }
        private IEnumerator Reload()
        {
            yield return new WaitForSecondsRealtime(delay);
            arrivingScene = gameObject.scene.path;
            SceneManager.LoadScene(arrivingScene);
        }
    }
}
