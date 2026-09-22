using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    /// <summary>One aiming ray for the HUD and every ranged ability.</summary>
    public static class WeaponAim
    {
        // Clear the third-person silhouette while keeping HUD and ability rays on the same point.
        // Viewport Y grows upward: 0.65 places the sight 15% of the screen above its center.
        public static readonly Vector2 Viewport = new Vector2(.5f, .65f);
        public static Ray RayFrom(Transform basis)
        {
            var camera = basis.GetComponent<UnityEngine.Camera>();
            return camera != null ? camera.ViewportPointToRay(Viewport) : new Ray(basis.position, basis.forward);
        }
        public static Vector3 Muzzle(GameObject owner)
        {
            var presentation = owner.GetComponent<WeaponPresentation>();
            return presentation != null && presentation.ActiveVisual != null ? presentation.ActiveVisual.position : owner.transform.position + Vector3.up * 1.2f;
        }
    }
}
