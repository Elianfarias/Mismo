using UnityEngine;
using UnityEngine.AI;
namespace Mismo.Gameplay.Enemies
{
    public sealed class SoulEaterArenaNavigation:MonoBehaviour
    {
        [SerializeField]NavMeshData data;
        NavMeshDataInstance instance;
        public void Configure(NavMeshData value)=>data=value;
        void OnEnable(){if(data!=null)instance=NavMesh.AddNavMeshData(data);}
        void OnDisable(){if(instance.valid)instance.Remove();}
    }
}
