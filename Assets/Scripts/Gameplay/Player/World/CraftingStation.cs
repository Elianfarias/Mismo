using System.Collections.Generic;
using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    public sealed class CraftingStation:MonoBehaviour
    {
        public static readonly HashSet<CraftingStation> Loaded=new HashSet<CraftingStation>();
        public CraftingRecipe[] recipes;
        [Min(1)] public float range=3;
        public bool InRange(Vector3 position)=>isActiveAndEnabled&&Vector3.Distance(position,transform.position)<=range;
        void OnEnable()=>Loaded.Add(this);
        void OnDisable()=>Loaded.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>Loaded.Clear();
    }
}
