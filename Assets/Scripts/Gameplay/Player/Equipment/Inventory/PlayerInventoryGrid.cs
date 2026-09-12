using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        public int GridColumns(bool chest)=>Mathf.Clamp(chest?InventorySettings.Current.chestColumns:InventorySettings.Current.backpackColumns,2,12);
        public int GridRows(bool chest)=>Mathf.Clamp(chest?InventorySettings.Current.chestRows:InventorySettings.Current.backpackRows,1,12);
        public List<GridItem> GridItems(bool chest)=>BuildGridItems(profile,chest);
        List<GridItem> BuildGridItems(InventoryProfile value,bool chest)
        {
            var items=new List<GridItem>();if(value==null)return items;
            foreach(var w in value.weapons)
            {
                if(w.inChest!=chest)continue;var definition=catalog.Find(w.definitionId);
                items.Add(new GridItem{key=w.instanceId,id=w.instanceId,width=Mathf.Clamp(definition.gridWidth,1,12),height=Mathf.Clamp(definition.gridHeight,1,12),canRotate=definition.canRotate});
            }
            var materials=chest?value.chestMaterials:value.materials;
            if(materials!=null)foreach(var stack in materials)
            {
                var definition=Material(stack.id);int size=StackSize(stack.id);
                long count=((long)stack.quantity+size-1)/size;
                for(int index=0;index<count&&index<512;index++)
                    items.Add(new GridItem{key="m:"+stack.id+":"+index,id=stack.id,material=true,quantity=(int)System.Math.Min(size,(long)stack.quantity-(long)index*size),
                        width=Mathf.Clamp(definition!=null?definition.gridWidth:1,1,12),height=Mathf.Clamp(definition!=null?definition.gridHeight:1,1,12),canRotate=definition==null||definition.canRotate});
                if(count>512)items.Add(new GridItem{key="m:"+stack.id+":overflow",id=stack.id,material=true,quantity=(int)(stack.quantity-(long)size*512),width=13});
            }
            return items;
        }
        public List<GridPlacement> GridPositions(bool chest)
        {
            var result=new List<GridPlacement>();
            if(profile?.gridPlacements!=null)foreach(var p in profile.gridPlacements)if(p.chest==chest)result.Add(p.Copy());
            return result;
        }
        void ReplaceGrid(InventoryProfile value,bool chest,List<GridPlacement> layout)
        {
            if(value.gridPlacements==null)value.gridPlacements=new List<GridPlacement>();
            value.gridPlacements.RemoveAll(p=>p.chest==chest);value.gridPlacements.AddRange(layout);
        }
        void NormalizeGrid(InventoryProfile value)
        {
            if(value.gridPlacements==null)value.gridPlacements=new List<GridPlacement>();
            foreach(bool chest in new[]{false,true})ReplaceGrid(value,chest,InventoryGrid.Arrange(BuildGridItems(value,chest),value.gridPlacements,GridColumns(chest),GridRows(chest),chest));
        }
        bool HasGridRoom(InventoryProfile value,bool chest)
        {
            var items=BuildGridItems(value,chest);
            var layout=InventoryGrid.Arrange(items,value.gridPlacements??new List<GridPlacement>(),GridColumns(chest),GridRows(chest),chest);
            if(layout.Count!=items.Count)return false;
            ReplaceGrid(value,chest,layout);return true;
        }
        public bool CanMoveGrid(string key,bool chest,int x,int y,bool rotated)
        {
            if(!IsReady||!CanManage||chest&&!AtChest)return false;
            return InventoryGrid.CanMove(GridItems(chest),GridPositions(chest),new GridPlacement{key=key,chest=chest,x=x,y=y,rotated=rotated},GridColumns(chest),GridRows(chest));
        }
        public bool MoveGrid(string key,bool chest,int x,int y,bool rotated)
        {
            if(!CanMoveGrid(key,chest,x,y,rotated))return false;
            var next=profile.Copy();next.gridPlacements.RemoveAll(p=>p.chest==chest&&p.key==key);
            next.gridPlacements.Add(new GridPlacement{key=key,chest=chest,x=x,y=y,rotated=rotated});
            return Commit(next,"Distribución guardada.",false);
        }
        public bool OrganizeGrid(bool chest)
        {
            if(!CanManage||chest&&!AtChest)return false;
            var next=profile.Copy();var items=BuildGridItems(next,chest);
            items.Sort((a,b)=>{int diff=(b.width*b.height).CompareTo(a.width*a.height);return diff!=0?diff:string.CompareOrdinal(a.key,b.key);});
            var layout=InventoryGrid.Arrange(items,new List<GridPlacement>(),GridColumns(chest),GridRows(chest),chest);
            if(layout.Count!=items.Count){Notice="No hay espacio para acomodar todos los objetos.";Changed?.Invoke();return false;}
            ReplaceGrid(next,chest,layout);return Commit(next,"Mochila organizada.",false);
        }
    }
}
