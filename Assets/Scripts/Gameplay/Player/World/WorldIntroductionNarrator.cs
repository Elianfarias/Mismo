using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Quests;

namespace Mismo.Gameplay.Player.World
{
    public sealed class WorldIntroductionNarrator:QuestInteractable
    {
        WorldIntroduction introduction;
        public void Initialize(WorldIntroduction owner)
        {introduction=owner;interactionRange=4;interactionLabel="Conversar con Liria";}
        public override bool Interact(PlayerInventory player)=>InRange(player)&&introduction!=null&&introduction.Talk();
    }
}
