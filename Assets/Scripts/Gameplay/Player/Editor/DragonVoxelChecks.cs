using System;
using System.Linq;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class DragonVoxelChecks
    {
        [MenuItem("Mismo/Modelos/Verificar rigs voxelizados de dragón")]
        public static void Run()
        {
            Check("Firyx", 18); Check("Nightmare", 16); Check("SoulEater", 17);
            CheckNightmarePackage();
            foreach (string variant in new[] { "Red", "Blue", "Green", "Purple" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DragonVoxelIntegration.BossFolder + "/Firyx_" + variant + ".prefab");
                Require(prefab != null, "Missing boss " + variant);
                var boss = prefab.GetComponent<DragonBossController>();
                Require(boss != null && boss.Settings.clips.All(c => c != null), "Boss clip bindings " + variant);
                Require(prefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials.All(m => m != null && m.name == variant + "HP"), "Boss colors " + variant);
                Require(prefab.GetComponentInChildren<MeshCollider>() == null, "No frozen mesh collider on animated dragon");
                Require(prefab.GetComponent<DragonBreathVfx>() != null && prefab.GetComponent<Rigidbody>().isKinematic, "Boss physics and VFX");
            }
            Debug.Log("DRAGON_RIG_CHECKS_OK");
        }

        static void CheckNightmarePackage()
        {
            string folder = DragonVoxelIntegration.FullPackage;
            var source = AssetDatabase.LoadAssetAtPath<Material>(DragonVoxelIntegration.FullMaterials + "/DragonNightmare/BluePBR.mat");
            if (source == null) return;
            var exported = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Nightmare_Animated_BlueHP.mat");
            Require(exported != null, "Missing exported Nightmare material");
            foreach (string property in new[] { "_BaseMap", "_MetallicGlossMap", "_OcclusionMap" })
                Require(source.GetTexture(property) != null && exported.GetTexture(property) == source.GetTexture(property), "Nightmare PBR " + property);
            Require(!exported.IsKeywordEnabled("_NORMALMAP"), "Flat voxel normals without source tangent map");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DragonVoxelIntegration.FullPrefabs + "/DragonNightmare/Blue.prefab");
            Require(prefab != null, "Missing complete Nightmare prefab");
            var clips = VoxelRigExporter.FindClips(prefab, null);
            Require(clips.Count == 16 && clips.All(entry => AssetDatabase.GetAssetPath(entry.clip).StartsWith(DragonVoxelIntegration.FullAnimations + "/DragonNightMare/", StringComparison.OrdinalIgnoreCase)),
                "Automatic discovery must select only this dragon's animation family");
            Require(clips.Select(entry => entry.name).Distinct().Count() == 16 && clips.Any(entry => entry.name == "run"),
                "Controller clips must retain their meaningful FBX names");
            Debug.Log("PASS Nightmare full package: PBR maps and automatic animation family selection.");
        }

        static void Check(string family, int expectedClips)
        {
            string path = "Assets/Art/Prefabs/Voxelized/" + family + "_Animated.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, "Missing " + path);
            var instance = Object.Instantiate(prefab);
            var baked = new Mesh();
            try
            {
                var library = instance.GetComponent<VoxelRigInstance>();
                Require(library != null && library.clips.Length == expectedClips, family + " clip count: " + library?.clips.Length);
                var skin = library.surface; Mesh mesh = skin.sharedMesh;
                var weights = mesh.boneWeights;
                Require(weights.Length == mesh.vertexCount && skin.bones.Length == mesh.bindposes.Length, "Complete skin data");
                Require(skin.bones.All(b => b != null && b.IsChildOf(instance.transform)), "Local complete skeleton");
                foreach (BoneWeight w in weights)
                    Require(Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) < .0001f &&
                        w.boneIndex0 < skin.bones.Length && w.boneIndex1 < skin.bones.Length && w.boneIndex2 < skin.bones.Length && w.boneIndex3 < skin.bones.Length, "Normalized valid weights");
                // Coincident grid corners must use identical weights, including different cubes.
                var cornerWeights = new System.Collections.Generic.Dictionary<Vector3Int, BoneWeight>();
                var positionsForWeights = mesh.vertices;
                float quantize = 100000f / mesh.bounds.size.magnitude;
                for (int i = 0; i < weights.Length; i++)
                {
                    Vector3Int key = Vector3Int.RoundToInt(positionsForWeights[i] * quantize);
                    if (cornerWeights.TryGetValue(key, out BoneWeight previous)) Require(previous.Equals(weights[i]), "Watertight shared corner weights");
                    else cornerWeights[key] = weights[i];
                }
                skin.BakeMesh(baked, true);
                var baseline = baked.vertices;
                var original = mesh.vertices;
                Require((BoundsOf(baseline).size - mesh.bounds.size).magnitude < .01f, "Rest size: " + family);
                for (int i = 0; i < original.Length; i += 61)
                    Require(Vector3.Distance(original[i], baseline[i]) < .01f, "Bind pose alignment: " + family);
                var transforms = library.animator.GetComponentsInChildren<Transform>();
                var positions = transforms.Select(t => t.localPosition).ToArray();
                var rotations = transforms.Select(t => t.localRotation).ToArray();
                var scales = transforms.Select(t => t.localScale).ToArray();
                int moving = 0;
                foreach (var clip in library.clips)
                {
                    bool changed = false;
                    foreach (float fraction in new[] { .0f, .37f, .79f })
                    {
                        for (int i = 0; i < transforms.Length; i++)
                        { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; transforms[i].localScale = scales[i]; }
                        clip.SampleAnimation(library.animator.gameObject, clip.length * fraction);
                        skin.BakeMesh(baked, true);
                        var points = baked.vertices;
                        Bounds pose = BoundsOf(points);
                        Require(pose.size.magnitude > mesh.bounds.size.magnitude * .1f && pose.size.magnitude < mesh.bounds.size.magnitude * 5,
                            family + " collapsed/exploded in " + clip.name);
                        for (int i = 0; i < points.Length; i += 97)
                        {
                            Require(!float.IsNaN(points[i].x) && !float.IsInfinity(points[i].x), "Finite pose");
                            if (Vector3.Distance(points[i], baseline[i]) > .005f) changed = true;
                        }
                    }
                    Require(changed, family + " animation did not deform voxels: " + clip.name);
                    moving++;
                }
                Debug.Log($"PASS {family}: {mesh.vertexCount} vertices, {skin.bones.Length} bones, {moving}/{expectedClips} moving clips, bind pose and cube weights verified.");
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(instance); }
        }
        static Bounds BoundsOf(Vector3[] points)
        { Bounds b = new Bounds(points[0], Vector3.zero); foreach (Vector3 p in points) b.Encapsulate(p); return b; }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
