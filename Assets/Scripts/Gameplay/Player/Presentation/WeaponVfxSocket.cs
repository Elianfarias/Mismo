using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    [DisallowMultipleComponent, AddComponentMenu("Mismo/Combat/Weapon VFX Socket")]
    public sealed class WeaponVfxSocket : MonoBehaviour
    {
        [Tooltip("ID estable y único dentro del modelo del arma. Ejemplos: tip, blade, bowstring. Ubicá este objeto donde debe nacer el efecto.")]
        public string socketId = "tip";

        public static Transform Resolve(Transform visual, string id)
        {
            if (visual == null || !visual.gameObject.activeInHierarchy) return null;
            if (string.IsNullOrEmpty(id)) return visual;
            foreach (var socket in visual.GetComponentsInChildren<WeaponVfxSocket>(true))
                if (socket.socketId == id && socket.gameObject.activeInHierarchy) return socket.transform;
            return null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, .035f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * .18f);
        }
    }
}
