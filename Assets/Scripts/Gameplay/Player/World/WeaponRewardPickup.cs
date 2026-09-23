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
            var marker=new GameObject("Reward glow");marker.transform.SetParent(go.transform,false);
            marker.transform.localPosition=Vector3.down*.7f;
            marker.AddComponent<LootMarker>().Configure(InventorySettings.Current,0,2);
        }

        void Update()
        {
            if (inventory == null || inventory.HasClaimed(rewardId)) { Destroy(gameObject); return; }
            OfferPickup();
            if (visual != null) visual.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);
        }
        void OfferPickup()
        {
            if(inventory==null||Time.unscaledTime<retryAt||Vector3.Distance(inventory.transform.position,transform.position)>2.4f)return;
            Presentation.PlayerInteraction.Offer(inventory,this,transform.position,"Recoger recompensa","Espada del guardián",()=>
            {
                retryAt=Time.unscaledTime+1;
                if(inventory.TryClaimReward(rewardId))Destroy(gameObject);
            });
        }
    }
}
