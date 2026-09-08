using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mismo.Gameplay.Voxels
{
    /// <summary>
    /// Fachada de edición runtime y guardado por slot. Está pensada para ser llamada desde
    /// una UI de inventario, editor de armas o sistema de personalización del jugador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoxelRuntimeCustomizer : MonoBehaviour
    {
        [Serializable]
        private sealed class SaveData
        {
            public int version = 1;
            public Vector3Int dimensions;
            public float voxelSize;
            public List<SavedVoxel> voxels = new List<SavedVoxel>();
        }

        [Serializable]
        private struct SavedVoxel
        {
            public Vector3Int coordinate;
            public Color color;
        }

        [Header("Runtime persistence")]
        [SerializeField] private VoxelModelInstance model;
        [SerializeField] private string saveSlot = "default";
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private bool saveOnApplicationQuit = true;

        public VoxelModelInstance Model => model;
        public string SaveSlot => saveSlot;
        public void SelectSlot(string slot) => saveSlot = Sanitize(slot);
        public string SavePath => Path.Combine(Application.persistentDataPath, $"voxel_{Sanitize(saveSlot)}.json");

        private void Awake()
        {
            if (model == null) model = GetComponent<VoxelModelInstance>();
        }

        private void Start()
        {
            if (loadOnStart) Load();
        }

        private void OnApplicationQuit()
        {
            if (saveOnApplicationQuit) Save();
        }

        public bool SetVoxel(Vector3Int coordinate, bool filled, Color color)
        {
            return model != null && model.SetVoxelRuntime(coordinate, filled, color);
        }

        public bool AddVoxel(Vector3Int coordinate, Color color)
        {
            return SetVoxel(coordinate, true, color);
        }

        public bool RemoveVoxel(Vector3Int coordinate)
        {
            return SetVoxel(coordinate, false, Color.clear);
        }

        public void ResetToBase()
        {
            model?.ResetRuntimeVoxels();
        }

        /// <summary>Guarda solo los voxeles ocupados de esta instancia, no el asset original.</summary>
        public bool Save()
        {
            if (!Application.isPlaying || model == null || model.Asset == null) return false;
            SaveData data = new SaveData
            {
                dimensions = model.Dimensions,
                voxelSize = model.Asset.VoxelSize
            };
            Vector3Int dimensions = model.Dimensions;
            for (int z = 0; z < dimensions.z; z++)
                for (int y = 0; y < dimensions.y; y++)
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        Vector3Int coordinate = new Vector3Int(x, y, z);
                        if (model.GetRuntimeFilled(coordinate))
                            data.voxels.Add(new SavedVoxel { coordinate = coordinate, color = model.GetRuntimeColor(coordinate) });
                    }

            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"No se pudo guardar la personalización voxel: {exception.Message}", this);
                return false;
            }
        }

        /// <summary>Carga el slot; si no existe, conserva el modelo base.</summary>
        public bool Load()
        {
            if (!Application.isPlaying || model == null || model.Asset == null || !File.Exists(SavePath)) return false;
            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (data == null || data.dimensions != model.Dimensions) return false;
                if (data.version != 1 || data.voxels == null) return false;
                Vector3Int size = model.Dimensions;
                for (int z = 0; z < size.z; z++)
                    for (int y = 0; y < size.y; y++)
                        for (int x = 0; x < size.x; x++)
                            model.SetVoxelRuntime(new Vector3Int(x, y, z), false, Color.clear, false);
                if (data.voxels != null)
                    foreach (SavedVoxel voxel in data.voxels)
                        model.SetVoxelRuntime(voxel.coordinate, true, voxel.color, rebuild: false);
                model.Rebuild();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"No se pudo cargar la personalización voxel: {exception.Message}", this);
                return false;
            }
        }

        public bool DeleteSave()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"No se pudo borrar el slot voxel: {exception.Message}", this);
                return false;
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "default";
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Trim();
        }
    }
}
