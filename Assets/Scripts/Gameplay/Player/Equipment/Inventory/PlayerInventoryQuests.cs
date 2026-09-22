using System;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.World;
namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        QuestCatalog questCatalog;
        public QuestCatalog Quests=>questCatalog!=null?questCatalog:questCatalog=QuestCatalog.Load();
        public int QuestCoins=>profile?.questCoins??0;
        public QuestDefinition TrackedQuest=>Quests?.Find(profile?.trackedQuest);
        public QuestProgress QuestState(QuestDefinition quest)=>QuestRules.State(profile,quest)?.Copy();
        public int QuestCount(QuestDefinition q,QuestObjective o)=>QuestRules.Count(profile,q,o);
        public bool QuestReady(QuestDefinition q)=>IsReady&&QuestRules.Ready(profile,q);
        public bool KnowsRecipe(CraftingRecipe recipe)=>recipe!=null&&(!recipe.requiresLearning||profile?.learnedRecipes?.Contains(recipe.id)==true);
        public bool CanAcceptQuest(QuestDefinition q)
        {
            if(!IsReady||Quests?.Contains(q)!=true)return false;
            return QuestRules.Accept(profile.Copy(),q);
        }
        public bool TryAcceptQuest(QuestDefinition q)
        {
            if(!CanManage||!CanAcceptQuest(q))return false;
            return StartQuest(q);
        }
        /// <summary>World events may begin during combat; dialogue acceptance remains out of combat.</summary>
        public bool TryStartQuestEvent(QuestDefinition q)
        {
            if(GetComponent<Mismo.Gameplay.Combat.Health>()?.IsDead==true||!CanAcceptQuest(q))return false;
            return StartQuest(q);
        }
        bool StartQuest(QuestDefinition q)
        {
            var next=profile.Copy();return QuestRules.Accept(next,q)&&Commit(next,"Nueva misión: "+q.title,false);
        }
        public bool TrackQuest(QuestDefinition q)
        {
            if(!IsReady||q!=null&&(Quests?.Contains(q)!=true||QuestRules.State(profile,q)==null||QuestRules.State(profile,q).completed))return false;
            var next=profile.Copy();next.trackedQuest=q?.id;return Commit(next,"",false);
        }
        public string QuestDeliveryBlockReason(QuestDefinition q)
        {
            if(!CanManage)return "Solo fuera de combate.";
            if(Quests?.Contains(q)!=true||!QuestReady(q))return q?.npc?.cannotDeliver??"El pedido todavía no está completo.";
            foreach(var item in q.items)if(!materialDefinitions.ContainsKey(item.material.id))return "La recompensa no está disponible.";
            var next=profile.Copy();
            if(!QuestRules.Deliver(next,q))return "No se puede recibir esta recompensa.";
            if(!HasGridRoom(next,false))return q.npc.inventoryFull;
            return null;
        }
        public bool TryDeliverQuest(QuestDefinition q)
        {
            var reason=QuestDeliveryBlockReason(q);
            if(reason!=null){Notice=reason;Changed?.Invoke();return false;}
            var next=profile.Copy();return QuestRules.Deliver(next,q)&&Commit(next,"Misión completada: "+q.title,false,GameSound.CraftSuccess);
        }
        public bool TryRecordQuestSignal(string signal,string sourceId)
        {
            if(!IsReady||Quests==null)return false;
            var next=profile.Copy();bool changed=false;
            foreach(var q in Quests.quests)if(q!=null&&q.Validate(out _))changed|=QuestRules.Signal(next,q,signal,sourceId);
            return changed&&Commit(next,"Diario actualizado.",false);
        }
        void RecordQuestCraft(InventoryProfile next,CraftingRecipe recipe)
        {
            if(Quests==null)return;
            foreach(var q in Quests.quests)if(q!=null&&q.Validate(out _))QuestRules.Crafted(next,q,recipe.id);
        }
    }
}
