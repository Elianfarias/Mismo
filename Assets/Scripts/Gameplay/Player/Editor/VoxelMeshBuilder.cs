using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Builds static or skinned voxel surfaces. Optional skin regions preserve deliberate articulation seams.</summary>
    internal static class VoxelMeshBuilder
    {
        internal static void FillInterior(bool[] occupied, Vector3Int dimensions)
        {
            bool[] outside = new bool[occupied.Length];
            Queue<int> pending = new Queue<int>();
            for (int z = 0; z < dimensions.z; z++)
                for (int y = 0; y < dimensions.y; y++)
                    for (int x = 0; x < dimensions.x; x++)
                        if ((x == 0 || y == 0 || z == 0 || x == dimensions.x - 1 || y == dimensions.y - 1 || z == dimensions.z - 1) && TryMarkOutside(x, y, z, occupied, outside, dimensions))
                            pending.Enqueue(Index(x, y, z, dimensions));

            Vector3Int[] directions = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                int x = index % dimensions.x;
                int y = index / dimensions.x % dimensions.y;
                int z = index / (dimensions.x * dimensions.y);
                foreach (Vector3Int direction in directions)
                {
                    int nx = x + direction.x, ny = y + direction.y, nz = z + direction.z;
                    if (nx < 0 || ny < 0 || nz < 0 || nx >= dimensions.x || ny >= dimensions.y || nz >= dimensions.z) continue;
                    if (TryMarkOutside(nx, ny, nz, occupied, outside, dimensions)) pending.Enqueue(Index(nx, ny, nz, dimensions));
                }
            }

            for (int i = 0; i < occupied.Length; i++)
                if (!occupied[i] && !outside[i]) occupied[i] = true;
        }

        private static bool TryMarkOutside(int x, int y, int z, bool[] occupied, bool[] outside, Vector3Int dimensions)
        {
            int index = Index(x, y, z, dimensions);
            if (occupied[index] || outside[index]) return false;
            outside[index] = true;
            return true;
        }

        internal static Mesh Build(bool[] occupied, Vector3Int dimensions, float voxelSize, Vector3 boundsMin, Vector3 pivot, Color color, string name, bool preserveMaterials, int[] voxelMaterials, Vector2[] voxelUVs, int submeshCount, BoneWeight[] voxelWeights = null, int[] skinRegions = null)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<List<int>> submeshTriangles = new List<List<int>>();
            for (int i = 0; i < Mathf.Max(1, preserveMaterials ? submeshCount : 1); i++) submeshTriangles.Add(new List<int>());
            List<Vector3> normals = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<Vector2> uvs = new List<Vector2>();
            List<BoneWeight> weights = voxelWeights != null ? new List<BoneWeight>() : null;
            var cornerWeights = weights != null ? new Dictionary<(Vector3Int corner, int region), BoneWeight>() : null;
            if (skinRegions != null && skinRegions.Length != occupied.Length)
                throw new System.ArgumentException("Skin region count must match voxel count.", nameof(skinRegions));
            Vector3 half = Vector3.one * voxelSize * .5f;
            Vector3Int[] directions = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
            Vector3[][] faces =
            {
                new[]{new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(1,1,1),new Vector3(1,-1,1)},
                new[]{new Vector3(-1,-1,-1),new Vector3(-1,-1,1),new Vector3(-1,1,1),new Vector3(-1,1,-1)},
                new[]{new Vector3(-1,1,-1),new Vector3(-1,1,1),new Vector3(1,1,1),new Vector3(1,1,-1)},
                new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,-1,1),new Vector3(-1,-1,1)},
                new[]{new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)},
                new[]{new Vector3(-1,-1,-1),new Vector3(-1,1,-1),new Vector3(1,1,-1),new Vector3(1,-1,-1)}
            };

            for (int z = 0; z < dimensions.z; z++)
                for (int y = 0; y < dimensions.y; y++)
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        int voxelIndex = Index(x, y, z, dimensions);
                        if (!occupied[voxelIndex]) continue;
                        if (weights != null && directions.All(direction =>
                        {
                            int ax = x + direction.x, ay = y + direction.y, az = z + direction.z;
                            return ax >= 0 && ay >= 0 && az >= 0 && ax < dimensions.x && ay < dimensions.y && az < dimensions.z && occupied[Index(ax, ay, az, dimensions)];
                        })) continue;
                        int materialIndex = preserveMaterials && voxelMaterials != null && voxelMaterials[voxelIndex] >= 0 ? voxelMaterials[voxelIndex] + 1 : 0;
                        materialIndex = Mathf.Clamp(materialIndex, 0, submeshTriangles.Count - 1);
                        Vector2 uv = voxelUVs != null && voxelIndex < voxelUVs.Length ? voxelUVs[voxelIndex] : Vector2.zero;
                        Vector3 center = boundsMin + new Vector3((x + .5f) * voxelSize, (y + .5f) * voxelSize, (z + .5f) * voxelSize) - pivot;
                        for (int face = 0; face < directions.Length; face++)
                        {
                            int nx = x + directions[face].x, ny = y + directions[face].y, nz = z + directions[face].z;
                            if (weights == null && nx >= 0 && ny >= 0 && nz >= 0 && nx < dimensions.x && ny < dimensions.y && nz < dimensions.z && occupied[Index(nx, ny, nz, dimensions)]) continue;
                            int start = vertices.Count;
                            for (int corner = 0; corner < 4; corner++)
                            {
                                vertices.Add(center + Vector3.Scale(faces[face][corner], half));
                                normals.Add(directions[face]);
                                colors.Add(color);
                                uvs.Add(uv);
                                if (weights != null)
                                {
                                    Vector3 offset = faces[face][corner];
                                    var key = new Vector3Int(x + (offset.x > 0 ? 1 : 0), y + (offset.y > 0 ? 1 : 0), z + (offset.z > 0 ? 1 : 0));
                                    int region = skinRegions != null ? skinRegions[voxelIndex] : 0;
                                    var cornerKey = (key, region);
                                    if (!cornerWeights.TryGetValue(cornerKey, out BoneWeight weight))
                                    {
                                        var adjacent = new List<BoneWeight>(8);
                                        for (int az = key.z - 1; az <= key.z; az++)
                                            for (int ay = key.y - 1; ay <= key.y; ay++)
                                                for (int ax = key.x - 1; ax <= key.x; ax++)
                                                {
                                                    if (ax < 0 || ay < 0 || az < 0 || ax >= dimensions.x || ay >= dimensions.y || az >= dimensions.z) continue;
                                                    int neighbor = Index(ax, ay, az, dimensions);
                                                    if (occupied[neighbor] && voxelWeights[neighbor].weight0 > 0 && (skinRegions == null || skinRegions[neighbor] == region)) adjacent.Add(voxelWeights[neighbor]);
                                                }
                                        weight = VoxelSurfaceSampler.AverageWeights(adjacent);
                                        cornerWeights[cornerKey] = weight;
                                    }
                                    weights.Add(weight);
                                }
                            }
                            submeshTriangles[materialIndex].Add(start); submeshTriangles[materialIndex].Add(start + 1); submeshTriangles[materialIndex].Add(start + 2);
                            submeshTriangles[materialIndex].Add(start); submeshTriangles[materialIndex].Add(start + 2); submeshTriangles[materialIndex].Add(start + 3);
                        }
                    }

            Mesh mesh = new Mesh { name = string.IsNullOrWhiteSpace(name) ? "VoxelWeapon" : name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.subMeshCount = submeshTriangles.Count;
            for (int i = 0; i < submeshTriangles.Count; i++) mesh.SetTriangles(submeshTriangles[i], i);
            mesh.SetNormals(normals); mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            if (weights != null) mesh.boneWeights = weights.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Index(int x, int y, int z, Vector3Int d) => x + d.x * (y + d.y * z);
    }
}
