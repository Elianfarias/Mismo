using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Small, inertial spring chain for the cloth tip; independent of Humanoid animation.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(400)]
    public sealed class MageHatMotion : MonoBehaviour
    {
        [SerializeField] private Transform[] chain = new Transform[0];
        [SerializeField] private Vector3[] restTips = new Vector3[0];
        [SerializeField] private Quaternion[] restRotations = new Quaternion[0];
        [SerializeField, Min(1)] private float stiffness = 145f;
        [SerializeField, Min(0)] private float damping = 17f;
        [SerializeField, Range(1, 40)] private float maxAngle = 18f;
        private Vector3[] tips, velocities;
        private Vector3 previousAnchor;
        private bool ready;

        public void Configure(Transform[] bones, Vector3 finalTip)
        {
            chain = bones;
            restTips = new Vector3[bones.Length];
            restRotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                restRotations[i] = bones[i].localRotation;
                Vector3 end = i + 1 < bones.Length ? bones[i + 1].position : finalTip;
                restTips[i] = bones[i].InverseTransformPoint(end);
            }
            ready = false;
        }

        void OnEnable() => ready = false;
        void LateUpdate() => Advance(Time.deltaTime);
        void OnDisable()
        {
            Restore();
            ready = false;
        }
        void Restore()
        {
            for (int i = 0; i < chain.Length && i < restRotations.Length; i++)
                if (chain[i] != null) chain[i].localRotation = restRotations[i];
        }

        public void Advance(float deltaTime)
        {
            if (chain.Length == 0 || chain.Length != restTips.Length || chain.Length != restRotations.Length) return;
            foreach (var bone in chain) if (bone == null) return;
            if (deltaTime <= 0f) return;
            if (!ready || deltaTime > .15f || (chain[0].position - previousAnchor).sqrMagnitude > 1f)
            {
                Restore();
                tips = new Vector3[chain.Length];
                velocities = new Vector3[chain.Length];
                for (int i = 0; i < chain.Length; i++) tips[i] = chain[i].TransformPoint(restTips[i]);
                ready = true;
                previousAnchor = chain[0].position;
                return;
            }
            previousAnchor = chain[0].position;
            int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / (1f / 90f)), 1, 14);
            float dt = deltaTime / steps;
            for (int step = 0; step < steps; step++)
            {
                for (int i = 0; i < chain.Length; i++)
                {
                    Transform bone = chain[i];
                    bone.localRotation = restRotations[i];
                    Vector3 anchor = bone.position;
                    Vector3 rest = bone.TransformPoint(restTips[i]) - anchor;
                    float length = rest.magnitude;
                    if (length < .0001f) continue;
                    Vector3 target = anchor + rest;
                    velocities[i] += (target - tips[i]) * (stiffness * dt);
                    velocities[i] *= Mathf.Exp(-damping * dt);
                    Vector3 candidate = tips[i] + velocities[i] * dt - anchor;
                    Vector3 direction = Vector3.RotateTowards(rest, candidate, maxAngle * Mathf.Deg2Rad, 0).normalized;
                    if (direction.sqrMagnitude < .01f) direction = rest.normalized;
                    Vector3 next = anchor + direction * length;
                    // Remove radial velocity; only angular motion bends the cloth.
                    velocities[i] = Vector3.ProjectOnPlane(velocities[i], direction);
                    bone.rotation = Quaternion.FromToRotation(rest, direction) * bone.rotation;
                    tips[i] = next;
                }
            }
        }
    }
}
