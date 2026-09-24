using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    public sealed class CombatFeedbackWindow : EditorWindow
    {
        WeaponDefinition weapon;
        CombatFeedbackProfile previous;
        bool editShared;
        CombatCue cue = CombatCue.Impact;
        Vector2 scroll, profileScroll;
        PreviewRenderUtility preview;
        GameObject effect, body;
        double began;

        [MenuItem("Mismo/Armas/Taller de feedback")]
        public static void Open() => Open(Selection.activeObject as WeaponDefinition);
        public static void Open(WeaponDefinition selected)
        { var window = GetWindow<CombatFeedbackWindow>("Feedback de combate"); window.weapon = selected; window.editShared = false; }
        void OnDisable() => ClosePreview();
        void ClosePreview() { preview?.Cleanup(); preview = null; effect = body = null; }
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawContent();
            EditorGUILayout.EndScrollView();
        }
        void DrawContent()
        {
            EditorGUI.BeginChangeCheck();
            weapon = (WeaponDefinition)EditorGUILayout.ObjectField("Arma", weapon, typeof(WeaponDefinition), false);
            if (EditorGUI.EndChangeCheck()) { editShared = false; ClosePreview(); }
            if (weapon == null) return;
            var serialized = new SerializedObject(weapon);
            serialized.Update(); EditorGUILayout.PropertyField(serialized.FindProperty("feedbackOverride"), new GUIContent("Override del arma")); serialized.ApplyModifiedProperties();
            var profile = weapon.FeedbackProfile;
            if (profile != previous) { previous = profile; editShared = false; ClosePreview(); }
            bool own = weapon.feedbackOverride != null;
            EditorGUILayout.HelpBox("Origen: " + (own ? "override de " + weapon.name : "familia " + (weapon.family != null ? weapon.family.name : "sin asignar")) +
                "\nDestino: " + (profile != null ? AssetDatabase.GetAssetPath(profile) : "sin perfil") +
                "\nLos assets referenciados pueden estar compartidos. Duplicar crea parámetros independientes; no duplica prefabs ni sonidos.", MessageType.Info);
            if (GUILayout.Button("Crear copia independiente para esta arma"))
            {
                EnsureFolder();
                var copy = profile != null ? Instantiate(profile) : CreateInstance<CombatFeedbackProfile>();
                AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath("Assets/Data/Combat/Feedback/" + weapon.name + "Feedback.asset"));
                Undo.RecordObject(weapon, "Feedback propio"); weapon.feedbackOverride = copy; EditorUtility.SetDirty(weapon);
                AssetDatabase.SaveAssetIfDirty(weapon); return;
            }
            if (weapon.family != null)
            {
                var familyData = new SerializedObject(weapon.family);
                familyData.Update();
                EditorGUILayout.PropertyField(familyData.FindProperty("feedback"), new GUIContent("Perfil de familia (compartido)"));
                familyData.ApplyModifiedProperties();
            }
            if (profile == null) return;
            // Explicit opt-in even for an override: users may have assigned a shared asset manually.
            editShared = EditorGUILayout.ToggleLeft("Editar este asset y todas las armas que lo referencian", editShared);
            profileScroll = EditorGUILayout.BeginScrollView(profileScroll, GUILayout.MaxHeight(340));
            using (new EditorGUI.DisabledScope(!editShared))
            {
                var data = new SerializedObject(profile); data.Update();
                var iterator = data.GetIterator(); iterator.NextVisible(true);
                while (iterator.NextVisible(false)) EditorGUILayout.PropertyField(iterator, true);
                if (data.ApplyModifiedProperties()) ClosePreview();
            }
            EditorGUILayout.EndScrollView();
            cue = (CombatCue)EditorGUILayout.EnumPopup("Evento a probar", cue);
            if (GUILayout.Button("Previsualizar efecto (referencia: cuerpo de 1,6 m)")) StartPreview(profile.Get(cue));
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (GUILayout.Button("Probar efecto + sonido en Play Mode"))
                {
                    var target = Selection.activeGameObject;
                    Vector3 point = target != null ? target.transform.position + Vector3.up : Vector3.zero;
                    CombatImpactPool.Instance.Play(profile.Get(cue), point, Vector3.back);
                }
            EditorGUILayout.HelpBox("La prueba usa el objeto seleccionado como punto de impacto. No aplica daño ni pausa. En combate, sólo los resultados confirmados generan efectos.", MessageType.None);
            if (GUILayout.Button("Guardar perfil")) AssetDatabase.SaveAssetIfDirty(profile);
            if (preview == null) return;
            Rect rect = GUILayoutUtility.GetRect(100, 240, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            float time = (float)(EditorApplication.timeSinceStartup - began);
            if (effect != null) foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>()) particle.Simulate(Mathf.Min(time, 3), false, true);
            preview.BeginPreview(rect, GUIStyle.none); preview.Render();
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill); if (time < 3) Repaint();
        }
        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/Combat")) AssetDatabase.CreateFolder("Assets/Data", "Combat");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Combat/Feedback")) AssetDatabase.CreateFolder("Assets/Data/Combat", "Feedback");
        }
        void StartPreview(CombatFeedbackCue entry)
        {
            ClosePreview(); if (entry == null || entry.prefab == null) return;
            preview = new PreviewRenderUtility();
            preview.camera.transform.position = new Vector3(0, 1.1f, -4); preview.camera.transform.LookAt(new Vector3(0, .8f, 0));
            preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 20;
            preview.camera.backgroundColor = new Color(.12f,.14f,.17f); preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.lights[0].intensity = 1;
            body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.transform.position = Vector3.up * .8f; body.transform.localScale = new Vector3(.5f,1.6f,.35f); preview.AddSingleGO(body);
            effect = Instantiate(entry.prefab); effect.transform.position = new Vector3(0, 1, -.25f);
            var properties = new MaterialPropertyBlock(); properties.SetColor("_Color", entry.tint);
            foreach(var renderer in effect.GetComponentsInChildren<ParticleSystemRenderer>())renderer.SetPropertyBlock(properties);
            foreach(var particle in effect.GetComponentsInChildren<ParticleSystem>()){var main=particle.main;main.scalingMode=ParticleSystemScalingMode.Hierarchy;}
            effect.transform.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(entry.rotation); effect.transform.localScale *= entry.scale;
            preview.AddSingleGO(effect); began = EditorApplication.timeSinceStartup; Repaint();
        }
    }
}
