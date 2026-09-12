using System;
using System.Collections.Generic;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    [Serializable]
    public sealed class GridPlacement
    {
        public string key;
        public bool chest,rotated;
        public int x,y;
        public GridPlacement Copy()=>(GridPlacement)MemberwiseClone();
    }
    public sealed class GridItem
    {
        public string key,id;
        public bool material,canRotate=true;
        public int width=1,height=1,quantity=1;
    }
    public static class InventoryGrid
    {
        public static bool Fits(GridItem item,GridPlacement placement,int columns,int rows,bool[,] occupied)
        {
            if(placement.rotated&&!item.canRotate)return false;
            int w=placement.rotated?item.height:item.width,h=placement.rotated?item.width:item.height;
            if(w<1||h<1||placement.x<0||placement.y<0||placement.x+w>columns||placement.y+h>rows)return false;
            for(int y=placement.y;y<placement.y+h;y++)for(int x=placement.x;x<placement.x+w;x++)if(occupied[x,y])return false;
            return true;
        }
        static void Occupy(GridItem item,GridPlacement p,bool[,] occupied)
        {
            int w=p.rotated?item.height:item.width,h=p.rotated?item.width:item.height;
            for(int y=p.y;y<p.y+h;y++)for(int x=p.x;x<p.x+w;x++)occupied[x,y]=true;
        }
        // Preserve every valid saved position first, then place only new/unplaced entries.
        public static List<GridPlacement> Arrange(IReadOnlyList<GridItem> items,IReadOnlyList<GridPlacement> saved,int columns,int rows,bool chest)
        {
            var occupied=new bool[columns,rows];var result=new List<GridPlacement>();var placed=new HashSet<string>();
            foreach(var item in items)
                foreach(var old in saved)
                    if(old.chest==chest&&old.key==item.key&&Fits(item,old,columns,rows,occupied))
                    {result.Add(old.Copy());Occupy(item,old,occupied);placed.Add(item.key);break;}
            foreach(var item in items)
            {
                if(placed.Contains(item.key))continue;
                bool done=false;
                for(int rotation=0;rotation<(item.canRotate?2:1)&&!done;rotation++)
                    for(int y=0;y<rows&&!done;y++)for(int x=0;x<columns;x++)
                    {
                        var candidate=new GridPlacement{key=item.key,chest=chest,x=x,y=y,rotated=rotation==1};
                        if(!Fits(item,candidate,columns,rows,occupied))continue;
                        result.Add(candidate);Occupy(item,candidate,occupied);done=true;break;
                    }
            }
            return result;
        }
        public static bool CanMove(IReadOnlyList<GridItem> items,IReadOnlyList<GridPlacement> placements,GridPlacement target,int columns,int rows)
        {
            var occupied=new bool[columns,rows];GridItem moving=null;
            foreach(var item in items)
            {
                if(item.key==target.key){moving=item;continue;}
                foreach(var p in placements)if(p.chest==target.chest&&p.key==item.key)
                {if(!Fits(item,p,columns,rows,occupied))return false;Occupy(item,p,occupied);break;}
            }
            return moving!=null&&Fits(moving,target,columns,rows,occupied);
        }
    }
}
