using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    internal static class SoulEaterEyeDetail
    {
        internal const string MaterialPath = "Assets/Art/Materials/DragonBosses/SoulEater_Eyes.mat";
        // Centers of the two green eye markings in SoulEater's original emission texture.
        static readonly Vector2[] EyeUvs = { new Vector2(.2593471f, .5282559f), new Vector2(.5416450f, .5257704f) };

        internal static Material Add(GameObject source, VoxelRigInstance target, Mesh mesh)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SoulEater_Eyes" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetColor("_BaseColor", new Color(.2f, .48f, .045f));
            material.SetColor("_EmissionColor", new Color(.12f, .25f, .01f));
            material.SetFloat("_Smoothness", .15f); material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh(); var cubeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                skin.BakeMesh(baked, true);
                var cube = cubeObject.GetComponent<MeshFilter>().sharedMesh;
                var cubeVertices = cube.vertices; var cubeNormals = cube.normals; var cubeTriangles = cube.triangles;
                var vertices = new List<Vector3>(mesh.vertices); var normals = new List<Vector3>(mesh.normals);
                var weights = new List<BoneWeight>(mesh.boneWeights); var uvs = new List<Vector2>(mesh.uv); var colors = new List<Color>(mesh.colors);
                var triangles = new List<int>();
                var sourceToTarget = target.surface.transform.worldToLocalMatrix * target.animator.transform.localToWorldMatrix * source.transform.worldToLocalMatrix;
                // Match VoxelSurfaceSampler: this imported FBX requires the renderer's scale too.
                var bakeToWorld = skin.transform.localToWorldMatrix;
                var mapping = sourceToTarget * bakeToWorld;
                var targetIndices = target.surface.bones.Select((bone, i) => (bone, i)).ToDictionary(
                    p => AnimationUtility.CalculateTransformPath(p.bone, target.animator.transform), p => p.i);
                var remap = skin.bones.Select(b => targetIndices[AnimationUtility.CalculateTransformPath(b, source.transform)]).ToArray();
                foreach (Vector2 uv in EyeUvs)
                {
                    FindEye(baked, skin.sharedMesh.boneWeights, remap, uv, out Vector3 point, out Vector3 normal, out BoneWeight weight);
                    Vector3 center = mapping.MultiplyPoint3x4(point);
                    Vector3 outward = mapping.inverse.transpose.MultiplyVector(normal).normalized;
                    // Match the original eye surface weights, including eyelid and muzzle motion.
                    center += outward * .055f;
                    var orientation = Quaternion.LookRotation(outward, Vector3.up);
                    int start = vertices.Count;
                    for (int v = 0; v < cubeVertices.Length; v++)
                    {
                        vertices.Add(center + orientation * Vector3.Scale(cubeVertices[v], new Vector3(.12f, .10f, .06f)));
                        normals.Add(orientation * cubeNormals[v]);
                        weights.Add(weight); uvs.Add(Vector2.zero); colors.Add(Color.white);
                    }
                    triangles.AddRange(cubeTriangles.Select(i => start + i));
                    Debug.Log("SOUL_EYE_DETAIL center=" + center + " normal=" + outward + " weights=" +
                        target.surface.bones[weight.boneIndex0].name + ":" + weight.weight0 + ", " +
                        target.surface.bones[weight.boneIndex1].name + ":" + weight.weight1);
                }
                int eyeSubmesh = mesh.subMeshCount;
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetColors(colors); mesh.boneWeights = weights.ToArray();
                mesh.subMeshCount = eyeSubmesh + 1; mesh.SetTriangles(triangles, eyeSubmesh); mesh.RecalculateBounds();
                return material;
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(cubeObject); }
        }

        static void FindEye(Mesh mesh, BoneWeight[] sourceWeights, int[] remap, Vector2 point,
            out Vector3 position, out Vector3 normal, out BoneWeight weight)
        {
            var uv = mesh.uv; var vertices = mesh.vertices; var normals = mesh.normals; var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Vector2 a = uv[ia], ab = uv[ib] - a, ac = uv[ic] - a, p = point - a;
                float determinant = ab.x * ac.y - ab.y * ac.x;
                if (Mathf.Abs(determinant) < 1e-10f) continue;
                float b = (p.x * ac.y - p.y * ac.x) / determinant, c = (ab.x * p.y - ab.y * p.x) / determinant;
                if (b < 0 || c < 0 || b + c > 1) continue;
                position = vertices[ia] * (1 - b - c) + vertices[ib] * b + vertices[ic] * c;
                normal = (normals[ia] * (1 - b - c) + normals[ib] * b + normals[ic] * c).normalized;
                var influences = new Dictionary<int, float>();
                void Add(BoneWeight w, float factor)
                {
                    void Part(int index, float value)
                    {
                        if (value <= 0) return;
                        int bone = remap[index];
                        influences.TryGetValue(bone, out float previous);
                        influences[bone] = previous + value * factor;
                    }
                    Part(w.boneIndex0, w.weight0); Part(w.boneIndex1, w.weight1);
                    Part(w.boneIndex2, w.weight2); Part(w.boneIndex3, w.weight3);
                }
                Add(sourceWeights[ia], 1 - b - c); Add(sourceWeights[ib], b); Add(sourceWeights[ic], c);
                var sorted = influences.OrderByDescending(p => p.Value).Take(4).ToArray();
                float total = sorted.Sum(p => p.Value);
                weight = new BoneWeight { boneIndex0 = sorted[0].Key, weight0 = sorted[0].Value / total };
                if (sorted.Length > 1) { weight.boneIndex1 = sorted[1].Key; weight.weight1 = sorted[1].Value / total; }
                if (sorted.Length > 2) { weight.boneIndex2 = sorted[2].Key; weight.weight2 = sorted[2].Value / total; }
                if (sorted.Length > 3) { weight.boneIndex3 = sorted[3].Key; weight.weight3 = sorted[3].Value / total; }
                return;
            }
            throw new InvalidOperationException("Could not locate SoulEater eye marking on source mesh.");
        }
    }
}
