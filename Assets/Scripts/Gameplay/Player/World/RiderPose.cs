using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    [CreateAssetMenu(menuName="Mismo/Bestiary/Rider pose")]
    public sealed class RiderPose:ScriptableObject
    {
        public Vector3 position=new Vector3(0,1,0),rotation;
        [Min(.01f)]public float scale=1;
        public AnimationClip seatedAnimation;
        [Range(0,1)]public float sampleTime;
        [System.Serializable]public sealed class Bone {public string path;public Vector3 rotation;}
        public Bone[] bones=System.Array.Empty<Bone>();
        public void Apply(Transform root){if(root==null||bones==null)return;foreach(var bone in bones){var target=root.Find(bone.path);if(target!=null)target.localRotation=Quaternion.Euler(bone.rotation);}}
    }
}
