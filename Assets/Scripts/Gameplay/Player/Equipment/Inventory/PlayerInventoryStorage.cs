using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        public int BackpackCapacity=>GridColumns(false)*GridRows(false);
        public int ChestCapacity=>GridColumns(true)*GridRows(true);
        public long UsedSlots(bool chest)=>IsReady?Used(profile,chest):0;
        long Used(InventoryProfile value,bool chest){long area=0;foreach(var item in BuildGridItems(value,chest))area+=(long)item.width*item.height;return area;}
        int StackSize(string id)=>materialDefinitions.TryGetValue(id,out var m)?Mathf.Max(1,m.stackSize):Mathf.Max(1,InventorySettings.Current.defaultStackSize);
        public MaterialDefinition Material(string id)=>id!=null&&materialDefinitions.TryGetValue(id,out var m)?m:null;
        public int StoredMaterialCount(string id)=>profile?.chestMaterials?.Find(s=>s.id==id)?.quantity??0;
        public IEnumerable<PendingInventoryLoot> PendingLoot
        { get { if(profile?.pendingLoot!=null)foreach(var loot in profile.pendingLoot)if(loot!=null&&!loot.IsExpired(WorldPlaySeconds))yield return loot.Copy(); } }
        public string PendingLootName(PendingInventoryLoot loot)
        {
            if(loot==null)return "";
            var names=new List<string>();
            if(loot.HasWeapon)
            {
                var definition=catalog?.Find(loot.weapon.definitionId);
                names.Add(Mismo.Gameplay.Player.Localization.GameLanguage.Text(definition!=null?definition.DisplayName:loot.weapon.definitionId)+" · T"+loot.weapon.tier);
            }
            foreach(var stack in loot.materials)
                names.Add(Mismo.Gameplay.Player.Localization.GameLanguage.Text(Material(stack.id)?.displayName??stack.id)+" x"+stack.quantity);
            return string.Join(" · ",names);
        }
        public bool CanInteract=>IsReady&&loadout!=null&&loadout.CanInteract;
        public bool CanManage=>IsReady&&loadout.CanChangeEquipment;
        public bool AtChest=>GetComponent<InventoryWorldAccess>()?.AtChest==true;
        public bool IsEquipped(string id)=>IsReady&&profile.IsEquipped(id);
        public bool Favorite(string id,bool material)=>material?profile?.favoriteMaterials?.Contains(id)==true:profile?.Find(id)?.favorite==true;
        bool Fits(InventoryProfile next,bool chest)
        {
            if(HasGridRoom(next,chest))return true;
            Notice=chest?"No hay un hueco con la forma necesaria en el cofre.":"No hay un hueco con la forma necesaria. Reorganizá, guardá o descartá objetos.";
            Changed?.Invoke();return false;
        }
        public bool ToggleFavorite(string id,bool material)
        {
            if(!IsReady)return false;
            var next=profile.Copy();
            if(material)
            {
                if(Material(id)==null)return false;
                if(!next.favoriteMaterials.Remove(id))next.favoriteMaterials.Add(id);
            }
            else {var item=next.Find(id);if(item==null)return false;item.favorite=!item.favorite;}
            return Commit(next,"Favoritos guardados.",false);
        }
        public bool Transfer(string id,bool material,int amount,bool toChest)
        {
            if(!CanManage||!AtChest)return false;
            var next=profile.Copy();
            if(material)
            {if(!InventoryStorage.TransferMaterial(next,id,amount,toChest))return false;}
            else
            {
                var item=next.Find(id);
                if(item==null||item.inChest==toChest||IsEquipped(id))return false;
                item.inChest=toChest;
            }
            return Fits(next,toChest)&&Commit(next,toChest?"Objeto guardado en tu cofre.":"Objeto retirado del cofre.",false);
        }
        public bool CanDiscard(string id,bool material)
        {
            if(!CanManage||Favorite(id,material))return false;
            if(material)return Material(id)?.canDiscard==true&&MaterialCount(id)>0;
            var item=profile.Find(id);
            // Unique claimed rewards remain owned, including when stored in the chest.
            return item!=null&&!item.inChest&&!IsEquipped(id)&&Definition(id).canDiscard&&
                !profile.claimedRewards.Exists(r=>rewards.TryGetValue(r,out var definition)&&definition==item.definitionId);
        }
        public bool Discard(string id,bool material,int amount)
        {
            if(!CanDiscard(id,material)||amount<=0)return false;
            var next=profile.Copy();
            if(material)
            {if(!next.TrySpendMaterials(new Dictionary<string,int>{{id,amount}}))return false;}
            else next.weapons.Remove(next.Find(id));
            return Commit(next,"Objeto descartado definitivamente.",false);
        }
        public bool CollectPending(string id)
        {
            if(!CanInteract)return false;
            var next=profile.Copy();var loot=next.pendingLoot.Find(l=>l.id==id);
            if(loot==null||loot.IsExpired(WorldPlaySeconds)||Vector3.Distance(transform.position,new Vector3(loot.x,loot.y,loot.z))>4)return false;
            if(loot.HasWeapon){if(next.weapons.Count>=256)return false;next.weapons.Add(loot.weapon.Copy());}
            foreach(var stack in loot.materials)
                if(!next.TryAddMaterial(stack.id,stack.quantity))return false;
            next.pendingLoot.Remove(loot);
            if(!string.IsNullOrEmpty(loot.rewardId))next.claimedRewards.Add(loot.rewardId);
            return Fits(next,false)&&Commit(next,"Botín recogido.",false,GameSound.LootCollected);
        }
    }
}
