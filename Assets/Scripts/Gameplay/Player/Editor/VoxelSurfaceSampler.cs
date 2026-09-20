using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Conservative triangle voxelization, independent of colliders and animated renderer bounds.</summary>
    internal static class VoxelSurfaceSampler
    {
        internal sealed class Result
        {
            public Bounds Bounds;
            public Vector3Int Dimensions;
            public float VoxelSize;
            public bool[] Occupied;
            public int[] MaterialIndices;
            public Vector2[] UVs;
            public BoneWeight[] BoneWeights;
            public readonly List<Transform> Bones = new List<Transform>();
            public readonly List<Material> Materials = new List<Material>();
        }

        private struct Triangle
        {
            public Vector3 A, B, C;
            public Vector2 UvA, UvB, UvC;
            public int Material;
            public BoneWeight WeightA, WeightB, WeightC;
        }

        internal static Result Sample(GameObject source, int resolution, bool preserveMaterials, bool showProgress = true, bool preserveRig = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            resolution = Mathf.Clamp(resolution, 8, 128);
            var result = new Result();
            var triangles = new List<Triangle>();
            // Cancel the root translation/rotation without discarding its physical scale.
            // Multiplying relative transforms first also avoids subtracting large world positions.
            Matrix4x4 rootToSampling = Matrix4x4.Rotate(Quaternion.Inverse(source.transform.rotation)) *
                source.transform.localToWorldMatrix;
            rootToSampling.SetColumn(3, new Vector4(0, 0, 0, 1));
            try
            {
                foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled || !IsIncluded(renderer.transform, source.transform)) continue;
                    Mesh mesh = null;
                    Mesh baked = null;
                    try
                    {
                        if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                        {
                            // Bake the actual renderer so bones outside the selected subtree still work.
                            baked = new Mesh { name = "Voxel sampling pose", hideFlags = HideFlags.HideAndDontSave };
                            // Compensate the renderer scale in the baked vertices; the matrix below
                            // applies it once. Without this, FBX scale .01 shrinks the surface 100x.
                            skin.BakeMesh(baked, true);
                            mesh = baked;
                        }
                        else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter))
                            mesh = filter.sharedMesh;
                        if (mesh == null || mesh.vertexCount == 0) continue;

                        Matrix4x4 toSampling = rootToSampling * RelativeTransform(renderer.transform, source.transform);
                        BoneWeight[] weights = null;
                        if (preserveRig)
                        {
                            if (renderer is SkinnedMeshRenderer skinned && skinned.bones.Length > 0)
                            {
                                int[] mapping = new int[skinned.bones.Length];
                                for (int i = 0; i < mapping.Length; i++) mapping[i] = RegisterBone(skinned.bones[i], source.transform, result);
                                weights = skinned.sharedMesh.boneWeights;
                                for (int i = 0; i < weights.Length; i++)
                                {
                                    BoneWeight w = weights[i];
                                    w.boneIndex0 = mapping[w.boneIndex0]; w.boneIndex1 = mapping[w.boneIndex1];
                                    w.boneIndex2 = mapping[w.boneIndex2]; w.boneIndex3 = mapping[w.boneIndex3]; weights[i] = w;
                                }
                            }
                            if (weights == null || weights.Length != mesh.vertexCount)
                            {
                                weights = new BoneWeight[mesh.vertexCount];
                                int bone = RegisterBone(renderer.transform, source.transform, result);
                                for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = bone, weight0 = 1 };
                            }
                        }
                        ReadTriangles(mesh, toSampling, renderer.sharedMaterials, preserveMaterials, triangles, result, weights);
                    }
                    finally
                    {
                        if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
                    }
                }

                if (triangles.Count == 0)
                    throw new InvalidOperationException("El objeto no contiene triángulos visibles en MeshRenderer o SkinnedMeshRenderer.");

                Bounds bounds = new Bounds(triangles[0].A, Vector3.zero);
                foreach (Triangle triangle in triangles)
                {
                    bounds.Encapsulate(triangle.A);
                    bounds.Encapsulate(triangle.B);
                    bounds.Encapsulate(triangle.C);
                }
                float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (!(largest > 0) || float.IsInfinity(largest) || float.IsNaN(largest))
                    throw new InvalidOperationException("La geometría tiene un tamaño nulo o coordenadas inválidas.");

                result.Bounds = bounds;
                result.VoxelSize = largest / resolution;
                Vector3 gridSize = bounds.size / result.VoxelSize;
                result.Dimensions = new Vector3Int(
                    Mathf.Clamp(Mathf.CeilToInt(gridSize.x - .0001f), 1, resolution),
                    Mathf.Clamp(Mathf.CeilToInt(gridSize.y - .0001f), 1, resolution),
                    Mathf.Clamp(Mathf.CeilToInt(gridSize.z - .0001f), 1, resolution));
                int total = result.Dimensions.x * result.Dimensions.y * result.Dimensions.z;
                result.Occupied = new bool[total];
                result.MaterialIndices = new int[total];
                result.UVs = new Vector2[total];
                if (preserveRig) result.BoneWeights = new BoneWeight[total];
                var distances = new float[total];
                for (int i = 0; i < total; i++)
                {
                    result.MaterialIndices[i] = -1;
                    distances[i] = float.PositiveInfinity;
                }

                for (int i = 0; i < triangles.Count; i++)
                {
                    if (showProgress && (i & 127) == 0 && EditorUtility.DisplayCancelableProgressBar(
                        "Voxelizar modelo", "Muestreando triángulos y texturas...", i / (float)triangles.Count))
                        throw new OperationCanceledException();
                    Rasterize(triangles[i], result, distances);
                }
                return result;
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
            }
        }

        private static int RegisterBone(Transform bone, Transform source, Result result)
        {
            if (bone == null || (bone != source && !bone.IsChildOf(source)))
                throw new InvalidOperationException("Seleccioná la raíz completa del rig: hay huesos fuera del modelo seleccionado.");
            int index = result.Bones.IndexOf(bone);
            if (index < 0) { index = result.Bones.Count; result.Bones.Add(bone); }
            return index;
        }

        private static bool IsIncluded(Transform child, Transform root)
        {
            // A disabled source/prefab can be processed without enabling hidden child variants.
            for (; child != root; child = child.parent)
                if (!child.gameObject.activeSelf) return false;
            return true;
        }

        private static Matrix4x4 RelativeTransform(Transform child, Transform root)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            for (; child != root; child = child.parent)
                matrix = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale) * matrix;
            return matrix;
        }

        private static void ReadTriangles(Mesh mesh, Matrix4x4 transform, Material[] materials,
            bool preserveMaterials, List<Triangle> triangles, Result result, BoneWeight[] weights)
        {
            // Editor snapshots also support imported meshes with Read/Write disabled.
            using (var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                var data = snapshot[0];
                using (var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.TempJob))
                using (var uvs = new NativeArray<Vector2>(data.vertexCount, Allocator.TempJob))
                {
                    data.GetVertices(vertices);
                    if (data.HasVertexAttribute(VertexAttribute.TexCoord0)) data.GetUVs(0, uvs);
                    var positions = new Vector3[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++) positions[i] = transform.MultiplyPoint3x4(vertices[i]);
                    for (int submesh = 0; submesh < data.subMeshCount; submesh++)
                    {
                        var descriptor = data.GetSubMesh(submesh);
                        if (descriptor.topology != MeshTopology.Triangles) continue;
                        int materialIndex = -1;
                        Material material = submesh < materials.Length ? materials[submesh] : null;
                        if (preserveMaterials && material != null)
                        {
                            materialIndex = result.Materials.IndexOf(material);
                            if (materialIndex < 0)
                            {
                                materialIndex = result.Materials.Count;
                                result.Materials.Add(material);
                            }
                        }
                        using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.TempJob))
                        {
                            data.GetIndices(indices, submesh, true);
                            for (int i = 0; i + 2 < indices.Length; i += 3)
                            {
                                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                                triangles.Add(new Triangle
                                {
                                    A = positions[a], B = positions[b], C = positions[c],
                                    UvA = uvs[a], UvB = uvs[b], UvC = uvs[c], Material = materialIndex,
                                    WeightA = weights != null ? weights[a] : default,
                                    WeightB = weights != null ? weights[b] : default,
                                    WeightC = weights != null ? weights[c] : default
                                });
                            }
                        }
                    }
                }
            }
        }

        private static void Rasterize(Triangle triangle, Result result, float[] distances)
        {
            // All geometric tolerances are in voxel units, including very small imported models.
            Vector3 a = (triangle.A - result.Bounds.min) / result.VoxelSize;
            Vector3 b = (triangle.B - result.Bounds.min) / result.VoxelSize;
            Vector3 c = (triangle.C - result.Bounds.min) / result.VoxelSize;
            Vector3 min = Vector3.Min(a, Vector3.Min(b, c)) - Vector3.one * 0.0001f;
            Vector3 max = Vector3.Max(a, Vector3.Max(b, c)) + Vector3.one * 0.0001f;
            Vector3Int d = result.Dimensions;
            int minX = Mathf.Clamp(Mathf.FloorToInt(min.x), 0, d.x - 1), maxX = Mathf.Clamp(Mathf.FloorToInt(max.x), 0, d.x - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(min.y), 0, d.y - 1), maxY = Mathf.Clamp(Mathf.FloorToInt(max.y), 0, d.y - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt(min.z), 0, d.z - 1), maxZ = Mathf.Clamp(Mathf.FloorToInt(max.z), 0, d.z - 1);
            for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector3 center = new Vector3(x + .5f, y + .5f, z + .5f);
                        if (!IntersectsBox(a - center, b - center, c - center)) continue;
                        int index = x + d.x * (y + d.y * z);
                        result.Occupied[index] = true;
                        Vector3 weights = ClosestWeights(center, a, b, c);
                        float distance = (a * weights.x + b * weights.y + c * weights.z - center).sqrMagnitude;
                        if (distance >= distances[index]) continue;
                        distances[index] = distance;
                        result.MaterialIndices[index] = triangle.Material;
                        result.UVs[index] = triangle.UvA * weights.x + triangle.UvB * weights.y + triangle.UvC * weights.z;
                        if (result.BoneWeights != null)
                            result.BoneWeights[index] = BlendWeights(triangle.WeightA, triangle.WeightB, triangle.WeightC, weights);
                    }
        }

        internal static BoneWeight BlendWeights(BoneWeight a, BoneWeight b, BoneWeight c, Vector3 barycentric)
        {
            var values = new Dictionary<int, float>();
            AddWeights(values, a, barycentric.x); AddWeights(values, b, barycentric.y); AddWeights(values, c, barycentric.z);
            return NormalizeWeights(values);
        }
        internal static BoneWeight AverageWeights(IEnumerable<BoneWeight> weights)
        {
            var values = new Dictionary<int, float>();
            foreach (BoneWeight weight in weights) AddWeights(values, weight, 1);
            return NormalizeWeights(values);
        }
        private static BoneWeight NormalizeWeights(Dictionary<int, float> values)
        {
            var sorted = new List<KeyValuePair<int, float>>(values);
            sorted.Sort((x, y) => y.Value.CompareTo(x.Value));
            float sum = 0; for (int i = 0; i < Mathf.Min(4, sorted.Count); i++) sum += sorted[i].Value;
            if (sum <= 0) return new BoneWeight { boneIndex0 = 0, weight0 = 1 };
            BoneWeight result = default;
            if (sorted.Count > 0) { result.boneIndex0 = sorted[0].Key; result.weight0 = sorted[0].Value / sum; }
            if (sorted.Count > 1) { result.boneIndex1 = sorted[1].Key; result.weight1 = sorted[1].Value / sum; }
            if (sorted.Count > 2) { result.boneIndex2 = sorted[2].Key; result.weight2 = sorted[2].Value / sum; }
            if (sorted.Count > 3) { result.boneIndex3 = sorted[3].Key; result.weight3 = sorted[3].Value / sum; }
            return result;
        }
        private static void AddWeights(Dictionary<int, float> result, BoneWeight w, float factor)
        {
            AddWeight(result, w.boneIndex0, w.weight0 * factor); AddWeight(result, w.boneIndex1, w.weight1 * factor);
            AddWeight(result, w.boneIndex2, w.weight2 * factor); AddWeight(result, w.boneIndex3, w.weight3 * factor);
        }
        private static void AddWeight(Dictionary<int, float> result, int bone, float weight)
        { if (weight <= 0) return; result.TryGetValue(bone, out float previous); result[bone] = previous + weight; }

        // Separating axis theorem: box axes, triangle normal, and 9 edge/box cross products.
        private static bool IntersectsBox(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 min = Vector3.Min(a, Vector3.Min(b, c)), max = Vector3.Max(a, Vector3.Max(b, c));
            const float half = .5001f;
            if (min.x > half || max.x < -half || min.y > half || max.y < -half || min.z > half || max.z < -half) return false;
            Vector3 ab = b - a, bc = c - b, ca = a - c;
            return OverlapsAxis(Vector3.Cross(ab, c - a), a, b, c) &&
                OverlapsEdge(ab, a, b, c) && OverlapsEdge(bc, a, b, c) && OverlapsEdge(ca, a, b, c);
        }

        private static bool OverlapsEdge(Vector3 edge, Vector3 a, Vector3 b, Vector3 c) =>
            OverlapsAxis(new Vector3(0, edge.z, -edge.y), a, b, c) &&
            OverlapsAxis(new Vector3(-edge.z, 0, edge.x), a, b, c) &&
            OverlapsAxis(new Vector3(edge.y, -edge.x, 0), a, b, c);

        private static bool OverlapsAxis(Vector3 axis, Vector3 a, Vector3 b, Vector3 c)
        {
            float radius = .5001f * (Mathf.Abs(axis.x) + Mathf.Abs(axis.y) + Mathf.Abs(axis.z));
            float p = Vector3.Dot(a, axis), q = Vector3.Dot(b, axis), r = Vector3.Dot(c, axis);
            return Mathf.Min(p, Mathf.Min(q, r)) <= radius && Mathf.Max(p, Mathf.Max(q, r)) >= -radius;
        }

        private static Vector3 ClosestWeights(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ac, ac), cross = Vector3.Dot(ab, ac);
            float determinant = aa * bb - cross * cross;
            if (determinant > 1e-12f * aa * bb)
            {
                float u = (bb * Vector3.Dot(ap, ab) - cross * Vector3.Dot(ap, ac)) / determinant;
                float v = (aa * Vector3.Dot(ap, ac) - cross * Vector3.Dot(ap, ab)) / determinant;
                if (u >= 0 && v >= 0 && u + v <= 1) return new Vector3(1 - u - v, u, v);
            }
            // Outside the face (or degenerate triangle): choose the closest point on an edge.
            float tAB = SegmentParameter(p, a, b), tBC = SegmentParameter(p, b, c), tCA = SegmentParameter(p, c, a);
            float dAB = (p - Vector3.Lerp(a, b, tAB)).sqrMagnitude;
            float dBC = (p - Vector3.Lerp(b, c, tBC)).sqrMagnitude;
            float dCA = (p - Vector3.Lerp(c, a, tCA)).sqrMagnitude;
            if (dAB <= dBC && dAB <= dCA) return new Vector3(1 - tAB, tAB, 0);
            if (dBC <= dCA) return new Vector3(0, 1 - tBC, tBC);
            return new Vector3(tCA, 0, 1 - tCA);
        }

        private static float SegmentParameter(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 edge = b - a;
            return edge.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(p - a, edge) / edge.sqrMagnitude) : 0;
        }
    }
}
