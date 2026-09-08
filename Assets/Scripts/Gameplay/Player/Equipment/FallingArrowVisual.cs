using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    /// <summary>Presentation only: area pulses own damage, so every arrow cannot multiply it.</summary>
    public sealed class FallingArrowVisual : MonoBehaviour
    {
        private float speed, remaining;
        public static FallingArrowVisual Spawn(GameObject visual, Vector3 origin, float speed, float travel)
        {
            var go = new GameObject("Falling rain arrow");
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            var arrow = go.AddComponent<FallingArrowVisual>();
            arrow.speed = Mathf.Max(.1f, speed); arrow.remaining = Mathf.Max(.1f, travel);
            if (visual != null) Instantiate(visual, go.transform, false);
            return arrow;
        }
        private void Update() => Step(Time.deltaTime);
        public void Step(float dt)
        {
            if (!enabled || dt <= 0) return;
            float distance = Mathf.Min(remaining, speed * dt);
            if (Physics.Raycast(transform.position, Vector3.down, out var hit, distance + .3f, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + Vector3.up * .3f;
                enabled = false; Destroy(gameObject, .15f); return;
            }
            transform.position += Vector3.down * distance; remaining -= distance;
            if (remaining <= 0) { enabled = false; Destroy(gameObject); }
        }
    }
}
