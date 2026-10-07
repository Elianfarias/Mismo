using Mismo.Gameplay.Player.Equipment;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Queries only; target memory and state transitions belong to EnemyController.</summary>
    public static class EnemyPerception
    {
        public static bool IsPreparingAimedAction(Transform target)
        {
            var casting = target != null ? target.GetComponent<AbilityRunner>() : null;
            return casting != null && casting.Current != null && !casting.Current.Began && casting.Current.Definition.aimFromCamera;
        }

        public static bool HasSight(Transform actor, Transform target, float sightHeight)
            => HasSightFrom(actor.position, actor, target, sightHeight);

        public static bool HasSightFrom(Vector3 position, Transform actor, Transform target, float sightHeight)
        {
            if (target == null) return false;
            Vector3 start = position + Vector3.up * sightHeight;
            Vector3 end = target.position + Vector3.up * .9f;
            foreach (var hit in Physics.RaycastAll(start, (end - start).normalized,
                         Vector3.Distance(start, end), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(actor) || hit.transform.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }
    }
}
