using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Cosmetic adapter, not a stat modifier. Gameplay supplies and removes its own effects.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(450)]
    public sealed class ActorBuffFeedback : MonoBehaviour
    {
        sealed class External { public BuffView view; public float until; }
        readonly Dictionary<(Object source,string id),External> external=new Dictionary<(Object,string),External>();
        readonly List<(Object source,string id)> expired=new List<(Object,string)>();
        readonly List<BuffView> views=new List<BuffView>(12);
        WeaponSkillEffects skills; EquipmentLoadout loadout; PlayerInventory inventory; Health health;
        public IReadOnlyList<BuffView> Views=>views;
        void Awake()
        {
            skills=GetComponent<WeaponSkillEffects>();loadout=GetComponent<EquipmentLoadout>();
            inventory=GetComponent<PlayerInventory>();health=GetComponent<Health>();
            if(health!=null)health.Died+=Died;
            if(GetComponent<ActorBuffVisual>()==null)gameObject.AddComponent<ActorBuffVisual>();
        }
        public static ActorBuffFeedback For(GameObject actor)=>actor.GetComponent<ActorBuffFeedback>()??actor.AddComponent<ActorBuffFeedback>();
        /// <summary>Refresh a source's visual using its remaining gameplay time (scaled seconds).
        /// Use remaining=-1 for a charge removed by RemoveBuff; this never grants damage/armor/speed.</summary>
        public void SetBuff(Object source,string id,BuffKind kind,float remaining,float duration=0,string label=null)
        {
            if(source==null||string.IsNullOrEmpty(id)||(int)kind<0||(int)kind>3||!isActiveAndEnabled||health!=null&&health.IsDead)return;
            var key=(source,id);
            if(remaining==0){external.Remove(key);return;}
            if(float.IsNaN(remaining)||float.IsInfinity(remaining))return;
            if(!external.TryGetValue(key,out var entry)){entry=new External();external.Add(key,entry);}
            entry.until=remaining<0?float.PositiveInfinity:Time.time+remaining;
            entry.view=new BuffView(kind,label??BuffHud.Name(kind),remaining<0?"ACTIVO":null,true,true,remaining,Mathf.Max(0,duration));
        }
        public void RemoveBuff(Object source,string id)=>external.Remove((source,id));
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            views.Clear();expired.Clear();
            if(health!=null&&health.IsDead)return;
            if(skills==null)skills=GetComponent<WeaponSkillEffects>();
            if(skills!=null&&skills.isActiveAndEnabled)skills.CollectBuffViews(views);
            if(inventory!=null&&loadout!=null&&inventory.WeaponConsumableBonus(loadout.ActiveDefinition)>0)
                views.Add(new BuffView(BuffKind.Damage,"AFILADO",null,true,true,inventory.WeaponBuffRemaining));
            foreach(var pair in external)
            {
                if(pair.Key.source==null||Time.time>=pair.Value.until){expired.Add(pair.Key);continue;}
                var view=pair.Value.view;
                view.remaining=float.IsPositiveInfinity(pair.Value.until)?-1:pair.Value.until-Time.time;
                views.Add(view);
            }
            foreach(var key in expired)external.Remove(key);
        }
        void Died(DamageInfo damage){external.Clear();views.Clear();GetComponent<ActorBuffVisual>()?.Clear();}
        void OnDisable(){external.Clear();views.Clear();GetComponent<ActorBuffVisual>()?.Clear();}
        void OnDestroy(){if(health!=null)health.Died-=Died;}
    }
}
