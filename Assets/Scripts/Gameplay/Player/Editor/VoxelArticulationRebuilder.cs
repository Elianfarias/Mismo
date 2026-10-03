using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Samples a separated articulation, then returns geometry to the existing rig's bind space.</summary>
    internal static class VoxelArticulationRebuilder
    {
        sealed class Pose
        {
            readonly Transform[] bones;
            readonly Vector3[] positions, scales;
            readonly Quaternion[] rotations;
            public Pose(IEnumerable<Transform> transforms)
            {
                bones = transforms.ToArray();
                positions = bones.Select(t => t.localPosition).ToArray();
                rotations = bones.Select(t => t.localRotation).ToArray();
                scales = bones.Select(t => t.localScale).ToArray();
            }
            public void Restore()
            {
                for (int i = 0; i < bones.Length; i++)
                { bones[i].localPosition = positions[i]; bones[i].localRotation = rotations[i]; bones[i].localScale = scales[i]; }
            }
        }

        // Call on temporary instances. Existing assets, clips, hierarchy and bindposes are preserved.
        public static Mesh Build(GameObject source, VoxelRigInstance target, AnimationClip referenceClip,
            float normalizedTime, string[] posedBonePaths, string separationBonePath, int resolution = 128)
        {
            if (source == null || target == null || target.surface == null || target.animator == null || referenceClip == null)
                throw new ArgumentException("Source, target rig and reference clip are required.");
            if (referenceClip.humanMotion || (target.animator.avatar != null && target.animator.avatar.isHuman))
                throw new InvalidOperationException("Articulation resampling currently requires a Generic rig.");
            var posedBones = posedBonePaths.Select(path => source.transform.Find(path) ??
                throw new InvalidOperationException("Missing articulation: " + path)).ToArray();
            var separate = source.transform.Find(separationBonePath);
            if (separate == null) throw new InvalidOperationException("Missing separation bone: " + separationBonePath);
            var sourceRest = new Pose(source.GetComponentsInChildren<Transform>(true));
            var targetRest = new Pose(target.transform.GetComponentsInChildren<Transform>(true));
            Mesh mesh = null;
            try
            {
                referenceClip.SampleAnimation(source, referenceClip.length * Mathf.Clamp01(normalizedTime));
                var articulation = new Pose(posedBones.SelectMany(t => t.GetComponentsInChildren<Transform>(true)).Distinct());
                sourceRest.Restore();
                articulation.Restore();
                // Sample only the mouth in its open pose; the body remains in its original rest pose.
                foreach (var bone in posedBones.SelectMany(t => t.GetComponentsInChildren<Transform>(true)).Distinct())
                {
                    string path = AnimationUtility.CalculateTransformPath(bone, source.transform);
                    var destination = target.animator.transform.Find(path);
                    if (destination == null) throw new InvalidOperationException("Missing target articulation: " + path);
                    destination.localPosition = bone.localPosition;
                    destination.localRotation = bone.localRotation;
                    destination.localScale = bone.localScale;
                }
                var sample = VoxelSurfaceSampler.Sample(source, resolution, true, false, true);
                var targetIndices = target.surface.bones.Select((bone, i) => (bone, i)).ToDictionary(
                    p => AnimationUtility.CalculateTransformPath(p.bone, target.animator.transform), p => p.i);
                int[] remap = sample.Bones.Select(bone => targetIndices[AnimationUtility.CalculateTransformPath(bone, source.transform)]).ToArray();
                var jaw = new HashSet<int>(sample.Bones.Select((bone, i) => (bone, i))
                    .Where(p => p.bone == separate || p.bone.IsChildOf(separate)).Select(p => p.i));
                int[] regions = sample.BoneWeights.Select(w => Influence(w, jaw) >= .5f ? 1 : 0).ToArray();
                // The open mouth is reachable from outside, so the flood fill preserves its cavity.
                // Filling only enclosed space lets the mesh builder omit buried body voxels.
                VoxelMeshBuilder.FillInterior(sample.Occupied, sample.Dimensions);
                mesh = VoxelMeshBuilder.Build(sample.Occupied, sample.Dimensions, sample.VoxelSize, sample.Bounds.min,
                    Vector3.zero, Color.white, target.name + "_Articulated", true, sample.MaterialIndices,
                    sample.UVs, sample.Materials.Count + 1, sample.BoneWeights, regions);
                var weights = mesh.boneWeights;
                for (int i = 0; i < weights.Length; i++)
                {
                    var w = weights[i];
                    w.boneIndex0 = remap[w.boneIndex0]; w.boneIndex1 = remap[w.boneIndex1];
                    w.boneIndex2 = remap[w.boneIndex2]; w.boneIndex3 = remap[w.boneIndex3]; weights[i] = w;
                }
                var bindposes = target.surface.sharedMesh.bindposes;
                var skinMatrices = target.surface.bones.Select((b, i) => target.surface.transform.worldToLocalMatrix * b.localToWorldMatrix * bindposes[i]).ToArray();
                Matrix4x4 samplingToTarget = target.surface.transform.worldToLocalMatrix * target.animator.transform.localToWorldMatrix *
                    source.transform.worldToLocalMatrix * Matrix4x4.TRS(source.transform.position, source.transform.rotation, Vector3.one);
                var points = mesh.vertices;
                var normals = mesh.normals;
                for (int i = 0; i < points.Length; i++)
                {
                    var blend = Blend(weights[i], skinMatrices);
                    if (Mathf.Abs(blend.determinant) < 1e-8f) throw new InvalidOperationException("Singular articulation skin matrix.");
                    points[i] = blend.inverse.MultiplyPoint3x4(samplingToTarget.MultiplyPoint3x4(points[i]));
                    normals[i] = blend.transpose.MultiplyVector(samplingToTarget.inverse.transpose.MultiplyVector(normals[i])).normalized;
                }
                mesh.vertices = points; mesh.normals = normals; mesh.boneWeights = weights; mesh.bindposes = bindposes;
                mesh.RecalculateBounds();
                return mesh;
            }
            catch { if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh); throw; }
            finally { sourceRest.Restore(); targetRest.Restore(); }
        }

        static float Influence(BoneWeight w, HashSet<int> bones) =>
            (bones.Contains(w.boneIndex0) ? w.weight0 : 0) + (bones.Contains(w.boneIndex1) ? w.weight1 : 0) +
            (bones.Contains(w.boneIndex2) ? w.weight2 : 0) + (bones.Contains(w.boneIndex3) ? w.weight3 : 0);

        static Matrix4x4 Blend(BoneWeight w, Matrix4x4[] bones)
        {
            var result = new Matrix4x4();
            for (int i = 0; i < 16; i++) result[i] = bones[w.boneIndex0][i] * w.weight0 + bones[w.boneIndex1][i] * w.weight1 +
                bones[w.boneIndex2][i] * w.weight2 + bones[w.boneIndex3][i] * w.weight3;
            return result;
        }
    }
}
