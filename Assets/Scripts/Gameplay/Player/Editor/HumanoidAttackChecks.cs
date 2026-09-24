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
    public static class HumanoidAttackChecks
    {
        const string Output = "output/humanoid-attacks";
        const string ImpPath = "Assets/Art/FBX/Monsters/Bestiary - Dungeon Monsters Kit[Standard]/Bestiary - Dungeon Monsters Kit[Standard]/Imp.fbx";
        static readonly List<string> Report = new List<string>();
        static double nextPoll;

        [InitializeOnLoadMethod]
        static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll = EditorApplication.timeSinceStartup + 2;
            const string request = "Temp/HumanoidAttackChecks.request";
            if (!File.Exists(request)) return;
            File.Delete(request); Run();
        }

        [MenuItem("Mismo/Animaciones/Verificar creador de ataques Humanoid")]
        public static void Run() => RunChecks(true);

        static void RunChecks(bool checkOrganization)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode para comprobar el creador.");
            Directory.CreateDirectory(Output); Report.Clear();
            var scene = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusHumanoidLocomotion.ModelPath);
                var imp = AssetDatabase.LoadAssetAtPath<GameObject>(ImpPath);
                Check(model != null && imp != null, "Dos rigs de prueba disponibles: aventurero e Imp");
                string original = File.ReadAllText(QuaterniusHumanoidLocomotion.ModelPath + ".meta");
                using (var rig = new HumanoidAttackRig(model))
                using (var target = new HumanoidAttackRig(imp))
                {
                    var recipe = ScriptableObject.CreateInstance<HumanoidAttackRecipe>();
                    try
                    {
                        recipe.model = model;
                        foreach (HumanoidAttackTemplate template in Enum.GetValues(typeof(HumanoidAttackTemplate)))
                        {
                            HumanoidAttackAuthoring.CreateTemplate(recipe, rig.Neutral, template, false);
                            var clip = HumanoidAttackAuthoring.Bake(recipe);
                            try
                            {
                                ValidateClip(recipe, clip, rig, target, template);
                                if (template == HumanoidAttackTemplate.HorizontalSlash) CheckPersistence(recipe, clip, target);
                            }
                            finally { Object.DestroyImmediate(clip); }
                        }
                        CheckAuthoring(recipe, rig);
                        CheckIK(recipe, rig, "aventurero");
                        CheckIK(recipe, target, "Imp");
                        CheckWeapons(recipe, rig);
                        CheckNestedModel(model, recipe);
                    }
                    finally { Object.DestroyImmediate(recipe); }
                }
                Check(File.ReadAllText(QuaterniusHumanoidLocomotion.ModelPath + ".meta") == original, "Importador y Avatar originales intactos");
                var after = EditorSceneManager.GetSceneManagerSetup();
                Check(scene.Length == after.Length && scene.Zip(after, (a, b) => a.path == b.path && a.isLoaded == b.isLoaded && a.isActive == b.isActive).All(v => v), "Escenas abiertas conservadas");
                if (StageUtility.GetCurrentStage() == StageUtility.GetMainStage()) CheckWindow(model);
                else Report.Add("INFO Ciclo de ventana omitido para conservar la vista de edición abierta.");
                if (checkOrganization)
                {
                    var organization = Type.GetType("ProjectOrganizationChecks, Assembly-CSharp-Editor");
                    if (organization == null) throw new InvalidOperationException("No se encontró ProjectOrganizationChecks");
                    organization.GetMethod("Run").Invoke(null, null);
                    Check(true, "ProjectOrganizationChecks.Run");
                }
                else Report.Add("INFO Organización global no evaluada: proyecto aislado con fixtures de animación.");
                File.WriteAllLines(Output + "/checks.txt", new[] { "PASS" }.Concat(Report));
                Debug.Log("HUMANOID_ATTACK_CHECKS_OK\n" + string.Join("\n", Report));
            }
            catch (Exception e)
            {
                File.WriteAllLines(Output + "/checks.txt", new[] { "FAIL", e.ToString() }.Concat(Report));
                Debug.LogException(e); throw;
            }
        }

        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch { EditorApplication.Exit(1); }
        }

        public static void RunIsolatedBatch()
        {
            try { RunChecks(false); EditorApplication.Exit(0); }
            catch { EditorApplication.Exit(1); }
        }

        static void CheckWindow(GameObject model)
        {
            var window = ScriptableObject.CreateInstance<HumanoidAttackWindow>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(HumanoidAttackWindow);
            try
            {
                var draft = (HumanoidAttackRecipe)type.GetField("draft", flags).GetValue(window);
                draft.model = model;
                draft.previewWeapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
                draft.previewPair = true;
                Check(draft.previewWeapon != null, "Ventana: definición de arma disponible para la vista");
                type.GetMethod("OpenPreview", flags).Invoke(window, null);
                Check(draft.poses.Count == 5, "Ventana abre el personaje y genera cinco poses editables");
                type.GetMethod("Preview", flags).Invoke(window, new object[] { .46f });
                Check((bool)type.GetField("previewing", flags).GetValue(window), "Ventana reproduce el clip exportable");
                type.GetMethod("ApplySelected", flags).Invoke(window, null);
                Check(!(bool)type.GetField("previewing", flags).GetValue(window), "Ventana vuelve de reproducción a edición de huesos");
                var previewRig = (HumanoidAttackRig)type.GetField("rig", flags).GetValue(window);
                var weapons = (HumanoidAttackWeapons)type.GetField("weapons", flags).GetValue(window);
                Check(weapons.Main != null, "Ventana: muestra el arma del proyecto");
                if (draft.previewWeapon.poseProfile != null)
                    CheckAttachment(weapons.Main, draft.previewWeapon.poseProfile.equipped, previewRig, "ventana después de reproducir y editar");
                HumanoidAttackIK.TryGetLimb(previewRig.Animator, HumanBodyBones.RightHand, out var limb);
                Vector3 destination = Vector3.Lerp(limb.upper.position, limb.tip.position, .85f) + Vector3.forward * .04f;
                int poseIndex = (int)type.GetField("selectedPose", flags).GetValue(window);
                string beforeDrag = EditorJsonUtility.ToJson(draft);
                Undo.IncrementCurrentGroup();
                type.GetMethod("MoveIk", flags).Invoke(window, new object[] { (int)HumanBodyBones.RightHand, destination });
                Undo.FlushUndoRecordObjects();
                Check(Vector3.Distance(limb.tip.position, destination) < .015f, "Ventana arrastra la mano y captura la pose Humanoid");
                if (draft.previewWeapon.poseProfile != null)
                    CheckAttachment(weapons.Main, draft.previewWeapon.poseProfile.equipped, previewRig, "ventana actualiza el arma al arrastrar");
                var authored = draft.poses[poseIndex].Copy(0);
                Undo.PerformUndo();
                Check(EditorJsonUtility.ToJson(draft) == beforeDrag, "Deshacer arrastre IK restaura la pose anterior");
                Undo.PerformRedo();
                Check(draft.poses[poseIndex].muscles.SequenceEqual(authored.muscles), "Rehacer arrastre IK recupera la pose");
                draft.poses.Clear();
                type.GetMethod("OnUndo", flags).Invoke(window, null);
                Check(type.GetField("rig", flags).GetValue(window) == null, "Deshacer hasta un proyecto vacío cierra la vista sin índices inválidos");
                type.GetMethod("OpenPreview", flags).Invoke(window, null);
                type.GetMethod("ClosePreview", flags).Invoke(window, null);
                Check(type.GetField("rig", flags).GetValue(window) == null, "Cerrar la vista libera la copia del personaje");
                CheckDocumentLoad(window, model);
            }
            finally { window.DiscardChanges(); Object.DestroyImmediate(window); }
        }

        static void CheckDocumentLoad(HumanoidAttackWindow window, GameObject model)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(HumanoidAttackWindow);
            var recipe = ScriptableObject.CreateInstance<HumanoidAttackRecipe>();
            string path = AssetDatabase.GenerateUniqueAssetPath(HumanoidAttackAuthoring.RecipeFolder + "/DocumentLoadCheck.asset");
            try
            {
                recipe.model = model; recipe.clipName = "Ataque para recuperar"; recipe.duration = 1.73f;
                recipe.previewWeapon = recipe.previewOffhand = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
                recipe.previewPair = true;
                using (var source = new HumanoidAttackRig(model)) HumanoidAttackAuthoring.CreateTemplate(recipe, source.Neutral, HumanoidAttackTemplate.Thrust, false);
                AssetDatabase.CreateAsset(recipe, path); AssetDatabase.SaveAssetIfDirty(recipe);
                type.GetMethod("OpenPreview", flags).Invoke(window, null);
                window.DiscardChanges();
                type.GetMethod("LoadRecipe", flags).Invoke(window, new object[] { AssetDatabase.LoadAssetAtPath<HumanoidAttackRecipe>(path) });
                var restored = (HumanoidAttackRecipe)type.GetField("draft", flags).GetValue(window);
                Check(restored.clipName == "Ataque para recuperar" && Mathf.Approximately(restored.duration, 1.73f) && restored.poses.Count == 5 &&
                    restored.previewWeapon != null && restored.previewOffhand == restored.previewWeapon && restored.previewPair,
                    "Abrir proyecto desde la vista conserva poses, duración y ambas armas");
                string guid = AssetDatabase.AssetPathToGUID(path);
                restored.clipName = "Edición recién guardada"; restored.duration = 2.19f;
                restored.poses[1].muscles[0] = .31f;
                restored.previewPair = false; restored.showPreviewWeapons = false;
                Check((bool)type.GetMethod("SaveRecipe", flags).Invoke(window, new object[] { false }), "Guardar cambios tras cargar desde la vista");
                type.GetMethod("OpenPreview", flags).Invoke(window, null);
                restored.duration = .3f;
                type.GetMethod("LoadRecipe", flags).Invoke(window, new object[] { AssetDatabase.LoadAssetAtPath<HumanoidAttackRecipe>(path) });
                restored = (HumanoidAttackRecipe)type.GetField("draft", flags).GetValue(window);
                Check(restored.clipName == "Edición recién guardada" && Mathf.Approximately(restored.duration, 2.19f) &&
                    Mathf.Approximately(restored.poses[1].muscles[0], .31f) && !restored.previewPair && !restored.showPreviewWeapons &&
                    AssetDatabase.AssetPathToGUID(path) == guid,
                    "Reabrir recupera la última configuración guardada sin duplicar el asset");
                var invalid = ScriptableObject.CreateInstance<HumanoidAttackRecipe>(); Object.DestroyImmediate(invalid);
                bool rejected = false;
                try { type.GetMethod("LoadRecipe", flags).Invoke(window, new object[] { invalid }); }
                catch (System.Reflection.TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
                Check(rejected && Mathf.Approximately(restored.duration, 2.19f), "Referencia destruida se rechaza sin perder la configuración actual");
                Object.DestroyImmediate(restored);
                type.GetMethod("EnsureDraft", flags).Invoke(window, null);
                restored = (HumanoidAttackRecipe)type.GetField("draft", flags).GetValue(window);
                Check(restored != null && !EditorUtility.IsPersistent(restored) && restored.previewWeapon != null &&
                    Mathf.Approximately(restored.duration, 2.19f), "Borrador descargado se recupera desde el proyecto guardado");
            }
            finally
            {
                window.DiscardChanges();
                type.GetMethod("ClosePreview", flags).Invoke(window, null);
                AssetDatabase.DeleteAsset(path);
                if (recipe != null && !AssetDatabase.Contains(recipe)) Object.DestroyImmediate(recipe);
            }
        }

        static void ValidateClip(HumanoidAttackRecipe recipe, AnimationClip clip, HumanoidAttackRig rig, HumanoidAttackRig target, HumanoidAttackTemplate template)
        {
            Check(clip.humanMotion && !clip.legacy && Mathf.Abs(clip.length - recipe.duration) < .0001f, template + ": clip Humanoid y duración exacta");
            var bindings = AnimationUtility.GetCurveBindings(clip);
            Check(bindings.All(b => b.type == typeof(Animator) && b.path == "") && bindings.Length == HumanTrait.MuscleCount + 35, template + ": músculos, cuerpo y cuatro objetivos IK sin rutas de huesos");
            Check(!AnimationUtility.GetAnimationClipSettings(clip).loopTime && AnimationUtility.GetAnimationEvents(clip).Length == 0, template + ": sin bucle ni eventos de daño");
            foreach (var binding in bindings)
                if (AnimationUtility.GetEditorCurve(clip, binding).keys.Any(k => float.IsNaN(k.value) || float.IsInfinity(k.value))) throw new Exception("Curva no finita: " + binding.propertyName);
            float maxError = 0;
            var tracked = new[] { HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
            foreach (var pose in recipe.poses)
            {
                rig.Apply(pose);
                var expected = tracked.Select(b => rig.Animator.GetBoneTransform(b).position).ToArray();
                rig.Sample(clip, pose.time * recipe.duration);
                for (int i = 0; i < tracked.Length; i++)
                    maxError = Mathf.Max(maxError, Vector3.Distance(expected[i], rig.Animator.GetBoneTransform(tracked[i]).position));
            }
            Check(maxError < .08f, template + $": poses conservadas al reproducir (error máximo {maxError:F4}m)");
            target.Sample(clip, 0);
            var arm = target.Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Quaternion initial = arm.localRotation;
            float travel = 0;
            for (int frame = 0; frame <= 30; frame++)
            {
                target.Sample(clip, recipe.duration * frame / 30);
                travel = Mathf.Max(travel, Quaternion.Angle(initial, arm.localRotation));
                foreach (var bone in tracked)
                {
                    var p = target.Animator.GetBoneTransform(bone).position;
                    if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) throw new Exception("Retargeting inválido");
                }
            }
            Check(template == HumanoidAttackTemplate.Blank || travel > 15, template + $": reproducción en Imp, brazo recorre {travel:F1}°");
        }

        static void CheckIK(HumanoidAttackRecipe recipe, HumanoidAttackRig rig, string label)
        {
            var savedModel = recipe.model;
            try
            {
                foreach (var end in HumanoidAttackIK.Endpoints)
                {
                    rig.Apply(rig.Neutral);
                    Check(HumanoidAttackIK.TryGetLimb(rig.Animator, end, out var limb), label + ": cadena IK de " + end);
                    float first = Vector3.Distance(limb.upper.position, limb.lower.position);
                    float second = Vector3.Distance(limb.lower.position, limb.tip.position);
                    Vector3 upperLocal = limb.upper.localPosition, lowerLocal = limb.lower.localPosition, tipLocal = limb.tip.localPosition;
                    Vector3 origin = limb.upper.position;
                    Vector3 head = rig.Animator.GetBoneTransform(HumanBodyBones.Head).position;
                    Quaternion rotation = limb.tip.rotation;
                    Vector3 target = Vector3.Lerp(origin, limb.tip.position, .8f) + Vector3.forward * limb.Length * .08f;
                    Vector3 hint = limb.HintPosition();
                    Check(HumanoidAttackIK.Solve(limb, target, hint), label + " " + end + ": solver acepta destino alcanzable");
                    Check(Vector3.Distance(limb.tip.position, target) < .001f && Quaternion.Angle(limb.tip.rotation, rotation) < .1f,
                        label + " " + end + ": alcanza destino y conserva orientación");
                    Vector3 oldBend = limb.lower.position;
                    Vector3 axis = (target - origin).normalized;
                    Vector3 direction = Vector3.ProjectOnPlane(hint - origin, axis);
                    Vector3 newHint = origin + Quaternion.AngleAxis(55, axis) * direction;
                    HumanoidAttackIK.Solve(limb, target, newHint);
                    Check(Vector3.Distance(limb.lower.position, oldBend) > .01f && Vector3.Distance(limb.tip.position, target) < .001f,
                        label + " " + end + ": control de codo/rodilla cambia el plano sin desplazar el extremo");
                    Check(Mathf.Abs(Vector3.Distance(limb.upper.position, limb.lower.position) - first) < .0001f &&
                        Mathf.Abs(Vector3.Distance(limb.lower.position, limb.tip.position) - second) < .0001f &&
                        limb.upper.localPosition == upperLocal && limb.lower.localPosition == lowerLocal && limb.tip.localPosition == tipLocal &&
                        Vector3.Distance(rig.Animator.GetBoneTransform(HumanBodyBones.Head).position, head) < .0001f,
                        label + " " + end + ": huesos conservan longitud y no desplazan torso");
                    var captured = rig.Capture("IK", .5f);
                    rig.Apply(captured);
                    Check(Vector3.Distance(limb.tip.position, target) < .015f, label + " " + end + ": arrastre sobrevive a captura muscular");
                    recipe.poses = new List<HumanoidAttackPose> { captured.Copy(0), captured.Copy(1) };
                    var clip = HumanoidAttackAuthoring.Bake(recipe, rig);
                    try
                    {
                        rig.Sample(clip, .5f * recipe.duration);
                        Check(clip.humanMotion && Vector3.Distance(limb.tip.position, target) < .015f, label + " " + end + ": clip exportado conserva el destino IK");
                    }
                    finally { rig.StopSampling(); Object.DestroyImmediate(clip); }
                    foreach (var extreme in new[] { origin, origin + Vector3.forward * limb.Length * 20, origin - axis * limb.Length })
                    {
                        HumanoidAttackIK.Solve(limb, extreme, origin);
                        Check(float.IsFinite(limb.tip.position.x) && float.IsFinite(limb.tip.position.y) && float.IsFinite(limb.tip.position.z) &&
                            Mathf.Abs(Vector3.Distance(limb.upper.position, limb.lower.position) - first) < .0001f &&
                            Mathf.Abs(Vector3.Distance(limb.lower.position, limb.tip.position) - second) < .0001f,
                            label + " " + end + ": destino extremo se limita sin estirar ni valores inválidos");
                    }
                }
            }
            finally { recipe.model = savedModel; }
        }

        static void CheckAuthoring(HumanoidAttackRecipe recipe, HumanoidAttackRig rig)
        {
            HumanoidAttackAuthoring.CreateTemplate(recipe, rig.Neutral, HumanoidAttackTemplate.HorizontalSlash, false);
            var original = recipe.poses[1].Copy(recipe.poses[1].time);
            HumanoidAttackAuthoring.Mirror(recipe.poses[1]); HumanoidAttackAuthoring.Mirror(recipe.poses[1]);
            Check(original.muscles.SequenceEqual(recipe.poses[1].muscles) && Quaternion.Angle(original.bodyRotation, recipe.poses[1].bodyRotation) < .01f, "Espejar dos veces restaura la pose");
            var mid = HumanoidAttackAuthoring.Evaluate(recipe, .4f);
            Check(mid.muscles.Where((m, i) => Mathf.Abs(m - recipe.poses[1].muscles[i]) > .01f).Any(), "Interpolación produce poses intermedias");
            rig.Apply(recipe.poses[1]);
            var arm = rig.Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            arm.localRotation *= Quaternion.Euler(0, 0, 15);
            var edited = rig.Capture("Edición", .3f);
            Check(edited.muscles.Where((m, i) => Mathf.Abs(m - recipe.poses[1].muscles[i]) > .01f).Any(), "Rotar hueso cambia músculos capturados");
            var hips = rig.Animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 previous = edited.bodyPosition;
            hips.position += Vector3.up * .1f;
            var moved = rig.Capture("Cadera", .3f);
            Check(moved.bodyPosition.y > previous.y + .01f, "Mover cadera se conserva en el cuerpo Humanoid");
            float savedTime = recipe.poses[1].time; recipe.poses[1].time = 0;
            ExpectInvalid(recipe, "Rechaza tiempos duplicados"); recipe.poses[1].time = savedTime;
            float savedMuscle = recipe.poses[1].muscles[0]; recipe.poses[1].muscles[0] = float.NaN;
            ExpectInvalid(recipe, "Rechaza poses no finitas"); recipe.poses[1].muscles[0] = savedMuscle;
            var invalid = new GameObject("Rig inválido");
            try { Check(HumanoidAttackRig.ValidateModel(invalid) != null, "Rechaza modelos sin Avatar Humanoid"); }
            finally { Object.DestroyImmediate(invalid); }
        }

        static void CheckWeapons(HumanoidAttackRecipe recipe, HumanoidAttackRig rig)
        {
            var main = ScriptableObject.CreateInstance<WeaponDefinition>();
            var offhand = ScriptableObject.CreateInstance<WeaponDefinition>();
            var profile = ScriptableObject.CreateInstance<WeaponPoseProfile>();
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prefab, rig.Container.scene);
            prefab.name = "Arma de prueba"; prefab.SetActive(false);
            var audio = prefab.AddComponent<AudioSource>(); audio.playOnAwake = false;
            var originalWeapon = recipe.previewWeapon; var originalOffhand = recipe.previewOffhand;
            bool originalPair = recipe.previewPair, originalVisible = recipe.showPreviewWeapons;
            try
            {
                main.visualPrefab = offhand.visualPrefab = prefab;
                main.poseProfile = profile;
                profile.equipped.offset = new Vector3(.12f, .02f, -.06f);
                profile.equipped.rotation = new Vector3(25, 10, 45); profile.equipped.scale = .42f;
                offhand.secondaryEquipped = new WeaponAttachmentPose { anchor = WeaponAnchor.LeftHand, offset = new Vector3(-.06f, .04f, .08f), rotation = new Vector3(-15, 35, 80), scale = .3f };
                recipe.previewWeapon = main; recipe.previewOffhand = offhand; recipe.previewPair = true; recipe.showPreviewWeapons = true;
                HumanoidAttackAuthoring.CreateTemplate(recipe, rig.Neutral, HumanoidAttackTemplate.HorizontalSlash, false);
                string mainBefore = EditorJsonUtility.ToJson(main), profileBefore = EditorJsonUtility.ToJson(profile);
                using (var weapons = new HumanoidAttackWeapons(rig))
                {
                    rig.Apply(recipe.poses[1]); weapons.Refresh(recipe);
                    Check(weapons.Main != null && weapons.Offhand != null && weapons.Main != weapons.Offhand, "Armas: dos copias independientes en la vista");
                    CheckAttachment(weapons.Main, profile.equipped, rig, "agarre principal del taller");
                    CheckAttachment(weapons.Offhand, offhand.secondaryEquipped, rig, "agarre propio de la secundaria");
                    Check(!weapons.Main.GetComponent<Collider>().enabled && !weapons.Main.GetComponent<AudioSource>().enabled &&
                        prefab.GetComponent<Collider>().enabled && audio.enabled, "Armas: desactiva componentes sólo en las copias");
                    var clip = HumanoidAttackAuthoring.Bake(recipe);
                    try
                    {
                        foreach (float t in new[] { 0f, .3f, .46f, .8f, 1f })
                        {
                            rig.Sample(clip, t * clip.length); weapons.Refresh(recipe);
                            CheckAttachment(weapons.Main, profile.equipped, rig, "reproducción principal " + t);
                            CheckAttachment(weapons.Offhand, offhand.secondaryEquipped, rig, "reproducción secundaria " + t);
                        }
                    }
                    finally { rig.StopSampling(); Object.DestroyImmediate(clip); }
                    rig.Apply(recipe.poses[1]);
                    HumanoidAttackIK.TryGetLimb(rig.Animator, HumanBodyBones.RightHand, out var limb);
                    HumanoidAttackIK.Solve(limb, Vector3.Lerp(limb.upper.position, limb.tip.position, .8f), limb.HintPosition());
                    weapons.Refresh(recipe); CheckAttachment(weapons.Main, profile.equipped, rig, "sigue el arrastre IK");
                    Check(EditorJsonUtility.ToJson(main) == mainBefore && EditorJsonUtility.ToJson(profile) == profileBefore, "Armas: no modifica definición ni perfil de agarre");
                    var hidden = rig.Animator.GetComponentInChildren<Renderer>();
                    profile.hiddenRendererPaths = new[] { AnimationUtility.CalculateTransformPath(hidden.transform, rig.Animator.transform) };
                    weapons.Refresh(recipe); Check(!hidden.enabled, "Armas: oculta el visual integrado del personaje según el perfil");
                    recipe.showPreviewWeapons = false; weapons.Refresh(recipe);
                    Check(!weapons.Main.activeInHierarchy && !weapons.Offhand.activeInHierarchy && hidden.enabled, "Armas: ocultar restaura el personaje y esconde ambas piezas");
                    recipe.showPreviewWeapons = true; recipe.previewOffhand = null; weapons.Refresh(recipe);
                    CheckAttachment(weapons.Offhand, main.secondaryEquipped, rig, "segunda copia con agarre secundario de la principal");
                    var removed = weapons.Offhand;
                    main.isTwoHanded = true; weapons.Refresh(recipe);
                    Check(weapons.Main != null && weapons.Offhand == null && removed == null, "Armas: a dos manos muestra una sola pieza y libera la segunda");
                    profile.equipped.anchor = WeaponAnchor.BonePath; profile.equipped.bonePath = "HuesoQueNoExiste";
                    weapons.Refresh(recipe);
                    Check(!weapons.Main.activeSelf && !string.IsNullOrEmpty(weapons.Warning), "Armas: anclaje incompatible avisa y oculta el visual");
                    recipe.previewWeapon = null; weapons.Refresh(recipe);
                    Check(weapons.Main == null && weapons.Offhand == null && hidden.enabled, "Armas: quitar selección limpia copias y restaura visibilidad");
                }
            }
            finally
            {
                recipe.previewWeapon = originalWeapon; recipe.previewOffhand = originalOffhand;
                recipe.previewPair = originalPair; recipe.showPreviewWeapons = originalVisible;
                Object.DestroyImmediate(main); Object.DestroyImmediate(offhand); Object.DestroyImmediate(profile); Object.DestroyImmediate(prefab);
            }
        }

        static void CheckAttachment(GameObject visual, WeaponAttachmentPose pose, HumanoidAttackRig rig, string label)
        {
            var anchor = pose.Resolve(rig.Animator.transform, rig.Animator);
            var rotation = pose.Orientation(anchor, rig.Animator);
            Check(visual.activeInHierarchy && Vector3.Distance(visual.transform.position, anchor.position + rotation * pose.offset) < .0001f &&
                Quaternion.Angle(visual.transform.rotation, rotation * Quaternion.Euler(pose.rotation)) < .05f &&
                Vector3.Distance(visual.transform.lossyScale, Vector3.one * pose.scale) < .0001f, "Armas: " + label);
        }

        static void CheckPersistence(HumanoidAttackRecipe recipe, AnimationClip clip, HumanoidAttackRig target)
        {
            HumanoidAttackAuthoring.EnsureFolder(HumanoidAttackAuthoring.ClipFolder);
            HumanoidAttackAuthoring.EnsureFolder(HumanoidAttackAuthoring.RecipeFolder);
            string clipPath = AssetDatabase.GenerateUniqueAssetPath(HumanoidAttackAuthoring.ClipFolder + "/AuthoringCheck.anim");
            string recipePath = AssetDatabase.GenerateUniqueAssetPath(HumanoidAttackAuthoring.RecipeFolder + "/AuthoringCheck.asset");
            var savedClip = Object.Instantiate(clip); var savedRecipe = Object.Instantiate(recipe);
            try
            {
                savedRecipe.previewWeapon = savedRecipe.previewOffhand = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
                savedRecipe.previewPair = true; savedRecipe.showPreviewWeapons = false;
                Check(savedRecipe.previewWeapon != null, "Armas: definición disponible para comprobar persistencia");
                AssetDatabase.CreateAsset(savedClip, clipPath); AssetDatabase.CreateAsset(savedRecipe, recipePath);
                AssetDatabase.SaveAssetIfDirty(savedClip); AssetDatabase.SaveAssetIfDirty(savedRecipe);
                AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(recipePath, ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                var document = AssetDatabase.LoadAssetAtPath<HumanoidAttackRecipe>(recipePath);
                Check(reloaded.humanMotion && document.poses.Count == 5 && document.model == recipe.model, "Clip y proyecto sobreviven a guardado y reimportación");
                Check(document.previewWeapon == savedRecipe.previewWeapon && document.previewOffhand == document.previewWeapon && document.previewPair && !document.showPreviewWeapons,
                    "Armas: selección de ambas manos y visibilidad sobreviven al guardado");
                target.Sample(reloaded, recipe.duration * .46f); target.StopSampling();
                Check(AssetDatabase.GenerateUniqueAssetPath(clipPath) != clipPath, "Exportación no sobrescribe clips existentes");
            }
            finally
            {
                AssetDatabase.DeleteAsset(clipPath); AssetDatabase.DeleteAsset(recipePath);
                if (savedClip != null && !AssetDatabase.Contains(savedClip)) Object.DestroyImmediate(savedClip);
                if (savedRecipe != null && !AssetDatabase.Contains(savedRecipe)) Object.DestroyImmediate(savedRecipe);
            }
        }

        static void CheckNestedModel(GameObject model, HumanoidAttackRecipe recipe)
        {
            var wrapper = new GameObject("Contenedor con offset");
            wrapper.SetActive(false);
            try
            {
                Object.Instantiate(model, wrapper.transform);
                wrapper.transform.SetPositionAndRotation(new Vector3(7, 3, -4), Quaternion.Euler(0, 73, 0));
                wrapper.transform.localScale = Vector3.one * 2;
                using (var nested = new HumanoidAttackRig(wrapper))
                {
                    var clip = HumanoidAttackAuthoring.Bake(recipe, nested);
                    try { Check(clip.humanMotion && nested.Animator.transform.position.sqrMagnitude < .0001f && Vector3.Distance(nested.Animator.transform.lossyScale, Vector3.one) < .001f, "Animator anidado: offset y escala del contenedor normalizados"); }
                    finally { Object.DestroyImmediate(clip); }
                }
            }
            finally { Object.DestroyImmediate(wrapper); }
        }

        static void ExpectInvalid(HumanoidAttackRecipe recipe, string label)
        {
            try { HumanoidAttackAuthoring.Validate(recipe); }
            catch (InvalidOperationException) { Check(true, label); return; }
            throw new Exception(label);
        }
        static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
            Report.Add("PASS " + message);
        }
    }
}
