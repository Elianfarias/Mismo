using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    public sealed partial class WeaponPoseWindow : EditorWindow
    {
        [SerializeField] private GameObject characterPrefab;
        private GameObject character, visual, secondVisual;
        private GameObject previewMainPrefab, previewSecondPrefab;
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private WeaponDefinition previewOffhand;
        private bool previewPair;
        private WeaponDefinition Offhand => previewOffhand != null ? previewOffhand : weapon;
        private GameObject OffhandPrefab => previewPair && Offhand != null ? Offhand.visualPrefab : null;
        private WeaponFamilyDefinition PreviewFamily => previewPair && Offhand != null
            ? (Offhand.isShield ? weapon.swordShieldFamily : weapon.dualSwordFamily) : weapon != null ? weapon.family : null;
        private AbilityDefinition PreviewAbility => previewPair && PreviewFamily != null
            ? PreviewFamily.GetAbility(AbilitySlot.Basic) : weapon != null ? weapon.GetAbility(AbilitySlot.Basic) : null;
        private WeaponPoseProfile profile;
        private WeaponPoseStage stage;
        private Animator animator;
        private Renderer[] previewRenderers;
        private bool[] previewVisibility;
        private Transform[] previewBones;
        private Vector3[] bonePositions, boneScales;
        private Quaternion[] boneRotations;
        private AnimationClip clip;
        private float time;
        private bool holstered;
        private bool editSecond;
        private bool editTrailTip;
        private Vector2 scroll;
        private UnityEditor.Editor profileEditor;
        private UnityEditor.Editor animationEditor;
        private WeaponAttachmentPose Pose => editSecond && previewPair
            ? (holstered ? Offhand.secondaryHolstered : Offhand.secondaryEquipped)
            : (holstered ? profile.holstered : profile.equipped);
        private Object PoseOwner => editSecond && previewPair ? (Object)Offhand : profile;
        private GameObject SelectedVisual => editSecond && previewPair ? secondVisual : visual;

        [MenuItem("Mismo/Armas/Taller de poses")]
        public static void Open() => GetWindow<WeaponPoseWindow>("Taller de armas");
        private void OnEnable()
        {
            EditorApplication.update += UpdateTrailPlayback;
            SceneView.duringSceneGui += DrawHandles; Undo.undoRedoPerformed += Refresh;
            if(characterPrefab==null)characterPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/PlayerVoxelSwordE.prefab");
            if(characterPrefab==null)characterPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab");
            if(weapon==null)weapon=Selection.activeObject as WeaponDefinition;
        }
        private void OnDisable()
        {
            EditorApplication.update -= UpdateTrailPlayback;
            SceneView.duringSceneGui -= DrawHandles; Undo.undoRedoPerformed -= Refresh;
            ClosePreview(); if (profileEditor != null) DestroyImmediate(profileEditor);
            if (animationEditor != null) DestroyImmediate(animationEditor);
        }
        private void ClosePreview()
        {
            playingTrail=false; DisposeTrailPreview();
            if (stage != null && StageUtility.GetCurrentStage() == stage) StageUtility.GoToMainStage();
            stage = null; character = null; visual = null; secondVisual = null; animator = null;
        }
        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("1. Elegí personaje y arma. 2. Creá o asigná un perfil. 3. Abrí la vista de ajuste. Usá W/E en Scene para mover/rotar. Los cambios del perfil admiten Undo.", MessageType.Info);
            using (new EditorGUI.DisabledScope(stage != null))
            {
                characterPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab del personaje", characterPrefab, typeof(GameObject), false);
                weapon = (WeaponDefinition)EditorGUILayout.ObjectField("Arma", weapon, typeof(WeaponDefinition), false);
            }
            if (weapon == null) { EditorGUILayout.EndScrollView(); return; }
            if (GUILayout.Button("Editar / probar feedback de combate")) CombatFeedbackWindow.Open(weapon);
            if (!previewPair) editSecond = false;
            var weaponData = new SerializedObject(weapon);
            weaponData.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(weaponData.FindProperty("id"), new GUIContent("ID"));
            EditorGUILayout.PropertyField(weaponData.FindProperty("displayName"), new GUIContent("Nombre"));
            EditorGUILayout.PropertyField(weaponData.FindProperty("visualPrefab"), new GUIContent("Visual principal"));
            EditorGUILayout.PropertyField(weaponData.FindProperty("isTwoHanded"), new GUIContent("A dos manos"));
            EditorGUILayout.PropertyField(weaponData.FindProperty("dualSwordFamily"), new GUIContent("Familia: dos espadas"));
            EditorGUILayout.PropertyField(weaponData.FindProperty("swordShieldFamily"), new GUIContent("Familia: espada y escudo"));
            if (EditorGUI.EndChangeCheck())
            {
                weaponData.ApplyModifiedProperties();
                if (!previewPair) editSecond = false;
                if (stage != null) CreatePreview();
            }
            EditorGUI.BeginChangeCheck();
            previewPair = EditorGUILayout.Toggle("Previsualizar dos armas", previewPair);
            if (previewPair)
            {
                previewOffhand = (WeaponDefinition)EditorGUILayout.ObjectField("Arma secundaria (vista)", previewOffhand, typeof(WeaponDefinition), false);
                EditorGUILayout.HelpBox("Vacío muestra otra copia del arma seleccionada. Cada objeto usa su propio visual y agarre secundario. Esta combinación es sólo una vista previa: en el juego mandan los objetos equipados. Las habilidades y animaciones pertenecen a la familia.", MessageType.Info);
            }
            if (EditorGUI.EndChangeCheck()) { if (!previewPair) editSecond = false; if (stage != null) CreatePreview(); }
            if (GUILayout.Button("Guardar definición del arma")) AssetDatabase.SaveAssetIfDirty(weapon);
            if(GUILayout.Button("Editar familia y animaciones de combate…"))WeaponFamilyWindow.Open(weapon);
            if(weapon.family!=null)EditorGUILayout.HelpBox("Las animaciones de combate se editan en la familia. Este perfil conserva el agarre y sus variantes de movimiento.",MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var selected = (WeaponPoseProfile)EditorGUILayout.ObjectField("Perfil compartido", weapon.poseProfile, typeof(WeaponPoseProfile), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(weapon,"Asignar perfil de arma"); weapon.poseProfile = selected; EditorUtility.SetDirty(weapon); ClosePreview();
            }
            if (weapon.poseProfile == null && GUILayout.Button("Crear perfil para esta arma"))
            {
                string path = EditorUtility.SaveFilePanelInProject("Crear perfil", weapon.name + "Pose", "asset", "Elegí dónde guardar el perfil");
                if (!string.IsNullOrEmpty(path))
                {
                    var created = CreateInstance<WeaponPoseProfile>(); AssetDatabase.CreateAsset(created,path);
                    Undo.RecordObject(weapon,"Asignar perfil de arma"); weapon.poseProfile = created; EditorUtility.SetDirty(weapon);
                }
            }
            profile = weapon.poseProfile;
            if (profile == null) { EditorGUILayout.EndScrollView(); return; }
            EditorGUILayout.HelpBox("El perfil reemplaza los offsets y el IK de brazos antiguos. La postura corporal viene del clip/Animator Override Controller. Para rigs Generic elegí Bone Path; para Humanoid podés elegir una mano. El prefab visual debe contener solo el arma.", MessageType.Info);
            if (stage == null)
            {
                using (new EditorGUI.DisabledScope(characterPrefab == null || weapon.visualPrefab == null || EditorApplication.isPlaying))
                    if (GUILayout.Button("Abrir vista de ajuste aislada")) CreatePreview();
                if (weapon.visualPrefab == null) EditorGUILayout.HelpBox("Asigná Visual Prefab en el arma.", MessageType.Warning);
            }
            else if (GUILayout.Button("Cerrar vista de ajuste")) ClosePreview();
            EditorGUI.BeginChangeCheck();
            holstered = EditorGUILayout.Toggle("Editar arma guardada", holstered);
            if (previewPair) editSecond = GUILayout.Toolbar(editSecond ? 1 : 0, new[] { "Mano principal", "Mano secundaria" }) == 1;
            clip = (AnimationClip)EditorGUILayout.ObjectField("Clip de previsualización", clip, typeof(AnimationClip), false);
            time = EditorGUILayout.Slider("Tiempo del clip",time,0,clip != null ? clip.length : 1);
            if (EditorGUI.EndChangeCheck()) Refresh();
            if (animator != null)
            {
                if (GUILayout.Button("Elegir hueso de anclaje…"))
                {
                    var menu = new GenericMenu();
                    foreach (var bone in animator.GetComponentsInChildren<Transform>(true).Where(t=>t!=animator.transform))
                    {
                        string path = AnimationUtility.CalculateTransformPath(bone, animator.transform);
                        menu.AddItem(new GUIContent(path),Pose.bonePath==path,()=> {
                            Undo.RecordObject(PoseOwner,"Cambiar anclaje"); Pose.anchor=WeaponAnchor.BonePath; Pose.bonePath=path;
                            EditorUtility.SetDirty(PoseOwner); Refresh();
                        });
                    }
                    menu.ShowAsContext();
                }
                if (Pose.Resolve(character.transform,animator)==null) EditorGUILayout.HelpBox("Anclaje sin resolver. Elegí un hueso válido para este rig.",MessageType.Warning);
                if(GUILayout.Button("Ocultar/mostrar malla integrada…"))
                {
                    var menu=new GenericMenu();
                    foreach(var renderer in animator.GetComponentsInChildren<Renderer>(true))
                    {
                        string path=AnimationUtility.CalculateTransformPath(renderer.transform,animator.transform);
                        bool hidden=profile.hiddenRendererPaths!=null && profile.hiddenRendererPaths.Contains(path);
                        menu.AddItem(new GUIContent(string.IsNullOrEmpty(path)?"(Animator)":path),hidden,()=> {
                            Undo.RecordObject(profile,"Visibilidad de malla integrada");
                            var paths=(profile.hiddenRendererPaths??new string[0]).ToList(); if(hidden)paths.Remove(path);else paths.Add(path);
                            profile.hiddenRendererPaths=paths.ToArray();EditorUtility.SetDirty(profile);Refresh();
                        });
                    }
                    menu.ShowAsContext();
                }
            }
            EditorGUILayout.LabelField("Pose de la pieza seleccionada", EditorStyles.boldLabel);
            var poseData = new SerializedObject(PoseOwner);
            poseData.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(poseData.FindProperty(editSecond && previewPair
                ? (holstered ? "secondaryHolstered" : "secondaryEquipped")
                : (holstered ? "holstered" : "equipped")), true);
            if (EditorGUI.EndChangeCheck()) { poseData.ApplyModifiedProperties(); Refresh(); }
            UnityEditor.Editor.CreateCachedEditor(profile,null,ref profileEditor);
            DrawTrailGUI();
            if(!profile.proceduralTrail)editTrailTip=EditorGUILayout.Toggle("Ajustar punta de estela simple",editTrailTip);
            if(editTrailTip)EditorGUILayout.HelpBox("Mové el punto naranja hasta la punta de la hoja. Edita Trail Tip del perfil compartido; no mueve el arma. Desactivá esta opción para volver a ajustar el agarre.",MessageType.Info);
            EditorGUI.BeginChangeCheck();
            profileEditor.serializedObject.Update();
            var excludedTrailFields=new[]{"meleeTrail","proceduralTrail","trailBase","trailTip","trailTaper","secondaryTrail","secondaryTrailBase","secondaryTrailTip","trailDuration","trailStartColor","trailEndColor","trailMaterial"};
            var profileProperty=profileEditor.serializedObject.GetIterator();bool enterChildren=true;
            while(profileProperty.NextVisible(enterChildren))
            {
                enterChildren=false;if(excludedTrailFields.Contains(profileProperty.name))continue;
                using(new EditorGUI.DisabledScope(profileProperty.name=="m_Script"))EditorGUILayout.PropertyField(profileProperty,true);
            }
            profileEditor.serializedObject.ApplyModifiedProperties();
            if(EditorGUI.EndChangeCheck()) Refresh();
            if(profile.animations==null && characterPrefab!=null && GUILayout.Button(weapon.family!=null?"Crear variantes de movimiento del perfil":"Crear conjunto de animaciones para este perfil"))
            {
                var sourceDriver=characterPrefab.GetComponent<Presentation.PlayerAnimationDriver>();
                var sourceAnimator=sourceDriver!=null && sourceDriver.Animator!=null ? sourceDriver.Animator : characterPrefab.GetComponentInChildren<Animator>();
                if(sourceAnimator==null || sourceAnimator.runtimeAnimatorController==null)
                    ShowNotification(new GUIContent("El personaje necesita un Animator Controller."));
                else
                {
                    string path=EditorUtility.SaveFilePanelInProject("Animaciones del arma",profile.name+"Animations","overrideController","Guardá el conjunto de clips");
                    if(!string.IsNullOrEmpty(path))
                    {
                        var overrides=new AnimatorOverrideController(sourceAnimator.runtimeAnimatorController);
                        AssetDatabase.CreateAsset(overrides,path); Undo.RecordObject(profile,"Asignar animaciones");
                        profile.animations=overrides; EditorUtility.SetDirty(profile);
                    }
                }
            }
            if(profile.animations!=null)
            {
                EditorGUILayout.LabelField(weapon.family!=null?"Movimiento del perfil · acciones en la familia":"Reemplazos de clips",EditorStyles.boldLabel);
                UnityEditor.Editor.CreateCachedEditor(profile.animations,null,ref animationEditor);
                animationEditor.OnInspectorGUI();
            }
            if (GUILayout.Button("Guardar arma y perfil")) { AssetDatabase.SaveAssetIfDirty(profile); AssetDatabase.SaveAssetIfDirty(weapon); if(previewPair){AssetDatabase.SaveAssetIfDirty(Offhand);if(Offhand.poseProfile!=null)AssetDatabase.SaveAssetIfDirty(Offhand.poseProfile);} if(profile.animations!=null)AssetDatabase.SaveAssetIfDirty(profile.animations); }
            EditorGUILayout.EndScrollView();
        }
        private void CreatePreview()
        {
            ClosePreview(); stage=CreateInstance<WeaponPoseStage>(); StageUtility.GoToStage(stage,true);
            character=stage.Clone(characterPrefab); character.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var driver=character.GetComponent<Presentation.PlayerAnimationDriver>();
            animator=driver != null && driver.Animator != null ? driver.Animator : character.GetComponentInChildren<Animator>();
            if(animator!=null)animator.enabled=false;
            previewBones=character.GetComponentsInChildren<Transform>(true);
            bonePositions=previewBones.Select(t=>t.localPosition).ToArray();
            boneRotations=previewBones.Select(t=>t.localRotation).ToArray();
            boneScales=previewBones.Select(t=>t.localScale).ToArray();
            previewRenderers=character.GetComponentsInChildren<Renderer>(true); previewVisibility=previewRenderers.Select(r=>r.enabled).ToArray();
            if (weapon.visualPrefab != null) visual=stage.Clone(weapon.visualPrefab);
            if (OffhandPrefab != null) secondVisual=stage.Clone(OffhandPrefab);
            previewMainPrefab = weapon.visualPrefab;
            previewSecondPrefab = OffhandPrefab;
            Refresh();
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(Vector3.up,Vector3.one*3),false);
        }
        private void Refresh()
        {
            if (character != null && weapon != null && (previewMainPrefab != weapon.visualPrefab || previewSecondPrefab != OffhandPrefab))
            {
                CreatePreview();
                return;
            }
            if (weapon != null) profile = weapon.poseProfile;
            if(character==null || visual==null || profile==null)return;
            for(int i=0;i<previewBones.Length;i++)
            {
                previewBones[i].localPosition=bonePositions[i];previewBones[i].localRotation=boneRotations[i];previewBones[i].localScale=boneScales[i];
            }
            if(clip!=null && animator!=null)clip.SampleAnimation(animator.gameObject,time);
            for(int i=0;i<previewRenderers.Length;i++)if(previewRenderers[i]!=null)previewRenderers[i].enabled=previewVisibility[i];
            profile.HideEmbeddedVisuals(animator);
            ApplyPreviewPose(visual, holstered ? profile.holstered : profile.equipped);
            if (secondVisual != null) ApplyPreviewPose(secondVisual, holstered ? Offhand.secondaryHolstered : Offhand.secondaryEquipped);
            RefreshTrailPreview();
            SceneView.RepaintAll(); Repaint();
        }
        private void ApplyPreviewPose(GameObject target, WeaponAttachmentPose pose)
        {
            var anchor = pose.Resolve(character.transform, animator);
            target.SetActive(anchor != null);
            if (anchor != null) pose.Apply(target.transform, anchor, animator);
        }
        private void DrawHandles(SceneView view)
        {
            if(stage==null || StageUtility.GetCurrentStage()!=stage || SelectedVisual==null || profile==null)return;
            if(editTrailEndpoints){DrawTrailEndpoints();return;}
            if(editTrailTip && visual!=null)
            {
                var root=visual.transform;
                Vector3 tip=root.TransformPoint(profile.trailTip);
                Handles.color=new Color(1,.6f,.1f);
                Handles.DrawLine(root.position,tip);
                Handles.Label(tip,"Punta de la estela");
                EditorGUI.BeginChangeCheck();
                tip=Handles.PositionHandle(tip,root.rotation);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(profile,"Ajustar punta de estela");
                    profile.trailTip=root.InverseTransformPoint(tip);
                    EditorUtility.SetDirty(profile);Repaint();
                }
                return;
            }
            var anchor=Pose.Resolve(character.transform,animator); if(anchor==null)return;
            Quaternion basis=Pose.Orientation(anchor,animator);
            EditorGUI.BeginChangeCheck();
            Vector3 position=SelectedVisual.transform.position; Quaternion rotation=SelectedVisual.transform.rotation;
            if(Tools.current==Tool.Rotate)rotation=Handles.RotationHandle(rotation,position);
            else position=Handles.PositionHandle(position,basis);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(PoseOwner,"Ajustar pose de arma");
                Pose.offset=Quaternion.Inverse(basis)*(position-anchor.position);
                Pose.rotation=(Quaternion.Inverse(basis)*rotation).eulerAngles;
                EditorUtility.SetDirty(PoseOwner); Refresh();
            }
        }
    }
}
