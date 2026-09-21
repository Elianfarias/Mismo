using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
namespace Mismo.Gameplay.Player.Quests
{
    /// <summary>Stable source IDs prevent duplicate progress; failed saves can be retried.</summary>
    public sealed class QuestSignalTrigger:MonoBehaviour
    {
        public string signal="altar.found";
        [Tooltip("Identidad única y estable de este descubrimiento dentro de la partida.")] public string sourceId="forest.altar";
        public QuestDefinition startQuest;
        [Tooltip("Solo para eventos que deliberadamente acepten una misión sin diálogo. Desactivado por defecto.")]
        public bool acceptAutomatically=false;
        public bool onPlayerEnter=true;
        float retryAt;
        public bool Emit(PlayerInventory player)
        {
            if(player==null||!player.IsReady)return false;
            if(startQuest!=null&&player.QuestState(startQuest)==null&&(!acceptAutomatically||!player.TryStartQuestEvent(startQuest)))return false;
            return player.TryRecordQuestSignal(signal,sourceId);
        }
        void OnTriggerEnter(Collider other){if(onPlayerEnter)Emit(other.GetComponentInParent<PlayerInventory>());}
        void OnTriggerStay(Collider other)
        {if(!onPlayerEnter||Time.unscaledTime<retryAt)return;retryAt=Time.unscaledTime+1;Emit(other.GetComponentInParent<PlayerInventory>());}
    }
}
