using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class HumanoidMaskDirectionChecks
    {
        const string Output = "output/mask-direction";
        static readonly HumanBodyBones[] Lower = { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
        static readonly HumanBodyBones[] Upper = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
        static readonly List<string> Report = new List<string>();

        [MenuItem("Mismo/Animaciones/Verificar dirección de ataques con máscara")]
        public static void Run()
        {
            Directory.CreateDirectory(Output); Report.Clear();
            try
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusHumanoidLocomotion.ModelPath);
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(QuaterniusHumanoidLocomotion.ControllerPath);
                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Art/Animations/CombatPresentation/PlayerUpperBody.mask");
                // Resolve by GUID so moving source clips does not invalidate these checks.
                var clips = new[] { "a3b3ff94a9d28c24b909c1a18fab281c", "981e926b82dcefb4bbe6507cef4ca850", "a4c2d5831275bfa46828c2ae2e4c7558" }
                    .Select(guid => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview", StringComparison.Ordinal))).ToArray();
                Check(model != null && controller != null && mask != null, "Modelo, controlador, máscara y tres clips reales disponibles");
                string maskBefore = EditorJsonUtility.ToJson(mask);
                foreach (var clip in clips)
                foreach (float yaw in new[] { 0f, 90f })
                foreach (float speed in new[] { 0f, 1f })
                    CheckClip(model, controller, mask, clip, yaw, speed);
                CheckTransitions(model, controller, mask, clips[0], clips[2]);
                Check(maskBefore == EditorJsonUtility.ToJson(mask), "Máscara compartida original intacta");
                File.WriteAllLines(Output + "/checks.txt", new[] { "PASS" }.Concat(Report));
                Debug.Log("HUMANOID_MASK_DIRECTION_OK " + Report.Count);
            }
            catch (Exception e)
            {
                File.WriteAllLines(Output + "/checks.txt", new[] { "FAIL", e.ToString() }.Concat(Report));
                throw;
            }
        }

        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static PlayableGraph Manual(WeaponActionPlayback playback) => (PlayableGraph)typeof(WeaponActionPlayback)
            .GetField("graph", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback);

        static void CheckClip(GameObject model, RuntimeAnimatorController controller, AvatarMask mask, AnimationClip clip, float yaw, float speed)
        {
            using (var rig = new HumanoidAttackRig(model))
            using (var expected = new HumanoidAttackRig(model))
            {
                rig.Animator.transform.rotation = expected.Animator.transform.rotation = Quaternion.Euler(0, yaw, 0);
                rig.Animator.enabled = true;
                using (var playback = new WeaponActionPlayback(rig.Animator, controller, mask))
                {
                    var graph = Manual(playback); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    playback.SetParameters(0, 0, 1); playback.SetLocomotionSpeed(speed);
                    playback.SetAction(null, 0, 0); playback.Tick(0); graph.Evaluate(.05f); graph.Evaluate(.3f);
                    var lower = Lower.Select(b => rig.Animator.GetBoneTransform(b)).ToArray();
                    var positions = lower.Select(t => t.position).ToArray(); var rotations = lower.Select(t => t.rotation).ToArray();
                    float upperError = 0, lowerDistance = 0, lowerAngle = 0;
                    foreach (float frame in new[] { .15f, .35f, .6f, .9f })
                    {
                        expected.Sample(clip, clip.length * frame);
                        playback.SetAction(clip, frame, 0); playback.Tick(0); graph.Evaluate(0);
                        foreach (var bone in Upper)
                            upperError = Mathf.Max(upperError, Quaternion.Angle(rig.Animator.GetBoneTransform(bone).rotation, expected.Animator.GetBoneTransform(bone).rotation));
                        for (int i = 0; i < lower.Length; i++)
                        {
                            lowerDistance = Mathf.Max(lowerDistance, Vector3.Distance(lower[i].position, positions[i]));
                            lowerAngle = Mathf.Max(lowerAngle, Quaternion.Angle(lower[i].rotation, rotations[i]));
                        }
                    }
                    string label = $"{clip.name}, giro {yaw}°, locomoción {speed}";
                    Check(upperError < .15f, label + $": orientación de torso, cabeza y manos coincide con el clip ({upperError:F4}°)");
                    Check(lowerDistance < .001f && lowerAngle < .15f, label + $": cadera y piernas conservan locomoción ({lowerDistance:F6} m, {lowerAngle:F4}°)");
                    Check(rig.Animator.transform.position.sqrMagnitude < .000001f && Quaternion.Angle(rig.Animator.transform.rotation, Quaternion.Euler(0, yaw, 0)) < .01f,
                        label + ": la corrección no gira ni desplaza la raíz del personaje");
                    if (speed > 0)
                    {
                        var before = lower.Select(t => t.rotation).ToArray(); graph.Evaluate(.2f);
                        Check(lower.Where((t, i) => Quaternion.Angle(t.rotation, before[i]) > .5f).Any(), label + ": las piernas siguen caminando durante el ataque");
                    }
                }
            }
        }

        static void CheckTransitions(GameObject model, RuntimeAnimatorController controller, AvatarMask mask, AnimationClip bow, AnimationClip lunge)
        {
            using (var rig = new HumanoidAttackRig(model))
            using (var expected = new HumanoidAttackRig(model))
            {
                rig.Animator.enabled = true;
                using (var playback = new WeaponActionPlayback(rig.Animator, controller, mask))
                {
                    var graph = Manual(playback); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    playback.SetParameters(0, 0, 1); playback.SetLocomotionSpeed(0);
                    playback.SetAction(null, 0, 0); playback.Tick(0); graph.Evaluate(.05f); graph.Evaluate(.3f);
                    var spine = rig.Animator.GetBoneTransform(HumanBodyBones.Spine);
                    Quaternion idle = spine.rotation;
                    for (int cycle = 0; cycle < 3; cycle++)
                    {
                        playback.SetAction(bow, .6f, .06f, mask, cycle * 2 + 1); playback.Tick(.03f); graph.Evaluate(0);
                        Check(float.IsFinite(spine.rotation.w), "Entrada con mezcla parcial válida: " + cycle);
                        playback.Tick(.03f); graph.Evaluate(0);
                        expected.Sample(bow, bow.length * .6f);
                        Check(Quaternion.Angle(spine.rotation, expected.Animator.GetBoneTransform(HumanBodyBones.Spine).rotation) < .15f, "Entrada completa conserva la dirección: " + cycle);
                        playback.SetAction(lunge, .35f, .06f, mask, cycle * 2 + 2); playback.Tick(.03f); graph.Evaluate(0);
                        playback.Tick(.03f); graph.Evaluate(0);
                        expected.Sample(lunge, lunge.length * .35f);
                        Check(Quaternion.Angle(spine.rotation, expected.Animator.GetBoneTransform(HumanBodyBones.Spine).rotation) < .15f, "Cambio de clip conserva dirección sin acumular corrección: " + cycle);
                        playback.SetAction(lunge, .6f, 0, null); playback.Tick(0); graph.Evaluate(0);
                        expected.Sample(lunge, lunge.length * .6f);
                        Check(Quaternion.Angle(spine.rotation, expected.Animator.GetBoneTransform(HumanBodyBones.Spine).rotation) < .15f, "Full Body sigue reproduciendo el clip sin corrección: " + cycle);
                        playback.SetAction(bow, .6f, 0, mask); playback.Tick(0); graph.Evaluate(0);
                        playback.SetAction(null, 0, .06f); playback.Tick(.03f); graph.Evaluate(0); playback.Tick(.03f); graph.Evaluate(0);
                        Check(Quaternion.Angle(spine.rotation, idle) < .15f, "Cancelar devuelve la orientación de reposo: " + cycle);
                    }
                }
            }
        }

        static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
            Report.Add("PASS " + message);
        }
    }
}
