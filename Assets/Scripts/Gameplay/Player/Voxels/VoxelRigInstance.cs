using UnityEngine;

namespace Mismo.Gameplay.Player.Voxels
{
    /// <summary>Animation library exported by the model voxelizer. State names match clip names.</summary>
    public sealed class VoxelRigInstance : MonoBehaviour
    {
        public Animator animator;
        public SkinnedMeshRenderer surface;
        public AnimationClip[] clips;
        public string[] stateNames;
        public void Play(string stateName, float blendSeconds = .15f)
        { if (animator != null && animator.HasState(0, Animator.StringToHash(stateName))) animator.CrossFadeInFixedTime(stateName, blendSeconds); }
    }
}
