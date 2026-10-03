using System;
using System.IO;
using System.Linq;
using System.Text;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterMouthChecks
    {
        sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly Transform AnimationRoot;
            readonly SkinnedMeshRenderer skin;
            readonly Mesh mesh = new Mesh();
            Matrix4x4 bakedToWorld;
            Vector3[] rayVertices;
            int[] rayTriangles;
            readonly Transform[] bones;
            readonly Vector3[] positions, scales;
            readonly Quaternion[] rotations;
            public Fixture(GameObject root, Transform animationRoot, SkinnedMeshRenderer renderer)
            {
                Root = root; AnimationRoot = animationRoot; skin = renderer;
                foreach (var animator in root.GetComponentsInChildren<Animator>()) animator.enabled = false;
                bones = root.GetComponentsInChildren<Transform>();
                positions = bones.Select(t => t.localPosition).ToArray(); rotations = bones.Select(t => t.localRotation).ToArray(); scales = bones.Select(t => t.localScale).ToArray();
            }
            public Vector3[] Sample(AnimationClip clip, float time, bool collisionNeeded = false)
            {
                for (int i = 0; i < bones.Length; i++) { bones[i].localPosition = positions[i]; bones[i].localRotation = rotations[i]; bones[i].localScale = scales[i]; }
                clip.SampleAnimation(AnimationRoot.gameObject, time);
                skin.BakeMesh(mesh, true); mesh.RecalculateBounds();
                if (collisionNeeded)
                {
                    bakedToWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    rayVertices = mesh.vertices; rayTriangles = mesh.triangles;
                }
                return mesh.vertices;
            }
            public Vector3 MouthCenter => (bones.First(t => t.name == "UpperMouth").position + bones.First(t => t.name == "JawTip").position) * .5f;
            public float Depth(Ray ray)
            {
                Vector3 origin = bakedToWorld.inverse.MultiplyPoint3x4(ray.origin);
                Vector3 direction = bakedToWorld.inverse.MultiplyVector(ray.direction);
                float nearest = 10;
                for (int i = 0; i < rayTriangles.Length; i += 3)
                {
                    Vector3 a = rayVertices[rayTriangles[i]], ab = rayVertices[rayTriangles[i + 1]] - a, ac = rayVertices[rayTriangles[i + 2]] - a;
                    Vector3 p = Vector3.Cross(direction, ac); float determinant = Vector3.Dot(ab, p);
                    if (Mathf.Abs(determinant) < 1e-8f) continue;
                    Vector3 delta = origin - a; float u = Vector3.Dot(delta, p) / determinant;
                    if (u < 0 || u > 1) continue;
                    Vector3 q = Vector3.Cross(delta, ab); float v = Vector3.Dot(direction, q) / determinant;
                    if (v < 0 || u + v > 1) continue;
                    float depth = Vector3.Dot(ac, q) / determinant;
                    if (depth > 0) nearest = Mathf.Min(nearest, depth);
                }
                return nearest;
            }
            public void Dispose() { Object.DestroyImmediate(mesh); Object.DestroyImmediate(Root); }
        }

        [MenuItem("Mismo/Modelos/Verificar boca de SoulEater verde")]
        public static void Run()
        {
            VoxelArticulationChecks.Run();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoulEaterGreenVariant.PrefabPath);
            var originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoulEaterGreenVariant.SourcePath);
            Require(prefab != null && originalPrefab != null, "Required SoulEater prefabs");
            var saved = prefab.GetComponent<VoxelRigInstance>();
            var original = originalPrefab.GetComponent<VoxelRigInstance>();
            Require(saved.clips.Length == 17 && saved.clips.SequenceEqual(original.clips), "All 17 original clips retained");
            Require(saved.animator.runtimeAnimatorController == original.animator.runtimeAnimatorController, "Original controller retained");
            Require(saved.surface.sharedMesh == AssetDatabase.LoadAssetAtPath<Mesh>(SoulEaterMouthRepair.MeshPath), "Corrected mesh assigned");
            Require(saved.surface.sharedMesh.bindposes.SequenceEqual(original.surface.sharedMesh.bindposes), "Existing bindposes retained");
            Require(saved.surface.sharedMaterials.Take(saved.surface.sharedMesh.subMeshCount - 1).All(m => m == AssetDatabase.LoadAssetAtPath<Material>(SoulEaterGreenVariant.MaterialPath)), "Green material retained");
            Require(saved.surface.sharedMaterials.Last() == AssetDatabase.LoadAssetAtPath<Material>(SoulEaterEyeDetail.MaterialPath), "Visible eye material assigned");
            Require(saved.surface.sharedMesh.boneWeights.Skip(saved.surface.sharedMesh.vertexCount - 48).All(w =>
                Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) < .0001f && w.weight0 > 0), "Both eye details retain normalized surface weights");
            var target = Object.Instantiate(prefab); var rig = target.GetComponent<VoxelRigInstance>();
            var report = new StringBuilder();
            using (var voxel = new Fixture(target, rig.animator.transform, rig.surface))
            {
                foreach (var clip in rig.clips)
                {
                    Vector3[] baseline = null; bool moved = false; float eyeGap = 0;
                    for (int frame = 0; frame <= 10; frame++)
                    {
                        var points = voxel.Sample(clip, clip.length * frame / 10);
                        Require(points.Skip(points.Length - 48).All(v => v.magnitude < 50), "Eye details remain attached: " + clip.name);
                        for (int eye = 0; eye < 2; eye++)
                        {
                            Vector3 center = Vector3.zero;
                            for (int i = 0; i < 24; i++) center += points[points.Length - 48 + eye * 24 + i] / 24;
                            float nearest = float.PositiveInfinity;
                            for (int i = 0; i < points.Length - 48; i++) nearest = Mathf.Min(nearest, (points[i] - center).sqrMagnitude);
                            eyeGap = Mathf.Max(eyeGap, Mathf.Sqrt(nearest));
                            Require(nearest < .14f * .14f, "Eye detail must stay at the facial surface: " + clip.name + " frame " + frame);
                        }
                        if (baseline == null) baseline = points;
                        for (int i = 0; i < points.Length; i += 97)
                        {
                            Require(float.IsFinite(points[i].x) && float.IsFinite(points[i].y) && float.IsFinite(points[i].z), "Finite pose " + clip.name);
                            Require(points[i].magnitude < 50, "No exploded pose " + clip.name);
                            if ((points[i] - baseline[i]).sqrMagnitude > .000025f) moved = true;
                        }
                    }
                    Require(moved, "Clip deforms voxels: " + clip.name);
                    report.AppendLine("PASS " + clip.name + ": 11 finite moving poses, eye/surface distance <= " + eyeGap.ToString("F4"));
                }
                foreach (string name in new[] { "Idle", "Scream", "Basic Attack", "Fireball Shoot" })
                {
                    var clip = rig.clips.First(c => c.name == name);
                    foreach (float t in new[] { 0f, .5f, .8f, 1f })
                    {
                        voxel.Sample(clip, clip.length * t, true);
                        var ray = new Ray(voxel.MouthCenter + Vector3.forward * 5, Vector3.back);
                        float actual = voxel.Depth(ray);
                        report.AppendLine($"MOUTH {name} t={t:F2}: surface depth={actual:F4}");
                        Require(actual < 10, "Mouth ray must hit geometry");
                        bool fullyOpen = name == "Scream" && (t == .5f || t == .8f) || name == "Basic Attack" && t == .5f || name == "Fireball Shoot" && t == .8f;
                        if (fullyOpen) Require(actual > 5.1f, "Oral cavity must extend behind the midpoint between lips: " + name);
                        if (name == "Idle" || t == 0 || t == 1) Require(actual < 4.9f, "Closed lips must return to the front of the mouth");
                    }
                }
            }
            report.AppendLine("PASS: original rig/controller/clips retained, green body and two eye details; open mouth rays reach inside the cavity, closed lips return to the front.");
            Directory.CreateDirectory("output/soul-eater-mouth");
            File.WriteAllText("output/soul-eater-mouth/checks.txt", report.ToString());
            Debug.Log("SOUL_EATER_MOUTH_CHECKS_OK\n" + report);
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
