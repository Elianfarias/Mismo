using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    // Editor-only, analytic two-bone IK. No runtime constraints or package dependency.
    public static class HumanoidAttackIK
    {
        public readonly struct Limb
        {
            public readonly Transform upper, lower, tip;
            public readonly HumanBodyBones tipBone, bendBone;
            public bool IsArm => tipBone == HumanBodyBones.LeftHand || tipBone == HumanBodyBones.RightHand;
            public float Length => Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, tip.position);

            public Limb(Animator animator, HumanBodyBones root, HumanBodyBones middle, HumanBodyBones end)
            {
                upper = animator.GetBoneTransform(root); lower = animator.GetBoneTransform(middle); tip = animator.GetBoneTransform(end);
                tipBone = end; bendBone = middle;
            }

            public Vector3 HintPosition()
            {
                Vector3 axis = (tip.position - upper.position).normalized;
                Vector3 bend = BendDirection(axis, lower.position - upper.position, upper);
                // Put the control away from the joint so it stays usable near full extension.
                return lower.position + bend * Length * .5f;
            }
        }

        public static readonly HumanBodyBones[] Endpoints =
        {
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot
        };

        public static bool TryGetLimb(Animator animator, HumanBodyBones bone, out Limb limb)
        {
            limb = default;
            if (animator == null || !animator.isHuman) return false;
            switch (bone)
            {
                case HumanBodyBones.LeftUpperArm: case HumanBodyBones.LeftLowerArm: case HumanBodyBones.LeftHand:
                    limb = new Limb(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand); break;
                case HumanBodyBones.RightUpperArm: case HumanBodyBones.RightLowerArm: case HumanBodyBones.RightHand:
                    limb = new Limb(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand); break;
                case HumanBodyBones.LeftUpperLeg: case HumanBodyBones.LeftLowerLeg: case HumanBodyBones.LeftFoot:
                    limb = new Limb(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot); break;
                case HumanBodyBones.RightUpperLeg: case HumanBodyBones.RightLowerLeg: case HumanBodyBones.RightFoot:
                    limb = new Limb(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot); break;
                default: return false;
            }
            return limb.upper != null && limb.lower != null && limb.tip != null &&
                limb.lower.IsChildOf(limb.upper) && limb.tip.IsChildOf(limb.lower);
        }

        public static bool Solve(Limb limb, Vector3 target, Vector3 hint, bool keepTipRotation = true)
        {
            if (limb.upper == null || limb.lower == null || limb.tip == null || !Finite(target) || !Finite(hint)) return false;
            Vector3 origin = limb.upper.position;
            float first = Vector3.Distance(origin, limb.lower.position);
            float second = Vector3.Distance(limb.lower.position, limb.tip.position);
            if (first < .00001f || second < .00001f) return false;
            Quaternion tipRotation = limb.tip.rotation;
            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            if (distance < .00001f) direction = limb.tip.position - origin;
            if (direction.sqrMagnitude < .00000001f) direction = limb.lower.position - origin;
            direction.Normalize();
            // Stay just inside the reachable annulus to keep a well-defined bend plane.
            float epsilon = Mathf.Min(first, second) * .0001f;
            distance = Mathf.Clamp(distance, Mathf.Abs(first - second) + epsilon, first + second - epsilon);
            Vector3 reached = origin + direction * distance;
            Vector3 bend = Vector3.ProjectOnPlane(hint - origin, direction);
            if (bend.sqrMagnitude < .00000001f) bend = limb.lower.position - origin;
            bend = BendDirection(direction, bend, limb.upper);
            float along = (first * first - second * second + distance * distance) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
            Vector3 elbow = origin + direction * along + bend * height;

            limb.upper.rotation = Quaternion.FromToRotation(limb.lower.position - origin, elbow - origin) * limb.upper.rotation;
            limb.lower.rotation = Quaternion.FromToRotation(limb.tip.position - limb.lower.position, reached - limb.lower.position) * limb.lower.rotation;
            if (keepTipRotation) limb.tip.rotation = tipRotation;
            return true;
        }

        static Vector3 BendDirection(Vector3 axis, Vector3 preferred, Transform root)
        {
            Vector3 result = Vector3.ProjectOnPlane(preferred, axis);
            if (result.sqrMagnitude < .00000001f) result = Vector3.ProjectOnPlane(root.up, axis);
            if (result.sqrMagnitude < .00000001f) result = Vector3.ProjectOnPlane(root.forward, axis);
            if (result.sqrMagnitude < .00000001f) result = Vector3.ProjectOnPlane(root.right, axis);
            return result.normalized;
        }

        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
    }
}
