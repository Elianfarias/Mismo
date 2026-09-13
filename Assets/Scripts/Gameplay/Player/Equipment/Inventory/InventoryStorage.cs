using System;
using System.Collections.Generic;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [Serializable]
    public sealed class PendingInventoryLoot
    {
        public string id;
        public string rewardId;
        public float x,y,z;
        public OwnedWeapon weapon;
        // Unity may deserialize a null inline serializable object as an empty object.
        public bool HasWeapon=>weapon!=null&&!string.IsNullOrEmpty(weapon.instanceId);
        public List<MaterialStack> materials = new List<MaterialStack>();
        public PendingInventoryLoot Copy()
        {
            var copy=new PendingInventoryLoot {id=id,rewardId=rewardId,x=x,y=y,z=z,weapon=HasWeapon?weapon.Copy():null};
            foreach(var stack in materials)copy.materials.Add(stack.Copy());
            return copy;
        }
    }

    // Capacity is a runtime rule, not a save validity rule: old, larger inventories stay intact.
    public static class InventoryStorage
    {
        public static long Used(InventoryProfile profile, bool chest, Func<string,int> stackSize)
        {
            long used=0;
            foreach(var weapon in profile.weapons)if(weapon.inChest==chest)used++;
            var materials=chest?profile.chestMaterials:profile.materials;
            if(materials!=null)foreach(var stack in materials)
                used+=((long)stack.quantity+Math.Max(1,stackSize(stack.id))-1)/Math.Max(1,stackSize(stack.id));
            return used;
        }
        public static bool TransferMaterial(InventoryProfile profile,string id,int amount,bool toChest)
        {
            if(amount<=0)return false;
            if(profile.chestMaterials==null)profile.chestMaterials=new List<MaterialStack>();
            if(profile.materials==null)profile.materials=new List<MaterialStack>();
            var source=toChest?profile.materials:profile.chestMaterials;
            var target=toChest?profile.chestMaterials:profile.materials;
            var from=source.Find(s=>s.id==id);var to=target.Find(s=>s.id==id);
            if(from==null||from.quantity<amount||(long)(to?.quantity??0)+amount>int.MaxValue)return false;
            if(to==null)target.Add(new MaterialStack{id=id,quantity=amount});else to.quantity+=amount;
            from.quantity-=amount;if(from.quantity==0)source.Remove(from);
            return true;
        }
    }
}
