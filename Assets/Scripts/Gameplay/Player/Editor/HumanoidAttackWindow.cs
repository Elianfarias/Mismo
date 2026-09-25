using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public sealed class HumanoidAttackWindow : EditorWindow
    {
        [SerializeField] HumanoidAttackRecipe draft;
        [SerializeField] HumanoidAttackRecipe savedRecipe;
        [SerializeField] string savedRecipeGuid;
        [SerializeField] int selectedPose;
        [SerializeField] int selectedBone = (int)HumanBodyBones.RightHand;
        [SerializeField] HumanoidAttackTemplate template;
        [SerializeField] bool leftHanded;
        WeaponPoseStage stage;
        HumanoidAttackRig rig;
        HumanoidAttackWeapons weapons;
        GameObject previewModel;
        AnimationClip previewClip, referenceClip;
        HumanoidAttackPose copiedPose;
        Vector2 scroll;
        float time, referenceTime, speed = 1;
        double lastTick;
        bool playing, previewing, showFingers, showMuscles;
        int handleMode;
        [SerializeField] bool keepIkRotation = true;
        [SerializeField] bool showPoseGuides = true;
        [SerializeField] int importFrameRate = 60;
        bool updatingIk, ikManipulating;
        int ikControlBone = -1;
        Vector3 ikTarget, ikHint;
        string error, notice;
        Transform[] mappedBones;
        string[] boneLabels;
        int[] boneIds;

        [MenuItem("Mismo/Animaciones/Crear ataques Humanoid")]
        public static void Open()
        {
            var window = GetWindow<HumanoidAttackWindow>("Ataques Humanoid");
            window.minSize = new Vector2(390, 550);
            if (window.rig == null && window.draft.poses.Count == 0 && Selection.activeObject is GameObject model && EditorUtility.IsPersistent(model))
                window.draft.model = model;
        }

        [OnOpenAsset]
        static bool OpenAsset(EntityId instanceId, int line)
        {
            if (!(EditorUtility.EntityIdToObject(instanceId) is HumanoidAttackRecipe recipe)) return false;
            var window = GetWindow<HumanoidAttackWindow>("Ataques Humanoid");
            window.Run(() => window.LoadRecipe(recipe)); return true;
        }

        void OnEnable()
        {
            minSize = new Vector2(390, 550);
            EnsureDraft();
            saveChangesMessage = "Guardá el proyecto del ataque para conservar sus poses editables.";
            SceneView.duringSceneGui += DrawScene;
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.playModeStateChanged += PlayState;
            AssemblyReloadEvents.beforeAssemblyReload += ClosePreview;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScene;
            EditorApplication.update -= Tick;
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.playModeStateChanged -= PlayState;
            AssemblyReloadEvents.beforeAssemblyReload -= ClosePreview;
            ClosePreview();
        }

        void OnDestroy() { if (draft != null && !EditorUtility.IsPersistent(draft)) DestroyImmediate(draft); }
        void EnsureDraft()
        {
            if (savedRecipe == null && !string.IsNullOrEmpty(savedRecipeGuid))
                savedRecipe = AssetDatabase.LoadAssetAtPath<HumanoidAttackRecipe>(AssetDatabase.GUIDToAssetPath(savedRecipeGuid));
            if (savedRecipe != null) savedRecipeGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(savedRecipe));
            if (draft != null && !EditorUtility.IsPersistent(draft)) return;
            var source = draft != null ? draft : savedRecipe;
            draft = CreateInstance<HumanoidAttackRecipe>();
            if (source != null) EditorUtility.CopySerialized(source, draft);
            else draft.model = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusHumanoidLocomotion.ModelPath);
            draft.hideFlags = HideFlags.HideAndDontSave;
        }
        void PlayState(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) ClosePreview(); }
        public override void SaveChanges() { if (SaveRecipe()) base.SaveChanges(); }
        void OnUndo()
        {
            EnsureDraft();
            InvalidateClip(); hasUnsavedChanges = true;
            if (draft.poses.Count < 2 || (rig != null && previewModel != draft.model)) ClosePreview();
            selectedPose = Mathf.Clamp(selectedPose, 0, Mathf.Max(0, draft.poses.Count - 1));
            ApplySelected(); Repaint();
        }

        void Run(Action action)
        {
            try { error = null; action(); }
            catch (Exception e) { error = e.Message; Debug.LogException(e); }
        }

        void Record(string label) { Undo.RecordObject(draft, label); }
        void Changed()
        {
            EditorUtility.SetDirty(draft); hasUnsavedChanges = true;
            notice = null; InvalidateClip(); ApplySelected();
        }

        void InvalidateClip()
        {
            playing = previewing = false;
            rig?.StopSampling();
            if (previewClip != null) DestroyImmediate(previewClip);
            previewClip = null;
        }

        void ClosePreview()
        {
            weapons?.Dispose(); weapons = null;
            InvalidateClip(); rig?.Dispose(); rig = null;
            if (stage != null && StageUtility.GetCurrentStage() == stage) StageUtility.GoToMainStage();
            stage = null; mappedBones = null; previewModel = null; ikControlBone = -1;
        }

        void OpenPreview()
        {
            EnsureDraft();
            ClosePreview();
            string invalid = HumanoidAttackRig.ValidateModel(draft.model);
            if (invalid != null) throw new InvalidOperationException(invalid);
            stage = CreateInstance<WeaponPoseStage>(); stage.Header = "Crear ataques Humanoid";
            try
            {
                StageUtility.GoToStage(stage, true);
                rig = new HumanoidAttackRig(draft.model, stage.scene);
                weapons = new HumanoidAttackWeapons(rig);
                previewModel = draft.model;
                mappedBones = Enumerable.Range(0, (int)HumanBodyBones.LastBone).Select(i => rig.Animator.GetBoneTransform((HumanBodyBones)i)).ToArray();
                boneIds = Enumerable.Range(0, mappedBones.Length).Where(i => mappedBones[i] != null).ToArray();
                boneLabels = boneIds.Select(i => ((HumanBodyBones)i).ToString()).ToArray();
                if (draft.poses.Count == 0)
                {
                    Record("Crear ataque"); HumanoidAttackAuthoring.CreateTemplate(draft, rig.Neutral, template, leftHanded); Changed();
                }
                ApplySelected();
                CenterView(false);
            }
            catch { ClosePreview(); throw; }
        }

        void CenterView(bool profile)
        {
            if (rig == null || StageUtility.GetCurrentStage() != stage) return;
            var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            float scale = rig.Animator.humanScale;
            // The rig's neutral forward is +Z. Keep the camera independent of the
            // edited hips, so leaning or moving the body stays visible against the guides.
            view.in2DMode = false;
            view.LookAt(Vector3.up * scale, Quaternion.LookRotation(profile ? Vector3.left : Vector3.back, Vector3.up), scale * 2.2f, true, true);
            view.Repaint();
        }

        void DrawViewControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Centrar de frente", "Restablece el encuadre frontal, sin perspectiva, tomando como referencia el origen del personaje."))) Run(() => CenterView(false));
                if (GUILayout.Button(new GUIContent("Ver de perfil", "Vista lateral para comprobar cuánto se inclina o avanza el cuerpo."))) Run(() => CenterView(true));
            }
            EditorGUI.BeginChangeCheck();
            showPoseGuides = EditorGUILayout.ToggleLeft("Mostrar vertical y suelo de referencia", showPoseGuides);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        }

        void ApplySelected()
        {
            if (rig == null || draft.poses.Count == 0) return;
            selectedPose = Mathf.Clamp(selectedPose, 0, draft.poses.Count - 1);
            rig.Apply(draft.poses[selectedPose]); time = draft.poses[selectedPose].time;
            weapons?.Refresh(draft);
            if (!updatingIk) { ikControlBone = -1; ikManipulating = false; }
            previewing = playing = false; SceneView.RepaintAll();
        }

        void SelectPose(int index) { selectedPose = index; ApplySelected(); }

        void Preview(float value)
        {
            if (rig == null) return;
            if (previewClip == null) previewClip = HumanoidAttackAuthoring.Bake(draft);
            time = Mathf.Clamp01(value); rig.Sample(previewClip, time * draft.duration);
            weapons?.Refresh(draft);
            previewing = true; SceneView.RepaintAll();
        }

        void Tick()
        {
            if (rig != null && (stage == null || rig.Container == null || StageUtility.GetCurrentStage() != stage)) { ClosePreview(); Repaint(); }
            if (weapons != null && weapons.Refresh(draft)) { SceneView.RepaintAll(); Repaint(); }
            if (!playing || rig == null) return;
            double now = EditorApplication.timeSinceStartup;
            float next = time + (float)(now - lastTick) * speed / draft.duration; lastTick = now;
            Run(() => Preview(next >= 1 ? 0 : next)); Repaint();
            if (error != null) playing = false;
        }

        void OnGUI()
        {
            EnsureDraft();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Crear ataques Humanoid", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("1. Abrí el personaje.  2. Elegí una pose y arrastrá manos o pies en Scene.  3. Reproducí y exportá el clip. Las poses se actualizan al editar.", MessageType.Info);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Salí de Play Mode para crear animaciones.", MessageType.Warning);
                EditorGUILayout.EndScrollView(); return;
            }
            DrawDocument();
            using (new EditorGUI.DisabledScope(rig != null))
            {
                EditorGUI.BeginChangeCheck();
                var model = (GameObject)EditorGUILayout.ObjectField("Personaje / FBX", draft.model, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck()) { Record("Elegir personaje"); draft.model = model; Changed(); }
            }
            string validation = HumanoidAttackRig.ValidateModel(draft.model);
            if (validation != null) EditorGUILayout.HelpBox(validation, MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(validation != null || rig != null))
                    if (GUILayout.Button("Abrir personaje en Scene", GUILayout.Height(28))) Run(OpenPreview);
                using (new EditorGUI.DisabledScope(rig == null))
                    if (GUILayout.Button("Cerrar vista", GUILayout.Width(95), GUILayout.Height(28))) ClosePreview();
            }
            if (rig != null) DrawViewControls();
            DrawWeapons();
            if (rig != null)
            {
                DrawReference(); DrawTemplate(); DrawTiming(); DrawPlayback(); DrawPoses(); DrawBone();
            }
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(draft.poses.Count < 2 || validation != null))
                if (GUILayout.Button("Exportar clip Humanoid .anim", GUILayout.Height(34))) Run(Export);
            if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, MessageType.Info);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        void DrawWeapons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Armas en la vista", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var main = (WeaponDefinition)EditorGUILayout.ObjectField("Arma principal", draft.previewWeapon, typeof(WeaponDefinition), false);
            bool visible = EditorGUILayout.Toggle("Mostrar armas", draft.showPreviewWeapons);
            bool pair = draft.previewPair;
            var offhand = draft.previewOffhand;
            using (new EditorGUI.DisabledScope(main == null || main.isTwoHanded))
            {
                pair = EditorGUILayout.Toggle("Mostrar segunda arma", pair);
                if (pair)
                    offhand = (WeaponDefinition)EditorGUILayout.ObjectField("Arma secundaria", offhand, typeof(WeaponDefinition), false);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Record("Elegir armas del ataque");
                draft.previewWeapon = main; draft.previewOffhand = offhand;
                draft.previewPair = pair; draft.showPreviewWeapons = visible;
                EditorUtility.SetDirty(draft); hasUnsavedChanges = true;
                weapons?.Refresh(draft); SceneView.RepaintAll();
            }
            if (main != null && main.isTwoHanded)
                EditorGUILayout.HelpBox("Arma a dos manos: se muestra una sola pieza. Arrastrá la mano de apoyo hasta la empuñadura al editar cada pose.", MessageType.Info);
            else if (pair && offhand == null)
                EditorGUILayout.HelpBox("Secundaria vacía: muestra otra copia del arma principal con su agarre secundario.", MessageType.None);
            EditorGUILayout.HelpBox("Elegí los assets de arma del taller. Se usan sus prefabs y agarres actuales; la selección se guarda con el proyecto del ataque.", MessageType.None);
            if (!string.IsNullOrEmpty(weapons?.Warning)) EditorGUILayout.HelpBox(weapons.Warning, MessageType.Warning);
        }

        void DrawDocument()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var load = (HumanoidAttackRecipe)EditorGUILayout.ObjectField("Proyecto del ataque", savedRecipe, typeof(HumanoidAttackRecipe), false);
                if (load != savedRecipe && load != null) Run(() => LoadRecipe(load));
                if (GUILayout.Button("Guardar", GUILayout.Width(70))) Run(() => SaveRecipe());
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Nuevo ataque")) Run(() =>
                {
                    if (!CanReplaceDraft()) return;
                    ClosePreview(); Record("Nuevo ataque"); draft.poses.Clear(); draft.clipName = "Ataque_Humanoid";
                    savedRecipe = null; savedRecipeGuid = null; Changed();
                });
                if (GUILayout.Button("Guardar copia")) Run(() => SaveRecipe(true));
            }
            EditorGUI.BeginChangeCheck();
            string clipName = EditorGUILayout.TextField("Nombre del clip", draft.clipName);
            if (EditorGUI.EndChangeCheck()) { Record("Nombre del ataque"); draft.clipName = clipName; Changed(); }
        }

        void DrawTemplate()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Punto de partida", EditorStyles.boldLabel);
            template = (HumanoidAttackTemplate)EditorGUILayout.Popup("Plantilla", (int)template, new[] { "Tajo horizontal", "Golpe descendente", "Estocada", "Puñetazo", "Guardia sin ataque" });
            leftHanded = EditorGUILayout.Toggle("Ataque con mano izquierda", leftHanded);
            if (GUILayout.Button("Generar poses de la plantilla (se puede deshacer)")) Run(() =>
            {
                Record("Generar plantilla de ataque"); HumanoidAttackAuthoring.CreateTemplate(draft, rig.Neutral, template, leftHanded);
                selectedPose = 0; Changed(); ApplySelected();
            });
        }

        void DrawTiming()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Duración y fases", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float duration = EditorGUILayout.Slider("Duración (segundos)", draft.duration, .05f, 30);
            float active = EditorGUILayout.Slider("Inicio activo", draft.activeStartsAt, .01f, draft.recoveryStartsAt - .01f);
            float recovery = EditorGUILayout.Slider("Inicio recuperación", draft.recoveryStartsAt, active + .01f, .99f);
            int fps = EditorGUILayout.IntSlider("FPS de exportación", draft.frameRate, 15, 120);
            if (EditorGUI.EndChangeCheck())
            {
                Record("Tiempos del ataque"); draft.duration = duration; draft.activeStartsAt = active; draft.recoveryStartsAt = recovery; draft.frameRate = fps; Changed(); ApplySelected();
            }
            EditorGUILayout.LabelField($"Preparación {active * duration:F2}s  ·  Activo {(recovery - active) * duration:F2}s  ·  Recuperación {(1 - recovery) * duration:F2}s", EditorStyles.miniLabel);
        }

        void DrawPlayback()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "Pausar" : "▶ Reproducir")) Run(() => { if (playing) playing = false; else { Preview(time); playing = true; lastTick = EditorApplication.timeSinceStartup; } });
                if (GUILayout.Button("Editar pose más cercana")) SelectNearestPose(time);
            }
            speed = EditorGUILayout.Slider("Velocidad de vista", speed, .1f, 2);
            EditorGUI.BeginChangeCheck();
            float nextTime = EditorGUILayout.Slider("Tiempo", time * draft.duration, 0, draft.duration) / draft.duration;
            if (EditorGUI.EndChangeCheck()) Run(() => { playing = false; Preview(nextTime); });
            var rect = GUILayoutUtility.GetRect(50, 40, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * draft.activeStartsAt, 13), new Color(.22f, .42f, .65f));
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * draft.activeStartsAt, rect.y, rect.width * (draft.recoveryStartsAt - draft.activeStartsAt), 13), new Color(.85f, .34f, .16f));
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * draft.recoveryStartsAt, rect.y, rect.width * (1 - draft.recoveryStartsAt), 13), new Color(.25f, .55f, .36f));
            if (draft.poses.Count <= 12)
            {
                for (int i = 0; i < draft.poses.Count; i++)
                {
                    float x = Mathf.Lerp(rect.x + 10, rect.xMax - 10, draft.poses[i].time);
                    if (GUI.Button(new Rect(x - 11, rect.y + 14, 22, 22), (i + 1).ToString())) SelectPose(i);
                }
            }
            else
            {
                var strip = new Rect(rect.x, rect.y + 15, rect.width, 22);
                if (GUI.Button(strip, GUIContent.none)) SelectNearestPose(Mathf.InverseLerp(strip.x, strip.xMax, Event.current.mousePosition.x));
                int step = Mathf.Max(1, Mathf.CeilToInt(draft.poses.Count * 3 / Mathf.Max(1, strip.width)));
                for (int i = 0; i < draft.poses.Count; i += step)
                    EditorGUI.DrawRect(new Rect(Mathf.Lerp(strip.x, strip.xMax - 1, draft.poses[i].time), strip.y + 5, 1, 12), Color.gray);
                EditorGUI.DrawRect(new Rect(Mathf.Lerp(strip.x, strip.xMax - 3, draft.poses[selectedPose].time), strip.y + 2, 3, 18), new Color(1, .7f, .1f));
            }
            EditorGUI.DrawRect(new Rect(Mathf.Lerp(rect.x, rect.xMax - 2, time), rect.y, 2, 13), Color.white);
        }

        void DrawPoses()
        {
            EditorGUILayout.LabelField("Poses clave", EditorStyles.boldLabel);
            int selection;
            if (draft.poses.Count <= 12)
                selection = EditorGUILayout.Popup("Pose a editar", selectedPose, draft.poses.Select((p, i) => $"{i + 1}. {p.label} ({p.time * draft.duration:F2}s)").ToArray());
            else
                selection = EditorGUILayout.IntSlider("Pose a editar", selectedPose + 1, 1, draft.poses.Count) - 1;
            if (selection != selectedPose) SelectPose(selection);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedPose == 0))
                    if (GUILayout.Button("◀ Anterior")) SelectPose(selectedPose - 1);
                EditorGUILayout.LabelField($"{selectedPose + 1} / {draft.poses.Count} · {draft.poses[selectedPose].time * draft.duration:F3}s", GUILayout.Width(145));
                using (new EditorGUI.DisabledScope(selectedPose == draft.poses.Count - 1))
                    if (GUILayout.Button("Siguiente ▶")) SelectPose(selectedPose + 1);
            }
            var pose = draft.poses[selectedPose];
            EditorGUI.BeginChangeCheck();
            string label = EditorGUILayout.TextField("Nombre de la pose", pose.label);
            float at = pose.time;
            using (new EditorGUI.DisabledScope(selectedPose == 0 || selectedPose == draft.poses.Count - 1))
                at = EditorGUILayout.Slider("Momento (normalizado)", pose.time,
                    selectedPose > 0 ? draft.poses[selectedPose - 1].time + .000001f : 0,
                    selectedPose + 1 < draft.poses.Count ? draft.poses[selectedPose + 1].time - .000001f : 1);
            var blend = (AttackPoseBlend)EditorGUILayout.Popup("Hacia la próxima pose", (int)pose.blend, new[] { "Suave", "Lineal (golpe rápido)" });
            if (EditorGUI.EndChangeCheck()) { Record("Editar pose clave"); pose.label = label; pose.time = at; pose.blend = blend; Changed(); ApplySelected(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool canInsert = draft.poses.All(p => Mathf.Abs(p.time - time) > .00001f);
                using (new EditorGUI.DisabledScope(!canInsert))
                    if (GUILayout.Button("Añadir pose en este tiempo")) Run(() =>
                    {
                        Record("Añadir pose"); var added = HumanoidAttackAuthoring.Evaluate(draft, time); added.label = "Nueva pose";
                        draft.poses.Add(added); draft.poses.Sort((a, b) => a.time.CompareTo(b.time)); selectedPose = draft.poses.IndexOf(added); Changed(); ApplySelected();
                    });
                using (new EditorGUI.DisabledScope(selectedPose == 0 || selectedPose == draft.poses.Count - 1))
                    if (GUILayout.Button("Eliminar pose")) { Record("Eliminar pose"); draft.poses.RemoveAt(selectedPose); selectedPose--; Changed(); ApplySelected(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copiar pose")) copiedPose = pose.Copy(pose.time);
                using (new EditorGUI.DisabledScope(copiedPose == null))
                    if (GUILayout.Button("Pegar pose")) { Record("Pegar pose"); draft.poses[selectedPose] = copiedPose.Copy(pose.time, pose.label); Changed(); ApplySelected(); }
                if (GUILayout.Button("Espejar")) { Record("Espejar pose"); HumanoidAttackAuthoring.Mirror(draft.poses[selectedPose]); Changed(); ApplySelected(); }
            }
        }

        void DrawBone()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Editar huesos", EditorStyles.boldLabel);
            if (previewing) EditorGUILayout.HelpBox("La vista muestra el clip. Pulsá Editar pose más cercana para volver a mover los huesos.", MessageType.Info);
            using (new EditorGUI.DisabledScope(previewing))
            {
                int index = Mathf.Max(0, Array.IndexOf(boneIds, selectedBone));
                int chosen = boneIds[EditorGUILayout.Popup("Hueso", index, boneLabels)];
                if (chosen != selectedBone) SelectBone(chosen);
                showFingers = EditorGUILayout.Toggle("Mostrar controles de dedos", showFingers);
                handleMode = GUILayout.Toolbar(handleMode, new[] { "Arrastrar (IK)", "Rotar hueso", "Mover cadera" });
                if (handleMode == 2) selectedBone = (int)HumanBodyBones.Hips;
                if (handleMode == 0)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string[] labels = { "Mano izq.", "Mano der.", "Pie izq.", "Pie der." };
                        for (int i = 0; i < labels.Length; i++)
                            if (GUILayout.Button(labels[i])) SelectBone((int)HumanoidAttackIK.Endpoints[i]);
                    }
                    keepIkRotation = EditorGUILayout.Toggle("Conservar orientación de mano/pie", keepIkRotation);
                    EditorGUILayout.HelpBox("Arrastrá las esferas de manos y pies libremente en la vista, o las flechas para cambiar la profundidad. El cubo rosa ajusta el codo o la rodilla. W: arrastrar · E: rotar · Ctrl+Z: deshacer.", MessageType.None);
                    if (EnsureIkControl(out var limb))
                    {
                        bool bend = selectedBone == (int)limb.bendBone;
                        EditorGUI.BeginChangeCheck();
                        Vector3 target = EditorGUILayout.Vector3Field(bend ? "Control de codo/rodilla" : "Destino de mano/pie", bend ? ikHint : ikTarget);
                        if (EditorGUI.EndChangeCheck()) MoveIk(selectedBone, target);
                    }
                    else if (selectedBone != (int)HumanBodyBones.Hips)
                        EditorGUILayout.HelpBox("Elegí una mano, un pie, un codo o una rodilla para arrastrar. Para torso, cabeza y dedos usá Rotar hueso.", MessageType.Info);
                }
                else EditorGUILayout.HelpBox("Usá los anillos para orientar el hueso. Mover cadera desplaza el cuerpo completo. W vuelve a arrastrar manos y pies.", MessageType.None);
                var bone = mappedBones[selectedBone];
                if (bone != null)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 euler = EditorGUILayout.Vector3Field("Rotación local", bone.localEulerAngles);
                    if (EditorGUI.EndChangeCheck()) { Record("Rotar hueso"); bone.localRotation = Quaternion.Euler(euler); CaptureSelected(); }
                }
                showMuscles = EditorGUILayout.Foldout(showMuscles, "Ajuste muscular del hueso", true);
                if (showMuscles)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int muscle = HumanTrait.MuscleFromBone(selectedBone, axis);
                        if (muscle < 0) continue;
                        EditorGUI.BeginChangeCheck();
                        float value = EditorGUILayout.Slider(HumanTrait.MuscleName[muscle], draft.poses[selectedPose].muscles[muscle], -1, 1);
                        if (EditorGUI.EndChangeCheck()) { Record("Ajustar músculo"); draft.poses[selectedPose].muscles[muscle] = value; Changed(); ApplySelected(); }
                    }
                }
            }
        }

        void CaptureSelected()
        {
            var pose = draft.poses[selectedPose];
            var captured = rig.Capture(pose.label, pose.time); captured.blend = pose.blend;
            draft.poses[selectedPose] = captured; Changed(); ApplySelected(); Repaint();
        }

        void SelectBone(int bone)
        {
            selectedBone = bone; ikControlBone = -1; ikManipulating = false;
            if (handleMode == 2 && bone != (int)HumanBodyBones.Hips) handleMode = 0;
            Repaint(); SceneView.RepaintAll();
        }

        bool EnsureIkControl(out HumanoidAttackIK.Limb limb)
        {
            if (!HumanoidAttackIK.TryGetLimb(rig?.Animator, (HumanBodyBones)selectedBone, out limb)) return false;
            if (ikControlBone != (int)limb.tipBone)
            {
                ikTarget = limb.tip.position; ikHint = limb.HintPosition();
                ikControlBone = (int)limb.tipBone;
            }
            return true;
        }

        void MoveIk(int bone, Vector3 position)
        {
            if (selectedBone != bone) SelectBone(bone);
            if (!EnsureIkControl(out var limb)) return;
            Record("Arrastrar extremidad del ataque");
            if (bone == (int)limb.bendBone) ikHint = position;
            else ikTarget = position;
            updatingIk = true;
            try
            {
                if (HumanoidAttackIK.Solve(limb, ikTarget, ikHint, keepIkRotation)) CaptureSelected();
                ikManipulating = true;
            }
            finally { updatingIk = false; }
        }

        void DrawReference()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Importar clip o copiar un fotograma", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            referenceClip = (AnimationClip)EditorGUILayout.ObjectField("Clip Humanoid", referenceClip, typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck() && referenceClip != null)
            {
                importFrameRate = Mathf.Clamp(Mathf.RoundToInt(referenceClip.frameRate), 15, 120);
                referenceTime = 0;
            }
            if (referenceClip == null) return;
            if (!referenceClip.humanMotion) { EditorGUILayout.HelpBox("Ese clip es Generic. Elegí una animación Humanoid.", MessageType.Warning); return; }
            string invalid = HumanoidAttackAuthoring.ValidateImportClip(referenceClip);
            using (new EditorGUI.DisabledScope(invalid != null))
            {
                importFrameRate = EditorGUILayout.IntSlider("FPS de importación", importFrameRate, 15, 120);
                if (invalid == null)
                    EditorGUILayout.LabelField($"Clip completo: {referenceClip.length:F3}s · {Mathf.CeilToInt(referenceClip.length * importFrameRate) + 1} poses editables", EditorStyles.miniLabel);
                if (GUILayout.Button("Importar clip completo (se puede deshacer)")) Run(ImportReferenceClip);
            }
            if (invalid != null) EditorGUILayout.HelpBox(invalid, MessageType.Warning);
            EditorGUILayout.HelpBox("Importar reemplaza las poses y la duración actuales. Conserva las armas y las fases del ataque. El movimiento del cuerpo queda en las poses; los eventos y las curvas de objetos o blendshapes no se copian.", MessageType.None);
            referenceTime = EditorGUILayout.Slider("Segundo del clip", referenceTime, 0, referenceClip.length);
            if (GUILayout.Button("Usar este fotograma en la pose seleccionada")) Run(CopyReferenceFrame);
        }

        void ImportReferenceClip()
        {
            HumanoidAttackAuthoring.ImportClip(draft, referenceClip, importFrameRate);
            selectedPose = 0; Changed();
            notice = $"Clip importado: {draft.poses.Count} poses editables. Recorré los fotogramas con Anterior / Siguiente o la línea de tiempo. Ctrl+Z deshace la importación.";
        }

        void CopyReferenceFrame()
        {
            Record("Tomar pose Humanoid"); rig.Sample(referenceClip, referenceTime); CaptureSelected();
        }

        void SelectNearestPose(float at)
        {
            int nearest = 0;
            for (int i = 1; i < draft.poses.Count; i++)
                if (Mathf.Abs(draft.poses[i].time - at) < Mathf.Abs(draft.poses[nearest].time - at)) nearest = i;
            SelectPose(nearest);
        }

        void DrawScene(SceneView view)
        {
            if (rig == null || mappedBones == null || StageUtility.GetCurrentStage() != stage) return;
            var input = Event.current;
            if (!previewing && input.type == EventType.KeyDown && input.modifiers == EventModifiers.None && (input.keyCode == KeyCode.W || input.keyCode == KeyCode.E))
            {
                handleMode = input.keyCode == KeyCode.W ? 0 : 1; input.Use(); Repaint();
            }
            if (ikManipulating && GUIUtility.hotControl == 0) { ikControlBone = -1; ikManipulating = false; }
            var previousColor = Handles.color;
            var previousDepth = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            try
            {
                if (showPoseGuides && input.type == EventType.Repaint) DrawPoseGuides();
                for (int i = 0; i < mappedBones.Length; i++)
                {
                    var bone = mappedBones[i];
                    if (bone == null || (!showFingers && i >= (int)HumanBodyBones.LeftThumbProximal && i <= (int)HumanBodyBones.RightLittleDistal)) continue;
                    Handles.color = i == selectedBone ? new Color(1, .7f, .1f) : new Color(.2f, .85f, 1, .85f);
                    var parent = bone.parent;
                    while (parent != null && parent != rig.Animator.transform && Array.IndexOf(mappedBones, parent) < 0) parent = parent.parent;
                    if (parent != null && Array.IndexOf(mappedBones, parent) >= 0) Handles.DrawLine(parent.position, bone.position);
                    float size = HandleUtility.GetHandleSize(bone.position) * .045f;
                    bool draggable = handleMode == 0 && HumanoidAttackIK.Endpoints.Contains((HumanBodyBones)i);
                    if (!previewing && !draggable && Handles.Button(bone.position, Quaternion.identity, size, size * 1.4f, Handles.SphereHandleCap)) SelectBone(i);
                }
                if (!previewing && handleMode == 0) DrawIkHandles();
                var selected = mappedBones[selectedBone];
                if (previewing || selected == null) return;
                Handles.Label(selected.position + Vector3.up * .06f, ((HumanBodyBones)selectedBone).ToString());
                if (handleMode == 0 && selectedBone != (int)HumanBodyBones.Hips) return;
                EditorGUI.BeginChangeCheck();
                Quaternion rotation = selected.rotation; Vector3 position = selected.position;
                if (handleMode != 1) position = Handles.PositionHandle(position, Quaternion.identity);
                else rotation = Handles.RotationHandle(rotation, position);
                if (EditorGUI.EndChangeCheck())
                {
                    Record("Posar hueso del ataque");
                    selected.SetPositionAndRotation(position, rotation); CaptureSelected();
                }
            }
            finally { Handles.color = previousColor; Handles.zTest = previousDepth; }
        }

        void DrawPoseGuides()
        {
            float scale = rig.Animator.humanScale;
            Handles.color = new Color(1, .85f, .35f, .65f);
            Handles.DrawDottedLine(Vector3.zero, Vector3.up * scale * 2.2f, 5);
            Handles.Label(Vector3.up * scale * 2.25f, "Vertical de referencia");
            Handles.color = new Color(.7f, .7f, .7f, .65f);
            Handles.DrawLine(Vector3.left * scale * 1.5f, Vector3.right * scale * 1.5f);
            Handles.DrawLine(Vector3.back * scale * 1.5f, Vector3.forward * scale * 1.5f);
        }

        void DrawIkHandles()
        {
            EnsureIkControl(out _);
            foreach (var end in HumanoidAttackIK.Endpoints)
            {
                var bone = mappedBones[(int)end];
                if (bone == null) continue;
                bool active = ikControlBone == (int)end;
                Vector3 position = active ? ikTarget : bone.position;
                Handles.color = active ? new Color(1, .7f, .1f) : new Color(.2f, 1, .65f);
                float size = HandleUtility.GetHandleSize(position) * .065f;
                int id = GUIUtility.GetControlID(0x5A210 + (int)end, FocusType.Passive);
                EditorGUI.BeginChangeCheck();
                Vector3 target = Handles.FreeMoveHandle(id, position, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck()) MoveIk((int)end, target);
                // Clicking without dragging also selects the target for the axis gizmo.
                if (GUIUtility.hotControl == id && selectedBone != (int)end) SelectBone((int)end);
                if (active && Vector3.Distance(position, bone.position) > .015f)
                    Handles.DrawDottedLine(bone.position, position, 3);
            }
            if (!EnsureIkControl(out var limb)) return;
            Handles.color = new Color(1, .5f, .85f);
            Handles.DrawDottedLine(limb.lower.position, ikHint, 3);
            Handles.Label(ikHint, limb.IsArm ? "Codo" : "Rodilla");
            float hintSize = HandleUtility.GetHandleSize(ikHint) * .06f;
            int hintId = GUIUtility.GetControlID(0x5B210 + (int)limb.tipBone, FocusType.Passive);
            EditorGUI.BeginChangeCheck();
            Vector3 hint = Handles.FreeMoveHandle(hintId, ikHint, hintSize, Vector3.zero, Handles.CubeHandleCap);
            if (EditorGUI.EndChangeCheck()) MoveIk((int)limb.bendBone, hint);
            if (GUIUtility.hotControl == hintId && selectedBone != (int)limb.bendBone) SelectBone((int)limb.bendBone);

            bool bend = selectedBone == (int)limb.bendBone;
            Handles.color = Color.white;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(bend ? ikHint : ikTarget, Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) MoveIk(selectedBone, moved);
        }

        bool CanReplaceDraft()
        {
            if (!hasUnsavedChanges) return true;
            int choice = EditorUtility.DisplayDialogComplex("Proyecto sin guardar", "Guardá las poses antes de cambiar de proyecto.", "Guardar", "Cancelar", "Descartar");
            return choice == 2 || (choice == 0 && SaveRecipe());
        }

        void LoadRecipe(HumanoidAttackRecipe recipe)
        {
            if (recipe == null) throw new InvalidOperationException("El proyecto seleccionado ya no está cargado. Volvé a elegir su asset en Project.");
            string path = AssetDatabase.GetAssetPath(recipe);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Elegí un proyecto de ataque guardado en Project.");
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!CanReplaceDraft()) return;
            // Leaving PreviewSceneStage can unload the selected native asset, even while
            // the incoming C# reference still exists. Resolve it again after stage cleanup.
            ClosePreview();
            var loaded = AssetDatabase.LoadAssetAtPath<HumanoidAttackRecipe>(AssetDatabase.GUIDToAssetPath(guid));
            if (loaded == null) throw new InvalidOperationException("No se pudo abrir el proyecto guardado: " + path);
            EnsureDraft(); Record("Abrir proyecto de ataque");
            EditorUtility.CopySerialized(loaded, draft); draft.hideFlags = HideFlags.HideAndDontSave;
            savedRecipe = loaded; savedRecipeGuid = guid;
            selectedPose = 0; hasUnsavedChanges = false; error = notice = null;
            Repaint();
        }

        bool SaveRecipe(bool copy = false)
        {
            EnsureDraft();
            string path = !copy && savedRecipe != null ? AssetDatabase.GetAssetPath(savedRecipe) : null;
            if (string.IsNullOrEmpty(path))
            {
                HumanoidAttackAuthoring.EnsureFolder(HumanoidAttackAuthoring.RecipeFolder);
                path = EditorUtility.SaveFilePanelInProject("Guardar proyecto editable", SafeName(draft.clipName) + "_Proyecto", "asset", "Guardar en Assets/Data/AnimationAuthoring", HumanoidAttackAuthoring.RecipeFolder);
                if (string.IsNullOrEmpty(path)) return false;
                if (!path.StartsWith("Assets/Data/AnimationAuthoring/", StringComparison.Ordinal)) throw new InvalidOperationException("Guardá el proyecto dentro de Assets/Data/AnimationAuthoring.");
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                savedRecipe = Instantiate(draft); savedRecipe.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(savedRecipe, path);
            }
            else
            {
                Undo.RecordObject(savedRecipe, "Guardar proyecto de ataque");
                EditorUtility.CopySerialized(draft, savedRecipe); savedRecipe.hideFlags = HideFlags.None;
                EditorUtility.SetDirty(savedRecipe);
            }
            AssetDatabase.SaveAssetIfDirty(savedRecipe);
            savedRecipeGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(savedRecipe));
            hasUnsavedChanges = false;
            notice = "Proyecto guardado: " + path; return true;
        }

        void Export()
        {
            HumanoidAttackAuthoring.Validate(draft);
            HumanoidAttackAuthoring.EnsureFolder(HumanoidAttackAuthoring.ClipFolder);
            string path = EditorUtility.SaveFilePanelInProject("Exportar ataque Humanoid", SafeName(draft.clipName), "anim", "Guardar en Assets/Art/Animations", HumanoidAttackAuthoring.ClipFolder);
            if (string.IsNullOrEmpty(path)) return;
            if (!path.StartsWith("Assets/Art/Animations/", StringComparison.Ordinal)) throw new InvalidOperationException("Guardá el clip dentro de Assets/Art/Animations.");
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var clip = HumanoidAttackAuthoring.Bake(draft);
            try
            {
                clip.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(clip, path); AssetDatabase.SaveAssetIfDirty(clip);
                EditorGUIUtility.PingObject(clip);
                notice = $"Clip Humanoid exportado: {path}\nAsignalo en Familias y animaciones o en el Taller de enemigos. Active Starts At: {draft.activeStartsAt:F3} · Recovery Starts At: {draft.recoveryStartsAt:F3}. El daño sigue configurándose en combate.";
            }
            finally { if (!AssetDatabase.Contains(clip)) Object.DestroyImmediate(clip); }
        }

        static string SafeName(string value)
        {
            string clean = string.Concat((value ?? "").Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim();
            return string.IsNullOrEmpty(clean) ? "Ataque_Humanoid" : clean;
        }
    }
}
