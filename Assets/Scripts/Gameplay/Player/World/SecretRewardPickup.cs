using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    [RequireComponent(typeof(Collider))]
    public sealed class SecretRewardPickup : MonoBehaviour
    {
        [SerializeField, Range(0.01f, 1f)] private float healingFraction = 0.4f;
        public bool Consumed { get; private set; }
        string worldId;
        public void ConfigureWorld(string id)=>worldId=id;
        private void Start()
        {
            if(worldId==null)return;
            var inventory=FindAnyObjectByType<Equipment.Inventory.PlayerInventory>();
            if(inventory!=null&&inventory.IsWorldEnemyDefeated(worldId)){Consumed=true;gameObject.SetActive(false);}
        }

        private void OnTriggerEnter(Collider other) => TryCollect(other);
        private void OnTriggerStay(Collider other) => TryCollect(other);

        private void TryCollect(Collider other)
        {
            if (Consumed) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            var health = player.GetComponent<Health>();
            if (health == null || health.IsDead || health.Current >= health.Maximum) return;
            if(worldId!=null)
            {
                var inventory=player.GetComponent<Equipment.Inventory.PlayerInventory>();
                if(inventory==null||!inventory.IsReady)return;
                if(inventory.IsWorldEnemyDefeated(worldId)){Consumed=true;gameObject.SetActive(false);return;}
                if(!inventory.TryGrantVictory(0,null,null,worldId))return;
            }
            Consumed = true;
            health.Heal(health.Maximum * healingFraction);
            gameObject.SetActive(false);
        }
    }
}
