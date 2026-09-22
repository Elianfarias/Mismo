using UnityEngine;
namespace Mismo.Gameplay.Player.Quests
{
    [CreateAssetMenu(menuName="Mismo/Quests/NPC dialogue")]
    public sealed class QuestNpcDefinition:ScriptableObject
    {
        public string id,displayName,occupation;
        public Sprite portrait;
        [TextArea(2,6)] public string noRequests="Por ahora no tengo nuevos pedidos. Volvé a visitarme después de explorar.";
        [TextArea(2,6)] public string greeting,offer,accepted,inProgress,readyToDeliver,completed;
        [TextArea(2,6)] public string cannotDeliver="Todavía te falta algo. Revisá el pedido y volvé cuando estés listo.";
        [TextArea(2,6)] public string inventoryFull="Hacé un poco de lugar en la mochila; quiero entregarte lo que te prometí.";
    }
}
