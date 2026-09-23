using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Keep the original Transform clips as the source of truth. They must never
    // be played directly on the mapped bones of a Humanoid Animator.
    public static class QuaterniusHumanoidLocomotion
    {
        public const string ModelPath = "Assets/Art/FBX/Characters/Voxel_Adventurer_Animated.fbx";
        public const string SourceFolder = "Assets/Art/Animations/Quaternius/Retargeted";
        public const string OutputFolder = "Assets/Art/Animations/Quaternius/Humanoid";
        public const string ControllerPath = "Assets/Art/Animations/VoxelLocomotion.controller";
        static readonly string[] Motions = { "Idle", "Walk", "Run", "Jump", "Fall", "Land" };
        static readonly string[] TrackedBones = { "Hips", "Head", "Hand.L", "Hand.R", "Foot.L", "Foot.R" };

        [MenuItem("Mismo/Character/Convertir locomoción Quaternius a Humanoid")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Salir de Play Mode antes de convertir la locomoción.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var avatar = model != null ? model.GetComponent<Animator>()?.avatar : null;
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("El aventurero necesita un Avatar Humanoid válido antes de convertir los clips.");
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/Art/Animations/Quaternius", "Humanoid");

            var replacements = new Dictionary<AnimationClip, AnimationClip>();
            var report = new List<string>();
            foreach (string motion in Motions)
            {
                var original = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceFolder + "/Quaternius_" + motion + ".anim");
                if (original == null || original.humanMotion)
                    throw new InvalidOperationException("Falta el clip Generic original: " + motion);
                var converted = Bake(model, original);
                try
                {
                    report.Add(Validate(model, original, converted));
                    string path = OutputFolder + "/Quaternius_" + motion + ".anim";
                    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(converted, path);
                        existing = converted;
                    }
                    else
                    {
                        EditorUtility.CopySerialized(converted, existing);
                        EditorUtility.SetDirty(existing);
                    }
                    replacements.Add(original, existing);
                }
                finally
                {
                    if (!AssetDatabase.Contains(converted)) Object.DestroyImmediate(converted);
                }
            }
            // Update only matching motions, preserving transitions, combat clips,
            // blend thresholds and all user edits to the controller.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) throw new InvalidOperationException("Falta VoxelLocomotion.");
            foreach (var layer in controller.layers) Replace(layer.stateMachine, replacements);
            EnsureHumanoidMaskSupport(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("output/humanoid-locomotion");
            File.WriteAllLines("output/humanoid-locomotion/checks.txt", report);
            if (File.Exists("output/humanoid-locomotion/FAILED.txt")) File.Delete("output/humanoid-locomotion/FAILED.txt");
            Debug.Log("HUMANOID_LOCOMOTION_OK\n" + string.Join("\n", report));
        }

        public static AnimationClip Bake(GameObject model, AnimationClip original)
        {
            var source = Object.Instantiate(model);
            source.hideFlags = HideFlags.HideAndDontSave;
            source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = source.GetComponent<Animator>();
            var avatar = animator.avatar;
            Object.DestroyImmediate(animator);
            var transforms = source.GetComponentsInChildren<Transform>();
            var positions = transforms.Select(t => t.localPosition).ToArray();
            var rotations = transforms.Select(t => t.localRotation).ToArray();
            var scales = transforms.Select(t => t.localScale).ToArray();
            int goalOffset = HumanTrait.MuscleCount + 7;
            var curves = Enumerable.Range(0, goalOffset + 28).Select(_ => new AnimationCurve()).ToArray();
            try
            {
                var bindings = AnimationUtility.GetCurveBindings(original);
                if (bindings.Any(b => b.type == typeof(Transform) && source.transform.Find(b.path) == null))
                    throw new InvalidOperationException("El esqueleto no coincide con las rutas del clip " + original.name + ": " + string.Join(", ", bindings.Where(b => b.type == typeof(Transform) && source.transform.Find(b.path) == null).Select(b => b.path).Distinct()));
                using (var handler = new HumanPoseHandler(avatar, source.transform))
                {
                    var pose = new HumanPose();
                    int frames = Mathf.CeilToInt(original.length * 60);
                    Quaternion previous = Quaternion.identity;
                    var previousGoals = new Quaternion[4];
                    for (int frame = 0; frame <= frames; frame++)
                    {
                        for (int i = 0; i < transforms.Length; i++)
                        {
                            transforms[i].localPosition = positions[i];
                            transforms[i].localRotation = rotations[i];
                            transforms[i].localScale = scales[i];
                        }
                        float time = original.length * frame / frames;
                        original.SampleAnimation(source, time);
                        handler.GetHumanPose(ref pose);
                        var q = pose.bodyRotation;
                        if (frame > 0 && Quaternion.Dot(previous, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        previous = q;
                        float[] root = { pose.bodyPosition.x, pose.bodyPosition.y, pose.bodyPosition.z, q.x, q.y, q.z, q.w };
                        for (int i = 0; i < goalOffset; i++)
                        {
                            float value = i < 7 ? root[i] : pose.muscles[i - 7];
                            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Pose inválida: " + original.name);
                            curves[i].AddKey(time, value);
                        }
                        // Humanoid layers also need the original limb goals. Without
                        // these tracks an upper-body attack can move the planted feet.
                        for (int goal = 0; goal < 4; goal++)
                        {
                            var p = pose.ikGoalPositions[goal];
                            var rotation = pose.internalIkGoalRotations[goal];
                            if (frame > 0 && Quaternion.Dot(previousGoals[goal], rotation) < 0)
                                rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                            previousGoals[goal] = rotation;
                            float[] values = { p.x, p.y, p.z, rotation.x, rotation.y, rotation.z, rotation.w };
                            for (int component = 0; component < 7; component++)
                                curves[goalOffset + goal * 7 + component].AddKey(time, values[component]);
                        }
                    }
                }
                var result = new AnimationClip { name = original.name, frameRate = 60 };
                string[] rootNames = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
                string[] goalNames = { "LeftFoot", "RightFoot", "LeftHand", "RightHand" };
                for (int i = 0; i < curves.Length; i++)
                {
                    var keys = curves[i].keys;
                    if (keys.All(k => Mathf.Abs(k.value - keys[0].value) < .000001f))
                        curves[i] = new AnimationCurve(new Keyframe(0, keys[0].value), new Keyframe(original.length, keys[0].value));
                    for (int k = 0; k < curves[i].length; k++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curves[i], k, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curves[i], k, AnimationUtility.TangentMode.Linear);
                    }
                    string property;
                    if (i < 7) property = rootNames[i];
                    else if (i < goalOffset) property = HumanTrait.MuscleName[i - 7];
                    else
                    {
                        int goal = (i - goalOffset) / 7, component = (i - goalOffset) % 7;
                        property = goalNames[goal] + (component < 3 ? "T." + "xyz"[component] : "Q." + "xyzw"[component - 3]);
                    }
                    AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curves[i]);
                }
                var settings = AnimationUtility.GetAnimationClipSettings(original);
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionY = true;
                settings.keepOriginalPositionXZ = true;
                settings.loopBlendOrientation = true;
                settings.loopBlendPositionY = true;
                settings.loopBlendPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(result, settings);
                AnimationUtility.SetAnimationEvents(result, AnimationUtility.GetAnimationEvents(original));
                return result;
            }
            finally { Object.DestroyImmediate(source); }
        }

        public static string Validate(GameObject model, AnimationClip original, AnimationClip converted)
        {
            if (!converted.humanMotion) throw new InvalidOperationException(converted.name + " no contiene curvas Humanoid.");
            var source = Object.Instantiate(model);
            var target = Object.Instantiate(model);
            source.hideFlags = target.hideFlags = HideFlags.HideAndDontSave;
            Object.DestroyImmediate(source.GetComponent<Animator>());
            var animator = target.GetComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var sourceBones = TrackedBones.Select(n => source.GetComponentsInChildren<Transform>().First(t => t.name == n)).ToArray();
            var targetBones = TrackedBones.Select(n => target.GetComponentsInChildren<Transform>().First(t => t.name == n)).ToArray();
            var graph = PlayableGraph.Create("Validate Humanoid locomotion");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, converted);
                playable.SetApplyFootIK(false);
                var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                float maxError = 0, minHead = float.MaxValue, legTravel = 0;
                var leg = target.GetComponentsInChildren<Transform>().First(t => t.name == "UpperLeg.R");
                var previous = Quaternion.identity;
                for (int sample = 0; sample < 24; sample++)
                {
                    float time = converted.length * sample / 24;
                    original.SampleAnimation(source, time);
                    playable.SetTime(time);
                    graph.Evaluate(0);
                    for (int i = 0; i < sourceBones.Length; i++)
                        maxError = Mathf.Max(maxError, Vector3.Distance(sourceBones[i].position, targetBones[i].position));
                    minHead = Mathf.Min(minHead, targetBones[1].position.y);
                    if (sample > 0) legTravel += Quaternion.Angle(previous, leg.localRotation);
                    previous = leg.localRotation;
                }
                string report = $"{converted.name}: humanMotion={converted.humanMotion}, max pose error={maxError:F4}m, min head height={minHead:F3}m, leg travel={legTravel:F1}deg";
                Debug.Log(report);
                if (maxError > .15f || minHead < .5f || ((original.name.EndsWith("Walk") || original.name.EndsWith("Run")) && legTravel < 30))
                    throw new InvalidOperationException("La conversión no conserva la locomoción: " + report);
                return "PASS " + report;
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        static Motion Replace(Motion motion, Dictionary<AnimationClip, AnimationClip> replacements)
        {
            if (motion is AnimationClip clip && replacements.TryGetValue(clip, out var replacement)) return replacement;
            if (motion is BlendTree tree)
            {
                var children = tree.children;
                for (int i = 0; i < children.Length; i++) children[i].motion = Replace(children[i].motion, replacements);
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }
            return motion;
        }

        public static void EnsureHumanoidMaskSupport(AnimatorController controller)
        {
            if (controller.layers.Length != 1) return;
            // With a single-layer Humanoid controller, Unity's optimized playable
            // evaluation lets an outer masked attack replace the locomotion legs.
            // A zero-weight empty layer retains correct per-body-part evaluation.
            controller.AddLayer("Humanoid Mask Support");
            var layers = controller.layers;
            layers[1].defaultWeight = 0;
            var empty = layers[1].stateMachine.AddState("Empty");
            empty.writeDefaultValues = false;
            layers[1].stateMachine.defaultState = empty;
            controller.layers = layers;
        }

        static void Replace(AnimatorStateMachine machine, Dictionary<AnimationClip, AnimationClip> replacements)
        {
            foreach (var child in machine.states)
            {
                var replacement = Replace(child.state.motion, replacements);
                if (replacement == child.state.motion) continue;
                child.state.motion = replacement;
                EditorUtility.SetDirty(child.state);
            }
            foreach (var child in machine.stateMachines) Replace(child.stateMachine, replacements);
        }

        public static void RunBatch()
        {
            try { Apply(); EditorApplication.Exit(0); }
            catch (Exception e)
            {
                Directory.CreateDirectory("output/humanoid-locomotion");
                File.WriteAllText("output/humanoid-locomotion/FAILED.txt", e.ToString());
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
