using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Inertial horsehair plume, pinned to the helmet and independent of the cape.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(410)]
    public sealed class WarriorPlumeMotion : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Transform[] chain=new Transform[0];
        [SerializeField] Vector3[] restTips=new Vector3[0];
        [SerializeField] Quaternion[] restRotations=new Quaternion[0];
        [SerializeField,Min(1)] float stiffness=115f;
        [SerializeField,Min(0)] float damping=15f;
        [SerializeField,Range(1,40)] float maxAngle=22f;
        Vector3[] tips,velocities;
        Vector3 previousAnchor;
        Quaternion previousHeadRotation;
        bool ready;

        public void Configure(Transform helmet,Transform[] bones,Vector3 finalTip)
        {
            if(helmet==null || bones==null || bones.Length!=5)throw new System.ArgumentException("A plume needs Head and five chain bones.");
            head=helmet;chain=bones;restTips=new Vector3[bones.Length];restRotations=new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                restRotations[i]=bones[i].localRotation;
                restTips[i]=bones[i].InverseTransformPoint(i+1<bones.Length?bones[i+1].position:finalTip);
            }
            ready=false;
        }
        void OnEnable()=>ready=false;
        void LateUpdate()=>Advance(Time.deltaTime);
        void OnDisable(){Restore();ready=false;}
        void Restore()
        {
            for(int i=0;i<chain.Length && i<restRotations.Length;i++)if(chain[i]!=null)chain[i].localRotation=restRotations[i];
        }
        void ResetSimulation()
        {
            Restore();
            if(tips==null || tips.Length!=chain.Length){tips=new Vector3[chain.Length];velocities=new Vector3[chain.Length];}
            for(int i=0;i<chain.Length;i++){tips[i]=chain[i].TransformPoint(restTips[i]);velocities[i]=Vector3.zero;}
            previousAnchor=chain[0].position;previousHeadRotation=head.rotation;ready=true;
        }
        public void Advance(float deltaTime)
        {
            if(deltaTime<=0 || head==null || chain.Length==0 || chain.Length!=restTips.Length || chain.Length!=restRotations.Length)return;
            foreach(var bone in chain)if(bone==null)return;
            if(!ready || deltaTime>.15f || (chain[0].position-previousAnchor).sqrMagnitude>4f || Quaternion.Angle(head.rotation,previousHeadRotation)>120)
            {ResetSimulation();return;}
            previousAnchor=chain[0].position;previousHeadRotation=head.rotation;
            int steps=Mathf.Clamp(Mathf.CeilToInt(deltaTime*90),1,14);float dt=deltaTime/steps;
            Vector3 helmetCenter=head.position+head.up*(.28f*Mathf.Abs(transform.lossyScale.y));
            float radius=.32f*Mathf.Abs(transform.lossyScale.y);
            for(int step=0;step<steps;step++)
            for(int i=0;i<chain.Length;i++)
            {
                var bone=chain[i];bone.localRotation=restRotations[i];
                Vector3 anchor=bone.position,rest=bone.TransformPoint(restTips[i])-anchor;
                float length=rest.magnitude;if(length<.0001f)continue;
                velocities[i]+=(anchor+rest-tips[i])*(stiffness*dt);velocities[i]*=Mathf.Exp(-damping*dt);
                Vector3 next=tips[i]+velocities[i]*dt;Vector3 direction=rest.normalized;
                for(int iteration=0;iteration<3;iteration++)
                {
                    Vector3 offset=next-helmetCenter;
                    if(offset.sqrMagnitude<radius*radius)next=helmetCenter+(offset.sqrMagnitude>.00001f?offset.normalized:-transform.forward)*radius;
                    direction=Vector3.RotateTowards(rest,next-anchor,(i==0?maxAngle*.7f:maxAngle)*Mathf.Deg2Rad,0).normalized;
                    if(direction.sqrMagnitude<.01f)direction=rest.normalized;
                    next=anchor+direction*length;
                }
                velocities[i]=Vector3.ProjectOnPlane(velocities[i],direction);
                bone.rotation=Quaternion.FromToRotation(rest,direction)*bone.rotation;tips[i]=next;
            }
        }
    }
}
