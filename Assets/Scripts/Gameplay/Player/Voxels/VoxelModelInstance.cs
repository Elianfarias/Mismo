using UnityEngine;

namespace Mismo.Gameplay.Voxels
{
    /// <summary>Reconstruye un VoxelModelAsset como cubos, listo para usar en personajes o armas.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VoxelModelInstance : MonoBehaviour
    {
        [SerializeField] private VoxelModelAsset asset;
        [SerializeField] private Material material;
        [SerializeField] private bool rebuildOnEnable = true;
        [SerializeField] private Transform voxelRoot;
        private bool rebuilding;
        private MaterialPropertyBlock propertyBlock;
        private VoxelModelAsset.VoxelCell[] runtimeCells;

        public VoxelModelAsset Asset => asset;
        public Material Material => material;
        public Vector3Int Dimensions => asset != null ? asset.Dimensions : Vector3Int.zero;

        public void Configure(VoxelModelAsset model, Material voxelMaterial)
        {
            asset = model;
            runtimeCells = null;
            material = voxelMaterial;
            Rebuild();
        }

        private void OnEnable()
        {
            if (rebuildOnEnable && asset != null) Rebuild();
        }

        private void OnValidate()
        {
            if (!rebuilding && rebuildOnEnable && asset != null) Rebuild();
        }

        [ContextMenu("Rebuild Voxels")]
        public void Rebuild()
        {
            if (rebuilding || asset == null) return;
            rebuilding = true;
            try
            {
                if (Application.isPlaying) EnsureRuntimeStorage();
                EnsureRoot();
                ClearRoot();
                Vector3Int dimensions = asset.Dimensions;
                for (int z = 0; z < dimensions.z; z++)
                    for (int y = 0; y < dimensions.y; y++)
                        for (int x = 0; x < dimensions.x; x++)
                        {
                            Vector3Int coordinate = new Vector3Int(x, y, z);
                            if (!GetFilled(coordinate)) continue;
                            CreateVoxel(coordinate);
                        }
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            finally
            {
                rebuilding = false;
            }
        }

        /// <summary>Modifica esta instancia durante runtime sin tocar el asset compartido.</summary>
        public bool SetVoxelRuntime(Vector3Int coordinate, bool filled, Color color, bool rebuild = true)
        {
            if (!Application.isPlaying || asset == null || !asset.IsInside(coordinate)) return false;
            EnsureRuntimeStorage();
            runtimeCells[Index(coordinate)] = new VoxelModelAsset.VoxelCell { filled = filled, color = color };
            if (rebuild) Rebuild();
            return true;
        }

        public bool GetRuntimeFilled(Vector3Int coordinate)
        {
            if (asset == null || !asset.IsInside(coordinate)) return false;
            return Application.isPlaying ? EnsureRuntimeStorage()[Index(coordinate)].filled : asset.GetFilled(coordinate);
        }

        public Color GetRuntimeColor(Vector3Int coordinate)
        {
            if (asset == null || !asset.IsInside(coordinate)) return Color.clear;
            return Application.isPlaying ? EnsureRuntimeStorage()[Index(coordinate)].color : asset.GetColor(coordinate);
        }

        /// <summary>Descarta las modificaciones runtime y vuelve a copiar el asset base.</summary>
        public void ResetRuntimeVoxels()
        {
            if (!Application.isPlaying || asset == null) return;
            runtimeCells = null;
            Rebuild();
        }

        private void EnsureRoot()
        {
            if (voxelRoot != null) return;
            Transform existing = transform.Find("Voxels");
            if (existing != null) voxelRoot = existing;
            else
            {
                GameObject root = new GameObject("Voxels");
                root.transform.SetParent(transform, false);
                voxelRoot = root.transform;
            }
        }

        private void ClearRoot()
        {
            for (int i = voxelRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = voxelRoot.GetChild(i).gameObject;
                child.SetActive(false);
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child);
                else Destroy(child);
#else
                Destroy(child);
#endif
            }
        }

        private void CreateVoxel(Vector3Int coordinate)
        {
            GameObject voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            voxel.name = $"Voxel_{coordinate.x}_{coordinate.y}_{coordinate.z}";
            voxel.transform.SetParent(voxelRoot, false);
            voxel.transform.localPosition = asset.GridToLocal(coordinate);
            voxel.transform.localScale = Vector3.one * asset.VoxelSize * 0.96f;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(voxel.GetComponent<Collider>());
            else Destroy(voxel.GetComponent<Collider>());
#else
            Destroy(voxel.GetComponent<Collider>());
#endif
            Renderer renderer = voxel.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            Color color = GetRuntimeColor(coordinate);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private bool GetFilled(Vector3Int coordinate)
        {
            return Application.isPlaying ? EnsureRuntimeStorage()[Index(coordinate)].filled : asset.GetFilled(coordinate);
        }

        private VoxelModelAsset.VoxelCell[] EnsureRuntimeStorage()
        {
            if (runtimeCells != null && runtimeCells.Length == asset.CellCount) return runtimeCells;
            runtimeCells = new VoxelModelAsset.VoxelCell[asset.CellCount];
            Vector3Int dimensions = asset.Dimensions;
            for (int z = 0; z < dimensions.z; z++)
                for (int y = 0; y < dimensions.y; y++)
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        Vector3Int coordinate = new Vector3Int(x, y, z);
                        runtimeCells[Index(coordinate)] = new VoxelModelAsset.VoxelCell
                        {
                            filled = asset.GetFilled(coordinate),
                            color = asset.GetColor(coordinate)
                        };
                    }
            return runtimeCells;
        }

        private int Index(Vector3Int coordinate)
        {
            return coordinate.x + Dimensions.x * (coordinate.y + Dimensions.y * coordinate.z);
        }
    }
}
