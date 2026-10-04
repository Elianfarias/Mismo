using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Quests;
namespace Mismo.Gameplay.Player.World
{
    public enum DragonArcRole { Lookout, King, Forest, Ruins, Gorge, Altar }
    public sealed class DragonArcInteractable : QuestInteractable
    {
        DragonArcCoordinator coordinator;
        public DragonArcRole Role {get;private set;}
        public void Initialize(DragonArcCoordinator owner,DragonArcRole role,string label)
        {coordinator=owner;Role=role;interactionLabel=label;interactionRange=4;}
        public override bool Interact(PlayerInventory player)=>InRange(player) && player.CanManage && coordinator!=null && coordinator.Interact(Role);
    }
    public interface IDragonEncounter
    {
        bool Ready {get;}
        bool IsActive {get;}
        bool AtPhaseBoundary {get;}
        void Initialize(UnityEngine.Transform player,UnityEngine.Vector3 arena,float radius);
        void Abort();
    }
}
