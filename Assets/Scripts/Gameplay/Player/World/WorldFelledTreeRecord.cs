using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    [Serializable]
    public struct WorldFelledTreeRecord
    {
        public float x,z;
        public WorldFelledTreeRecord(Vector3 p){x=p.x;z=p.z;}
        public bool Valid=>Finite(x)&&Finite(z);
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Mathf.Abs(v)<=1000000;
        public bool Matches(Vector3 p)=>Mathf.Abs(p.x-x)<.05f&&Mathf.Abs(p.z-z)<.05f;
        public static bool ValidList(List<WorldFelledTreeRecord> records)
        {if(records==null)return true;if(records.Count>8192)return false;foreach(var r in records)if(!r.Valid)return false;return true;}
        public static bool Destroyed(Vector3 p)
        {var records=WorldSession.Current?.dragonFelledTrees;if(records!=null)foreach(var r in records)if(r.Matches(p))return true;return false;}
    }
}
