using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    public sealed class AreaInstance : MonoBehaviour
    {
        private GameObject owner;
        private float radius, duration, interval, damage, age, nextVolley;
        private GameObject fallingVisual;
        private int arrowsPerVolley, volleyIndex;
        private float fallHeight, fallSpeed;
        private readonly Queue<float> impacts = new Queue<float>();
        private Material material;
        private string family; private float focusGain;
        public static AreaInstance Spawn(GameObject owner, Vector3 point, float radius, float duration, float interval, float damage,
            GameObject fallingVisual = null, int arrowsPerVolley = 9, float fallHeight = 5, float fallSpeed = 12,string weaponFamilyId=null, float focusGainOnHit=0)
        {
            var go = new GameObject("Ground arrow area"); go.transform.position = point;
            var area = go.AddComponent<AreaInstance>(); area.owner = owner; area.radius = radius; area.duration = Mathf.Max(.1f, duration);
            area.interval = Mathf.Max(.1f, interval); area.damage = damage;
            area.family=weaponFamilyId; area.focusGain=focusGainOnHit;
            area.fallingVisual = fallingVisual; area.arrowsPerVolley = Mathf.Clamp(arrowsPerVolley, 1, 40);
            area.fallHeight = Mathf.Max(.5f, fallHeight); area.fallSpeed = Mathf.Max(.1f, fallSpeed);
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.positionCount = 48; line.widthMultiplier = .055f;
            area.material = Mismo.Gameplay.Player.Presentation.RuntimeParticleMaterial.Create("Ground arrow area", Color.white); line.sharedMaterial = area.material;
            line.startColor = line.endColor = new Color(1, .7f, .2f, .85f);
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2 / 48; line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, .06f, Mathf.Sin(a) * radius)); }
            return area;
        }
        private void Update() => Step(Time.deltaTime);
        public void Step(float dt)
        {
            if (!enabled || dt <= 0) return;
            age += dt;
            while (nextVolley <= age && nextVolley < duration)
            {
                if (fallingVisual != null)
                {
                    EmitVolley(); impacts.Enqueue(nextVolley + fallHeight / fallSpeed);
                }
                else impacts.Enqueue(nextVolley);
                nextVolley += interval;
            }
            while (impacts.Count > 0 && impacts.Peek() <= age) { impacts.Dequeue(); Pulse(); }
            if (age >= duration && impacts.Count == 0) { enabled = false; Destroy(gameObject); }
        }
        private void EmitVolley()
        {
            for (int i = 0; i < arrowsPerVolley; i++)
            {
                float angle = (i * 2.399963f) + volleyIndex * .7f;
                float distance = radius * Mathf.Sqrt((i + .5f) / arrowsPerVolley) * .9f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, fallHeight, Mathf.Sin(angle) * distance);
                FallingArrowVisual.Spawn(fallingVisual, transform.position + offset, fallSpeed, fallHeight + 2);
            }
            volleyIndex++;
        }
        public void Pulse()
        {
            long attackId=AttackIdentity.Next();
            var visited = new HashSet<UnityEngine.Object>(); Vector3 origin = transform.position + Vector3.up * .7f;
            foreach (var other in Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var receiver = other.GetComponentInParent<IDamageReceiver>();
                if (!(receiver is Component target) || owner != null && target.transform.root == owner.transform.root || !visited.Add(target)) continue;
                if (Mathf.Abs(target.transform.position.y - transform.position.y) > 1.5f) continue;
                Vector3 point = other.ClosestPoint(origin);
                if (Physics.Linecast(origin, point, out var obstruction, ~0, QueryTriggerInteraction.Ignore) && obstruction.collider.GetComponentInParent<IDamageReceiver>() != receiver) continue;
                receiver.ReceiveDamage(new DamageInfo(damage, owner, point, Vector3.down, attackId, damage*.5f, true, true, transform.position, false,family,focusGain));
            }
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
