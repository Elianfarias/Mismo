using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Quests;

namespace Mismo.Gameplay.Player.World
{
    public enum DragonArcStep { Arrival, Warning, Audience, Forest, Ruins, Gorge, Summoned }
    /// <summary>All story flags are existing quest progress. The caller commits one copied profile.</summary>
    public static class DragonArcRules
    {
        public static readonly string[] BeaconSignals = { "dragon.beacon.forest", "dragon.beacon.ruins", "dragon.beacon.gorge" };
        public static bool Has(InventoryProfile profile, QuestDefinition quest, int objective) =>
            quest != null && objective >= 0 && objective < quest.objectives.Length && QuestRules.Count(profile,quest,quest.objectives[objective]) >= quest.objectives[objective].quantity;
        public static bool CanSummon(InventoryProfile p, DragonArcDefinition d) => d != null && d.Valid &&
            QuestRules.State(p,d.audience)?.completed == true && Has(p,d.awakening,0) && Has(p,d.awakening,1) && Has(p,d.awakening,2);
        public static bool Apply(InventoryProfile p, DragonArcDefinition d, DragonArcStep step)
        {
            if(p == null || d == null || !d.Valid) return false;
            // These story transitions deliver no inventory rewards and cannot consume items.
            foreach(var q in new[]{d.audience,d.awakening})
                if(q.coins != 0 || q.items.Length != 0 || q.recipes.Length != 0 ||
                    System.Array.Exists(q.objectives,o=>o.kind != QuestObjectiveKind.Signal)) return false;
            switch(step)
            {
                case DragonArcStep.Arrival:
                    if(!QuestRules.Accept(p,d.audience)) return false;
                    p.trackedQuest=d.audience.id;return true;
                case DragonArcStep.Warning:
                    return QuestRules.Signal(p,d.audience,"dragon.warning","village-lookout");
                case DragonArcStep.Audience:
                    if(!Has(p,d.audience,0) || !QuestRules.Signal(p,d.audience,"dragon.audience","king") ||
                        !QuestRules.Deliver(p,d.audience) || !QuestRules.Accept(p,d.awakening)) return false;
                    p.trackedQuest=d.awakening.id;return true;
                case DragonArcStep.Forest:
                case DragonArcStep.Ruins:
                case DragonArcStep.Gorge:
                    if(QuestRules.State(p,d.audience)?.completed != true) return false;
                    int i=(int)step-(int)DragonArcStep.Forest;
                    return QuestRules.Signal(p,d.awakening,BeaconSignals[i],BeaconSignals[i]);
                case DragonArcStep.Summoned:
                    return CanSummon(p,d) && QuestRules.Signal(p,d.awakening,"dragon.summoned","summoning-altar") && QuestRules.Deliver(p,d.awakening);
                default:return false;
            }
        }
    }
}
