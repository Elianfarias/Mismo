using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    internal static class VoxelRigExporter
    {
        internal sealed class RigPlan
        {
            public Animator SourceAnimator;
            public Transform AnimationRoot;
            public bool Humanoid;
            public List<(AnimationClip clip, string name)> Clips;
        }

        // Validate before creating mesh/material assets, so incompatible clips cannot
        // leave a half-exported character behind.
        internal static RigPlan Prepare(GameObject source, string animationFolder)
        {
            var animators = source.GetComponentsInChildren<Animator>(true);
            if (animators.Length > 1)
                throw new InvalidOperationException("Seleccioná un solo personaje con un único Animator y su esqueleto completo.");
            var animator = animators.FirstOrDefault();
            var avatar = animator != null ? animator.avatar : null;
            if (avatar != null && !avatar.isValid)
                throw new InvalidOperationException("El Avatar del modelo no es válido. Corregí su configuración Rig antes de voxelizar.");
            var plan = new RigPlan { SourceAnimator = animator, AnimationRoot = animator != null ? animator.transform : source.transform,
                Humanoid = avatar != null && avatar.isHuman, Clips = FindClips(source, animationFolder) };
            foreach (var entry in plan.Clips)
                if (entry.clip.humanMotion != plan.Humanoid)
                    throw new InvalidOperationException($"El clip '{entry.name}' es {(entry.clip.humanMotion ? "Humanoid" : "Generic")}, pero el modelo {(plan.Humanoid ? "usa un Avatar Humanoid" : "no tiene un Avatar Humanoid válido")}. Elegí clips del mismo tipo; no cambies el original a Generic.");
            if (plan.Humanoid && animator.runtimeAnimatorController != null &&
                animator.runtimeAnimatorController.animationClips.Any(c => c != null && !c.humanMotion))
                throw new InvalidOperationException("El Controller del modelo Humanoid contiene clips Generic. Asigná un Controller compatible o quitá esa referencia antes de exportar; elegir otra carpeta no corrige el Controller original.");
            return plan;
        }

        internal static GameObject Create(GameObject source, VoxelSurfaceSampler.Result sample, Mesh mesh,
            Material[] materials, string prefabPath, bool collider, string animationFolder, RigPlan plan = null)
        {
            plan = plan ?? Prepare(source, animationFolder);
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var mapping = new Dictionary<Transform, Transform>();
                Transform rig = CloneHierarchy(source.transform, root.transform, mapping);
                rig.localPosition = -sample.Bounds.center;
                rig.localRotation = Quaternion.identity;
                rig.localScale = source.transform.lossyScale;
                // Clip paths and Humanoid skeleton mapping are relative to the original
                // Animator, which may live below an outer prefab container.
                var animator = mapping[plan.AnimationRoot].gameObject.AddComponent<Animator>();
                if (plan.SourceAnimator != null) animator.avatar = plan.SourceAnimator.avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMaterials = materials;
                skin.bones = sample.Bones.Select(bone => mapping[bone]).ToArray();
                skin.rootBone = rig;
                mesh.bindposes = skin.bones.Select(bone => bone.worldToLocalMatrix * skin.transform.localToWorldMatrix).ToArray();
                skin.sharedMesh = mesh; skin.localBounds = mesh.bounds;
                // Animated extremities can leave the rest-pose bounds; Unity updates the bounds from the bones.
                skin.updateWhenOffscreen = true;
                EditorUtility.SetDirty(mesh);
                var library = root.AddComponent<VoxelRigInstance>();
                library.animator = animator; library.surface = skin;
                string folder = Path.ChangeExtension(prefabPath.Replace("/Prefabs/", "/Animations/"), null) + "_Animations";
                EnsureFolder(Path.GetDirectoryName(folder).Replace('\\', '/'));
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
                if (animator.avatar != null && !EditorUtility.IsPersistent(animator.avatar))
                {
                    var avatar = Object.Instantiate(animator.avatar);
                    avatar.name = root.name + "Avatar";
                    AssetDatabase.CreateAsset(avatar, AssetDatabase.GenerateUniqueAssetPath(folder + "/Avatar.asset"));
                    animator.avatar = avatar;
                }
                animator.Rebind();
                if (plan.Humanoid && !animator.isHuman)
                    throw new InvalidOperationException("El Avatar Humanoid no reconoce la jerarquía exportada. Seleccioná la raíz completa del personaje original.");
                var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Animations.controller");
                var clips = new List<AnimationClip>();
                var names = new HashSet<string>();
                foreach (var entry in plan.Clips)
                {
                    AnimationClip clip = CopyClip(entry.clip, plan.AnimationRoot, entry.name);
                    if (!clip.humanMotion && AnimationUtility.GetCurveBindings(clip).Length == 0) { Object.DestroyImmediate(clip); continue; }
                    string name = clip.name; int suffix = 2;
                    while (!names.Add(name)) name = clip.name + "_" + suffix++;
                    clip.name = name;
                    AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(name) + ".anim"));
                    var state = controller.layers[0].stateMachine.AddState(name); state.motion = clip;
                    if (clips.Count == 0 || name.ToLowerInvariant().StartsWith("idle")) controller.layers[0].stateMachine.defaultState = state;
                    clips.Add(clip);
                }
                library.clips = clips.ToArray(); library.stateNames = clips.Select(clip => clip.name).ToArray();
                // Keep authored parameters, transitions and blend trees for Humanoid
                // characters; the generated controller is a fallback clip library.
                animator.runtimeAnimatorController = plan.Humanoid && plan.SourceAnimator.runtimeAnimatorController != null
                    ? plan.SourceAnimator.runtimeAnimatorController : controller;
                if (collider)
                {
                    var body = root.AddComponent<CapsuleCollider>();
                    body.center = mesh.bounds.center;
                    body.height = mesh.bounds.size.y;
                    body.radius = Mathf.Max(.05f, Mathf.Min(mesh.bounds.size.x, mesh.bounds.size.z) * .18f);
                    body.height = Mathf.Max(body.height, body.radius * 2);
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (clips.Count == 0) Debug.LogWarning(plan.Humanoid
                    ? "Avatar Humanoid conservado. No se encontraron clips: podés asignar clips Humanoid en el taller de enemigos."
                    : "Rig exportado sin clips. Elegí la carpeta de animaciones en el voxelizador.", prefab);
                Debug.Log($"Voxel rig: {sample.Bones.Count} huesos, {clips.Count} animaciones, {mesh.vertexCount} vértices: {prefabPath}");
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Transform CloneHierarchy(Transform source, Transform parent, Dictionary<Transform, Transform> mapping)
        {
            var clone = new GameObject(source.name).transform;
            clone.SetParent(parent, false);
            clone.localPosition = source.localPosition; clone.localRotation = source.localRotation; clone.localScale = source.localScale;
            mapping.Add(source, clone);
            foreach (Transform child in source) CloneHierarchy(child, clone, mapping);
            return clone;
        }

        internal static List<(AnimationClip clip, string name)> FindClips(GameObject source, string folder)
        {
            var result = new List<(AnimationClip clip, string name)>();
            var seen = new HashSet<AnimationClip>();
            foreach (var animator in source.GetComponentsInChildren<Animator>(true))
                if (animator.runtimeAnimatorController != null)
                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                        if (seen.Add(clip)) result.Add((clip, ImportedClipName(clip)));
            string modelPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(modelPath)) modelPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source);
            if (!string.IsNullOrEmpty(modelPath))
            {
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>())
                    if (!clip.name.StartsWith("__preview") && seen.Add(clip)) result.Add((clip, ImportedClipName(clip)));
                if (string.IsNullOrEmpty(folder))
                {
                    string parent = Path.GetDirectoryName(modelPath).Replace('\\', '/');
                    for (int level = 0; level < 3 && !string.IsNullOrEmpty(parent); level++)
                    {
                        string candidate = new[] { "Animation", "Animations" }.Select(name => parent + "/" + name).FirstOrDefault(AssetDatabase.IsValidFolder);
                        if (candidate == null && parent.StartsWith("Assets/Art/", StringComparison.Ordinal))
                        {
                            string mirrored = parent.Replace("/FBX/", "/Animations/").Replace("/Prefabs/", "/Animations/").Replace("/Models/", "/Animations/");
                            if (mirrored != parent && AssetDatabase.IsValidFolder(mirrored)) candidate = mirrored;
                        }
                        if (candidate != null)
                        {
                            // Packs often group several unrelated rigs under Animations.
                            // Prefer the prefab's family folder instead of mixing their clips.
                            string[] children = AssetDatabase.GetSubFolders(candidate);
                            string family = children.FirstOrDefault(child => modelPath.Split('/').Any(part =>
                                string.Equals(part, Path.GetFileName(child), StringComparison.OrdinalIgnoreCase)));
                            if (family != null) folder = family;
                            else if (children.Length <= 1) folder = candidate;
                            else if (result.Count > 0)
                                Debug.LogWarning("Hay varias familias de animaciones. Se conservan los clips del Animator; elegí una carpeta específica para añadir otros.");
                            else throw new InvalidOperationException("La carpeta Animations contiene varios modelos. Elegí la subcarpeta de animaciones de este modelo.");
                            break;
                        }
                        parent = Path.GetDirectoryName(parent)?.Replace('\\', '/');
                    }
                }
            }
            if (!string.IsNullOrEmpty(folder))
            {
                if (!AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("La carpeta de animaciones no es válida.");
                // A selected/detected family is authoritative. Controllers may still reference
                // clips from a previous package import, which would otherwise be duplicated.
                result.Clear(); seen.Clear();
                foreach (string path in AssetDatabase.FindAssets("", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path))
                {
                    var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview")).ToArray();
                    foreach (var clip in clips)
                        if (seen.Add(clip)) result.Add((clip, clips.Length == 1 ? Path.GetFileNameWithoutExtension(path) : clip.name));
                }
            }
            return result;
        }

        static string ImportedClipName(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            // Many FBXs expose a clip called "Take 001" even though the file is Run/Die/etc.
            if (!string.IsNullOrEmpty(path) && !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Count(c => !c.name.StartsWith("__preview")) == 1)
                return Path.GetFileNameWithoutExtension(path);
            return clip.name;
        }

        private static AnimationClip CopyClip(AnimationClip source, Transform rig, string name)
        {
            if (source.humanMotion)
            {
                // Humanoid muscle/root curves are Animator bindings, not Transform
                // curves. Cloning preserves them, import settings, events and looping.
                var humanoid = Object.Instantiate(source);
                humanoid.name = name;
                humanoid.hideFlags = HideFlags.None;
                humanoid.legacy = false;
                return humanoid;
            }
            var clip = new AnimationClip { name = name, frameRate = source.frameRate, legacy = false };
            int skipped = 0;
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.type != typeof(Transform)) { skipped++; continue; }
                if (!string.IsNullOrEmpty(binding.path) && rig.Find(binding.path) == null) { skipped++; continue; }
                // The container owns world placement. Empty-path curves would overwrite pivot/scale.
                if (string.IsNullOrEmpty(binding.path)) continue;
                AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(source, binding));
            }
            string lower = name.ToLowerInvariant();
            bool loop = source.isLooping || lower.Contains("idle") || lower.Contains("walk") || lower == "run" ||
                lower.Contains("sleep") || lower.Contains("glide") || lower.Contains("float") || lower.Contains("forward");
            var settings = AnimationUtility.GetAnimationClipSettings(source); settings.loopTime = loop; settings.loopBlend = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.EnsureQuaternionContinuity();
            if (skipped > 0) Debug.LogWarning($"{name}: {skipped} curvas ajenas al rig/Transform no se copiaron.");
            return clip;
        }
        private static string SafeName(string name)
        { foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_'); return name; }
    }
}
