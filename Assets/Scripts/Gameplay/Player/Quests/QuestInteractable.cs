using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
namespace Mismo.Gameplay.Player.Quests
{
    public abstract class QuestInteractable:MonoBehaviour
    {
        public static readonly HashSet<QuestInteractable> Active=new HashSet<QuestInteractable>();
        [Min(.5f)] public float interactionRange=3;
        public string interactionLabel="Interactuar";
        protected virtual void OnEnable()=>Active.Add(this);
        protected virtual void OnDisable()=>Active.Remove(this);
        public bool InRange(PlayerInventory p)=>p!=null&&(transform.position-p.transform.position).sqrMagnitude<=interactionRange*interactionRange;
        public abstract bool Interact(PlayerInventory player);
    }
}
