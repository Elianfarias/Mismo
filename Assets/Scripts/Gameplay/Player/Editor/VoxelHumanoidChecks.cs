using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class VoxelHumanoidChecks
    {
        const string Model = "Assets/Art/FBX/Monsters/Bestiary - Dungeon Monsters Kit[Standard]/Bestiary - Dungeon Monsters Kit[Standard]/Imp.fbx";
        const string Clips = "Assets/Art/Animations/Quaternius/Humanoid";
        const string Output = "output/voxel-humanoid";
        const string Request = "Temp/VoxelHumanoidChecks.request";
        static double next;
        static readonly List<string> report = new List<string>();
        [InitializeOnLoadMethod] static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            next = EditorApplication.timeSinceStartup + 2;
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            Run();
        }

        [MenuItem("Mismo/Modelos/Verificar voxelizador Humanoid con Imp")]
        public static void Run()
        {
            report.Clear(); Directory.CreateDirectory(Output);
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
                var sourceAnimator = source.GetComponentInChildren<Animator>(true);
                Check(sourceAnimator != null && sourceAnimator.avatar != null && sourceAnimator.avatar.isValid && sourceAnimator.avatar.isHuman, "Imp original: Avatar Humanoid válido");
                string originalImporter = File.ReadAllText(Model + ".meta");
                VoxelizerChecks.RunAll();
                report.Add("PASS regresiones de geometría y rig Generic existentes");
                var output = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Voxelized/Imp_Humanoid.prefab");
                if (output == null) output = WeaponVoxelizerWindow.ExportModel(source, "Imp_Humanoid", 64, true, AssetDatabase.LoadAssetAtPath<DefaultAsset>(Clips));
                Check(File.ReadAllText(Model + ".meta") == originalImporter, "Importador original intacto");
                Check(output.GetComponent<VoxelRigInstance>().clips.All(c => c.humanMotion), "Clips exportados conservan curvas Humanoid");
                Check(output.GetComponentInChildren<Animator>().avatar == sourceAnimator.avatar, "Avatar original referenciado en prefab guardado");
                ValidatePreview(source, output);
                CheckInvalidInput(source);
                CheckNestedAnimator(source, output);
                report.Add("Prefab de ejemplo: " + AssetDatabase.GetAssetPath(output));
                File.WriteAllLines(Output + "/checks.txt", new[] { "PASS" }.Concat(report));
                Debug.Log("VOXEL_HUMANOID_CHECKS_OK");
            }
            catch (Exception e)
            {
                File.WriteAllLines(Output + "/checks.txt", new[] { "FAIL", e.ToString() }.Concat(report));
                Debug.LogException(e);
            }
            try
            {
                var checker = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ProjectOrganizationChecks")).FirstOrDefault(t => t != null);
                if (checker == null) throw new Exception("No se encontró ProjectOrganizationChecks");
                checker.GetMethod("Run").Invoke(null, null);
                File.WriteAllText(Output + "/organization.txt", "PASS");
            }
            catch (Exception e) { File.WriteAllText(Output + "/organization.txt", "FAIL\n" + (e.InnerException ?? e)); }
        }

        static void ValidatePreview(GameObject source, GameObject prefab)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var instance = Object.Instantiate(prefab);
            var original = Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(instance, scene); SceneManager.MoveGameObjectToScene(original, scene);
            var mesh = new Mesh();
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                var reference = original.GetComponentInChildren<Animator>();
                animator.Rebind(); reference.Rebind();
                Check(animator.isHuman, "Animator reconoce el Avatar en la instancia exportada");
                animator.enabled = false; reference.enabled = false;
                var skin = instance.GetComponent<SkinnedMeshRenderer>();
                Check(skin.bones.All(b => b != null) && skin.sharedMesh.bindposes.Length == skin.bones.Length, "Huesos y bindposes válidos");
                var rig = instance.GetComponent<VoxelRigInstance>();
                var run = rig.clips.Single(c => c.name == "Quaternius_Run");
                var sourceRun = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/Quaternius_Run.anim");
                var leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Quaternion initial = Quaternion.identity; Vector3[] vertices = null;
                float travel = 0, deformation = 0, error = 0;
                // Exactly the workshop preview path: disabled Animator + SampleAnimation.
                for (int i = 0; i < 12; i++)
                {
                    float time = run.length * i / 12;
                    run.SampleAnimation(animator.gameObject, time);
                    sourceRun.SampleAnimation(reference.gameObject, time);
                    if (i == 0) initial = leg.localRotation;
                    travel = Mathf.Max(travel, Quaternion.Angle(initial, leg.localRotation));
                    foreach (var bone in new[] { HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightFoot })
                        error = Mathf.Max(error, Quaternion.Angle(animator.GetBoneTransform(bone).localRotation, reference.GetBoneTransform(bone).localRotation));
                    skin.BakeMesh(mesh);
                    var current = mesh.vertices;
                    if (vertices == null) vertices = current;
                    for (int v = 0; v < current.Length; v += 31) deformation = Mathf.Max(deformation, Vector3.Distance(current[v], vertices[v]));
                }
                Check(travel > 10 && deformation > .01f && error < .5f, $"Preview del taller: pierna {travel:F1}°, deformación {deformation:F3} m, error contra original {error:F3}°");
                animator.enabled = true;
                var graph = PlayableGraph.Create("Voxel Humanoid runtime regression");
                try
                {
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var clip = AnimationClipPlayable.Create(graph, run);
                    AnimationPlayableOutput.Create(graph, "Animation", animator).SetSourcePlayable(clip);
                    graph.Play(); clip.SetTime(0); graph.Evaluate(0); initial = leg.localRotation;
                    float runtimeTravel = 0;
                    for (int i = 1; i <= 12; i++) { clip.SetTime(run.length * i / 12); graph.Evaluate(0); runtimeTravel = Mathf.Max(runtimeTravel, Quaternion.Angle(initial, leg.localRotation)); }
                    Check(runtimeTravel > 10, $"Playable Humanoid: pierna {runtimeTravel:F1}°");
                }
                finally { graph.Destroy(); }
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(instance); Object.DestroyImmediate(original); EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void CheckInvalidInput(GameObject source)
        {
            var instance = Object.Instantiate(source);
            try
            {
                instance.GetComponentInChildren<Animator>().avatar = null;
                bool rejected = false;
                try { VoxelRigExporter.Prepare(instance, Clips); } catch (InvalidOperationException e) { rejected = e.Message.Contains("Humanoid"); }
                Check(rejected, "Clips Humanoid sin Avatar rechazados antes de generar assets");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static void CheckNestedAnimator(GameObject source, GameObject exported)
        {
            var root = new GameObject("Nested Humanoid check");
            var child = Object.Instantiate(source, root.transform);
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Art/Prefabs/Voxelized/Imp_NestedCheck.prefab");
            string meshPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Art/Meshes/Voxelized/Imp_NestedCheck.asset");
            string animationPath = Path.ChangeExtension(prefabPath.Replace("/Prefabs/", "/Animations/"), null) + "_Animations";
            try
            {
                var originalAnimator = child.GetComponentInChildren<Animator>();
                originalAnimator.runtimeAnimatorController = exported.GetComponentInChildren<Animator>().runtimeAnimatorController;
                child.transform.localPosition = new Vector3(.2f, .3f, .1f);
                child.transform.localScale = Vector3.one * .8f;
                var plan = VoxelRigExporter.Prepare(root, Clips);
                Check(plan.Humanoid && plan.AnimationRoot == child.GetComponentInChildren<Animator>().transform, "Animator anidado conserva su raíz de animación");
                var sample = VoxelSurfaceSampler.Sample(root, 8, false, false, true);
                var mesh = (Mesh)typeof(WeaponVoxelizerWindow).GetMethod("BuildMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { sample.Occupied, sample.Dimensions, sample.VoxelSize, sample.Bounds.min, sample.Bounds.center,
                        Color.white, "Nested Humanoid check", false, sample.MaterialIndices, sample.UVs, sample.Materials.Count + 1, sample.BoneWeights });
                AssetDatabase.CreateAsset(mesh, meshPath);
                var nested = VoxelRigExporter.Create(root, sample, mesh, exported.GetComponent<SkinnedMeshRenderer>().sharedMaterials, prefabPath, false, Clips, plan);
                var savedAnimator = nested.GetComponentInChildren<Animator>();
                Check(savedAnimator.runtimeAnimatorController == originalAnimator.runtimeAnimatorController, "Controller original conservado en prefab guardado");
                Check(savedAnimator.transform.parent.name == root.name, "Animator exportado en el hijo original, no en el contenedor");
                ValidatePreview(root, nested);
            }
            finally
            {
                Object.DestroyImmediate(root);
                // Only the uniquely named assets created by this check are removed.
                AssetDatabase.DeleteAsset(prefabPath); AssetDatabase.DeleteAsset(meshPath); AssetDatabase.DeleteAsset(animationPath);
            }
        }

        static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); report.Add("PASS " + message); }
    }
}
