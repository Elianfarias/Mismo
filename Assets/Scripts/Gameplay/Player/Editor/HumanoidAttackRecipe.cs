using System;
using System.Collections.Generic;
using UnityEngine;
using Mismo.Gameplay.Player.Equipment;

namespace Mismo.Gameplay.Player.Editor
{
    public enum AttackPoseBlend { Smooth, Linear }
    public enum HumanoidAttackTemplate { HorizontalSlash, Overhead, Thrust, Punch, Blank }

    [Serializable]
    public sealed class HumanoidAttackPose
    {
        public string label = "Pose";
        [Range(0, 1)] public float time;
        public AttackPoseBlend blend;
        public Vector3 bodyPosition;
        public Quaternion bodyRotation = Quaternion.identity;
        // Unity constructs serialized poses off the main thread. Capture/Copy fills this array.
        public float[] muscles = Array.Empty<float>();

        public HumanoidAttackPose Copy(float at, string title = null) => new HumanoidAttackPose
        {
            label = title ?? label, time = at, blend = blend, bodyPosition = bodyPosition,
            bodyRotation = bodyRotation, muscles = (float[])muscles.Clone()
        };

        public HumanPose ToHumanPose() => new HumanPose
        {
            bodyPosition = bodyPosition, bodyRotation = bodyRotation, muscles = (float[])muscles.Clone()
        };

        public void Read(HumanPose pose)
        {
            bodyPosition = pose.bodyPosition;
            bodyRotation = pose.bodyRotation;
            muscles = (float[])pose.muscles.Clone();
        }
    }

    // Authoring-only document. Exported AnimationClips have no dependency on it.
    public sealed class HumanoidAttackRecipe : ScriptableObject
    {
        public GameObject model;
        public WeaponDefinition previewWeapon;
        public bool showPreviewWeapons = true;
        public bool previewPair;
        public WeaponDefinition previewOffhand;
        public string clipName = "Ataque_Humanoid";
        public float duration = .9f;
        public int frameRate = 60;
        public float activeStartsAt = .36f;
        public float recoveryStartsAt = .62f;
        public List<HumanoidAttackPose> poses = new List<HumanoidAttackPose>();
    }
}
