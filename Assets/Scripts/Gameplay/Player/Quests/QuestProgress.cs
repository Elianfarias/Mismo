using System;
using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment.Inventory;
namespace Mismo.Gameplay.Player.Quests
{
    [Serializable] public sealed class QuestObjectiveProgress
    {
        public string id;
        public int count,baseline;
        public QuestObjectiveProgress Copy()=>(QuestObjectiveProgress)MemberwiseClone();
    }
    [Serializable] public sealed class QuestProgress
    {
        public string id;
        public bool completed;
        public List<QuestObjectiveProgress> objectives=new List<QuestObjectiveProgress>();
        public List<string> signals=new List<string>();
        public QuestProgress Copy()
        {
            var p=new QuestProgress{id=id,completed=completed,signals=new List<string>(signals)};
            foreach(var o in objectives)p.objectives.Add(o.Copy());return p;
        }
    }
    public static class QuestRules
    {
        public static QuestProgress State(InventoryProfile p,QuestDefinition q)=>p?.quests?.Find(s=>s.id==q?.id);
        public static int Count(InventoryProfile p,QuestDefinition q,QuestObjective o)
        {
            var state=State(p,q);if(state==null||o==null)return 0;if(state.completed)return o.quantity;
            var progress=state.objectives.Find(v=>v.id==o.id);if(progress==null)return 0;
            if(o.kind==QuestObjectiveKind.Material)return Math.Min(o.quantity,p.MaterialCount(o.material.id));
            if(o.kind==QuestObjectiveKind.DefeatSpecies)return Math.Min(o.quantity,Math.Max(0,(p.speciesDefeats?.Find(v=>v.id==o.species.id)?.quantity??0)-progress.baseline));
            return Math.Min(o.quantity,progress.count);
        }
        public static bool Ready(InventoryProfile p,QuestDefinition q)
        {
            var state=State(p,q);if(state==null||state.completed||!q.Validate(out _))return false;
            foreach(var o in q.objectives)if(Count(p,q,o)<o.quantity)return false;
            if(!DeliveryCost(q,out var cost))return false;
            foreach(var c in cost)if(p.MaterialCount(c.Key)<c.Value)return false;
            return true;
        }
        public static bool DeliveryCost(QuestDefinition q,out Dictionary<string,int> cost)
        {
            cost=new Dictionary<string,int>();
            foreach(var o in q.objectives)if(o.kind==QuestObjectiveKind.Material&&o.consumeOnDelivery)
            {
                cost.TryGetValue(o.material.id,out int old);if(old>int.MaxValue-o.quantity)return false;
                cost[o.material.id]=old+o.quantity;
            }
            return true;
        }
        public static bool Accept(InventoryProfile p,QuestDefinition q)
        {
            if(q==null||!q.Validate(out _)||State(p,q)!=null||p.quests.Count>=1024)return false;
            foreach(var prerequisite in q.prerequisites)if(State(p,prerequisite)?.completed!=true)return false;
            var progress=new QuestProgress{id=q.id};
            foreach(var o in q.objectives)progress.objectives.Add(new QuestObjectiveProgress{id=o.id,baseline=o.kind==QuestObjectiveKind.DefeatSpecies?p.speciesDefeats?.Find(v=>v.id==o.species.id)?.quantity??0:0});
            p.quests.Add(progress);if(string.IsNullOrEmpty(p.trackedQuest))p.trackedQuest=q.id;return true;
        }
        public static bool Signal(InventoryProfile p,QuestDefinition q,string signal,string source)
        {
            var state=State(p,q);if(state==null||state.completed||string.IsNullOrWhiteSpace(source)||source.Length>160)return false;
            bool changed=false;
            for(int i=0;i<q.objectives.Length;i++)
            {
                var o=q.objectives[i];if(o.kind!=QuestObjectiveKind.Signal||o.signal!=signal||Count(p,q,o)>=o.quantity)continue;
                bool ready=true;if(o.requirePrevious)for(int j=0;j<i;j++)if(Count(p,q,q.objectives[j])<q.objectives[j].quantity)ready=false;
                string key=o.id+":"+source;if(!ready||state.signals.Contains(key)||state.signals.Count>=4096)continue;
                var progress=state.objectives.Find(v=>v.id==o.id);if(progress==null)continue;
                progress.count++;state.signals.Add(key);changed=true;
            }
            return changed;
        }
        public static void Crafted(InventoryProfile p,QuestDefinition q,string recipeId)
        {
            var state=State(p,q);if(state==null||state.completed)return;
            foreach(var o in q.objectives)if(o.kind==QuestObjectiveKind.CraftRecipe&&o.recipe.id==recipeId)
            {var v=state.objectives.Find(s=>s.id==o.id);if(v!=null&&v.count<o.quantity)v.count++;}
        }
        public static bool Deliver(InventoryProfile p,QuestDefinition q)
        {
            if(!Ready(p,q)||!DeliveryCost(q,out var cost)||p.questCoins>int.MaxValue-q.coins)return false;
            // Caller works on a copy and commits only after every reward and inventory-fit check succeeds.
            if(cost.Count>0&&!p.TrySpendMaterials(cost))return false;
            foreach(var r in q.items)if(!p.TryAddMaterial(r.material.id,r.quantity))return false;
            foreach(var r in q.recipes)if(!p.learnedRecipes.Contains(r.id))p.learnedRecipes.Add(r.id);
            p.questCoins+=q.coins;State(p,q).completed=true;if(p.trackedQuest==q.id)p.trackedQuest=null;return true;
        }
        public static bool ValidSave(InventoryProfile p)
        {
            if(p.questCoins<0||p.quests==null||p.quests.Count>1024||p.learnedRecipes==null||p.learnedRecipes.Count>4096)return false;
            var ids=new HashSet<string>();
            foreach(var q in p.quests)
            {
                if(q==null||!MaterialCatalog.ValidId(q.id)||!ids.Add(q.id)||q.objectives==null||q.objectives.Count>32||q.signals==null||q.signals.Count>4096)return false;
                var keys=new HashSet<string>();foreach(var o in q.objectives)if(o==null||!MaterialCatalog.ValidId(o.id)||!keys.Add(o.id)||o.count<0||o.baseline<0)return false;
                keys.Clear();foreach(var s in q.signals)if(string.IsNullOrEmpty(s)||s.Length>300||!keys.Add(s))return false;
            }
            if(!string.IsNullOrEmpty(p.trackedQuest)&&!p.quests.Exists(q=>q.id==p.trackedQuest&&!q.completed))return false;
            ids.Clear();foreach(var r in p.learnedRecipes)if(!MaterialCatalog.ValidId(r)||!ids.Add(r))return false;
            return true;
        }
    }
}
