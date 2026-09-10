using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    /// <summary>Consumed only after the unique reward has been saved successfully.</summary>
    public sealed class WeaponRewardPickup : MonoBehaviour
    {
        PlayerInventory inventory;
        Transform visual;
        string rewardId;
        float retryAt;
        Material material;

        public static void Spawn(PlayerInventory owner, Vector3 position, string id)
        {
            if (owner == null || !owner.IsReady || owner.BossReward == null || owner.HasClaimed(id)) return;
            var go = new GameObject("Espada del guardian - recompensa");
            go.transform.position = position + Vector3.up * .7f;
            var pickup = go.AddComponent<WeaponRewardPickup>(); pickup.inventory = owner; pickup.rewardId = id;
            var collider = go.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = 1.4f;
            var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            if (owner.BossReward.visualPrefab != null)
            {
                var model = Instantiate(owner.BossReward.visualPrefab, go.transform);
                model.transform.localPosition = Vector3.zero;
                pickup.visual = model.transform;
                foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            }
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Reward marker"; marker.transform.SetParent(go.transform, false);
            marker.transform.localPosition = Vector3.down * .55f;
            marker.transform.localScale = new Vector3(.8f, .025f, .8f);
            marker.GetComponent<Collider>().enabled = false;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                pickup.material = new Material(shader) { color = new Color(.95f, .75f, .28f) };
                marker.GetComponent<Renderer>().sharedMaterial = pickup.material;
            }
        }

        void Update()
        {
            if (inventory == null || inventory.HasClaimed(rewardId)) { Destroy(gameObject); return; }
            if (visual != null) visual.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);
        }
        void OnTriggerEnter(Collider other) => Collect(other);
        void OnTriggerStay(Collider other) => Collect(other);
        void Collect(Collider other)
        {
            if (Time.unscaledTime < retryAt || inventory == null || other.GetComponentInParent<PlayerInventory>() != inventory) return;
            retryAt = Time.unscaledTime + 2f;
            if (inventory.TryClaimReward(rewardId)) Destroy(gameObject);
        }
        void OnGUI()
        {
            if (InventoryPanel.AnyOpen) return;
            if (inventory == null || UnityEngine.Camera.main == null) return;
            var point = UnityEngine.Camera.main.WorldToScreenPoint(transform.position + Vector3.up);
            if (point.z <= 0 || Vector3.Distance(inventory.transform.position, transform.position) > 15f) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(point.x - 145, Screen.height - point.y - 25, 290, 54), "ESPADA DEL GUARDIÁN\nAcercate para recoger", style);
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
