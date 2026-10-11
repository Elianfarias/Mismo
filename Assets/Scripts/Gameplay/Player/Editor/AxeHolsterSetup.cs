using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Axes on the back, from the proposal sketches seen from behind: one axe hangs from the right shoulder with its head
    // toward the lower back, left of the spine; a pair crosses in an X, handles at the shoulders and heads low on each side.
    // Each pose is solved from two points in the skin root's frame (metres, as WeaponPresentation applies it at rest):
    // where the handle's end sits and where the head should hang. The model keeps its blade flat against the back.
    public static class AxeHolsterSetup
    {
        const string AxePath = "Assets/Data/Weapons/Axe/Axe.asset", PosePath = "Assets/Data/Weapons/Axe/AxePose.asset";
        const string PlayerPath = "Assets/Art/Prefabs/Player/Player.prefab", IdlePath = "Assets/Art/Animations/Quaternius/Humanoid/Quaternius_Idle.anim";
        const string Reference = "Assets/Art/Prefabs/Player/Skins/MageSkin.prefab";
        static readonly string[] Skins = { Reference, "Assets/Art/Prefabs/Player/Skins/WarriorSkin.prefab", "Assets/Art/Prefabs/Player/Skins/NinjaFrogSkin.prefab" };
        // Review images only: Logs is ignored by git.
        const string Output = "Logs/AxeHolster";
        // Clearance from the back, and the extra depth of the second axe so the X does not intersect where it crosses.
        const float Clearance = .06f, CrossDepth = .035f;

        public struct Rig { public Vector3 rightShoulder, leftShoulder; public float hips, chest, back, height; }
        public struct AxeModel { public Vector3 butt, head; }
        public struct Pose { public Vector3 offset, euler; }

        [MenuItem("Mismo/Armas/Hachas cruzadas en la espalda")]
        public static void Install()
        {
            var rig = MeasureRig(Reference); var model = MeasureAxe();
            Solve(rig, model, out var single, out var first, out var second);
            var pose = AssetDatabase.LoadAssetAtPath<WeaponPoseProfile>(PosePath) ?? throw new Exception("Falta " + PosePath);
            var axe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AxePath) ?? throw new Exception("Falta " + AxePath);
            var data = new SerializedObject(pose); data.Update();
            Write(data.FindProperty("holstered"), single);
            data.FindProperty("useDualHolstered").boolValue = true;
            Write(data.FindProperty("dualHolstered"), first);
            data.ApplyModifiedPropertiesWithoutUndo();
            var weapon = new SerializedObject(axe); weapon.Update();
            Write(weapon.FindProperty("secondaryHolstered"), second);
            weapon.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("AXE_HOLSTER single " + Describe(single) + " | pair " + Describe(first) + " / " + Describe(second));
        }
        public static void InstallBatch() { Run(Install); }
        public static void MeasureBatch()
        {
            Run(() =>
            {
                foreach (var skin in Skins)
                {
                    var r = MeasureRig(skin);
                    Debug.Log("AXE_HOLSTER_RIG " + Path.GetFileNameWithoutExtension(skin) + " shoulders R" + r.rightShoulder.ToString("F3") + " L" + r.leftShoulder.ToString("F3")
                        + " hips " + r.hips.ToString("F3") + " chest " + r.chest.ToString("F3") + " back " + r.back.ToString("F3") + " height " + r.height.ToString("F3"));
                }
                var m = MeasureAxe();
                Debug.Log("AXE_HOLSTER_MODEL butt " + m.butt.ToString("F3") + " head " + m.head.ToString("F3") + " length " + Vector3.Distance(m.butt, m.head).ToString("F3"));
            });
        }
        static void Run(Action action)
        {
            try { action(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        static void Write(SerializedProperty pose, Pose value)
        {
            pose.FindPropertyRelative("anchor").enumValueIndex = (int)WeaponAnchor.Character;
            pose.FindPropertyRelative("offset").vector3Value = value.offset;
            pose.FindPropertyRelative("rotation").vector3Value = value.euler;
            pose.FindPropertyRelative("scale").floatValue = 1;
        }
        static string Describe(Pose p) => p.offset.ToString("F3") + " " + p.euler.ToString("F1");

        // ---- The three poses ----
        // The axe (~1 m from the handle's end to the head) is longer than the torso, so each pose is set by where the head
        // hangs and which shoulder the handle rests on: the handle passes over that shoulder and its end rises behind it,
        // like a sword on the back. The idle pose twists the shoulders, so their mean width is used, centred on the spine.
        public static void Solve(Rig rig, AxeModel model, out Pose single, out Pose first, out Pose second)
        {
            float z = rig.back - Clearance, half = (rig.rightShoulder.x - rig.leftShoulder.x) / 2, shoulder = (rig.rightShoulder.y + rig.leftShoulder.y) / 2 + .03f;
            // One axe: over the right shoulder, head just left of the spine at the lower back.
            single = Place(model, new Vector3(-.08f, rig.hips + .1f, z), new Vector3(half * .8f, shoulder, z), -1);
            // The X: each handle over a shoulder, crossing mid-back to the other side; the heads hang apart, wider than the
            // single axe, so they do not pile up on the spine.
            first = Place(model, new Vector3(-half * 1.45f, rig.hips + .14f, z), new Vector3(half * .7f, shoulder, z), -1);
            second = Place(model, new Vector3(half * 1.45f, rig.hips + .14f, z - CrossDepth), new Vector3(-half * .7f, shoulder, z - CrossDepth), 1);
        }
        // Model +Y (handle to head) along the line from the shoulder to the head, blade flat against the back and its edge (+X)
        // facing away from the spine.
        public static Pose Place(AxeModel model, Vector3 headTarget, Vector3 shoulder, float outward)
        {
            Vector3 along = (model.head - model.butt).normalized, toward = (headTarget - shoulder).normalized;
            Vector3 butt = headTarget - toward * Vector3.Distance(model.head, model.butt);
            var modelFrame = Quaternion.LookRotation(Vector3.forward, along);
            Quaternion best = Quaternion.identity; float score = float.MinValue;
            foreach (var depth in new[] { Vector3.back, Vector3.forward })
            {
                var rotation = Quaternion.LookRotation(depth, toward) * Quaternion.Inverse(modelFrame);
                float s = (rotation * Vector3.right).x * outward;
                if (s > score) { score = s; best = rotation; }
            }
            return new Pose { offset = butt - best * model.butt, euler = best.eulerAngles };
        }
        public static Vector3 Head(AxeModel model, Pose pose) => pose.offset + Quaternion.Euler(pose.euler) * model.head;
        public static Vector3 Butt(AxeModel model, Pose pose) => pose.offset + Quaternion.Euler(pose.euler) * model.butt;
        static Pose Read(WeaponAttachmentPose pose) => new Pose { offset = pose.offset, euler = pose.rotation };

        // ---- Measures ----
        // The skin at the scale the player gives it (PlayerAppearance copies the authored avatar's scale), in idle.
        static GameObject Actor(string skin, out Animator animator)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) ?? throw new Exception("Falta " + PlayerPath);
            var authored = player.GetComponentInChildren<Animator>(true);
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(skin) ?? throw new Exception("Falta " + skin));
            actor.hideFlags = HideFlags.HideAndDontSave;
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (authored != null) actor.transform.localScale = authored.transform.lossyScale;
            animator = actor.GetComponentInChildren<Animator>(); animator.Rebind();
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
            if (idle != null)
            {
                bool started = !AnimationMode.InAnimationMode();
                if (started) AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(animator.gameObject, idle, idle.length * .5f); AnimationMode.EndSampling();
            }
            return actor;
        }
        static void Release(GameObject actor) { if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); if (actor != null) Object.DestroyImmediate(actor); }
        public static Rig MeasureRig(string skin)
        {
            var actor = Actor(skin, out var animator);
            try
            {
                var root = animator.transform;
                Vector3 Local(Vector3 world) => Quaternion.Inverse(root.rotation) * (world - root.position);
                Vector3 Bone(HumanBodyBones b) => Local((animator.GetBoneTransform(b) ?? throw new Exception(skin + " no tiene " + b)).position);
                var chestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
                var rig = new Rig { rightShoulder = Bone(HumanBodyBones.RightUpperArm), leftShoulder = Bone(HumanBodyBones.LeftUpperArm), hips = Bone(HumanBodyBones.Hips).y, chest = Local(chestBone.position).y };
                var points = Vertices(actor).Select(Local).ToList();
                rig.height = points.Max(p => p.y);
                // The back: the rearmost body surface across the torso, between the hips and the shoulders, near the spine.
                var torso = points.Where(p => Mathf.Abs(p.x) < .14f && p.y > rig.hips && p.y < rig.rightShoulder.y).ToList();
                rig.back = torso.Count > 0 ? torso.Min(p => p.z) : -.2f;
                return rig;
            }
            finally { Release(actor); }
        }
        static IEnumerable<Vector3> Vertices(GameObject root)
        {
            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skinned.enabled || skinned.sharedMesh == null) continue;
                var mesh = new Mesh(); skinned.BakeMesh(mesh, true);
                var t = skinned.transform;
                foreach (var v in mesh.vertices) yield return t.position + t.rotation * v;
                Object.DestroyImmediate(mesh);
            }
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                foreach (var v in filter.sharedMesh.vertices) yield return filter.transform.TransformPoint(v);
            }
        }
        // The axe model in its prefab space: the handle's end (lowest along +Y) and the centre of the head (the top part).
        public static AxeModel MeasureAxe()
        {
            var axe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AxePath) ?? throw new Exception("Falta " + AxePath);
            var model = Object.Instantiate(axe.visualPrefab); model.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); model.transform.localScale = Vector3.one;
                var points = Vertices(model).ToList();
                if (points.Count == 0) throw new Exception("El hacha no tiene mallas");
                float low = points.Min(p => p.y), high = points.Max(p => p.y), length = high - low;
                var butt = points.Where(p => p.y < low + length * .1f).ToList();
                var head = points.Where(p => p.y > high - length * .35f).ToList();
                return new AxeModel
                {
                    butt = new Vector3(butt.Average(p => p.x), low, butt.Average(p => p.z)),
                    head = new Vector3((head.Min(p => p.x) + head.Max(p => p.x)) / 2, (head.Min(p => p.y) + high) / 2, (head.Min(p => p.z) + head.Max(p => p.z)) / 2)
                };
            }
            finally { Object.DestroyImmediate(model); }
        }

        // ---- Captures: every skin from behind, three-quarters and the side, with one axe and with the pair ----
        public static void CaptureBatch() { Run(Capture); }
        [MenuItem("Mismo/Armas/Capturas de hachas en la espalda")]
        public static void Capture()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Output);
            RenderSettings.fog = false; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.9f, .94f, .9f); RenderSettings.ambientEquatorColor = new Color(.65f, .7f, .64f); RenderSettings.ambientGroundColor = new Color(.2f, .24f, .2f);
            var sun = new GameObject("Key").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f; sun.transform.rotation = Quaternion.Euler(40, 200, 0);
            var fill = new GameObject("Fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .6f; fill.transform.rotation = Quaternion.Euler(25, 20, 0);
            var pose = AssetDatabase.LoadAssetAtPath<WeaponPoseProfile>(PosePath); var axe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AxePath);
            var camera = new GameObject("Camera").AddComponent<UnityEngine.Camera>(); camera.fieldOfView = 28; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.42f, .55f, .36f);
            const int cell = 360;
            foreach (var skin in Skins)
            {
                // Measure first: ending the measure's animation mode would put a live actor back in its bind pose.
                var rig = MeasureRig(skin);
                var actor = Actor(skin, out var animator);
                actor.hideFlags = HideFlags.None;
                try
                {
                    var sheet = new Texture2D(cell * 3, cell * 2, TextureFormat.RGB24, false);
                    var sets = new[] { new[] { pose.holstered }, new[] { pose.Holstered(true), axe.secondaryHolstered } };
                    for (int row = 0; row < sets.Length; row++)
                    {
                        var axes = sets[row].Select(p => Attach(axe.visualPrefab, animator.transform, p)).ToList();
                        var focus = new Vector3(0, rig.chest, 0); float distance = Mathf.Max(1.6f, rig.height * 2.1f);
                        var views = new[] { new Vector3(0, .15f, -1), new Vector3(-.75f, .3f, -.75f), new Vector3(1, .1f, 0) };
                        for (int v = 0; v < views.Length; v++)
                        {
                            camera.transform.position = focus + views[v].normalized * distance; camera.transform.LookAt(focus);
                            // Twice: a material's first render compiles its shader and comes out magenta.
                            var rt = new RenderTexture(cell, cell, 24) { antiAliasing = 4 }; camera.targetTexture = rt; camera.Render(); camera.Render();
                            RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, cell, cell), v * cell, (1 - row) * cell); RenderTexture.active = null;
                            camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
                        }
                        foreach (var a in axes) Object.DestroyImmediate(a);
                    }
                    sheet.Apply();
                    File.WriteAllBytes(Output + "/" + Path.GetFileNameWithoutExtension(skin) + ".png", sheet.EncodeToPNG());
                    Object.DestroyImmediate(sheet);
                }
                finally { Release(actor); }
            }
            Debug.Log("AXE_HOLSTER_CAPTURES " + Output);
        }
        // Same placement as WeaponPresentation for a Character anchor with the torso at rest.
        static GameObject Attach(GameObject prefab, Transform root, WeaponAttachmentPose pose)
        {
            var visual = Object.Instantiate(prefab);
            visual.transform.SetPositionAndRotation(root.position + root.rotation * pose.offset, root.rotation * Quaternion.Euler(pose.rotation));
            visual.transform.localScale = Vector3.one * pose.scale;
            return visual;
        }

        // ---- Check ----
        public static void Check()
        {
            var failures = new List<string>();
            void Require(bool pass, string message) { Debug.Log((pass ? "AXE_HOLSTER_CHECK " : "AXE_HOLSTER_FAIL ") + message); if (!pass) failures.Add(message); }
            var pose = AssetDatabase.LoadAssetAtPath<WeaponPoseProfile>(PosePath); var axe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AxePath);
            var rig = MeasureRig(Reference); var model = MeasureAxe();
            var single = Read(pose.holstered); var first = Read(pose.Holstered(true)); var second = Read(axe.secondaryHolstered);
            Require(pose.useDualHolstered, "The axe has its own pose when paired");
            Require(Butt(model, single).x > .03f && Butt(model, single).y > rig.chest && Head(model, single).x < 0 && Head(model, single).y < rig.chest,
                "One axe: handle on the right shoulder, head low and left of the spine");
            Require(Butt(model, first).x > 0 && Head(model, first).x < 0 && Butt(model, second).x < 0 && Head(model, second).x > 0
                && Head(model, first).y < Butt(model, first).y && Head(model, second).y < Butt(model, second).y, "The pair crosses in an X with the heads low");
            Require(Head(model, first).x < Head(model, single).x, "Paired, the head opens wider than the single axe");
            foreach (var (name, p) in new[] { ("single", single), ("first", first), ("second", second) })
                Require(Butt(model, p).z < rig.back && Head(model, p).z < rig.back, "The " + name + " axe stays behind the back");
            Require(Mathf.Abs(Butt(model, second).z - Butt(model, first).z) > .02f, "The crossed axes sit at different depths");
            var sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
            Require(sword == null || sword.poseProfile == null || !sword.poseProfile.useDualHolstered, "Swords keep their holster pose");
            Debug.Log(failures.Count == 0 ? "AXE_HOLSTER_PASS" : "AXE_HOLSTER_FAIL " + failures.Count + " failures");
            if (failures.Count > 0) throw new Exception(string.Join("; ", failures));
        }
        public static void CheckBatch() { Run(Check); }
    }
}
