using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        readonly WeaponDefinition[] composedWeapons=new WeaponDefinition[2];
        public string OffhandId(int slot)=>profile?.Offhand(slot);
        public bool CanUseOffhand(int slot,string id)
        {
            if(!IsReady||slot<0||slot>1)return false;
            var main=Definition(profile.equipped[slot]);var other=Definition(id);
            return Compatible(main,other)&&!profile.Find(id).inChest&&profile.equipped[0]!=id&&profile.equipped[1]!=id;
        }
        static bool Compatible(WeaponDefinition main,WeaponDefinition other)=>main!=null&&other!=null&&!main.isBow&&!main.isTwoHanded&&!main.isShield&&
            (other.isShield?main.swordShieldFamily!=null:!other.isBow&&!other.isTwoHanded&&other.dualSwordFamily!=null&&main.dualSwordFamily!=null);
        public bool TryEquipOffhand(int slot,string id)
        {
            if(!CanManage||slot<0||slot>1||id!=null&&!CanUseOffhand(slot,id))return false;
            var next=profile.Copy();
            return next.TryEquipOffhand(slot,id)&&Commit(next,id==null?"Mano secundaria libre.":"Combinación y habilidades actualizadas.");
        }
        void NormalizeHands(InventoryProfile data)
        {
            if(data.offhands==null)return;
            for(int i=0;i<2;i++)
            {
                var main=catalog.Find(data.Find(data.equipped[i]).definitionId);
                var off=data.Find(data.Offhand(i));
                if(off==null||!Compatible(main,catalog.Find(off.definitionId)))data.offhands[i]=null;
            }
        }
        WeaponDefinition ComposeWeapon(int slot)
        {
            if(composedWeapons[slot]!=null)Destroy(composedWeapons[slot]);
            var main=Definition(profile.equipped[slot]);var off=Definition(profile.Offhand(slot));
            if(main==null)return null;
            if(!Compatible(main,off)){composedWeapons[slot]=null;return main;}
            // Runtime copies keep per-loadout style data out of shared inventory assets.
            var result=Instantiate(main);result.hideFlags=HideFlags.DontSave;composedWeapons[slot]=result;
            if(Compatible(main,off))
            {
                result.family=off.isShield?main.swordShieldFamily:main.dualSwordFamily;
                result.overrideFamilyAbilities=false;result.dualWield=true;result.secondaryVisualPrefab=off.visualPrefab;
                result.secondaryEquipped=off.isShield?off.secondaryEquipped:main.secondaryEquipped;
                result.styleSpeedBonus=off.isShield?0:.15f;
                result.Configure(main.Id,off.isShield?"Espada y escudo":"Dos espadas",main.BasicAttackCooldown);
            }
            return result;
        }
        WeaponFamilyDefinition FindFamily(string id)
        {
            foreach(var weapon in catalog.weapons)
            {
                if(weapon.family!=null&&weapon.family.progressionId==id)return weapon.family;
                if(weapon.dualSwordFamily!=null&&weapon.dualSwordFamily.progressionId==id)return weapon.dualSwordFamily;
                if(weapon.swordShieldFamily!=null&&weapon.swordShieldFamily.progressionId==id)return weapon.swordShieldFamily;
            }
            return null;
        }
        int WeaponSet(WeaponDefinition weapon)=>weapon==loadout.GetSlot(0)?0:weapon==loadout.GetSlot(1)?1:loadout.ActiveSlot;
        float HandDamageBonus(WeaponDefinition weapon)
        {
            float main=Rules.Bonuses(EquippedItem(weapon)).damage;
            var off=profile.Find(profile.Offhand(WeaponSet(weapon)));
            if(off!=null&&catalog.Find(off.definitionId)?.isShield==false)return (main+Rules.Bonuses(off).damage)*.5f;
            return main;
        }
        float HandSpeedBonus(WeaponDefinition weapon)
        {
            float main=Rules.Bonuses(EquippedItem(weapon)).speed;
            var off=profile.Find(profile.Offhand(WeaponSet(weapon)));
            return off!=null&&catalog.Find(off.definitionId)?.isShield==false?(main+Rules.Bonuses(off).speed)*.5f:main;
        }
        void ReleaseComposedWeapons(){foreach(var weapon in composedWeapons)if(weapon!=null)Destroy(weapon);}
    }
}
