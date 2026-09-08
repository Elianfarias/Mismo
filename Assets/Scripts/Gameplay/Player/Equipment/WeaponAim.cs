using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    /// <summary>One aiming ray for the HUD and every ranged ability.</summary>
    public static class WeaponAim
    {
        public static readonly Vector2 Viewport = new Vector2(.5f, .6f);
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
