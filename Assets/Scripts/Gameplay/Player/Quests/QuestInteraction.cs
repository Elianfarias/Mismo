using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Mismo.Gameplay.Player.Quests
{
    public sealed class QuestInteraction:MonoBehaviour
    {
        PlayerInventory inventory;QuestInteractable target;
        void Awake()=>inventory=GetComponent<PlayerInventory>();
        bool Available=>inventory!=null&&inventory.IsReady&&inventory.CanManage&&!InventoryPanel.AnyOpen&&!WorldMapPanel.BlocksGameplay&&!GameplayPause.BlocksInput&&GetComponent<GatheringPlayer>()?.Busy!=true&&!CompanionPlayer.IsRiding(gameObject);
        void Update()
        {
            target=null;if(!Available)return;float distance=float.MaxValue;
            foreach(var candidate in QuestInteractable.Active)if(candidate!=null&&candidate.InRange(inventory)&&(!(candidate is QuestGiver giver)||giver.CanTalk(inventory,null)))
            {float d=(candidate.transform.position-transform.position).sqrMagnitude;if(d<distance){distance=d;target=candidate;}}
            if(target!=null)PlayerInteraction.Offer(inventory,target,target.transform.position,target.interactionLabel,"",()=>{if(target!=null&&Available)target.Interact(inventory);});
        }
    }
}
