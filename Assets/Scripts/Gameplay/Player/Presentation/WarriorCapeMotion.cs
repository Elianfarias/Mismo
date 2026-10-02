using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Three articulated cloth strips. Only cape bones are simulated;
    /// Humanoid animation, root motion and the character collider remain untouched.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(400)]
    public sealed class WarriorCapeMotion : MonoBehaviour
    {
        const int Links = 4;
        [SerializeField] Transform[] bones = new Transform[0];
        [SerializeField] Vector3[] restTips = new Vector3[0];
        [SerializeField] Quaternion[] restRotations = new Quaternion[0];
        [SerializeField] Transform hips, neck, leftThigh, leftKnee, rightThigh, rightKnee;
        [SerializeField, Min(1)] float stiffness = 95f;
        [SerializeField, Min(0)] float damping = 13f;
        [SerializeField, Range(5, 45)] float maxAngle = 28f;
        Vector3[] tips, velocities;
        Vector3 previousAnchor;
        Quaternion previousRotation;
        bool ready;

        public void Configure(Animator animator, Transform[] chainBones, Vector3[] finalTips)
        {
            if(chainBones.Length != 3 * Links || finalTips.Length != 3)
                throw new System.ArgumentException("The cape needs three four-bone chains.");
            bones = chainBones;
            restTips = new Vector3[bones.Length];
            restRotations = new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                restRotations[i] = bones[i].localRotation;
                var end = i%Links == Links-1 ? finalTips[i/Links] : bones[i+1].position;
                restTips[i] = bones[i].InverseTransformPoint(end);
            }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            if(hips==null || neck==null || leftThigh==null || leftKnee==null || rightThigh==null || rightKnee==null)
                throw new System.ArgumentException("Cape body collision needs a rebound Humanoid animator.");
            ready = false;
        }

        void OnEnable() => ready = false;
        void LateUpdate() => Advance(Time.deltaTime);
        void OnDisable() { Restore(); ready = false; }

        void Restore()
        {
            for(int i=0;i<bones.Length && i<restRotations.Length;i++)
                if(bones[i]!=null) bones[i].localRotation=restRotations[i];
        }

        void ResetSimulation()
        {
            Restore();
            if(tips==null || tips.Length!=bones.Length)
            { tips=new Vector3[bones.Length]; velocities=new Vector3[bones.Length]; }
            for(int i=0;i<bones.Length;i++)
            { tips[i]=bones[i].TransformPoint(restTips[i]); velocities[i]=Vector3.zero; }
            previousAnchor=bones[0].position; previousRotation=transform.rotation; ready=true;
        }

        public void Advance(float deltaTime)
        {
            if(deltaTime<=0 || bones.Length!=12 || restTips.Length!=bones.Length || restRotations.Length!=bones.Length)return;
            for(int i=0;i<bones.Length;i++)if(bones[i]==null)return;
            if(!ready || deltaTime>.15f || (bones[0].position-previousAnchor).sqrMagnitude>4f ||
               Quaternion.Angle(transform.rotation,previousRotation)>120f)
            { ResetSimulation(); return; }
            previousAnchor=bones[0].position; previousRotation=transform.rotation;
            int steps=Mathf.Clamp(Mathf.CeilToInt(deltaTime*90),1,14);
            float dt=deltaTime/steps;
            float scale=Mathf.Abs(transform.lossyScale.y);
            for(int step=0;step<steps;step++)
            for(int i=0;i<bones.Length;i++)
            {
                var bone=bones[i]; bone.localRotation=restRotations[i];
                Vector3 anchor=bone.position, rest=bone.TransformPoint(restTips[i])-anchor;
                float length=rest.magnitude; if(length<.0001f)continue;
                velocities[i]+=(anchor+rest-tips[i])*(stiffness*dt);
                velocities[i]*=Mathf.Exp(-damping*dt);
                Vector3 next=tips[i]+velocities[i]*dt;
                float angle=(i%Links==0 ? maxAngle*.75f : maxAngle)*Mathf.Deg2Rad;
                Vector3 direction=rest.normalized;
                for(int iteration=0;iteration<4;iteration++)
                {
                    next=OutsideBody(next,scale);
                    direction=Vector3.RotateTowards(rest,next-anchor,angle,0).normalized;
                    if(direction.sqrMagnitude<.01f)direction=rest.normalized;
                    next=anchor+direction*length;
                }
                velocities[i]=Vector3.ProjectOnPlane(velocities[i],direction);
                bone.rotation=Quaternion.FromToRotation(rest,direction)*bone.rotation;
                tips[i]=next;
            }
        }

        Vector3 OutsideBody(Vector3 point,float scale)
        {
            Vector3 back=-transform.forward;
            if(hips!=null && neck!=null)
            {
                // A rear half-plane prevents a lagging strip choosing the front of
                // the body when the character reverses direction or turns sharply.
                float height=Vector3.Dot(point-hips.position,transform.up);
                float clearance=(height>.15f*scale?.205f:.125f)*scale;
                float rear=Vector3.Dot(point-hips.position,back);
                if(rear<clearance)point+=back*(clearance-rear);
                point=OutsideCapsule(point,hips.position,neck.position,.235f*scale,back);
            }
            if(leftThigh!=null && leftKnee!=null)
                point=OutsideCapsule(point,leftThigh.position,leftKnee.position,.155f*scale,back);
            if(rightThigh!=null && rightKnee!=null)
                point=OutsideCapsule(point,rightThigh.position,rightKnee.position,.155f*scale,back);
            return point;
        }

        static Vector3 OutsideCapsule(Vector3 point,Vector3 a,Vector3 b,float radius,Vector3 fallback)
        {
            Vector3 axis=b-a;
            float t=axis.sqrMagnitude<.00001f?0:Mathf.Clamp01(Vector3.Dot(point-a,axis)/axis.sqrMagnitude);
            Vector3 center=a+t*axis, offset=point-center;
            if(offset.sqrMagnitude>=radius*radius)return point;
            return center+(offset.sqrMagnitude>.000001f?offset.normalized:fallback)*radius;
        }
    }
}
