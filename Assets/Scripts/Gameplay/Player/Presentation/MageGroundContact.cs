using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Aligns a voxel skin's supporting boot with the grounded capsule.
    /// Runs after body posing and before weapon attachments and cloth motion.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(150)]
    public sealed class MageGroundContact : MonoBehaviour
    {
        const float Clearance = .002f;
        const float MaximumCorrection = .3f;
        float maximumCorrection = MaximumCorrection;
        CharacterController body;
        Transform hips;
        readonly List<Transform> feet = new List<Transform>();
        readonly List<Vector3[]> samples = new List<Vector3[]>();
        Vector3 originalHipsPosition;
        bool applied;

        void Awake() => Initialize(GetComponent<Animator>(), GetComponentInParent<CharacterController>());

        public void Initialize(Animator animator, CharacterController controller)
        {
            RestorePose();feet.Clear();samples.Clear();body=controller;maximumCorrection=MaximumCorrection;
            if(animator==null || animator.avatar==null || !animator.isHuman)return;
            hips=animator.GetBoneTransform(HumanBodyBones.Hips);
            foreach(var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!renderer.name.EndsWith("_Boot_L",System.StringComparison.Ordinal) &&
                   !renderer.name.EndsWith("_Boot_R",System.StringComparison.Ordinal) &&
                   !renderer.name.EndsWith("_Foot_L",System.StringComparison.Ordinal) &&
                   !renderer.name.EndsWith("_Foot_R",System.StringComparison.Ordinal))continue;
                var foot=animator.GetBoneTransform(renderer.name.EndsWith("_L")?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                var mesh=renderer.sharedMesh;
                if(foot==null || mesh==null || !mesh.isReadable)continue;
                int index=System.Array.IndexOf(renderer.bones,foot);
                if(index<0 || index>=mesh.bindposes.Length)continue;
                // These boots are rigidly skinned to one foot. Cache their exact support
                // points in bone space once; no mesh baking or allocation each frame.
                var unique=new HashSet<Vector3>();var vertices=mesh.vertices;var weights=mesh.boneWeights;
                var bind=mesh.bindposes[index];
                for(int i=0;i<vertices.Length;i++)
                    if(weights[i].boneIndex0==index && weights[i].weight0>.999f)
                        unique.Add(bind.MultiplyPoint3x4(vertices[i]));
                if(unique.Count==0)continue;
                // Amphibian toes reach farther past the Humanoid foot pivot than
                // the boots. Include that extra reach in the bounded correction.
                if(renderer.name.EndsWith("_Foot_L",System.StringComparison.Ordinal) ||
                   renderer.name.EndsWith("_Foot_R",System.StringComparison.Ordinal))
                    maximumCorrection=Mathf.Max(maximumCorrection,.45f*Mathf.Abs(transform.lossyScale.y));
                var points=new Vector3[unique.Count];unique.CopyTo(points);feet.Add(foot);samples.Add(points);
            }
        }

        void Update() => RestorePose();
        void OnDisable() => RestorePose();
        public void RestorePose()
        {
            if(applied && hips!=null)hips.localPosition=originalHipsPosition;
            applied=false;
        }

        void LateUpdate()
        {
            if(body==null || !body.enabled)return;
            // The grounded CharacterController leaves skinWidth between its bottom
            // and the surface. Keep the collider and its motion entirely unchanged.
            float surface=body.bounds.min.y-body.skinWidth*Mathf.Abs(body.transform.lossyScale.y);
            ApplyGrounding(body.isGrounded,surface);
        }

        public void ApplyGrounding(bool grounded,float surfaceY)
        {
            if(!grounded || hips==null || feet.Count!=2)return;
            float sole=float.PositiveInfinity;
            for(int i=0;i<feet.Count;i++)
            {
                var matrix=feet[i].localToWorldMatrix;
                foreach(var point in samples[i])sole=Mathf.Min(sole,matrix.MultiplyPoint3x4(point).y);
            }
            float correction=Mathf.Clamp(surfaceY+Clearance-sole,-maximumCorrection,maximumCorrection);
            originalHipsPosition=hips.localPosition;applied=true;
            hips.position+=Vector3.up*correction;
        }
    }
}
