using System;
using UnityEngine;

namespace Mismo.Gameplay.Voxels
{
    /// <summary>Datos editables de una grilla voxel, reutilizable para personajes, armas y props.</summary>
    [CreateAssetMenu(fileName = "VoxelModel", menuName = "Mismo/Voxels/Voxel Model")]
    public sealed class VoxelModelAsset : ScriptableObject
    {
        [Serializable]
        public struct VoxelCell
        {
            public bool filled;
            public Color color;
        }

        [SerializeField] private Vector3Int dimensions = new Vector3Int(16, 16, 16);
        [SerializeField, Min(0.001f)] private float voxelSize = 0.1f;
        [SerializeField] private Vector3 origin;
        [SerializeField] private Color defaultColor = Color.white;
        [SerializeField] private VoxelCell[] cells;

        public Vector3Int Dimensions => dimensions;
        public float VoxelSize => Mathf.Max(0.001f, voxelSize);
        public Vector3 Origin => origin;
        public Color DefaultColor => defaultColor;
        public int CellCount => dimensions.x * dimensions.y * dimensions.z;

        public void SetDefaultColor(Color color)
        {
            defaultColor = color;
        }

        public void Configure(Vector3Int size, float cellSize)
        {
            dimensions = new Vector3Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y), Mathf.Max(1, size.z));
            voxelSize = Mathf.Max(0.001f, cellSize);
            origin = new Vector3(-dimensions.x, -dimensions.y, -dimensions.z) * voxelSize * 0.5f;
            cells = new VoxelCell[CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i].color = defaultColor;
        }

        public bool IsInside(Vector3Int coordinate) => coordinate.x >= 0 && coordinate.y >= 0 && coordinate.z >= 0 &&
            coordinate.x < dimensions.x && coordinate.y < dimensions.y && coordinate.z < dimensions.z;

        public bool GetFilled(Vector3Int coordinate)
        {
            return IsInside(coordinate) && EnsureStorage()[Index(coordinate)].filled;
        }

        public Color GetColor(Vector3Int coordinate)
        {
            return IsInside(coordinate) ? EnsureStorage()[Index(coordinate)].color : defaultColor;
        }

        public void SetVoxel(Vector3Int coordinate, bool filled, Color color)
        {
            if (!IsInside(coordinate)) return;
            VoxelCell[] storage = EnsureStorage();
            storage[Index(coordinate)] = new VoxelCell { filled = filled, color = color };
        }

        public Vector3 GridToLocal(Vector3Int coordinate)
        {
            return origin + new Vector3(coordinate.x + 0.5f, coordinate.y + 0.5f, coordinate.z + 0.5f) * VoxelSize;
        }

        public int CountFilled()
        {
            int count = 0;
            foreach (VoxelCell cell in EnsureStorage()) if (cell.filled) count++;
            return count;
        }

        private int Index(Vector3Int coordinate) => coordinate.x + dimensions.x * (coordinate.y + dimensions.y * coordinate.z);

        private VoxelCell[] EnsureStorage()
        {
            if (dimensions.x < 1 || dimensions.y < 1 || dimensions.z < 1)
                dimensions = new Vector3Int(1, 1, 1);
            if (cells == null || cells.Length != CellCount)
            {
                VoxelCell[] previous = cells;
                cells = new VoxelCell[CellCount];
                if (previous != null) Array.Copy(previous, cells, Mathf.Min(previous.Length, cells.Length));
            }
            return cells;
        }
    }
}
