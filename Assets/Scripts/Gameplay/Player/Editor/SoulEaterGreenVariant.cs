using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterGreenVariant
    {
        public const string SourcePath = "Assets/Art/Prefabs/Voxelized/SoulEater_Animated.prefab";
        public const string PrefabPath = "Assets/Art/Prefabs/Voxelized/SoulEater_Green_Animated.prefab";
        public const string MaterialPath = "Assets/Art/Materials/DragonBosses/SoulEater_Green_Voxel.mat";
        const string GreenSource = "Assets/Art/Materials/Monsters/FourEvilDragonsPBR/DragonSoulEater/GreenPBR.mat";

        [MenuItem("Mismo/Modelos/Crear variante verde de SoulEater")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Crear la variante fuera de Play Mode.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            var green = AssetDatabase.LoadAssetAtPath<Material>(GreenSource);
            if (source == null || green == null || green.GetTexture("_BaseMap") == null)
                throw new InvalidOperationException("Falta SoulEater voxelizado o la textura verde del paquete completo.");
            // Re-running the menu must never overwrite an artist's adjustments.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null || File.Exists(PrefabPath))
            {
                Debug.Log("La variante verde ya existe: " + PrefabPath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            AssetDatabase.Refresh();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(green) { name = "SoulEater_Green_Voxel" };
                // Voxel faces have flat normals and no original tangent basis.
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = "SoulEater_Green_Animated";
                var rig = instance.GetComponent<VoxelRigInstance>();
                if (rig == null || rig.surface == null || rig.animator == null || rig.clips.Any(c => c == null))
                    throw new InvalidOperationException("SoulEater tiene referencias de rig incompletas.");
                rig.surface.sharedMaterials = Enumerable.Repeat(material, rig.surface.sharedMesh.subMeshCount).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(rig.surface);
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("SOUL_EATER_GREEN_OK: variante con malla, rig y " + rig.clips.Length + " clips compartidos.");
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
