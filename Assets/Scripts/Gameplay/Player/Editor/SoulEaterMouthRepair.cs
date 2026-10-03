using System;
using System.Linq;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterMouthRepair
    {
        public const string MeshPath = "Assets/Art/Meshes/Voxelized/SoulEater_Articulated.asset";
        public const string SourcePath = "Assets/Art/FBX/Monsters/Dragon SoulEater/Mesh/DragonSoulEaterMesh.fbx";
        const string Head = "Root_Pelvis/Spine/Chest/Neck/Head/";

        [MenuItem("Mismo/Modelos/Corregir boca de SoulEater verde")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode antes de regenerar.");
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoulEaterGreenVariant.PrefabPath);
            if (original == null || prefab == null) throw new InvalidOperationException("Falta el original o SoulEater verde.");
            var source = Object.Instantiate(original);
            var target = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Mesh generated = null;
            try
            {
                var sourceAnimator = source.GetComponent<Animator>(); if (sourceAnimator != null) sourceAnimator.enabled = false;
                var library = target.GetComponent<VoxelRigInstance>(); library.animator.enabled = false;
                var green = library.surface.sharedMaterial;
                foreach (var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())
                    skin.sharedMaterials = Enumerable.Repeat(green, skin.sharedMesh.subMeshCount).ToArray();
                // Always use the unchanged base rig as the bind-space reference, including on repeated runs.
                var baseMesh = AssetDatabase.LoadAssetAtPath<GameObject>(SoulEaterGreenVariant.SourcePath).GetComponent<VoxelRigInstance>().surface.sharedMesh;
                library.surface.sharedMesh = baseMesh;
                generated = VoxelArticulationRebuilder.Build(source, library, library.clips.First(c => c.name == "Scream"),
                    .5f, new[] { Head + "Jaw", Head + "UpperMouth" }, Head + "Jaw");
                var eyes = SoulEaterEyeDetail.Add(source, library, generated);
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (saved == null) { generated.name = "SoulEater_Articulated"; AssetDatabase.CreateAsset(generated, MeshPath); saved = generated; generated = null; }
                else { EditorUtility.CopySerialized(generated, saved); saved.name = "SoulEater_Articulated"; EditorUtility.SetDirty(saved); }
                library.surface.sharedMesh = saved;
                library.surface.localBounds = saved.bounds;
                library.surface.sharedMaterials = Enumerable.Repeat(green, saved.subMeshCount - 1).Concat(new[] { eyes }).ToArray();
                library.animator.enabled = prefab.GetComponent<VoxelRigInstance>().animator.enabled;
                PrefabUtility.RecordPrefabInstancePropertyModifications(library.surface);
                PrefabUtility.SaveAsPrefabAsset(target, SoulEaterGreenVariant.PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("SOUL_EATER_MOUTH_REPAIR_OK: geometría articulada; rig y clips originales conservados.");
            }
            finally { if (generated != null) Object.DestroyImmediate(generated); Object.DestroyImmediate(target); Object.DestroyImmediate(source); }
        }
    }
}
