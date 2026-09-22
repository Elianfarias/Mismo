using System;
using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.World;
using UnityEngine;

namespace Mismo.Gameplay.Player.Quests
{
    public enum QuestCategory { Pueblo, Misterios, Eventos }
    public enum QuestObjectiveKind { Material, DefeatSpecies, CraftRecipe, Signal }
    [Serializable] public sealed class QuestObjective
    {
        [Tooltip("ID estable dentro de la misión. No cambiar después de publicar.")] public string id;
        public string label;
        public QuestObjectiveKind kind;
        [Min(1)] public int quantity=1;
        public MaterialDefinition material;
        public CreatureSpecies species;
        public CraftingRecipe recipe;
        [Tooltip("Clave emitida por un altar, descubrimiento o evento.")] public string signal;
        public bool consumeOnDelivery=true;
        [Tooltip("La señal solo cuenta si los objetivos anteriores están completos.")] public bool requirePrevious;
        public Sprite icon;
    }
    [Serializable] public sealed class QuestItemReward
    {
        public MaterialDefinition material;
        [Min(1)] public int quantity=1;
    }
    [CreateAssetMenu(menuName="Mismo/Quests/Quest")]
    public sealed class QuestDefinition:ScriptableObject
    {
        public string id,title;
        public QuestCategory category;
        [TextArea(2,6)] public string description;
        public string location;
        public Sprite icon;
        public QuestNpcDefinition npc;
        public QuestDefinition[] prerequisites=Array.Empty<QuestDefinition>();
        [Tooltip("Visible entre los pedidos. Desactivar para misiones iniciadas por eventos o descubrimientos.")] public bool offerInJournal=true;
        public QuestObjective[] objectives=Array.Empty<QuestObjective>();
        [TextArea(2,6)] public string clue;
        [Tooltip("ID del objetivo que revela la pista. Vacío: disponible desde la aceptación.")] public string clueAfterObjective;
        [Min(0)] public int coins;
        public QuestItemReward[] items=Array.Empty<QuestItemReward>();
        public CraftingRecipe[] recipes=Array.Empty<CraftingRecipe>();
        [Header("Diálogo específico de esta misión (vacío: usa el del NPC)")]
        [TextArea(2,6)] public string offerText,acceptedText,progressText,readyText,completedText;

        public bool Validate(out string error)
        {
            error="Misión inválida: "+name;
            if(!MaterialCatalog.ValidId(id)||string.IsNullOrWhiteSpace(title)||npc==null||!MaterialCatalog.ValidId(npc.id)||objectives==null||objectives.Length==0||objectives.Length>32||coins<0)return false;
            var keys=new HashSet<string>();
            foreach(var o in objectives)
            {
                if(o==null||!MaterialCatalog.ValidId(o.id)||!keys.Add(o.id)||string.IsNullOrWhiteSpace(o.label)||o.quantity<1)return false;
                switch(o.kind)
                {
                    case QuestObjectiveKind.Material:if(o.material==null||!MaterialCatalog.ValidId(o.material.id))return false;break;
                    case QuestObjectiveKind.DefeatSpecies:if(o.species==null||!MaterialCatalog.ValidId(o.species.id))return false;break;
                    case QuestObjectiveKind.CraftRecipe:if(o.recipe==null||string.IsNullOrWhiteSpace(o.recipe.id))return false;break;
                    case QuestObjectiveKind.Signal:if(!MaterialCatalog.ValidId(o.signal))return false;break;
                    default:return false;
                }
            }
            if(!string.IsNullOrEmpty(clueAfterObjective)&&!keys.Contains(clueAfterObjective))return false;
            if(items==null||recipes==null||prerequisites==null)return false;
            foreach(var r in items)if(r==null||r.material==null||r.quantity<1)return false;
            foreach(var r in recipes)if(r==null||!MaterialCatalog.ValidId(r.id))return false;
            foreach(var q in prerequisites)if(q==null||q==this)return false;
            error=null;return true;
        }
    }
}
