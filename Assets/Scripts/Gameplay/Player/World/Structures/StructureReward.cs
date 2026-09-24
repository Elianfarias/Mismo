using UnityEngine;
using Mismo.Gameplay.Player.Equipment.Inventory;

namespace Mismo.Gameplay.Player.World.Structures
{
    [RequireComponent(typeof(Collider))]
    public sealed class StructureReward : MonoBehaviour
    {
        [Min(0)] public int experience=100;
        public bool Consumed{get;private set;}
        string identity;
        public void Configure(string id)=>identity=id;
        void Start()
        {
            if(string.IsNullOrEmpty(identity))return;
            var inventory=FindAnyObjectByType<PlayerInventory>();
            if(inventory!=null&&inventory.IsReady&&inventory.IsWorldEnemyDefeated(identity)){Consumed=true;gameObject.SetActive(false);}
        }
        void OnTriggerEnter(Collider other)=>Collect(other);
        void OnTriggerStay(Collider other)=>Collect(other);
        void Collect(Collider other)
        {
            if(Consumed)return;var player=other.GetComponentInParent<PlayerController>();if(player==null)return;
            var health=player.GetComponent<Mismo.Gameplay.Combat.Health>();if(health!=null&&health.IsDead)return;
            var structure=GetComponentInParent<StructureInstance>();
            if(structure!=null&&!structure.RequirementMet(structure.finalRequirement,structure.finalRoomId,player.GetComponent<PlayerInventory>()))return;
            // The authoring playground never writes to a player's profile.
            if(string.IsNullOrEmpty(identity)){Consumed=true;gameObject.SetActive(false);return;}
            var inventory=player.GetComponent<PlayerInventory>();if(inventory==null||!inventory.IsReady)return;
            if(inventory.IsWorldEnemyDefeated(identity)||inventory.TryGrantVictory(experience,null,null,identity))
            {Consumed=true;gameObject.SetActive(false);}
        }
    }
}
