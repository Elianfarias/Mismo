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
            var key=inventory.Quests?.interactKey??Key.T;
            if(target!=null&&key!=Key.None&&Keyboard.current?[key].wasPressedThisFrame==true)target.Interact(inventory);
        }
        void OnGUI()
        {
            if(target==null||!Available)return;
            var old=GUI.matrix;GUI.matrix=Matrix4x4.identity;float scale=Mathf.Clamp(Screen.height/900f,.75f,1.5f);
            var offset=inventory.Quests?.interactionPromptOffset??new Vector2(0,145);
            float width=Mathf.Min(Screen.width-24,470*scale),height=51*scale;
            var rect=new Rect((Screen.width-width)/2+offset.x,Screen.height-offset.y-height,width,height);
            FantasyUI.Panel(rect);QuietFantasyUI.Text(new Rect(rect.x+12*scale,rect.y+10*scale,rect.width-24*scale,32*scale),"["+(inventory.Quests?.interactKey??Key.T)+"] "+target.interactionLabel,Mathf.RoundToInt(21*scale),null,false,TextAnchor.MiddleCenter);GUI.matrix=old;
        }
    }
}
