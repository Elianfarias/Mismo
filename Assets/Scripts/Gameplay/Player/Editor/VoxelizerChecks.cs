using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Editor regression checks; no scene or source assets are changed.</summary>
    public static class VoxelizerChecks
    {
        [MenuItem("Mismo/Modelos/Verificar voxelizador")]
        public static void RunAll()
        {
            CheckClosedMeshAndTransforms();
            CheckThinSurfaceAndMaterials();
            CheckSkinnedPose();
            CheckDragon();
            Debug.Log("VOXELIZER_CHECKS_OK");
        }

        private static void CheckClosedMeshAndTransforms()
        {
            GameObject root = new GameObject("Voxel regression");
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh unreadable = null;
            try
            {
                cube.transform.SetParent(root.transform, false);
                var first = VoxelSurfaceSampler.Sample(root, 16, false, false);
                Require(first.Occupied.Count(v => v) == 4096 - 2744, "Cube shell coverage");
                Fill(first);
                Require(first.Occupied.All(v => v), "Closed interior fill");
                Mesh mesh = Build(first);
                try
                {
                    Require(mesh.triangles.Length == 16 * 16 * 6 * 6, "Only exterior faces are emitted");
                    Require((mesh.bounds.size - Vector3.one).magnitude < .0001f, "Output size");
                }
                finally { Object.DestroyImmediate(mesh); }

                // Ignore colliders entirely, including an oversized convex collider on layer 31.
                cube.layer = 31;
                cube.GetComponent<BoxCollider>().size = Vector3.one * 1000;
                cube.transform.localScale = new Vector3(1, 2, .5f);
                var baseline = VoxelSurfaceSampler.Sample(root, 24, false, false);
                root.transform.position = new Vector3(-8290, 0, 46177);
                root.transform.rotation = Quaternion.Euler(23, 47, 12);
                root.transform.localScale = Vector3.one * .00001f;
                var tiny = VoxelSurfaceSampler.Sample(root, 24, false, false);
                Require(tiny.Dimensions == baseline.Dimensions && tiny.Occupied.SequenceEqual(baseline.Occupied), "Translation, rotation and tiny scale invariance");

                unreadable = Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
                unreadable.UploadMeshData(true);
                cube.GetComponent<MeshFilter>().sharedMesh = unreadable;
                var uploaded = VoxelSurfaceSampler.Sample(root, 24, false, false);
                Require(uploaded.Occupied.SequenceEqual(tiny.Occupied), "Read/Write disabled mesh");

                GameObject hidden = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hidden.transform.SetParent(root.transform, false);
                hidden.transform.localPosition = Vector3.one * 100;
                hidden.SetActive(false);
                root.SetActive(false);
                Require(VoxelSurfaceSampler.Sample(root, 24, false, false).Occupied.SequenceEqual(tiny.Occupied), "Inactive root and hidden child variants");
                Debug.Log("PASS cube shell/fill/mesh, colliders, transforms, small scale, unreadable mesh, inactive variants");
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (unreadable != null) Object.DestroyImmediate(unreadable);
            }
        }

        private static void CheckThinSurfaceAndMaterials()
        {
            GameObject root = new GameObject("Thin wings");
            Mesh mesh = new Mesh();
            Material red = new Material(Shader.Find("Standard")), blue = new Material(Shader.Find("Standard"));
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up,
                    new Vector3(3, 0, 0), new Vector3(4, 0, 0), new Vector3(3, 1, 0) };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one, Vector2.one, Vector2.one };
                mesh.subMeshCount = 2;
                mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                mesh.SetTriangles(new[] { 5, 4, 3 }, 1); // Opposite winding must still be sampled.
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = new[] { red, blue };
                var sample = VoxelSurfaceSampler.Sample(root, 32, true, false);
                Require(sample.Dimensions.z == 1, "Zero-thickness surface");
                Require(sample.MaterialIndices.Contains(0) && sample.MaterialIndices.Contains(1), "Actual triangle submesh materials");
                int index = 1 + sample.Dimensions.x;
                Require((sample.UVs[index] - new Vector2(.1875f, .1875f)).magnitude < .0001f, "Barycentric UV interpolation");
                int before = sample.Occupied.Count(v => v);
                Fill(sample);
                Require(sample.Occupied.Count(v => v) == before, "Open surfaces are not filled as solid bounding boxes");
                for (int y = 0; y < sample.Dimensions.y; y++)
                    Require(!sample.Occupied[16 + sample.Dimensions.x * y], "Disconnected pieces remain separated");
                Debug.Log("PASS thin/reversed triangles, separated pieces, submesh palette, UV interpolation");
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(red); Object.DestroyImmediate(blue);
            }
        }

        private static void CheckSkinnedPose()
        {
            GameObject root = new GameObject("Skinned regression");
            GameObject externalBone = new GameObject("External bone");
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(primitive);
            try
            {
                mesh.bindposes = new[] { Matrix4x4.identity };
                mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, mesh.vertexCount).ToArray();
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.bones = new[] { externalBone.transform };
                skin.rootBone = externalBone.transform;
                skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 10000);
                externalBone.transform.position = new Vector3(2, 3, 4);
                var sample = VoxelSurfaceSampler.Sample(root, 16, false, false);
                Require((sample.Bounds.size - Vector3.one).magnitude < .0001f, "Use baked vertex bounds, not inflated skin bounds");
                Require((sample.Bounds.center - externalBone.transform.position).magnitude < .0001f, "Current pose with bones outside selection");
                root.transform.localScale = new Vector3(.01f, .02f, .03f);
                var scaled = VoxelSurfaceSampler.Sample(root, 16, false, false);
                Require((scaled.Bounds.size - Vector3.one).magnitude < .0001f &&
                    (scaled.Bounds.center - sample.Bounds.center).magnitude < .0001f, "Skinned renderer scale is applied exactly once");
                Require(skin.localBounds.size.x == 10000, "Source renderer remains unchanged");
                Debug.Log("PASS inflated skinned bounds, current pose and external bones");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(externalBone); Object.DestroyImmediate(mesh); }
        }

        private static void CheckDragon()
        {
            string path = AssetDatabase.GUIDToAssetPath("79b50b34e44b5fd4db1cdc9535a3c86b");
            if (string.IsNullOrEmpty(path)) { Debug.Log("SKIP optional DragonUsurper asset"); return; }
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var sample = VoxelSurfaceSampler.Sample(root, 64, true, false);
                int count = sample.Occupied.Count(v => v);
                Require(count > 1000, "Dragon must have a detailed surface");
                Require(count < sample.Occupied.Length * .6f, "Dragon must not become a bounding box");
                Require(sample.UVs.Where((uv, i) => sample.Occupied[i]).Distinct().Count() > 100, "Dragon texture coordinates vary across its body");
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                var vertices = skin.sharedMesh.vertices;
                var weights = skin.sharedMesh.boneWeights;
                var bindposes = skin.sharedMesh.bindposes;
                var matrices = skin.bones.Select((bone, i) => bone.localToWorldMatrix * bindposes[i]).ToArray();
                Bounds actual = default;
                for (int i = 0; i < vertices.Length; i++)
                {
                    BoneWeight w = weights[i];
                    Vector3 p = matrices[w.boneIndex0].MultiplyPoint3x4(vertices[i]) * w.weight0 +
                        matrices[w.boneIndex1].MultiplyPoint3x4(vertices[i]) * w.weight1 +
                        matrices[w.boneIndex2].MultiplyPoint3x4(vertices[i]) * w.weight2 +
                        matrices[w.boneIndex3].MultiplyPoint3x4(vertices[i]) * w.weight3;
                    p = Quaternion.Inverse(root.transform.rotation) * (p - root.transform.position);
                    if (i == 0) actual = new Bounds(p, Vector3.zero); else actual.Encapsulate(p);
                }
                Require((sample.Bounds.size - actual.size).magnitude < .001f, "Dragon voxel dimensions match independently skinned geometry");
                Fill(sample);
                Mesh output = Build(sample);
                try { Require(output.vertexCount > 1000, "Dragon output mesh"); }
                finally { Object.DestroyImmediate(output); }
                Debug.Log($"PASS DragonUsurper: grid={sample.Dimensions}, surface={count}, filled={sample.Occupied.Count(v => v)}, bounds={sample.Bounds.size}");
            }
            finally { Object.DestroyImmediate(root); }
        }

        internal static void Fill(VoxelSurfaceSampler.Result sample) => typeof(WeaponVoxelizerWindow)
            .GetMethod("FillInterior", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { sample.Occupied, sample.Dimensions });

        internal static Mesh Build(VoxelSurfaceSampler.Result sample) => (Mesh)typeof(WeaponVoxelizerWindow)
            .GetMethod("BuildMesh", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] {
                sample.Occupied, sample.Dimensions, sample.VoxelSize, sample.Bounds.min, sample.Bounds.center,
                Color.white, "Voxel regression", true, sample.MaterialIndices, sample.UVs, sample.Materials.Count + 1, null });

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Voxel regression: " + message);
        }
    }
}
