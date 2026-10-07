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
            float total = ability.recastStages.Sum(s => s.Duration);
            if (Mathf.Abs(total - (OldPreparation + OldActive + OldRecovery)) > .002f)
                throw new Exception("Las etapas deben sumar " + (OldPreparation + OldActive + OldRecovery) + " s, el ritmo del clip original; suman " + total);
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

        static void Generate(bool overwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
            var ability = Ability(); var source = SourceClip();
            HumanoidAttackAuthoring.EnsureFolder(Folder);
            var bindings = AnimationUtility.GetCurveBindings(source);
            var curves = bindings.Select(b => AnimationUtility.GetEditorCurve(source, b)).ToArray();
            for (int stage = 0; stage < Names.Length; stage++)
            {
                string path = Folder + "/" + Names[stage] + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing != null && !overwrite) continue;
                float start = StageStart(ability, stage), duration = ability.recastStages[stage].Duration;
                var clip = new AnimationClip { name = Names[stage], frameRate = source.frameRate, wrapMode = source.wrapMode };
                AnimationUtility.SetEditorCurves(clip, bindings, curves.Select(c => Slice(c, start, duration, source.length)).ToArray());
                var settings = AnimationUtility.GetAnimationClipSettings(source);
                settings.startTime = 0; settings.stopTime = duration; settings.loopTime = false; settings.loopBlend = false;
                // Root stays baked into the pose: stages 2 and 3 must keep the twisted torso where the previous slice ended.
                settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
                settings.loopBlendOrientation = settings.loopBlendPositionY = settings.loopBlendPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                if (!clip.humanMotion) { Object.DestroyImmediate(clip); throw new Exception("Unity no reconoció el corte como Humanoid: " + Names[stage]); }
                if (existing == null) AssetDatabase.CreateAsset(clip, path);
                else { EditorUtility.CopySerialized(clip, existing); existing.name = Names[stage]; Object.DestroyImmediate(clip); EditorUtility.SetDirty(existing); }
            }
            AssetDatabase.SaveAssets();
        }

        // Curve maths only: each slice matches the source at the old rhythm and consecutive slices meet without a jump.
        public static void VerifySlices()
        {
            var ability = Ability(); var source = SourceClip();
            var bindings = AnimationUtility.GetCurveBindings(source);
            var clips = Names.Select(n => AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + n + ".anim")).ToArray();
            for (int stage = 0; stage < clips.Length; stage++)
            {
                var clip = clips[stage];
                float start = StageStart(ability, stage), duration = ability.recastStages[stage].Duration;
                if (clip == null || !clip.humanMotion) throw new Exception("Falta el corte Humanoid " + Names[stage]);
                if (Mathf.Abs(clip.length - duration) > .001f) throw new Exception(Names[stage] + " dura " + clip.length + " s y su etapa " + duration + " s: regenerar los cortes.");
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (settings.loopTime || !settings.keepOriginalOrientation || !settings.keepOriginalPositionY || !settings.keepOriginalPositionXZ)
                    throw new Exception(Names[stage] + " debe conservar la raíz horneada en la pose y no repetirse.");
                foreach (var binding in bindings)
                {
                    var original = AnimationUtility.GetEditorCurve(source, binding);
                    var slice = AnimationUtility.GetEditorCurve(clip, binding) ?? throw new Exception(Names[stage] + " no tiene la curva " + binding.propertyName);
                    for (int i = 0; i <= 200; i++)
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
            binding.FindPropertyRelative("blendSeconds").floatValue = BlendSeconds;
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
