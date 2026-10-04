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

        // Replacing the whole body with an open-mouth sample can erase torso voxels where
        // the posed jaw overlaps the chest. Preserve the existing rest surface outside the
        // articulation, including its UVs and skin weights. MeshBuilder emits 24 vertices per voxel.
        public static Mesh PreserveRestSurface(Mesh rest, Mesh articulated, HashSet<int> articulationBones, int lowerJaw=-1, float chinHeight=float.NegativeInfinity, HashSet<int> upperLipBones=null)
        {
            if (rest.vertexCount % 24 != 0 || articulated.vertexCount % 24 != 0)
                throw new InvalidOperationException("Expected complete voxel blocks before adding eye details.");
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uvs = new List<Vector2>(); var colors = new List<Color>(); var weights = new List<BoneWeight>();
            var triangles = Enumerable.Range(0, articulated.subMeshCount).Select(_ => new List<int>()).ToArray();
            foreach (var mesh in new[] { rest, articulated })
            {
                var points = mesh.vertices; var sourceNormals = mesh.normals; var sourceUvs = mesh.uv;
                var sourceColors = mesh.colors; var sourceWeights = mesh.boneWeights;
                var remap = Enumerable.Repeat(-1, points.Length).ToArray();
                for (int start = 0; start < points.Length; start += 24)
                {
                    float influence = 0;
                    for (int i = start; i < start + 24; i++)
                        influence = Mathf.Max(influence, Influence(sourceWeights[i], articulationBones));
                    bool belongsToMouth = influence >= .5f;
                    Vector3 center=Vector3.zero;
                    for(int i=start;i<start+24;i++)center+=points[i]/24;
                    bool throat=mesh==rest && belongsToMouth && lowerJaw>=0 && center.y<chinHeight;
                    if(mesh==rest ? belongsToMouth && !throat : !belongsToMouth)continue;
                    for (int i = start; i < start + 24; i++)
                    {
                        remap[i] = vertices.Count; vertices.Add(points[i]); normals.Add(sourceNormals[i]);
                        uvs.Add(sourceUvs[i]); colors.Add(sourceColors.Length == points.Length ? sourceColors[i] : Color.white);
                        var weight=sourceWeights[i];
                        // Preserve the jaw/neck blend of the throat. Only remove upper-lip influence
                        // from the lower lip: it must not bridge both sides of the opening.
                        if(throat && upperLipBones!=null)
                        {
                            if(upperLipBones.Contains(weight.boneIndex0))weight.boneIndex0=lowerJaw;
                            if(upperLipBones.Contains(weight.boneIndex1))weight.boneIndex1=lowerJaw;
                            if(upperLipBones.Contains(weight.boneIndex2))weight.boneIndex2=lowerJaw;
                            if(upperLipBones.Contains(weight.boneIndex3))weight.boneIndex3=lowerJaw;
                        }
                        weights.Add(weight);
                    }
                }
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var indices = mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        int a = remap[indices[i]], b = remap[indices[i + 1]], c = remap[indices[i + 2]];
                        if (a < 0 || b < 0 || c < 0) continue;
                        triangles[sub].Add(a); triangles[sub].Add(b); triangles[sub].Add(c);
                    }
                }
            }
            var result = new Mesh { name = articulated.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            result.SetVertices(vertices); result.SetNormals(normals); result.SetUVs(0, uvs); result.SetColors(colors);
            result.boneWeights = weights.ToArray(); result.bindposes = rest.bindposes; result.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) result.SetTriangles(triangles[i], i);
            result.RecalculateBounds(); return result;
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
