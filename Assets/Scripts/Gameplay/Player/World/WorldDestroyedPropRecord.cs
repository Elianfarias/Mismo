using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    [Serializable]
    public struct WorldDestroyedPropRecord
    {
        public float x,z;
        public WorldAssetKind kind;
        public WorldDestroyedPropRecord(Vector3 p,WorldAssetKind kind){x=p.x;z=p.z;this.kind=kind;}
        public bool Valid=>new WorldFelledTreeRecord(new Vector3(x,0,z)).Valid&&WorldDestructible.Allowed(kind);
        public bool Matches(Vector3 p,WorldAssetKind type)=>kind==type&&Mathf.Abs(p.x-x)<.05f&&Mathf.Abs(p.z-z)<.05f;
        public static bool ValidList(List<WorldDestroyedPropRecord> records)
        {if(records==null)return true;if(records.Count>32768)return false;foreach(var r in records)if(!r.Valid)return false;return true;}
        public static bool Destroyed(Vector3 p,WorldAssetKind kind)
        {var records=WorldSession.Current?.dragonDestroyedProps;if(records!=null)foreach(var r in records)if(r.Matches(p,kind))return true;return false;}
    }
}
