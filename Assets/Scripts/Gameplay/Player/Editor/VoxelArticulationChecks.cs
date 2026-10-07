using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class VoxelArticulationChecks
    {
        [MenuItem("Mismo/Modelos/Verificar separación de articulaciones voxel")]
        public static void Run()
        {
            var occupied = new[] { true, true };
            var dimensions = new Vector3Int(1, 2, 1);
            var weights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1 }, new BoneWeight { boneIndex0 = 1, weight0 = 1 } };
            Mesh separated = null, continuous = null, solid = null;
            try
            {
                separated = VoxelMeshBuilder.Build(occupied, dimensions, 1, Vector3.zero, Vector3.zero, Color.white,
                    "Separated jaws", false, null, null, 1, weights, new[] { 0, 1 });
                continuous = VoxelMeshBuilder.Build(occupied, dimensions, 1, Vector3.zero, Vector3.zero, Color.white,
                    "Continuous skin", false, null, null, 1, weights);
                solid = VoxelMeshBuilder.Build(occupied, dimensions, 1, Vector3.zero, Vector3.zero, Color.white,
                    "Static voxel", false, null, null, 1);
                Require(solid.triangles.Length == 60, "Static adjacent faces must still be culled");
                var w = separated.boneWeights;
                Require(separated.vertexCount == 48 && w.Take(24).All(v => v.boneIndex0 == 0 && v.weight0 == 1) &&
                    w.Skip(24).All(v => v.boneIndex0 == 1 && v.weight0 == 1), "A touching jaw must not inherit the opposite jaw's weights");
                // Move the upper part two units away. The original contact plane must become two distinct surfaces.
                var vertices = separated.vertices;
                var lowerContact = Enumerable.Range(0, 24).Where(i => Mathf.Approximately(vertices[i].y, 1)).ToArray();
                var upperContact = Enumerable.Range(24, 24).Where(i => Mathf.Approximately(vertices[i].y, 1)).ToArray();
                Require(lowerContact.All(i => Mathf.Approximately(vertices[i].y + (w[i].boneIndex0 == 1 ? 2 : 0), 1)) &&
                    upperContact.All(i => Mathf.Approximately(vertices[i].y + (w[i].boneIndex0 == 1 ? 2 : 0), 3)), "Jaw opening must leave a real gap");
                var regularWeights = continuous.boneWeights;
                var regularVertices = continuous.vertices;
                foreach (var group in Enumerable.Range(0, regularVertices.Length).GroupBy(i => regularVertices[i]))
                    Require(group.All(i => regularWeights[i].Equals(regularWeights[group.First()])), "Regular body corners must stay continuous");
                Require(regularWeights.Any(v => v.weight1 > 0), "Default skin smoothing must remain enabled");
                Debug.Log("VOXEL_ARTICULATION_CHECKS_OK: touching parts separate, regular skin stays continuous, static faces unchanged.");
            }
            finally { if (separated != null) Object.DestroyImmediate(separated); if (continuous != null) Object.DestroyImmediate(continuous); if (solid != null) Object.DestroyImmediate(solid); }
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
