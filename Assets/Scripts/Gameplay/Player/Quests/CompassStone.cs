using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
namespace Mismo.Gameplay.Player.Quests
{
    public sealed class CompassStone:QuestInteractable
    {
        public CompassAltar altar;
        public CardinalDirection direction;
        public Transform pointer;
        public float northYaw;
        void Start()=>SetDirection(direction);
        public void SetDirection(CardinalDirection value)
        {direction=value;if(pointer!=null)pointer.localRotation=Quaternion.Euler(0,northYaw+(int)value*90,0);}
        public override bool Interact(PlayerInventory player)
        {
            if(!InRange(player)||!player.CanManage||altar==null)return false;
            altar.Restore(player);if(altar.IsOpened)return false;
            SetDirection((CardinalDirection)(((int)direction+1)%4));altar.TrySolve(player);return true;
        }
    }
}
