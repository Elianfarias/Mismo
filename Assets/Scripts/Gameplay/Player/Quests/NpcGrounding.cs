using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Quests
{
    /// <summary>Plants the supporting foot on visible collision geometry without moving the navigation agent.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(100)]
    public sealed class NpcGrounding:MonoBehaviour
    {
        const float Clearance=.008f;
        readonly RaycastHit[] hits=new RaycastHit[32];
        Transform visual;
        Animator animator;
        SkinContacts[] skins;
        bool initialized;
        struct Contact { public Vector3 vertex;public BoneWeight weight;public bool left; }
        sealed class SkinContacts
        {
            public Transform[] bones;
            public Matrix4x4[] bindPoses,matrices;
            public Contact[] contacts;
            public void UpdateMatrices()
            {for(int i=0;i<bones.Length;i++)if(bones[i]!=null)matrices[i]=bones[i].localToWorldMatrix*bindPoses[i];}
            public Vector3 Position(Vector3 vertex,BoneWeight w)
            {
                var p=matrices[w.boneIndex0].MultiplyPoint3x4(vertex)*w.weight0;
                if(w.weight1>0)p+=matrices[w.boneIndex1].MultiplyPoint3x4(vertex)*w.weight1;
                if(w.weight2>0)p+=matrices[w.boneIndex2].MultiplyPoint3x4(vertex)*w.weight2;
                if(w.weight3>0)p+=matrices[w.boneIndex3].MultiplyPoint3x4(vertex)*w.weight3;
                return p;
            }
        }
        bool Initialize()
        {
            animator=GetComponentInChildren<Animator>();
            if(animator==null||!animator.isInitialized||!animator.isHuman)return false;
            var left=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var right=animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if(left==null||right==null)return false;
            visual=animator.transform;
            while(visual.parent!=null&&visual.parent!=transform)visual=visual.parent;
            if(visual==transform)return false;
            var result=new List<SkinContacts>();
            foreach(var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=renderer.sharedMesh;if(mesh==null||!mesh.isReadable)continue;
                var vertices=mesh.vertices;var weights=mesh.boneWeights;var bones=renderer.bones;var bindPoses=mesh.bindposes;
                if(weights.Length!=vertices.Length||bindPoses.Length!=bones.Length)continue;
                var skin=new SkinContacts{bones=bones,bindPoses=bindPoses,matrices=new Matrix4x4[bones.Length]};skin.UpdateMatrices();
                var contacts=new List<Contact>();float ankleHeight=Mathf.Max(left.position.y,right.position.y)+.08f;
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=skin.Position(vertices[i],weights[i]);if(p.y>ankleHeight)continue;
                    contacts.Add(new Contact{vertex=vertices[i],weight=weights[i],left=(p-left.position).sqrMagnitude<(p-right.position).sqrMagnitude});
                }
                if(contacts.Count==0)continue;skin.contacts=contacts.ToArray();result.Add(skin);
            }
            skins=result.ToArray();initialized=true;return skins.Length>0;
        }
        void LateUpdate()
        {
            if(!initialized&&!Initialize()||skins==null||skins.Length==0)return;
            var left=new Vector3(0,float.MaxValue,0);var right=left;
            // Only cached shoe/hem vertices are skinned; no BakeMesh, mesh arrays or allocations per frame.
            foreach(var skin in skins)
            {
                skin.UpdateMatrices();
                foreach(var contact in skin.contacts)
                {
                    var p=skin.Position(contact.vertex,contact.weight);
                    if(contact.left){if(p.y<left.y)left=p;}
                    else if(p.y<right.y)right=p;
                }
            }
            float correction=float.NegativeInfinity;
            if(left.y<float.MaxValue&&Ground(left,out float ly))correction=ly+Clearance-left.y;
            if(right.y<float.MaxValue&&Ground(right,out float ry))correction=Mathf.Max(correction,ry+Clearance-right.y);
            if(float.IsNegativeInfinity(correction)||Mathf.Abs(correction)>1.5f||Mathf.Abs(correction)<.0005f)return;
            // The higher foot remains free to swing. Root, collider, agent and authored routes stay intact.
            visual.position+=Vector3.up*correction;
        }
        bool Ground(Vector3 foot,out float y)
        {
            var origin=new Vector3(foot.x,transform.position.y+1,foot.z);
            int count=Physics.RaycastNonAlloc(origin,Vector3.down,hits,3,~0,QueryTriggerInteraction.Ignore);
            float distance=float.MaxValue;y=0;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];if(hit.normal.y<.5f||hit.distance>=distance||hit.transform.IsChildOf(transform)||
                    hit.collider.GetComponentInParent<Health>()!=null||hit.collider.GetComponentInParent<QuestInteractable>()!=null)continue;
                distance=hit.distance;y=hit.point.y;
            }
            return distance<float.MaxValue;
        }
    }
}
