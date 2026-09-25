using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Used by the interactive stage, exporter and checks, always on disposable copies.
    public sealed class HumanoidAttackRig : IDisposable
    {
        public GameObject Container { get; private set; }
        public Animator Animator { get; private set; }
        public HumanPoseHandler Handler { get; private set; }
        public HumanoidAttackPose Neutral { get; private set; }
        readonly Scene scene;
        readonly bool ownsScene;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationClip sampledClip;

        public static string ValidateModel(GameObject model)
        {
            if (model == null) return "Elegí un prefab o FBX Humanoid del Project.";
            var animators = model.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1) return "El modelo debe tener exactamente un Animator, también puede estar en un hijo.";
            var avatar = animators[0].avatar;
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                return "El Animator necesita un Avatar Humanoid válido. Configurá el Rig del FBX como Humanoid.";
            if (!animators[0].hasTransformHierarchy)
                return "Desactivá Optimize Game Objects en el FBX para poder editar los huesos.";
            if (model.GetComponentsInChildren<Transform>(true).Any(t => t.localScale.x <= 0 || t.localScale.y <= 0 || t.localScale.z <= 0))
                return "El rig tiene escalas nulas o negativas. Usá un modelo sin reflejos de escala.";
            return null;
        }

        public HumanoidAttackRig(GameObject source, Scene previewScene = default)
        {
            string error = ValidateModel(source);
            if (error != null) throw new InvalidOperationException(error);
            ownsScene = !previewScene.IsValid();
            scene = ownsScene ? EditorSceneManager.NewPreviewScene() : previewScene;
            try
            {
                Container = new GameObject("Vista de ataque (copia)");
                SceneManager.MoveGameObjectToScene(Container, scene);
                Container.SetActive(false);
                var instance = Object.Instantiate(source, Container.transform);
                foreach (var behaviour in instance.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true)) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
                Animator = instance.GetComponentInChildren<Animator>(true);
                for (var t = Animator.transform; t != Container.transform; t = t.parent) t.gameObject.SetActive(true);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Vector3 scale = Animator.transform.lossyScale;
                Animator.transform.localScale = Vector3.Scale(Animator.transform.localScale, new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z));
                Animator.runtimeAnimatorController = null;
                Animator.applyRootMotion = false;
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Animator.fireEvents = false;
                Container.SetActive(true);
                Animator.Rebind();
                Handler = new HumanPoseHandler(Animator.avatar, Animator.transform);
                var pose = new HumanPose();
                Handler.GetHumanPose(ref pose);
                pose.muscles = new float[HumanTrait.MuscleCount];
                pose.bodyRotation = Quaternion.identity;
                Handler.SetHumanPose(ref pose);
                Neutral = Capture("Base", 0);
            }
            catch { Dispose(); throw; }
        }

        public HumanoidAttackPose Capture(string label, float time)
        {
            var pose = new HumanPose();
            Handler.GetHumanPose(ref pose);
            var result = new HumanoidAttackPose { label = label, time = time };
            result.Read(pose);
            return result;
        }

        public void Apply(HumanoidAttackPose pose)
        {
            StopSampling();
            var human = pose.ToHumanPose();
            Handler.SetHumanPose(ref human);
        }

        public void Sample(AnimationClip clip, float time)
        {
            if (clip == null || !clip.humanMotion) throw new InvalidOperationException("Se necesita un clip Humanoid.");
            if (!graph.IsValid() || sampledClip != clip)
            {
                StopSampling();
                Animator.enabled = true;
                graph = PlayableGraph.Create("Humanoid attack preview");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph, "Attack", Animator).SetSourcePlayable(playable);
                sampledClip = clip;
                graph.Play();
            }
            playable.SetTime(Mathf.Clamp(time, 0, clip.length));
            graph.Evaluate(0);
        }

        public void StopSampling()
        {
            if (graph.IsValid()) graph.Destroy();
            sampledClip = null;
            if (Animator != null) Animator.enabled = false;
        }

        public void Dispose()
        {
            StopSampling();
            Handler?.Dispose(); Handler = null;
            if (Container != null) Object.DestroyImmediate(Container);
            Container = null;
            if (ownsScene && scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    public static class HumanoidAttackAuthoring
    {
        public const string ClipFolder = "Assets/Art/Animations/HumanoidAttacks";
        public const string RecipeFolder = "Assets/Data/AnimationAuthoring/HumanoidAttacks";

        public static void Validate(HumanoidAttackRecipe recipe)
        {
            if (recipe == null || !Finite(recipe.duration) || recipe.duration < .05f || recipe.duration > 30)
                throw new InvalidOperationException("La duración debe estar entre 0,05 y 30 segundos.");
            if (recipe.frameRate < 15 || recipe.frameRate > 120) throw new InvalidOperationException("Usá entre 15 y 120 fotogramas por segundo.");
            if (!Finite(recipe.activeStartsAt) || !Finite(recipe.recoveryStartsAt) || recipe.activeStartsAt <= 0 || recipe.recoveryStartsAt <= recipe.activeStartsAt || recipe.recoveryStartsAt >= 1)
                throw new InvalidOperationException("Las fases deben cumplir: 0 < inicio activo < recuperación < 1.");
            if (recipe.poses == null || recipe.poses.Count < 2 || recipe.poses[0].time != 0 || recipe.poses[recipe.poses.Count - 1].time != 1)
                throw new InvalidOperationException("Se necesitan al menos dos poses, una en 0 y otra en 1.");
            float previous = -1;
            foreach (var pose in recipe.poses)
            {
                if (pose == null || !Finite(pose.time) || pose.time <= previous || pose.time > 1)
                    throw new InvalidOperationException("Las poses deben tener tiempos crecientes, sin duplicados.");
                previous = pose.time;
                if (pose.muscles == null || pose.muscles.Length != HumanTrait.MuscleCount || pose.muscles.Any(m => !Finite(m)) ||
                    !Finite(pose.bodyPosition.x) || !Finite(pose.bodyPosition.y) || !Finite(pose.bodyPosition.z) ||
                    !Finite(pose.bodyRotation.x) || !Finite(pose.bodyRotation.y) || !Finite(pose.bodyRotation.z) || !Finite(pose.bodyRotation.w) ||
                    Quaternion.Dot(pose.bodyRotation, pose.bodyRotation) < .001f)
                    throw new InvalidOperationException("La pose '" + pose.label + "' contiene valores inválidos.");
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static string ValidateImportClip(AnimationClip clip)
        {
            if (clip == null) return "Elegí un clip Humanoid para importar.";
            if (!clip.humanMotion) return "Ese clip es Generic. Elegí una animación Humanoid.";
            if (!Finite(clip.length) || clip.length < .05f || clip.length > 30)
                return "El taller admite clips completos de 0,05 a 30 segundos.";
            return null;
        }

        public static void ImportClip(HumanoidAttackRecipe recipe, AnimationClip source, int frameRate)
        {
            string invalid = ValidateImportClip(source);
            if (invalid != null) throw new InvalidOperationException(invalid);
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (frameRate < 15 || frameRate > 120) throw new InvalidOperationException("Usá entre 15 y 120 fotogramas por segundo.");
            var sampled = Object.Instantiate(source);
            var imported = Object.Instantiate(recipe);
            sampled.hideFlags = imported.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                // Sample a disposable copy: retain body displacement in the poses and
                // read the last frame without wrapping back to the start of a loop.
                var settings = AnimationUtility.GetAnimationClipSettings(sampled);
                settings.loopTime = false;
                settings.loopBlendOrientation = settings.loopBlendPositionY = settings.loopBlendPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(sampled, settings);
                sampled.wrapMode = WrapMode.ClampForever;
                imported.clipName = source.name + "_Editable";
                imported.duration = source.length;
                imported.frameRate = frameRate;
                imported.poses = new List<HumanoidAttackPose>();
                int frames = Mathf.CeilToInt(source.length * frameRate);
                using (var rig = new HumanoidAttackRig(recipe.model))
                {
                    for (int frame = 0; frame <= frames; frame++)
                    {
                        float seconds = frame == frames ? source.length : frame / (float)frameRate;
                        rig.Sample(sampled, seconds);
                        var pose = rig.Capture("Fotograma " + frame, seconds / source.length);
                        pose.blend = AttackPoseBlend.Linear;
                        imported.poses.Add(pose);
                    }
                }
                Validate(imported);
                // Commit only once all frames are valid. A failed import leaves the draft intact.
                Undo.RegisterCompleteObjectUndo(recipe, "Importar clip Humanoid completo");
                recipe.clipName = imported.clipName; recipe.duration = imported.duration;
                recipe.frameRate = imported.frameRate; recipe.poses = imported.poses;
            }
            finally { Object.DestroyImmediate(sampled); Object.DestroyImmediate(imported); }
        }

        public static HumanoidAttackPose Evaluate(HumanoidAttackRecipe recipe, float time)
        {
            time = Mathf.Clamp01(time);
            int right = 1;
            while (right < recipe.poses.Count - 1 && recipe.poses[right].time < time) right++;
            var a = recipe.poses[right - 1]; var b = recipe.poses[right];
            float t = Mathf.InverseLerp(a.time, b.time, time);
            if (a.blend == AttackPoseBlend.Smooth) t = t * t * (3 - 2 * t);
            var result = a.Copy(time);
            result.bodyPosition = Vector3.Lerp(a.bodyPosition, b.bodyPosition, t);
            result.bodyRotation = Quaternion.Slerp(a.bodyRotation, b.bodyRotation, t);
            for (int i = 0; i < result.muscles.Length; i++) result.muscles[i] = Mathf.Lerp(a.muscles[i], b.muscles[i], t);
            return result;
        }

        public static AnimationClip Bake(HumanoidAttackRecipe recipe)
        {
            Validate(recipe);
            using (var rig = new HumanoidAttackRig(recipe.model)) return Bake(recipe, rig);
        }

        public static AnimationClip Bake(HumanoidAttackRecipe recipe, HumanoidAttackRig rig)
        {
            Validate(recipe);
            int goalOffset = 7 + HumanTrait.MuscleCount;
            var curves = Enumerable.Range(0, goalOffset + 28).Select(_ => new AnimationCurve()).ToArray();
            int frames = Mathf.CeilToInt(recipe.duration * recipe.frameRate);
            var times = new SortedSet<float>(recipe.poses.Select(p => p.time));
            for (int i = 0; i <= frames; i++) times.Add((float)i / frames);
            var previous = new Quaternion[5];
            var human = new HumanPose();
            foreach (float t in times)
            {
                rig.Apply(Evaluate(recipe, t));
                rig.Handler.GetHumanPose(ref human);
                float seconds = t * recipe.duration;
                AddTransform(curves, 0, seconds, human.bodyPosition, Continuous(human.bodyRotation, ref previous[0]));
                for (int m = 0; m < human.muscles.Length; m++) curves[7 + m].AddKey(seconds, human.muscles[m]);
                for (int goal = 0; goal < 4; goal++)
                    AddTransform(curves, goalOffset + goal * 7, seconds, human.ikGoalPositions[goal], Continuous(human.internalIkGoalRotations[goal], ref previous[goal + 1]));
            }
            var clip = new AnimationClip { name = recipe.clipName, frameRate = recipe.frameRate, wrapMode = WrapMode.Once };
            try
            {
                var bindings = new EditorCurveBinding[curves.Length];
                string[] limbs = { "LeftFoot", "RightFoot", "LeftHand", "RightHand" };
                for (int i = 0; i < curves.Length; i++)
                {
                    for (int k = 0; k < curves[i].length; k++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curves[i], k, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curves[i], k, AnimationUtility.TangentMode.Linear);
                    }
                    string property = i < 7 ? TransformProperty("Root", i) : i < goalOffset ? HumanTrait.MuscleName[i - 7] : TransformProperty(limbs[(i - goalOffset) / 7], (i - goalOffset) % 7);
                    bindings[i] = EditorCurveBinding.FloatCurve("", typeof(Animator), property);
                }
                AnimationUtility.SetEditorCurves(clip, bindings, curves);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0; settings.stopTime = recipe.duration;
                settings.loopTime = false; settings.loopBlend = false;
                settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true;
                settings.loopBlendOrientation = true; settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                if (!clip.humanMotion) throw new InvalidOperationException("Unity no reconoció las curvas como Humanoid.");
                return clip;
            }
            catch { Object.DestroyImmediate(clip); throw; }
        }

        static string TransformProperty(string prefix, int component) => prefix + (component < 3 ? "T." + "xyz"[component] : "Q." + "xyzw"[component - 3]);
        static Quaternion Continuous(Quaternion value, ref Quaternion previous)
        {
            if (Quaternion.Dot(previous, value) < 0) value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
            previous = value; return value;
        }
        static void AddTransform(AnimationCurve[] curves, int offset, float time, Vector3 p, Quaternion q)
        {
            float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
            for (int i = 0; i < values.Length; i++)
            {
                if (!Finite(values[i])) throw new InvalidOperationException("El Avatar produjo una pose inválida.");
                curves[offset + i].AddKey(time, values[i]);
            }
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        public static void CreateTemplate(HumanoidAttackRecipe recipe, HumanoidAttackPose neutral, HumanoidAttackTemplate template, bool leftHanded)
        {
            var guard = neutral.Copy(0, "Guardia");
            Set(guard, "Left Arm Down-Up", -.65f); Set(guard, "Right Arm Down-Up", -.55f);
            Set(guard, "Left Forearm Stretch", -.35f); Set(guard, "Right Forearm Stretch", -.4f);
            Set(guard, "Left Arm Front-Back", .15f); Set(guard, "Right Arm Front-Back", .2f);
            var windup = guard.Copy(.30f, "Preparación");
            var hit = guard.Copy(.46f, "Impacto");
            var follow = guard.Copy(.64f, "Salida del golpe");
            if (template != HumanoidAttackTemplate.Blank)
            {
                Set(windup, "Chest Twist Left-Right", -.3f); Set(hit, "Chest Twist Left-Right", .25f); Set(follow, "Chest Twist Left-Right", .4f);
                if (template == HumanoidAttackTemplate.HorizontalSlash)
                {
                    Set(windup, "Right Arm Down-Up", -.05f); Set(windup, "Right Arm Front-Back", -.55f);
                    Set(hit, "Right Arm Down-Up", -.2f); Set(hit, "Right Arm Front-Back", .7f); Set(hit, "Right Forearm Stretch", .7f);
                    Set(follow, "Right Arm Down-Up", -.4f); Set(follow, "Right Arm Front-Back", .8f); Set(follow, "Right Forearm Stretch", .3f);
                }
                else if (template == HumanoidAttackTemplate.Overhead)
                {
                    Set(windup, "Right Arm Down-Up", .7f); Set(windup, "Right Arm Front-Back", .15f); Set(windup, "Right Forearm Stretch", -.6f);
                    Set(hit, "Right Arm Down-Up", -.1f); Set(hit, "Right Arm Front-Back", .8f); Set(hit, "Right Forearm Stretch", .7f);
                    Set(follow, "Right Arm Down-Up", -.65f); Set(follow, "Right Arm Front-Back", .6f); Set(follow, "Spine Front-Back", .2f);
                }
                else
                {
                    Set(windup, "Right Arm Front-Back", -.15f); Set(windup, "Right Forearm Stretch", -.8f);
                    Set(hit, "Right Arm Down-Up", -.1f); Set(hit, "Right Arm Front-Back", .9f); Set(hit, "Right Forearm Stretch", .85f);
                    Set(follow, "Right Arm Down-Up", -.15f); Set(follow, "Right Arm Front-Back", .75f); Set(follow, "Right Forearm Stretch", .5f);
                    if (template == HumanoidAttackTemplate.Punch)
                        foreach (var p in new[] { guard, windup, hit, follow })
                            for (int i = 0; i < HumanTrait.MuscleCount; i++)
                                if (HumanTrait.MuscleName[i].StartsWith("Right ", StringComparison.Ordinal) && HumanTrait.MuscleName[i].EndsWith("Stretched", StringComparison.Ordinal)) p.muscles[i] = -.65f;
                }
            }
            // Cross the fast part of the strike without easing to a stop at contact.
            windup.blend = hit.blend = AttackPoseBlend.Linear;
            recipe.poses = new List<HumanoidAttackPose> { guard, windup, hit, follow, guard.Copy(1, "Recuperación") };
            recipe.activeStartsAt = .36f; recipe.recoveryStartsAt = .64f;
            if (leftHanded) foreach (var pose in recipe.poses) Mirror(pose);
        }

        static void Set(HumanoidAttackPose pose, string muscle, float value)
        {
            int index = Array.IndexOf(HumanTrait.MuscleName, muscle);
            if (index < 0) throw new InvalidOperationException("Músculo desconocido: " + muscle);
            pose.muscles[index] = value;
        }

        public static void Mirror(HumanoidAttackPose pose)
        {
            var source = (float[])pose.muscles.Clone();
            var names = HumanTrait.MuscleName;
            for (int i = 0; i < names.Length; i++)
            {
                string other = names[i].StartsWith("Left ", StringComparison.Ordinal) ? "Right " + names[i].Substring(5) :
                    names[i].StartsWith("Right ", StringComparison.Ordinal) ? "Left " + names[i].Substring(6) : names[i];
                int index = Array.IndexOf(names, other);
                pose.muscles[i] = source[index];
                if (other == names[i] && names[i].EndsWith("Left-Right", StringComparison.Ordinal)) pose.muscles[i] *= -1;
            }
            pose.bodyPosition.x = -pose.bodyPosition.x;
            var q = pose.bodyRotation; pose.bodyRotation = new Quaternion(q.x, -q.y, -q.z, q.w);
        }
    }
}
