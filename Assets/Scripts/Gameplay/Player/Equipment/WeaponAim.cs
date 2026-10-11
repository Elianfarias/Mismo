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
        // First thing the aim ray reaches beyond the owner. Whatever lies between the camera and the character (a tree at
        // their back, an enemy pressed against them) is not aimed at: it would pull the aim point behind the character.
        public static Vector3 AimPoint(Ray ray,float distance,Transform owner)
        {
            float ownerDepth=Vector3.Dot(owner.position-ray.origin,ray.direction);
            var hits=Physics.RaycastAll(ray,distance,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.transform.root==owner.root||hit.distance<ownerDepth-.25f)continue;
                return hit.point;
            }
            return ray.GetPoint(distance);
        }
        // Direction of an ability toward the aim point. A point beside or behind the character (an enemy that overlaps them,
        // a weapon swung to their back) never turns them around: it keeps the camera's heading and the vertical aim.
        public static Vector3 Direction(Vector3 origin,Vector3 aimPoint,Transform cameraBasis)
        {
            Vector3 direction=aimPoint-origin;
            Vector3 heading=Vector3.ProjectOnPlane(cameraBasis.forward,Vector3.up),flat=Vector3.ProjectOnPlane(direction,Vector3.up);
            if(heading.sqrMagnitude<1e-4f)return direction.normalized;
            heading.Normalize();
            if(flat.sqrMagnitude>1e-4f&&Vector3.Dot(flat.normalized,heading)>=.2f)return direction.normalized;
            return (heading*Mathf.Max(flat.magnitude,1f)+Vector3.up*direction.y).normalized;
        }
        public static Vector3 Muzzle(GameObject owner)
        {
            var presentation = owner.GetComponent<WeaponPresentation>();
            return presentation != null && presentation.ActiveVisual != null ? presentation.ActiveVisual.position : owner.transform.position + Vector3.up * 1.2f;
        }
    }
}
