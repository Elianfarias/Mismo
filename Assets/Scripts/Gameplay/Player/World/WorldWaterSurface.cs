using System.Collections.Generic;
using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    public sealed class WorldWaterSurface : MonoBehaviour
    {
        public static readonly List<WorldWaterSurface> Active=new List<WorldWaterSurface>();
        public Color color=new Color(.2f,.6f,.72f,.85f);
        public float halfSize=6;
        public bool Contains(Vector3 point)=>Mathf.Abs(point.x-transform.position.x)<halfSize&&Mathf.Abs(point.z-transform.position.z)<halfSize;
        void OnEnable()=>Active.Add(this);
        void OnDisable()=>Active.Remove(this);
    }
}
