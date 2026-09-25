using System;
using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;

namespace Mismo.Gameplay.Player.World.Structures
{
    public sealed class StructureChest : MonoBehaviour
    {
        StructureContentPoint point;
        PlayerInventory inventory;
        Transform lid;
        Quaternion closedRotation;
        Dictionary<string,int> rolled;
        OwnedWeapon weapon;
        public bool Opened {get;private set;}
        public bool Unlocked=>point!=null&&point.Owner.RequirementMet(point.data.requirement,point.roomId,inventory);
        public void Configure(StructureContentPoint slot,PlayerInventory player,Transform visual)
        {
            point=slot;inventory=player;lid=visual.Find("Lid");if(lid!=null)closedRotation=lid.localRotation;
            Opened=inventory.IsWorldEnemyDefeated(point.PersistentId);UpdateLid(true);
        }
        void Update()
        {
            if(point==null||inventory==null||!inventory.IsReady)return;
            UpdateLid(false);
            if(!Opened&&InRange(inventory.transform))PlayerInteraction.Offer(inventory,this,transform.position,Unlocked?"Abrir":"Cofre sellado",Unlocked?point.data.label:"Derrotá a los guardianes",()=>TryOpen());
        }
        void UpdateLid(bool instant)
        {if(lid!=null){var target=closedRotation*Quaternion.Euler(Opened?-105:0,0,0);lid.localRotation=instant?target:Quaternion.Slerp(lid.localRotation,target,Time.deltaTime*9);}}
        public bool InRange(Transform player)
        {
            if(player==null||Vector3.Distance(player.position,transform.position)>2.8f)return false;
            var from=player.position+Vector3.up;var to=transform.position+Vector3.up*.65f;
            foreach(var hit in Physics.RaycastAll(from,(to-from).normalized,Vector3.Distance(from,to),~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(player)&&!hit.transform.IsChildOf(transform))return false;
            return true;
        }
        public bool TryOpen()
        {
            if(Opened||inventory==null||!inventory.CanInteract||!Unlocked||!InRange(inventory.transform))return false;
            var health=inventory.GetComponent<Mismo.Gameplay.Combat.Health>();if(health!=null&&health.IsDead)return false;
            if(rolled==null)rolled=point.data.loot!=null?point.data.loot.Roll():new Dictionary<string,int>();
            if(weapon==null&&point.data.weapon!=null)weapon=new OwnedWeapon{instanceId=Guid.NewGuid().ToString("N"),definitionId=point.data.weapon.Id,tier=point.data.weaponTier};
            // Open state and pending loot commit together, including when the backpack is full.
            if(!inventory.TryGrantVictory(point.data.experience,null,weapon,point.PersistentId,rolled,transform.position+transform.forward*1.3f+Vector3.up*.15f))return false;
            Opened=true;return true;
        }
    }
}
