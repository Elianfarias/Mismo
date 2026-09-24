using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Authored poses for the existing Dual Swords abilities. Runtime uses ordinary clips.
    public static class DualSwordAttackAuthoring
    {
        public const string Clips = "Assets/Art/Animations/HumanoidAttacks/DualSwords";
        public const string Projects = "Assets/Data/AnimationAuthoring/HumanoidAttacks/DualSwords";
        public const string BindingPath = "Assets/Data/WeaponFamilies/DualSwordsCombatAnimations.asset";
        const string Output = "output/dual-sword-attacks";
        static readonly string[] Names = { "Dual_01_Corte_Derecho", "Dual_02_Cruce_Izquierdo", "Dual_Rafaga", "Dual_Torbellino", "Dual_Herida_Abierta", "Dual_Cruce" };
        static readonly string[] Abilities = { "SwordCombo", "DualFlurry", "DualWhirlwind", "OpenWound", "DualCross" };
        static readonly Vector3 RightGrip = new Vector3(334.86148f, 188.194f, 29.465136f);
        static readonly Vector3 LeftGrip = new Vector3(0, 0, 66.68407f);
        static readonly Vector3 ReadyR = new Vector3(.37f, 1.22f, .30f), ReadyL = new Vector3(-.37f, 1.15f, .27f);
        static readonly Vector3 ReadyBladeR = new Vector3(.3f, .9f, .6f), ReadyBladeL = new Vector3(-.25f, .85f, .6f);
        [MenuItem("Mismo/Animaciones/Crear y asignar ataques Dual Swords")]
        public static void CreateAndAssign()
        {
            GenerateMissing(); Assign(); VerifyAssignment(); CheckOrganization();
        }

        public static void GenerateBatch()
        {
            try { GenerateMissing(); RenderPreviews(); BuildContent(false); EditorApplication.Exit(0); }
            catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/FAILED.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void VerifyBatch()
        {
            try { VerifyAssignment(); BuildContent(true); EditorApplication.Exit(0); }
            catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/FAILED.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void GenerateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
            Directory.CreateDirectory(Output);
            HumanoidAttackAuthoring.EnsureFolder(Clips); HumanoidAttackAuthoring.EnsureFolder(Projects);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusHumanoidLocomotion.ModelPath);
            var report = new List<string>();
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
                        Author(recipe, i, rig, basePose);
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
                    ValidateMotion(recipe, clip, rig, report);
                }
            }
            File.WriteAllLines(Output + "/clips.txt", new[] { "PASS: seis clips Humanoid y sus proyectos editables" }.Concat(report));
        }

        static void Author(HumanoidAttackRecipe recipe, int attack, HumanoidAttackRig rig, HumanoidAttackPose basePose)
        {
            var poses = recipe.poses;
            void Pose(float t, string label, Vector3 right, Vector3 left, Vector3 bladeR, Vector3 bladeL, float twist = 0, float lean = 0, float yaw = 0)
            {
                var pose = basePose.Copy(t, label);
                Set(pose, "Chest Twist Left-Right", twist); Set(pose, "Spine Twist Left-Right", twist * .35f);
                Set(pose, "Chest Front-Back", lean); Set(pose, "Spine Front-Back", lean * .45f);
                Set(pose, "Head Turn Left-Right", -twist * .2f);
                for (int m = 0; m < pose.muscles.Length; m++)
                    if (HumanTrait.MuscleName[m].EndsWith("Stretched", StringComparison.Ordinal)) pose.muscles[m] = -.55f;
                rig.Apply(pose);
                SetArm(rig, HumanBodyBones.RightHand, right, bladeR, RightGrip, 1);
                SetArm(rig, HumanBodyBones.LeftHand, left, bladeL, LeftGrip, -1);
                pose = rig.Capture(label, t);
                pose.bodyRotation = Quaternion.AngleAxis(yaw, Vector3.up) * pose.bodyRotation;
                pose.blend = AttackPoseBlend.Linear;
                poses.Add(pose);
            }
            void Ready(float t, string label = "Guardia") => Pose(t, label, ReadyR, ReadyL, ReadyBladeR, ReadyBladeL);
            void RightLoad(float t) => Pose(t, "Cargar derecha", new Vector3(.55f, 1.55f, .05f), new Vector3(-.30f, 1.10f, .35f), new Vector3(.55f, .45f, -.8f), ReadyBladeL, -.28f, -.04f);
            void RightHit(float t) => Pose(t, "Corte derecho", new Vector3(.06f, 1.25f, .57f), new Vector3(-.40f, 1.20f, .15f), new Vector3(-.2f, -.15f, 1), ReadyBladeL, .18f, .08f);
            void RightFollow(float t) => Pose(t, "Salida derecha", new Vector3(-.34f, 1.04f, .38f), new Vector3(-.43f, 1.46f, .08f), new Vector3(-.85f, -.35f, .35f), new Vector3(-.45f, .5f, -.7f), .3f, .06f);
            void LeftLoad(float t) => Pose(t, "Cargar izquierda", new Vector3(.30f, 1.10f, .35f), new Vector3(-.55f, 1.5f, .05f), ReadyBladeR, new Vector3(-.55f, .5f, -.8f), .28f, -.04f);
            void LeftHit(float t) => Pose(t, "Corte izquierdo", new Vector3(.40f, 1.2f, .15f), new Vector3(-.06f, 1.20f, .57f), ReadyBladeR, new Vector3(.2f, -.12f, 1), -.18f, .08f);
            void LeftFollow(float t) => Pose(t, "Salida izquierda", new Vector3(.43f, 1.46f, .08f), new Vector3(.34f, 1.04f, .38f), new Vector3(.45f, .5f, -.7f), new Vector3(.85f, -.35f, .35f), -.3f, .06f);

            switch (attack)
            {
                case 0:
                    recipe.duration = 1; recipe.activeStartsAt = .20f; recipe.recoveryStartsAt = .60f;
                    Ready(0); RightLoad(.16f); RightHit(.30f); RightFollow(.44f);
                    Pose(.64f, "Recoger las dos hojas", new Vector3(.08f, 1.1f, .31f), new Vector3(-.34f, 1.25f, .23f), ReadyBladeR, ReadyBladeL, .1f);
                    Ready(.84f, "Volver a guardia"); Ready(1); break;
                case 1:
                    recipe.duration = 1; recipe.activeStartsAt = .20f; recipe.recoveryStartsAt = .66f;
                    Ready(0); LeftLoad(.16f); LeftHit(.28f); LeftFollow(.40f);
                    Pose(.49f, "Abrir ambas hojas", new Vector3(.46f, 1.50f, .20f), new Vector3(-.46f, 1.50f, .22f), new Vector3(.55f, .6f, .4f), new Vector3(-.55f, .6f, .4f));
                    Pose(.61f, "Remate cruzado", new Vector3(-.10f, 1.12f, .55f), new Vector3(.12f, 1.28f, .48f), new Vector3(-.55f, -.5f, .7f), new Vector3(.55f, -.4f, .7f), 0, .12f);
                    Ready(.88f, "Recuperación"); Ready(1); break;
                case 2:
                    recipe.duration = 1.1f; recipe.activeStartsAt = .1f / 1.1f; recipe.recoveryStartsAt = .9f / 1.1f;
                    Ready(0); RightLoad(.035f / 1.1f); RightHit(.1f / 1.1f); RightFollow(.16f / 1.1f);
                    LeftLoad(.235f / 1.1f); LeftHit(.3f / 1.1f); LeftFollow(.36f / 1.1f);
                    RightLoad(.435f / 1.1f); RightHit(.5f / 1.1f); RightFollow(.56f / 1.1f);
                    LeftLoad(.635f / 1.1f); LeftHit(.7f / 1.1f); LeftFollow(.78f / 1.1f);
                    Ready(.9f / 1.1f, "Recoger"); Ready(1); break;
                case 3:
                    recipe.duration = .65f; recipe.activeStartsAt = .1f / .65f; recipe.recoveryStartsAt = .45f / .65f;
                    Ready(0);
                    Pose(.08f / .65f, "Cargar giro", new Vector3(.2f, 1.28f, .28f), new Vector3(-.2f, 1.22f, .3f), new Vector3(-.6f, .3f, .7f), new Vector3(.6f, .3f, .7f), -.3f, .1f, -25);
                    for (int j = 0; j <= 8; j++)
                    {
                        float t = Mathf.Lerp(.1f, .45f, j / 8f) / .65f;
                        Pose(t, "Barrido " + j, new Vector3(.71f, 1.22f, .17f), new Vector3(-.71f, 1.30f, .12f), new Vector3(1, -.08f, .25f), new Vector3(-1, .08f, .25f), .05f, .08f, j * 45);
                    }
                    Ready(1, "Cerrar guardia"); break;
                case 4:
                    recipe.duration = .5f; recipe.activeStartsAt = .2f; recipe.recoveryStartsAt = .6f;
                    Ready(0); RightLoad(.08f);
                    Pose(.20f, "Corte de sangrado", new Vector3(.10f, 1.02f, .56f), new Vector3(-.40f, 1.38f, .17f), new Vector3(-.25f, -.4f, 1), ReadyBladeL, .18f, .09f);
                    Pose(.40f, "Arrastrar filo", new Vector3(-.32f, .99f, .36f), new Vector3(-.42f, 1.31f, .16f), new Vector3(-.85f, -.32f, .4f), ReadyBladeL, .30f, .08f);
                    Ready(.80f, "Recuperación"); Ready(1); break;
                default:
                    recipe.duration = .6f; recipe.activeStartsAt = .1f / .6f; recipe.recoveryStartsAt = .4f / .6f;
                    Ready(0);
                    Pose(.06f / .6f, "Abrir cruce", new Vector3(.48f, 1.32f, .12f), new Vector3(-.48f, 1.40f, .12f), new Vector3(.75f, .2f, .6f), new Vector3(-.75f, .2f, .6f), 0, -.04f);
                    Pose(.1f / .6f, "Cruce de entrada", new Vector3(-.08f, 1.18f, .57f), new Vector3(.08f, 1.37f, .50f), new Vector3(-.5f, -.12f, 1), new Vector3(.5f, -.10f, 1), 0, .12f);
                    Pose(.24f / .6f, "Atravesar", new Vector3(-.27f, 1.08f, .44f), new Vector3(.28f, 1.35f, .42f), new Vector3(-.8f, -.1f, .6f), new Vector3(.8f, -.1f, .6f), .08f, .14f);
                    Pose(.4f / .6f, "Separar hojas", new Vector3(.48f, 1.1f, .24f), new Vector3(-.48f, 1.25f, .24f), new Vector3(.7f, .2f, .5f), new Vector3(-.7f, .2f, .5f), 0, .06f);
                    Ready(1, "Recuperación"); break;
            }
            poses[0].blend = AttackPoseBlend.Smooth;
            poses[poses.Count - 2].blend = AttackPoseBlend.Smooth;
        }

        static void Set(HumanoidAttackPose pose, string muscle, float value)
        {
            int i = Array.IndexOf(HumanTrait.MuscleName, muscle);
            if (i >= 0) pose.muscles[i] = value;
        }

        static void SetArm(HumanoidAttackRig rig, HumanBodyBones hand, Vector3 destination, Vector3 bladeDirection, Vector3 grip, int side)
        {
            if (!HumanoidAttackIK.TryGetLimb(rig.Animator, hand, out var limb)) throw new InvalidOperationException("Falta el brazo " + hand);
            float scale = rig.Animator.humanScale;
            Vector3 pole = new Vector3(side * .9f, .95f, -.25f) * scale;
            HumanoidAttackIK.Solve(limb, destination * scale, pole, false);
            var bladeRotation = Quaternion.FromToRotation(Vector3.up, bladeDirection.normalized);
            limb.tip.rotation = bladeRotation * Quaternion.Inverse(Quaternion.Euler(grip));
        }

        static void ValidateMotion(HumanoidAttackRecipe recipe, AnimationClip clip, HumanoidAttackRig rig, List<string> report)
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
            if (rightTravel < .15f || leftTravel < .15f) throw new Exception("Una espada no participa: " + clip.name);
            float error = 0;
            foreach (var pose in recipe.poses)
            {
                rig.Apply(pose); Vector3 expected = rig.Animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                rig.Sample(clip, pose.time * clip.length); error = Mathf.Max(error, Vector3.Distance(expected, rig.Animator.GetBoneTransform(HumanBodyBones.RightHand).position));
            }
            if (error > .02f) throw new Exception("Pose exportada distinta: " + clip.name);
            report.Add($"PASS {clip.name}: {clip.length:F2}s, movimiento derecha {rightTravel:F2}m / izquierda {leftTravel:F2}m, error de pose {error:F5}m");
        }

        public static void Assign()
        {
            Directory.CreateDirectory(Output);
            var asset = AssetDatabase.LoadMainAssetAtPath(BindingPath);
            if (asset == null) throw new Exception("Falta DualSwordsCombatAnimations");
            var clips = Names.Select(n => AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/" + n + ".anim")).ToArray();
            if (clips.Any(c => c == null || !c.humanMotion)) throw new Exception("Generar los seis clips Humanoid antes de asignar.");
            var data = new SerializedObject(asset); data.Update();
            var actions = data.FindProperty("actions");
            var bindings = new SerializedProperty[5];
            for (int i = 0; i < actions.arraySize; i++)
            {
                var action = actions.GetArrayElementAtIndex(i);
                var ability = action.FindPropertyRelative("ability").objectReferenceValue;
                int index = ability != null ? Array.IndexOf(Abilities, ability.name) : -1;
                if (index >= 0) bindings[index] = action;
            }
            if (bindings.Any(p => p == null)) throw new Exception("Falta un binding de las habilidades de Dual Swords.");
            Undo.RecordObject(asset, "Asignar clips de Dual Swords");
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                int clipIndex = i == 0 ? 0 : i + 1;
                var recipe = AssetDatabase.LoadAssetAtPath<HumanoidAttackRecipe>(Projects + "/" + Names[clipIndex] + ".asset");
                if (i == 0)
                {
                    var combo = binding.FindPropertyRelative("comboClips"); combo.arraySize = 2;
                    combo.GetArrayElementAtIndex(0).objectReferenceValue = clips[0]; combo.GetArrayElementAtIndex(1).objectReferenceValue = clips[1];
                }
                else binding.FindPropertyRelative("clip").objectReferenceValue = clips[clipIndex];
                binding.FindPropertyRelative("activeStartsAt").floatValue = recipe.activeStartsAt;
                binding.FindPropertyRelative("recoveryStartsAt").floatValue = recipe.recoveryStartsAt;
                binding.FindPropertyRelative("torsoUprightDegrees").floatValue = 0;
                if (i == 2) binding.FindPropertyRelative("maskMode").enumValueIndex = 1; // Torbellino needs the hips/body rotation.
            }
            data.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(asset);
        }

        public static void VerifyAssignment()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(BindingPath);
            if (asset == null) throw new Exception("Falta el conjunto de animaciones Dual Swords.");
            var data = new SerializedObject(asset);
            var actions = data.FindProperty("actions");
            var found = new HashSet<string>();
            for (int i = 0; i < actions.arraySize; i++)
            {
                var binding = actions.GetArrayElementAtIndex(i);
                var ability = binding.FindPropertyRelative("ability").objectReferenceValue;
                int index = ability != null ? Array.IndexOf(Abilities, ability.name) : -1;
                if (index < 0) continue;
                if (!found.Add(ability.name)) throw new Exception("Binding duplicado: " + ability.name);
                int clipIndex = index == 0 ? 0 : index + 1;
                var recipe = AssetDatabase.LoadAssetAtPath<HumanoidAttackRecipe>(Projects + "/" + Names[clipIndex] + ".asset");
                if (recipe == null) throw new Exception("Falta el proyecto editable " + Names[clipIndex]);
                if (index == 0)
                {
                    var combo = binding.FindPropertyRelative("comboClips");
                    if (combo.arraySize != 2) throw new Exception("El combo debe tener dos clips.");
                    for (int step = 0; step < 2; step++)
                        if (AssetDatabase.GetAssetPath(combo.GetArrayElementAtIndex(step).objectReferenceValue) != Clips + "/" + Names[step] + ".anim")
                            throw new Exception("Etapa de combo incorrecta: " + step);
                }
                else if (AssetDatabase.GetAssetPath(binding.FindPropertyRelative("clip").objectReferenceValue) != Clips + "/" + Names[clipIndex] + ".anim")
                    throw new Exception("Clip incorrecto: " + ability.name);
                if (!Mathf.Approximately(binding.FindPropertyRelative("activeStartsAt").floatValue, recipe.activeStartsAt) ||
                    !Mathf.Approximately(binding.FindPropertyRelative("recoveryStartsAt").floatValue, recipe.recoveryStartsAt))
                    throw new Exception("Fases incorrectas: " + ability.name);
                if (index == 2 && binding.FindPropertyRelative("maskMode").enumValueIndex != 1)
                    throw new Exception("Torbellino necesita máscara de cuerpo completo.");
            }
            if (found.Count != Abilities.Length) throw new Exception("Faltan habilidades de Dual Swords.");
            var dependencies = AssetDatabase.GetDependencies(BindingPath, true);
            foreach (var name in Names)
                if (!dependencies.Contains(Clips + "/" + name + ".anim")) throw new Exception("No está conectado: " + name);
            File.WriteAllLines(Output + "/assignment.txt", new[] { "PASS dos etapas de combo y cuatro habilidades asignadas a sus clips.", "PASS fases de reproducción coinciden con los proyectos editables.", "PASS máscara de cuerpo completo para Torbellino." }.Concat(Names));
        }

        static void CheckOrganization()
        {
            try
            {
                var check = Type.GetType("ProjectOrganizationChecks, Assembly-CSharp-Editor")?.GetMethod("Run");
                if (check == null) throw new Exception("ProjectOrganizationChecks.Run no está disponible en este proyecto.");
                check.Invoke(null, null);
                File.WriteAllText(Output + "/organization.txt", "PASS ProjectOrganizationChecks.Run");
            }
            catch (Exception e) { File.WriteAllText(Output + "/organization.txt", "FAIL\n" + (e.InnerException ?? e)); }
        }

        public static void BuildContent(bool includeFamily)
        {
            Directory.CreateDirectory(Output + "/content-build");
            string[] assets = includeFamily ? new[] { BindingPath } : Names.Select(n => Clips + "/" + n + ".anim").ToArray();
            var manifest = BuildPipeline.BuildAssetBundles(Output + "/content-build", new[] { new AssetBundleBuild { assetBundleName = "dual-swords", assetNames = assets } }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new Exception("Falló la build de contenido Dual Swords.");
            var bundle = AssetBundle.LoadFromFile(Output + "/content-build/dual-swords");
            if (bundle == null) throw new Exception("No se pudo leer el contenido construido.");
            try
            {
                IEnumerable<AnimationClip> loaded = bundle.LoadAllAssets<AnimationClip>();
                if (includeFamily)
                {
                    var family = bundle.LoadAsset<Mismo.Gameplay.Player.Presentation.WeaponAnimationSet>(BindingPath);
                    if (family == null) throw new Exception("Falta el conjunto de la familia en la build.");
                    loaded = family.actions.SelectMany(a => a.comboClips.Length > 0 ? a.comboClips : new[] { a.clip });
                }
                if (loaded.Count(c => c.humanMotion && Names.Contains(c.name)) != Names.Length) throw new Exception("Clips ausentes o inválidos en la build.");
                File.WriteAllText(Output + "/build.txt", "PASS build Windows y carga de los seis clips Humanoid" + (includeFamily ? " a través del conjunto de la familia." : " (bundle de clips; vinculación se verifica aparte)."));
            }
            finally { bundle.Unload(true); }
        }

        static void RenderPreviews()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusHumanoidLocomotion.ModelPath);
            var bodyMaterial = new Material(Shader.Find("Standard")) { color = new Color(.48f, .58f, .68f) };
            var rightMaterial = new Material(Shader.Find("Standard")) { color = new Color(.95f, .65f, .2f) };
            var leftMaterial = new Material(Shader.Find("Standard")) { color = new Color(.2f, .85f, .8f) };
            var preview = new PreviewRenderUtility();
            var bakedMeshes = new List<Mesh>();
            using (var rig = new HumanoidAttackRig(model))
            try
            {
                preview.AddSingleGO(rig.Container);
                foreach (var renderer in rig.Container.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => bodyMaterial).ToArray();
                // Batch renders happen within one editor frame; take a fresh CPU skin snapshot per pose.
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
                var rightSword = AddPreviewSword(rig, "Assets/Art/Prefabs/Weapons/sword/Sword.prefab", rightMaterial);
                var leftSword = AddPreviewSword(rig, "Assets/Art/Prefabs/Weapons/sword_E.prefab", leftMaterial);
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.055f, .065f, .08f);
                preview.camera.orthographic = true; preview.camera.orthographicSize = 1.50f; preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 50;
                preview.camera.transform.position = new Vector3(3.6f, 2.4f, 6); preview.camera.transform.LookAt(new Vector3(0, 1.02f, 0));
                preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(35, -30, 0); preview.lights[1].intensity = .8f;
                foreach (string name in Names)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/" + name + ".anim");
                    var recipe = AssetDatabase.LoadAssetAtPath<HumanoidAttackRecipe>(Projects + "/" + name + ".asset");
                    var sheet = new Texture2D(320 * 5, 440, TextureFormat.RGB24, false);
                    try
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            float t = recipe.poses[Mathf.RoundToInt((recipe.poses.Count - 1) * i / 4f)].time;
                            rig.Sample(clip, t * clip.length);
                            for (int s = 0; s < skins.Length; s++) skins[s].BakeMesh(bakedMeshes[s]);
                            ApplyPreviewWeapon(rightSword, rig.Animator.GetBoneTransform(HumanBodyBones.RightHand), new Vector3(.31893185f, .6028334f, .12894957f), RightGrip);
                            ApplyPreviewWeapon(leftSword, rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand), new Vector3(-.385101f, .3266207f, 0), LeftGrip);
                            preview.BeginStaticPreview(new Rect(0, 0, 320, 440)); preview.Render(); var frame = preview.EndStaticPreview();
                            sheet.SetPixels(i * 320, 0, 320, 440, frame.GetPixels()); Object.DestroyImmediate(frame);
                        }
                        sheet.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", sheet.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(sheet); }
                }
            }
            finally { preview.Cleanup(); foreach (var mesh in bakedMeshes) Object.DestroyImmediate(mesh); Object.DestroyImmediate(bodyMaterial); Object.DestroyImmediate(rightMaterial); Object.DestroyImmediate(leftMaterial); }
        }

        static GameObject AddPreviewSword(HumanoidAttackRig rig, string path, Material material)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new Exception("Falta la espada para verificar la vista: " + path);
            var sword = Object.Instantiate(prefab, rig.Container.transform);
            foreach (var behaviour in sword.GetComponentsInChildren<Behaviour>()) behaviour.enabled = false;
            foreach (var collider in sword.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var renderer in sword.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            return sword;
        }
        static void ApplyPreviewWeapon(GameObject sword, Transform hand, Vector3 offset, Vector3 rotation)
        {
            sword.transform.SetPositionAndRotation(hand.position + hand.rotation * offset, hand.rotation * Quaternion.Euler(rotation));
            sword.transform.localScale = Vector3.one;
        }
    }
}
