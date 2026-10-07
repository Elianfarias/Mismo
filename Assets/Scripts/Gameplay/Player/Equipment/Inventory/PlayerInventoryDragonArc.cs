using Mismo.Gameplay.Player.World;
namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        public bool CanSummonDragon(DragonArcDefinition data) => IsReady && DragonArcRules.CanSummon(profile,data);
        /// <summary>Completa los requisitos del ritual y los tres faros en un solo guardado.</summary>
        public bool TryActivateCheatDragonAltars(DragonArcDefinition data)
        {
            if (!CanManage || data == null || !data.Valid || Quests?.Contains(data.audience) != true || !Quests.Contains(data.awakening)) return false;
            const string message = "Cheat: los 3 faros están activos. Invocá a Soul Eater en el Altar de la llamada.";
            if (CanSummonDragon(data)) { Notice = message; return true; }
            var next = profile.Copy();
            // Los pasos ya completados son idempotentes. No se ejecuta Summoned.
            for (var step = DragonArcStep.Arrival; step <= DragonArcStep.Gorge; step++)
                DragonArcRules.Apply(next, data, step);
            if (!DragonArcRules.CanSummon(next, data)) return false;
            return Commit(next, message, false);
        }
        public bool TryAdvanceDragonArc(DragonArcDefinition data, DragonArcStep step)
        {
            if(!CanManage || data == null || Quests?.Contains(data.audience) != true || !Quests.Contains(data.awakening)) return false;
            var next=profile.Copy();
            if(!DragonArcRules.Apply(next,data,step)) return false;
            string message=step==DragonArcStep.Audience?"Nueva misión: "+data.awakening.title:
                step==DragonArcStep.Arrival?"Nueva misión: "+data.audience.title:
                step==DragonArcStep.Summoned?"Soul Eater respondió a la llamada.":"Diario actualizado.";
            return Commit(next,message,false);
        }
    }
}
