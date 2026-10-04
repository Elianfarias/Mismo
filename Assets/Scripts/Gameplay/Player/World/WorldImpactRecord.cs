using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    [Serializable]
    public struct WorldImpactRecord
    {
        public float x, z, radius, depth;
        public WorldImpactRecord(Vector3 p, float radius, float depth)
        { x=p.x; z=p.z; this.radius=radius; this.depth=depth; }
        public bool Valid => Finite(x) && Finite(z) && Finite(radius) && Finite(depth) && radius>=2 && radius<=8 && depth>0 && depth<=1.2f;
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Mathf.Abs(v)<=1000000;
        public bool Contains(Vector3 p)=>(new Vector2(p.x-x,p.z-z)).sqrMagnitude<=radius*radius;
        public float Depression(float px,float pz)
        {
            float t=Mathf.Clamp01(Vector2.Distance(new Vector2(px,pz),new Vector2(x,z))/radius);
            // Broad, shallow steps remain walkable. Overlapping impacts never accumulate depth.
            return Mathf.Round(depth*(1-Mathf.SmoothStep(0,1,t))/.2f)*.2f;
        }
        public static bool ValidList(List<WorldImpactRecord> records)
        {if(records==null)return true;if(records.Count>1024)return false;foreach(var r in records)if(!r.Valid)return false;return true;}
        public static bool Destroyed(Vector3 p)
        {var records=WorldSession.Current?.dragonImpacts;if(records!=null)foreach(var r in records)if(r.Contains(p))return true;return false;}
    }
}
