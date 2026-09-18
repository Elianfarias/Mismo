using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    [Serializable]
    public sealed class MapDiscoveryBlock
    {
        public int x, z;
        public string bits;
    }

    // Each 512 m block packs 256 discovered 32 m cells into 32 bytes.
    // Discovery is independent of the view's zoom, camera, and terrain LOD.
    public sealed class MapExploration
    {
        public const int CellSize=32, BlockCells=16;
        readonly Dictionary<Vector2Int,byte[]> blocks=new Dictionary<Vector2Int,byte[]>();
        public bool Changed {get;private set;}
        public int Revision {get;private set;}
        public MapExploration(List<MapDiscoveryBlock> saved)
        {
            if(saved==null)return;
            foreach(var block in saved)
            {
                if(block==null||string.IsNullOrEmpty(block.bits))continue;
                try{var bits=Convert.FromBase64String(block.bits);if(bits.Length==32)blocks[new Vector2Int(block.x,block.z)]=bits;}
                catch(FormatException){/* Invalid blocks stay unexplored. */}
            }
        }
        static Vector2Int Block(int x,int z)=>new Vector2Int(Mathf.FloorToInt(x/16f),Mathf.FloorToInt(z/16f));
        public bool Contains(float x,float z)
        {
            int cx=Mathf.FloorToInt(x/CellSize),cz=Mathf.FloorToInt(z/CellSize);var key=Block(cx,cz);
            if(!blocks.TryGetValue(key,out var bits))return false;
            int index=(cz-key.y*16)*16+cx-key.x*16;return (bits[index>>3]&(1<<(index&7)))!=0;
        }
        public bool Reveal(Vector3 position,float radius)
        {
            bool changed=false;
            int x0=Mathf.FloorToInt((position.x-radius)/CellSize),x1=Mathf.FloorToInt((position.x+radius)/CellSize);
            int z0=Mathf.FloorToInt((position.z-radius)/CellSize),z1=Mathf.FloorToInt((position.z+radius)/CellSize);
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                if(new Vector2((x+.5f)*CellSize-position.x,(z+.5f)*CellSize-position.z).sqrMagnitude>radius*radius)continue;
                var key=Block(x,z);if(!blocks.TryGetValue(key,out var bits)){bits=new byte[32];blocks.Add(key,bits);}
                int index=(z-key.y*16)*16+x-key.x*16,mask=1<<(index&7);
                if((bits[index>>3]&mask)!=0)continue;bits[index>>3]|=(byte)mask;changed=true;
            }
            if(changed)Revision++;
            Changed|=changed;return changed;
        }
        public List<MapDiscoveryBlock> Snapshot()
        {
            var result=new List<MapDiscoveryBlock>(blocks.Count);
            foreach(var pair in blocks)result.Add(new MapDiscoveryBlock{x=pair.Key.x,z=pair.Key.y,bits=Convert.ToBase64String(pair.Value)});
            return result;
        }
        public void MarkSaved()=>Changed=false;
        public bool HasNewDiscovery(MapExploration previous,Rect area)
        {
            foreach(var pair in blocks)
            {
                int originX=pair.Key.x*BlockCells,originZ=pair.Key.y*BlockCells;
                if(!area.Overlaps(new Rect(originX*CellSize,originZ*CellSize,BlockCells*CellSize,BlockCells*CellSize)))continue;
                byte[] old=null;
                if(previous!=null)previous.blocks.TryGetValue(pair.Key,out old);
                for(int i=0;i<32;i++)
                {
                    int added=pair.Value[i]&~(old==null?0:old[i]);
                    if(added==0)continue;
                    for(int bit=0;bit<8;bit++)if((added&(1<<bit))!=0)
                    {
                        int index=i*8+bit;
                        if(area.Overlaps(new Rect((originX+index%16)*CellSize,(originZ+index/16)*CellSize,CellSize,CellSize)))return true;
                    }
                }
            }
            return false;
        }
    }
}
