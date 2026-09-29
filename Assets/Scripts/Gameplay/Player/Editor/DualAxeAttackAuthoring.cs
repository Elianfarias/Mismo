using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Authored poses for the dual-axe basic chain. The recipes stay editable in Crear ataques Humanoid; runtime uses the clips.
    public static class DualAxeAttackAuthoring
    {
        public const string Clips = "Assets/Art/Animations/HumanoidAttacks/DoubleAxe";
        public const string Projects = "Assets/Data/AnimationAuthoring/HumanoidAttacks/DoubleAxe";
        public const string BindingPath = "Assets/Data/WeaponFamilies/DualAxesAnimations.asset";
        const string WeaponPath = "Assets/Data/Weapons/Axe/Axe.asset";
        const string BasicId = "DualAxeBasic";
        const string Output = "output/dual-axe-attacks";
        // Same order as the DualAxeBasic combo steps (ComboOrder.AlternateHands).
        public static readonly string[] Names = { "Combat_DualAxe_Basic_Right", "Combat_DualAxe_Basic_Left", "Combat_DualAxe_Double_Right", "Combat_DualAxe_Double_Left" };

        readonly struct Hand
        {
            public readonly Vector3 at, blade, edge;
            public Hand(Vector3 position, Vector3 bladeDirection, Vector3 edgeDirection) { at = position; blade = bladeDirection; edge = edgeDirection; }
            public Hand Mirror() => new Hand(Flip(at), Flip(blade), Flip(edge));
            static Vector3 Flip(Vector3 v) => new Vector3(-v.x, v.y, v.z);
        }

        sealed class Grip
        {
            public Vector3 rotation, handle, edge;
            public Quaternion HandRotation(Vector3 blade, Vector3 edgeDirection)
            {
                Vector3 up = blade.normalized, forward = Vector3.ProjectOnPlane(edgeDirection, up);
                if (forward.sqrMagnitude < .0001f) forward = Vector3.ProjectOnPlane(Vector3.forward, up);
                var world = Quaternion.LookRotation(forward, up);
                var local = Quaternion.LookRotation(Vector3.ProjectOnPlane(edge, handle), handle);
                return world * Quaternion.Inverse(local) * Quaternion.Inverse(Quaternion.Euler(rotation));
            }
        }

        [MenuItem("Mismo/Animaciones/Crear y asignar básicos Dos hachas")]
        public static void CreateAndAssign()
        {
            GenerateMissing(); Assign(); VerifyAssignment();
        }

        public static void GenerateBatch()
        {
            try { GenerateMissing(); RenderPreviews(); Assign(); VerifyAssignment(); EditorApplication.Exit(0); }
            catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/FAILED.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static WeaponDefinition Axe()
        {
            var axe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponPath);
            if (axe == null || axe.poseProfile == null || axe.visualPrefab == null) throw new Exception("Falta el hacha, su pose o su visual: " + WeaponPath);
            return axe;
        }

        public static void GenerateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
            Directory.CreateDirectory(Output);
            HumanoidAttackAuthoring.EnsureFolder(Clips); HumanoidAttackAuthoring.EnsureFolder(Projects);
            var axe = Axe();
            Vector3 handle = axe.poseProfile.trailTip - axe.poseProfile.trailBase;
            if (handle.sqrMagnitude < .0001f) handle = Vector3.up;
            Vector3 edge = EdgeDirection(axe.visualPrefab, handle.normalized);
            var right = new Grip { rotation = axe.poseProfile.equipped.rotation, handle = handle.normalized, edge = edge };
            var left = new Grip { rotation = axe.secondaryEquipped.rotation, handle = handle.normalized, edge = edge };
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusHumanoidLocomotion.ModelPath);
            var report = new List<string> { $"Filo del hacha (espacio local): {edge}" };
            using (var rig = new HumanoidAttackRig(model))
            {
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Quaternius/Humanoid/Quaternius_Idle.anim");
                rig.Sample(idle, .3f); var basePose = rig.Capture("Base", 0); rig.StopSampling();
                for (int i = 0; i < Names.Length; i++)
                {
                    var recipe = AssetDatabase.LoadAssetAtPath<HumanoidAttackRecipe>(Projects + "/" + Names[i] + ".asset");
                    if (recipe == null)
                    {
                        recipe = ScriptableObject.CreateInstance<HumanoidAttackRecipe>();
                        recipe.name = recipe.clipName = Names[i]; recipe.model = model; recipe.frameRate = 60;
                        recipe.previewWeapon = recipe.previewOffhand = axe; recipe.previewPair = true;
                        Author(recipe, i, rig, basePose, right, left);
                        AssetDatabase.CreateAsset(recipe, Projects + "/" + Names[i] + ".asset");
                        AssetDatabase.SaveAssetIfDirty(recipe);
                    }
                    string path = Clips + "/" + Names[i] + ".anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip == null)
                    {
                        clip = HumanoidAttackAuthoring.Bake(recipe, rig);
                        AssetDatabase.CreateAsset(clip, path); AssetDatabase.SaveAssetIfDirty(clip);
                    }
                    ValidateMotion(recipe, clip, rig, i, report);
                }
            }
            File.WriteAllLines(Output + "/clips.txt", new[] { "PASS: cuatro clips Humanoid y sus proyectos editables" }.Concat(report));
        }

        // Side of the handle where the head sticks out; the cutting edge faces that way.
        static Vector3 EdgeDirection(GameObject prefab, Vector3 handle)
        {
            var copy = Object.Instantiate(prefab);
            try
            {
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var points = new List<Vector3>();
                foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    try { points.AddRange(filter.sharedMesh.vertices.Select(v => copy.transform.InverseTransformPoint(filter.transform.TransformPoint(v)))); }
                    catch (Exception) { }
                }
                if (points.Count < 8) return Vector3.forward;
                float low = points.Min(p => Vector3.Dot(p, handle)), high = points.Max(p => Vector3.Dot(p, handle)), range = high - low;
                var head = points.Where(p => Vector3.Dot(p, handle) >= high - range * .3f).ToList();
                var shaft = points.Where(p => Vector3.Dot(p, handle) <= low + range * .4f).ToList();
                if (head.Count == 0 || shaft.Count == 0) return Vector3.forward;
                Vector3 side = Vector3.ProjectOnPlane(Average(head) - Average(shaft), handle);
                return side.sqrMagnitude > .0001f ? side.normalized : Vector3.forward;
            }
            finally { Object.DestroyImmediate(copy); }
        }

        static Vector3 Average(List<Vector3> points) => points.Aggregate(Vector3.zero, (sum, p) => sum + p) / points.Count;

        // Poses are written for a right-hand lead; side -1 mirrors them and gives the lead to the left hand.
        static void Author(HumanoidAttackRecipe recipe, int attack, HumanoidAttackRig rig, HumanoidAttackPose basePose, Grip rightGrip, Grip leftGrip)
        {
            int side = attack % 2 == 0 ? 1 : -1;
            var poses = recipe.poses;
            void Pose(float t, string label, Hand lead, Hand other, float twist, float lean)
            {
                var pose = basePose.Copy(t, label);
                Set(pose, "Chest Twist Left-Right", twist * side); Set(pose, "Spine Twist Left-Right", twist * side * .35f);
                Set(pose, "Chest Front-Back", lean); Set(pose, "Spine Front-Back", lean * .45f);
                Set(pose, "Head Turn Left-Right", -twist * side * .2f);
                for (int m = 0; m < pose.muscles.Length; m++)
                    if (HumanTrait.MuscleName[m].EndsWith("Stretched", StringComparison.Ordinal)) pose.muscles[m] = -.55f;
                rig.Apply(pose);
                Hand right = side > 0 ? lead : other.Mirror(), left = side > 0 ? other : lead.Mirror();
                SetArm(rig, HumanBodyBones.RightHand, right, rightGrip, 1);
                SetArm(rig, HumanBodyBones.LeftHand, left, leftGrip, -1);
                pose = rig.Capture(label, t);
                pose.blend = AttackPoseBlend.Linear;
                poses.Add(pose);
            }
            var guardLead = new Hand(new Vector3(.37f, 1.22f, .30f), new Vector3(.2f, .85f, .5f), Vector3.forward);
            var guardOther = new Hand(new Vector3(-.37f, 1.15f, .27f), new Vector3(-.2f, .85f, .5f), Vector3.forward);

            if (attack < 2)
            {
                // Valheim-style single: axe up to the same shoulder, diagonal cut down to the other side.
                recipe.duration = .75f; recipe.activeStartsAt = .26f; recipe.recoveryStartsAt = .56f;
                var loaded = new Hand(new Vector3(.40f, 1.60f, -.04f), new Vector3(.25f, .55f, -.8f), new Vector3(-.1f, .9f, .4f));
                var resting = new Hand(new Vector3(-.34f, 1.12f, .32f), guardOther.blade, Vector3.forward);
                var raised = new Hand(new Vector3(-.42f, 1.26f, .14f), new Vector3(-.2f, .9f, .35f), Vector3.forward);
                Pose(0, "Guardia", guardLead, guardOther, 0, 0);
                Pose(.18f, "Hacha al hombro", loaded, resting, -.32f, -.05f);
                Pose(.26f, "Carga sostenida", new Hand(new Vector3(.42f, 1.62f, -.08f), loaded.blade, loaded.edge), resting, -.36f, -.05f);
                Pose(.40f, "Corte diagonal", new Hand(new Vector3(.06f, 1.18f, .58f), new Vector3(-.35f, -.05f, 1), new Vector3(-.65f, -.7f, .1f)), new Hand(new Vector3(-.36f, 1.16f, .26f), guardOther.blade, Vector3.forward), .18f, .1f);
                Pose(.52f, "Salida abajo", new Hand(new Vector3(-.30f, .95f, .40f), new Vector3(-.85f, -.4f, .3f), new Vector3(-.3f, -.9f, -.3f)), raised, .32f, .12f);
                var settled = new Hand(new Vector3(-.24f, 1.0f, .40f), new Vector3(-.7f, -.15f, .6f), new Vector3(-.2f, -.8f, .4f));
                Pose(.75f, "Asentar", settled, raised, .26f, .08f);
                Pose(1, "Sostener salida", settled, raised, .26f, .08f);
            }
            else
            {
                // Double: the lead axe crosses to the other shoulder and sweeps back; the second axe follows the same path ~0.4 s later.
                recipe.duration = 1.3f; recipe.activeStartsAt = .24f; recipe.recoveryStartsAt = .76f;
                var leadLoaded = new Hand(new Vector3(-.20f, 1.50f, .12f), new Vector3(-.6f, .45f, -.65f), new Vector3(.4f, .6f, .6f));
                var otherLoaded = new Hand(new Vector3(-.46f, 1.58f, -.02f), new Vector3(-.35f, .6f, -.72f), new Vector3(.3f, .7f, .55f));
                var cut = new Vector3(.85f, -.45f, .1f);
                Pose(0, "Guardia", guardLead, guardOther, 0, 0);
                Pose(.14f, "Ambas al hombro contrario", leadLoaded, otherLoaded, .36f, -.04f);
                Pose(.22f, "Carga sostenida", new Hand(new Vector3(-.22f, 1.52f, .08f), leadLoaded.blade, leadLoaded.edge), new Hand(new Vector3(-.47f, 1.60f, -.05f), otherLoaded.blade, otherLoaded.edge), .40f, -.04f);
                Pose(.31f, "Revés", new Hand(new Vector3(.12f, 1.20f, .58f), new Vector3(.4f, -.05f, 1), cut), new Hand(new Vector3(-.44f, 1.56f, 0), otherLoaded.blade, otherLoaded.edge), .08f, .08f);
                var leadOut = new Hand(new Vector3(.42f, 1.0f, .36f), new Vector3(.85f, -.35f, .35f), new Vector3(.3f, -.9f, -.3f));
                Pose(.40f, "Salida del revés", leadOut, new Hand(new Vector3(-.44f, 1.56f, .02f), otherLoaded.blade, otherLoaded.edge), -.08f, .08f);
                var leadWait = new Hand(new Vector3(.42f, 1.02f, .32f), new Vector3(.8f, -.3f, .4f), new Vector3(.3f, -.9f, -.2f));
                Pose(.52f, "Segunda cargada", leadWait, new Hand(new Vector3(-.46f, 1.60f, -.04f), otherLoaded.blade, otherLoaded.edge), .14f, -.02f);
                Pose(.62f, "Segundo tajo", new Hand(new Vector3(.44f, 1.0f, .28f), new Vector3(.85f, -.3f, .35f), leadWait.edge), new Hand(new Vector3(.02f, 1.20f, .58f), new Vector3(.35f, -.05f, 1), cut), -.12f, .1f);
                Pose(.72f, "Salida doble", new Hand(new Vector3(.46f, .98f, .24f), new Vector3(.85f, -.35f, .3f), leadOut.edge), new Hand(new Vector3(.30f, .96f, .40f), new Vector3(.8f, -.4f, .4f), leadOut.edge), -.30f, .12f);
                var leadSettled = new Hand(new Vector3(.44f, 1.02f, .26f), new Vector3(.8f, -.2f, .45f), new Vector3(.2f, -.9f, .3f));
                var otherSettled = new Hand(new Vector3(.24f, 1.0f, .40f), new Vector3(.7f, -.15f, .6f), new Vector3(.2f, -.8f, .4f));
                Pose(1, "Sostener salida", leadSettled, otherSettled, -.25f, .08f);
            }
            poses[0].blend = AttackPoseBlend.Smooth;
            poses[poses.Count - 2].blend = AttackPoseBlend.Smooth;
        }

        static void Set(HumanoidAttackPose pose, string muscle, float value)
        {
            int i = Array.IndexOf(HumanTrait.MuscleName, muscle);
            if (i >= 0) pose.muscles[i] = value;
        }

        static void SetArm(HumanoidAttackRig rig, HumanBodyBones hand, Hand target, Grip grip, int side)
        {
            if (!HumanoidAttackIK.TryGetLimb(rig.Animator, hand, out var limb)) throw new InvalidOperationException("Falta el brazo " + hand);
            float scale = rig.Animator.humanScale;
            Vector3 pole = new Vector3(side * .9f, .95f, -.25f) * scale;
            HumanoidAttackIK.Solve(limb, target.at * scale, pole, false);
            limb.tip.rotation = grip.HandRotation(target.blade, target.edge);
        }

        static void ValidateMotion(HumanoidAttackRecipe recipe, AnimationClip clip, HumanoidAttackRig rig, int attack, List<string> report)
        {
            if (!clip.humanMotion || Mathf.Abs(clip.length - recipe.duration) > .001f) throw new Exception("Clip incorrecto: " + clip.name);
            if (AnimationUtility.GetCurveBindings(clip).Any(b => b.type != typeof(Animator) || b.path != "")) throw new Exception("Rutas de rig en " + clip.name);
            float rightTravel = 0, leftTravel = 0;
            rig.Sample(clip, 0);
            Vector3 right = rig.Animator.GetBoneTransform(HumanBodyBones.RightHand).position;
            Vector3 left = rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
            for (int frame = 1; frame <= 60; frame++)
            {
                rig.Sample(clip, clip.length * frame / 60);
                Vector3 r = rig.Animator.GetBoneTransform(HumanBodyBones.RightHand).position, l = rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                if (!float.IsFinite(r.x) || !float.IsFinite(l.x)) throw new Exception("Pose no finita: " + clip.name);
                rightTravel += Vector3.Distance(right, r); leftTravel += Vector3.Distance(left, l); right = r; left = l;
            }
            bool rightLeads = attack % 2 == 0;
            float lead = rightLeads ? rightTravel : leftTravel, other = rightLeads ? leftTravel : rightTravel;
            if (lead < .8f) throw new Exception("La mano que abre casi no se mueve: " + clip.name);
            if (attack >= 2 && other < .8f) throw new Exception("La segunda hacha no participa: " + clip.name);
            float error = 0;
            foreach (var pose in recipe.poses)
            {
                rig.Apply(pose);
                Vector3 expectedRight = rig.Animator.GetBoneTransform(HumanBodyBones.RightHand).position, expectedLeft = rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                rig.Sample(clip, pose.time * clip.length);
                error = Mathf.Max(error, Vector3.Distance(expectedRight, rig.Animator.GetBoneTransform(HumanBodyBones.RightHand).position),
                    Vector3.Distance(expectedLeft, rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position));
            }
            if (error > .02f) throw new Exception("Pose exportada distinta: " + clip.name);
            report.Add($"PASS {clip.name}: {clip.length:F2}s, derecha {rightTravel:F2}m / izquierda {leftTravel:F2}m, error de pose {error:F5}m");
        }

        public static void Assign()
        {
            var asset = AssetDatabase.LoadAssetAtPath<WeaponAnimationSet>(BindingPath);
            if (asset == null) throw new Exception("Falta DualAxesAnimations");
            var clips = Names.Select(n => AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/" + n + ".anim")).ToArray();
            if (clips.Any(c => c == null || !c.humanMotion)) throw new Exception("Generar los cuatro clips Humanoid antes de asignar.");
            var data = new SerializedObject(asset); data.Update();
            var binding = FindBasicBinding(data) ?? throw new Exception("Falta el binding de " + BasicId);
            Undo.RecordObject(asset, "Asignar básicos de Dos hachas");
            binding.FindPropertyRelative("clip").objectReferenceValue = null;
            var combo = binding.FindPropertyRelative("comboClips"); combo.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) combo.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            // Chained steps start from the previous follow-through; a longer blend hides the seam and the return to idle.
            binding.FindPropertyRelative("blendSeconds").floatValue = .15f;
            data.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(asset);
        }

        static SerializedProperty FindBasicBinding(SerializedObject data)
        {
            var actions = data.FindProperty("actions");
            for (int i = 0; i < actions.arraySize; i++)
            {
                var action = actions.GetArrayElementAtIndex(i);
                if (action.FindPropertyRelative("ability").objectReferenceValue is AbilityDefinition ability && ability.Id == BasicId) return action;
            }
            return null;
        }

        public static void VerifyAssignment()
        {
            var asset = AssetDatabase.LoadAssetAtPath<WeaponAnimationSet>(BindingPath);
            if (asset == null) throw new Exception("Falta el conjunto de animaciones Dos hachas.");
            var binding = FindBasicBinding(new SerializedObject(asset)) ?? throw new Exception("Falta el binding de " + BasicId);
            var combo = binding.FindPropertyRelative("comboClips");
            if (combo.arraySize != Names.Length) throw new Exception("El básico debe tener cuatro clips de combo.");
            for (int i = 0; i < Names.Length; i++)
                if (AssetDatabase.GetAssetPath(combo.GetArrayElementAtIndex(i).objectReferenceValue) != Clips + "/" + Names[i] + ".anim")
                    throw new Exception("Etapa de combo incorrecta: " + i);
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + "/assignment.txt", new[] { "PASS básico de Dos hachas con cuatro etapas (derecha, izquierda, doble derecha, doble izquierda)." }.Concat(Names));
        }

        // Front and back views of every authored pose; the right axe is orange and the left one cyan.
        static void RenderPreviews()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var axe = Axe();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusHumanoidLocomotion.ModelPath);
            var bodyMaterial = new Material(Shader.Find("Standard")) { color = new Color(.48f, .58f, .68f) };
            var rightMaterial = new Material(Shader.Find("Standard")) { color = new Color(.95f, .65f, .2f) };
            var leftMaterial = new Material(Shader.Find("Standard")) { color = new Color(.2f, .85f, .8f) };
            var preview = new PreviewRenderUtility();
            var bakedMeshes = new List<Mesh>();
            const int width = 260, height = 340;
            using (var rig = new HumanoidAttackRig(model))
            try
            {
                preview.AddSingleGO(rig.Container);
                foreach (var renderer in rig.Container.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => bodyMaterial).ToArray();
                var skins = rig.Container.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var skin in skins)
                {
                    var snapshot = new GameObject("Pose snapshot", typeof(MeshFilter), typeof(MeshRenderer));
                    snapshot.transform.SetParent(skin.transform, false);
                    Vector3 skinScale = skin.transform.lossyScale;
                    snapshot.transform.localScale = new Vector3(1 / skinScale.x, 1 / skinScale.y, 1 / skinScale.z);
                    var mesh = new Mesh(); bakedMeshes.Add(mesh);
                    snapshot.GetComponent<MeshFilter>().sharedMesh = mesh;
                    snapshot.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                }
                var rightAxe = AddPreviewAxe(rig, axe.visualPrefab, rightMaterial);
                var leftAxe = AddPreviewAxe(rig, axe.visualPrefab, leftMaterial);
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.055f, .065f, .08f);
                preview.camera.orthographic = true; preview.camera.orthographicSize = 1.2f; preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 50;
                preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(35, -30, 0); preview.lights[1].intensity = .8f;
                var views = new[] { new Vector3(2.2f, 2.3f, 6), new Vector3(-1.6f, 2.6f, -6) };
                foreach (string name in Names)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/" + name + ".anim");
                    var recipe = AssetDatabase.LoadAssetAtPath<HumanoidAttackRecipe>(Projects + "/" + name + ".asset");
                    int count = recipe.poses.Count;
                    var sheet = new Texture2D(width * count, height * views.Length, TextureFormat.RGB24, false);
                    try
                    {
                        for (int v = 0; v < views.Length; v++)
                        {
                            preview.camera.transform.position = views[v]; preview.camera.transform.LookAt(new Vector3(0, 1.05f, 0));
                            for (int i = 0; i < count; i++)
                            {
                                rig.Sample(clip, recipe.poses[i].time * clip.length);
                                for (int s = 0; s < skins.Length; s++) skins[s].BakeMesh(bakedMeshes[s]);
                                axe.poseProfile.equipped.Apply(rightAxe.transform, rig.Animator.GetBoneTransform(HumanBodyBones.RightHand));
                                axe.secondaryEquipped.Apply(leftAxe.transform, rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand));
                                preview.BeginStaticPreview(new Rect(0, 0, width, height)); preview.Render(); var frame = preview.EndStaticPreview();
                                sheet.SetPixels(i * width, (views.Length - 1 - v) * height, width, height, frame.GetPixels()); Object.DestroyImmediate(frame);
                            }
                        }
                        sheet.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", sheet.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(sheet); }
                }
            }
            finally { preview.Cleanup(); foreach (var mesh in bakedMeshes) Object.DestroyImmediate(mesh); Object.DestroyImmediate(bodyMaterial); Object.DestroyImmediate(rightMaterial); Object.DestroyImmediate(leftMaterial); }
        }

        static GameObject AddPreviewAxe(HumanoidAttackRig rig, GameObject prefab, Material material)
        {
            var copy = Object.Instantiate(prefab, rig.Container.transform);
            foreach (var behaviour in copy.GetComponentsInChildren<Behaviour>()) behaviour.enabled = false;
            foreach (var collider in copy.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            return copy;
        }
    }
}
