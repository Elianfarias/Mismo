using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Splits the single-press Combo furioso clip into one combo clip per recast stage. Each slice is remapped through
    // the old preparation/active/recovery timing, so playing it linearly over its press looks exactly like before.
    public static class OneHandAxeAttackAuthoring
    {
        public const string Folder = "Assets/Art/Animations/WeaponCombat/OneHandAxe";
        public const string Source = Folder + "/Combat_Axe_Triple_Attack.anim";
        public const string AbilityPath = "Assets/Data/Weapons/Axe/AxeFuriousCombo.asset";
        public const string BindingPath = "Assets/Data/WeaponFamilies/OneHandAxeAnimations.asset";
        const string AbilityId = "AxeFuriousCombo";
        // Same order as the recast stages of AxeFuriousCombo.
        public static readonly string[] Names = { "Combat_Axe_Triple_Attack_Opener", "Combat_Axe_Triple_Attack_Followup", "Combat_Axe_Triple_Attack_Finisher" };
        // The single execution the source clip was tuned for, and its phase markers inside the clip.
        const float OldPreparation = .46f, OldActive = 1.94f, OldRecovery = .78f, ActiveStartsAt = .1966f, RecoveryStartsAt = .6667f;
        // The slices keep the original rhythm whatever the Inspector timing: each press stretches its slice over its own duration.
        public static readonly float[] SliceSeconds = { .9015f, .7943f, 1.4842f };
        // Stages after a pause blend from idle into a wound-up pose; back-to-back stages cut (PlayerAnimationDriver).
        const float BlendSeconds = .2f;

        [MenuItem("Mismo/Animaciones/Dividir y asignar Combo furioso (Hacha)")]
        public static void CreateAndAssign() { Generate(false); VerifySlices(); Assign(); Debug.Log("ONE_HAND_AXE_SPLIT_PASS"); }

        // Rewrites existing slices in place (same GUIDs), e.g. after retiming the stages.
        [MenuItem("Mismo/Animaciones/Regenerar cortes del Combo furioso (Hacha)")]
        public static void Regenerate() { Generate(true); VerifySlices(); Assign(); Debug.Log("ONE_HAND_AXE_SPLIT_PASS"); }

        public static void GenerateBatch()
        {
            try { CreateAndAssign(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static AbilityDefinition Ability()
        {
            var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AbilityPath);
            if (ability == null || ability.Id != AbilityId || ability.RecastCount != Names.Length)
                throw new Exception("Combo furioso debe tener " + Names.Length + " etapas de reactivación: " + AbilityPath);
            return ability;
        }

        static AnimationClip SourceClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Source);
            if (clip == null || !clip.humanMotion || Mathf.Abs(clip.length - 4.2f) > .01f) throw new Exception("Falta el clip Humanoid original de 4,2 s: " + Source);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length == 0 || bindings.Any(b => b.type != typeof(Animator) || b.path != ""))
                throw new Exception("El clip original debe tener sólo curvas Humanoid del Animator.");
            return clip;
        }

        // Old gameplay time → source clip time, piecewise linear through the phase markers; GameTime is its inverse.
        public static float ClipTime(float time, float length)
        {
            float active = ActiveStartsAt * length, recovery = RecoveryStartsAt * length;
            if (time <= OldPreparation) return time / OldPreparation * active;
            if (time <= OldPreparation + OldActive) return active + (time - OldPreparation) / OldActive * (recovery - active);
            return Mathf.Min(length, recovery + (time - OldPreparation - OldActive) / OldRecovery * (length - recovery));
        }
        static float GameTime(float clipTime, float length)
        {
            float active = ActiveStartsAt * length, recovery = RecoveryStartsAt * length;
            if (clipTime <= active) return clipTime / active * OldPreparation;
            if (clipTime <= recovery) return OldPreparation + (clipTime - active) / (recovery - active) * OldActive;
            return OldPreparation + OldActive + (clipTime - recovery) / (length - recovery) * OldRecovery;
        }
        public static float StageStart(AbilityDefinition ability, int stage)
        {
            float start = 0;
            for (int i = 0; i < stage; i++) start += ability.recastStages[i].Duration;
            return start;
        }
        static float SliceStart(int stage)
        {
            float start = 0;
            for (int i = 0; i < stage; i++) start += SliceSeconds[i];
            return start;
        }

        // The source curves are linear, so keys at the remapped source keys and at the phase breakpoints reproduce them exactly.
        static AnimationCurve Slice(AnimationCurve source, float start, float duration, float length)
        {
            var times = new List<float>();
            foreach (float breakpoint in new[] { OldPreparation, OldPreparation + OldActive }) times.Add(breakpoint - start);
            float from = ClipTime(start, length), to = ClipTime(start + duration, length);
            foreach (var key in source.keys) if (key.time > from && key.time < to) times.Add(GameTime(key.time, length) - start);
            times.RemoveAll(t => t < 1e-5f || t > duration - 1e-5f);
            times.Add(0); times.Add(duration); times.Sort();
            var keys = new List<Keyframe>();
            foreach (float t in times)
                if (keys.Count == 0 || t - keys[keys.Count - 1].time >= 1e-5f) keys.Add(new Keyframe(t, source.Evaluate(ClipTime(start + t, length))));
            // The constructor keeps every key; AddKey would silently drop near-duplicates.
            var curve = new AnimationCurve(keys.ToArray());
            for (int k = 0; k < curve.length; k++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        // Rewrites only the finisher (same GUID): the opener and follow-up stay as they are.
        [MenuItem("Mismo/Animaciones/Corregir remate del Combo furioso (Hacha)")]
        public static void CorrectFinisherClip() { Generate(true, Names.Length - 1); VerifySlices(); CheckFinisher(); }

        public static void CorrectFinisherBatch()
        {
            try { CorrectFinisherClip(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static AnimationClip NewSlice(AnimationClip source, string name, EditorCurveBinding[] bindings, AnimationCurve[] curves, float duration)
        {
            var clip = new AnimationClip { name = name, frameRate = source.frameRate, wrapMode = source.wrapMode };
            AnimationUtility.SetEditorCurves(clip, bindings, curves);
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.startTime = 0; settings.stopTime = duration; settings.loopTime = false; settings.loopBlend = false;
            // Root stays baked into the pose: stages 2 and 3 must keep the twisted torso where the previous slice ended.
            settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
            settings.loopBlendOrientation = settings.loopBlendPositionY = settings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        static void Generate(bool overwrite, params int[] only)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
            Ability(); var source = SourceClip();
            HumanoidAttackAuthoring.EnsureFolder(Folder);
            var bindings = AnimationUtility.GetCurveBindings(source);
            var curves = bindings.Select(b => AnimationUtility.GetEditorCurve(source, b)).ToArray();
            for (int stage = 0; stage < Names.Length; stage++)
            {
                if (only.Length > 0 && !only.Contains(stage)) continue;
                string path = Folder + "/" + Names[stage] + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing != null && !overwrite) continue;
                float start = SliceStart(stage), duration = SliceSeconds[stage];
                var sliced = curves.Select(c => Slice(c, start, duration, source.length)).ToArray();
                if (stage == Names.Length - 1) sliced = CorrectFinisher(source, bindings, sliced, duration);
                var clip = NewSlice(source, Names[stage], bindings, sliced, duration);
                if (!clip.humanMotion) { Object.DestroyImmediate(clip); throw new Exception("Unity no reconoció el corte como Humanoid: " + Names[stage]); }
                if (existing == null) AssetDatabase.CreateAsset(clip, path);
                else { EditorUtility.CopySerialized(clip, existing); existing.name = Names[stage]; Object.DestroyImmediate(clip); EditorUtility.SetDirty(existing); }
            }
            AssetDatabase.SaveAssets();
        }

        // Curve maths only: each slice matches the source at the old rhythm and consecutive slices meet without a jump.
        // The finisher's root is turned on purpose (CorrectFinisher), so only its muscles are compared.
        public static void VerifySlices()
        {
            Ability(); var source = SourceClip();
            var bindings = AnimationUtility.GetCurveBindings(source);
            var clips = Names.Select(n => AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + n + ".anim")).ToArray();
            for (int stage = 0; stage < clips.Length; stage++)
            {
                var clip = clips[stage];
                float start = SliceStart(stage), duration = SliceSeconds[stage];
                if (clip == null || !clip.humanMotion) throw new Exception("Falta el corte Humanoid " + Names[stage]);
                if (Mathf.Abs(clip.length - duration) > .001f) throw new Exception(Names[stage] + " dura " + clip.length + " s y su etapa " + duration + " s: regenerar los cortes.");
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (settings.loopTime || !settings.keepOriginalOrientation || !settings.keepOriginalPositionY || !settings.keepOriginalPositionXZ)
                    throw new Exception(Names[stage] + " debe conservar la raíz horneada en la pose y no repetirse.");
                foreach (var binding in bindings)
                {
                    var original = AnimationUtility.GetEditorCurve(source, binding);
                    var slice = AnimationUtility.GetEditorCurve(clip, binding) ?? throw new Exception(Names[stage] + " no tiene la curva " + binding.propertyName);
                    bool turned = stage == Names.Length - 1 && IsRoot(binding);
                    for (int i = 0; i <= 200 && !turned; i++)
                    {
                        float t = duration * i / 200;
                        float error = Mathf.Abs(slice.Evaluate(t) - original.Evaluate(ClipTime(start + t, source.length)));
                        if (error > 1e-4f) throw new Exception(Names[stage] + " se aparta del original en " + binding.propertyName + " a " + t + " s (" + error + ")");
                    }
                    if (stage > 0)
                    {
                        var previous = AnimationUtility.GetEditorCurve(clips[stage - 1], binding);
                        float jump = Mathf.Abs(previous.Evaluate(clips[stage - 1].length) - slice.Evaluate(0));
                        if (jump > 1e-5f) throw new Exception("Salto entre " + Names[stage - 1] + " y " + Names[stage] + " en " + binding.propertyName + " (" + jump + ")");
                    }
                }
            }
        }

        // ---- Finisher: the source turns the whole body ~90° to one side at the hit, so the axe lands sideways. ----
        // The root (RootQ, RootT) is turned about the vertical so the axe head lands straight ahead at contact. The turn fades
        // in from the follow-up's last pose and out before idle, so both joins stay exact; the muscles are untouched.
        const float FinisherTolerance = 10, MaxFinisherYaw = 75, ContactFrom = .25f, ContactTo = .95f, RampIn = .08f, Hold = .2f, ReleaseAt = 1.25f;
        const string SkinPath = "Assets/Art/Prefabs/Player/Skins/WarriorSkin.prefab", AxePosePath = "Assets/Data/Weapons/Axe/AxePose.asset";
        static readonly string[] RootNames = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };

        static bool IsRoot(EditorCurveBinding binding) => binding.propertyName.StartsWith("Root", StringComparison.Ordinal);

        public struct AxeSample { public float time, headAngle, headHeight, bodyYaw, roll; }

        static AnimationCurve[] CorrectFinisher(AnimationClip source, EditorCurveBinding[] bindings, AnimationCurve[] curves, float duration)
        {
            var probe = NewSlice(source, "Finisher probe", bindings, curves, duration);
            AxeSample contact;
            try { contact = Contact(Measure(probe)); }
            finally { Object.DestroyImmediate(probe); }
            float turn = -contact.headAngle;
            int[] root = RootNames.Select(n => Array.FindIndex(bindings, b => b.propertyName == n)).ToArray();
            if (root.Any(i => i < 0)) throw new Exception("El remate no tiene las curvas de raíz.");
            var times = new SortedSet<float>();
            foreach (int i in root) foreach (var key in curves[i].keys) times.Add(key.time);
            for (int f = 0; f <= Mathf.CeilToInt(duration * 60); f++) times.Add(Mathf.Min(duration, f / 60f));
            foreach (float t in new[] { contact.time - RampIn, contact.time + Hold, ReleaseAt }) if (t > 0 && t < duration) times.Add(t);
            var turned = curves.ToArray();
            var keys = RootNames.Select(_ => new List<Keyframe>()).ToArray();
            Quaternion previous = Quaternion.identity;
            foreach (float t in times)
            {
                var spin = Quaternion.AngleAxis(turn * FinisherWeight(t, contact.time), Vector3.up);
                var position = spin * new Vector3(curves[root[0]].Evaluate(t), curves[root[1]].Evaluate(t), curves[root[2]].Evaluate(t));
                var rotation = spin * new Quaternion(curves[root[3]].Evaluate(t), curves[root[4]].Evaluate(t), curves[root[5]].Evaluate(t), curves[root[6]].Evaluate(t)).normalized;
                if (keys[0].Count > 0 && Quaternion.Dot(previous, rotation) < 0) rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                previous = rotation;
                float[] values = { position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w };
                for (int c = 0; c < values.Length; c++) keys[c].Add(new Keyframe(t, values[c]));
            }
            for (int c = 0; c < root.Length; c++)
            {
                var curve = new AnimationCurve(keys[c].ToArray());
                for (int k = 0; k < curve.length; k++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                }
                turned[root[c]] = curve;
            }
            Debug.Log("ONE_HAND_AXE_FINISHER turned " + turn.ToString("0.0") + "° (axe head at " + contact.headAngle.ToString("0.0") + "° at " + contact.time.ToString("0.000") + " s)");
            return turned;
        }
        // 0 at the start (the follow-up's last pose), 1 just before contact through the follow-through, 0 again before idle.
        static float FinisherWeight(float t, float contact)
        {
            float full = contact - RampIn, release = contact + Hold;
            if (t <= full) return Mathf.SmoothStep(0, 1, t / full);
            if (t <= release) return 1;
            return 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - release) / (ReleaseAt - release)));
        }

        // Samples a slice on a player skin with the axe as equipped: head angle around the character (0 = straight ahead,
        // positive = right), head height, body yaw and the blade's accumulated roll about its handle.
        public static List<AxeSample> Measure(AnimationClip clip)
        {
            var pose = AssetDatabase.LoadAssetAtPath<WeaponPoseProfile>(AxePosePath) ?? throw new Exception("Falta " + AxePosePath);
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SkinPath) ?? throw new Exception("Falta " + SkinPath));
            actor.hideFlags = HideFlags.HideAndDontSave;
            var rootCurves = RootNames.Skip(3).Select(n => AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), n))).ToArray();
            var samples = new List<AxeSample>();
            bool started = !AnimationMode.InAnimationMode();
            try
            {
                var animator = actor.GetComponentInChildren<Animator>(); animator.Rebind();
                if (started) AnimationMode.StartAnimationMode();
                var root = animator.transform; var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Vector3 previousEdge = Vector3.zero; float roll = 0;
                int frames = Mathf.CeilToInt(clip.length * 60);
                for (int f = 0; f <= frames; f++)
                {
                    float t = clip.length * f / frames;
                    AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(animator.gameObject, clip, t); AnimationMode.EndSampling();
                    Quaternion rotation = hand.rotation * Quaternion.Euler(pose.equipped.rotation);
                    Vector3 head = hand.position + hand.rotation * pose.equipped.offset + rotation * (pose.trailTip * pose.equipped.scale);
                    Vector3 local = root.InverseTransformPoint(head);
                    Vector3 handle = (rotation * (pose.trailTip - pose.trailBase)).normalized;
                    Vector3 edge = Vector3.ProjectOnPlane(rotation * Vector3.right, handle).normalized;
                    if (f > 0) roll += Vector3.SignedAngle(Vector3.ProjectOnPlane(previousEdge, handle), edge, handle);
                    previousEdge = edge;
                    var body = new Quaternion(rootCurves[0].Evaluate(t), rootCurves[1].Evaluate(t), rootCurves[2].Evaluate(t), rootCurves[3].Evaluate(t)).normalized;
                    Vector3 facing = body * Vector3.forward;
                    samples.Add(new AxeSample { time = t, headAngle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, headHeight = local.y,
                        bodyYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, roll = roll });
                }
            }
            finally { if (started && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); Object.DestroyImmediate(actor); }
            return samples;
        }
        // The axe lands where its head is lowest during the swing.
        static AxeSample Contact(List<AxeSample> samples)
        {
            var swing = samples.Where(s => s.time >= ContactFrom && s.time <= ContactTo).ToList();
            if (swing.Count == 0) throw new Exception("El remate es más corto que su ventana de golpe.");
            return swing.OrderBy(s => s.headHeight).First();
        }
        static float RollAround(List<AxeSample> samples, float time, float window)
        {
            var near = samples.Where(s => Mathf.Abs(s.time - time) <= window).ToList();
            return near.Max(s => s.roll) - near.Min(s => s.roll);
        }
        static void Report(string label, List<AxeSample> samples)
        {
            var contact = Contact(samples);
            Debug.Log("ONE_HAND_AXE_MEASURE " + label + ": contact " + contact.time.ToString("0.000") + " s, head " + contact.headAngle.ToString("0.0") + "°, height "
                + contact.headHeight.ToString("0.00") + " m, body yaw " + samples.Min(s => s.bodyYaw).ToString("0") + "°.." + samples.Max(s => s.bodyYaw).ToString("0")
                + "°, blade roll ±0.15 s " + RollAround(samples, contact.time, .15f).ToString("0") + "°");
            for (int i = 0; i < samples.Count; i += 6)
                Debug.Log("ONE_HAND_AXE_MEASURE " + label + " t=" + samples[i].time.ToString("0.000") + " head " + samples[i].headAngle.ToString("0") + "° h "
                    + samples[i].headHeight.ToString("0.00") + " yaw " + samples[i].bodyYaw.ToString("0") + "° roll " + samples[i].roll.ToString("0") + "°");
        }

        // Logs the three slices and the uncorrected finisher, without writing anything.
        public static void MeasureBatch()
        {
            try
            {
                var source = SourceClip(); var bindings = AnimationUtility.GetCurveBindings(source);
                var curves = bindings.Select(b => AnimationUtility.GetEditorCurve(source, b)).ToArray();
                int last = Names.Length - 1;
                var raw = NewSlice(source, "Raw finisher", bindings, curves.Select(c => Slice(c, SliceStart(last), SliceSeconds[last], source.length)).ToArray(), SliceSeconds[last]);
                Report("raw finisher", Measure(raw)); Object.DestroyImmediate(raw);
                foreach (var name in Names) Report(name, Measure(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + name + ".anim")));
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // The corrected finisher lands ahead, turns less and still ends exactly on the original last pose (idle).
        public static void CheckFinisher()
        {
            int last = Names.Length - 1;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + Names[last] + ".anim") ?? throw new Exception("Falta " + Names[last]);
            var samples = Measure(clip); var contact = Contact(samples);
            Report(Names[last], samples);
            if (Mathf.Abs(contact.headAngle) > FinisherTolerance) throw new Exception("El remate pega a " + contact.headAngle.ToString("0.0") + "° del frente (máximo " + FinisherTolerance + "°).");
            float yaw = samples.Max(s => Mathf.Abs(s.bodyYaw));
            if (yaw > MaxFinisherYaw) throw new Exception("El remate todavía gira el cuerpo " + yaw.ToString("0") + "° (máximo " + MaxFinisherYaw + "°).");
            var source = SourceClip(); float end = ClipTime(SliceStart(last) + SliceSeconds[last], source.length);
            foreach (var name in RootNames)
            {
                var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), name);
                float jump = Mathf.Abs(AnimationUtility.GetEditorCurve(clip, binding).Evaluate(clip.length) - AnimationUtility.GetEditorCurve(source, binding).Evaluate(end));
                if (jump > 1e-4f) throw new Exception("El remate corregido no termina en la pose original (" + name + ", " + jump + ").");
            }
            Debug.Log("ONE_HAND_AXE_FINISHER_PASS head " + contact.headAngle.ToString("0.0") + "°, body yaw within " + yaw.ToString("0") + "°");
        }

        public static void Assign()
        {
            var asset = AssetDatabase.LoadAssetAtPath<WeaponAnimationSet>(BindingPath);
            if (asset == null) throw new Exception("Falta OneHandAxeAnimations: " + BindingPath);
            var clips = Names.Select(n => AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + n + ".anim")).ToArray();
            if (clips.Any(c => c == null || !c.humanMotion)) throw new Exception("Generar los tres cortes Humanoid antes de asignar.");
            var data = new SerializedObject(asset); data.Update();
            var binding = FindBinding(data) ?? throw new Exception("Falta el binding de " + AbilityId);
            Undo.RecordObject(asset, "Asignar cortes del Combo furioso");
            // The full clip stays as reference; recast stages only sample the combo clips.
            var combo = binding.FindPropertyRelative("comboClips"); combo.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) combo.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            // The blend is Inspector tuning: only a fresh binding gets the default.
            var blend = binding.FindPropertyRelative("blendSeconds");
            if (blend.floatValue <= 0) blend.floatValue = BlendSeconds;
            data.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(asset);
        }

        public static SerializedProperty FindBinding(SerializedObject data)
        {
            var actions = data.FindProperty("actions");
            for (int i = 0; i < actions.arraySize; i++)
            {
                var action = actions.GetArrayElementAtIndex(i);
                if (action.FindPropertyRelative("ability").objectReferenceValue is AbilityDefinition ability && ability.Id == AbilityId) return action;
            }
            return null;
        }
    }
}
