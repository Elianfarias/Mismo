using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    public sealed class GeneratedWorldMesh : MonoBehaviour
    {
        public Mesh Value;
        void OnDestroy(){if(Value!=null)Destroy(Value);}
    }
}
