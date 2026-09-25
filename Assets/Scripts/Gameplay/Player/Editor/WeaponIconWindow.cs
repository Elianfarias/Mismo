using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    public sealed class WeaponIconWindow : EditorWindow
    {
        [SerializeField] WeaponDefinition weapon;
        [SerializeField] Vector3 rotation;
        InventoryPreview preview;
        bool needsPreview = true;

        [MenuItem("Mismo/Armas/Taller de iconos")]
        public static void Open()
        {
            var window = GetWindow<WeaponIconWindow>("Iconos de armas");
            if (Selection.activeObject is WeaponDefinition selected) window.Select(selected);
            window.Show();
        }

        void Select(WeaponDefinition value)
        {
            weapon = value;
            rotation = weapon != null ? weapon.inventoryPreviewRotation : Vector3.zero;
            needsPreview = true;
        }

        void OnDisable() { preview?.Dispose(); preview = null; }

        void OnGUI()
        {
            var selected = (WeaponDefinition)EditorGUILayout.ObjectField("Arma", weapon, typeof(WeaponDefinition), false);
            if (selected != weapon) Select(selected);
            if (weapon == null || weapon.visualPrefab == null)
            {
                EditorGUILayout.HelpBox("Elegí un arma con modelo visual para fotografiarla.", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox("Arrastrá sobre el modelo para girarlo. Rotación Z inclina la foto. Guardar conserva este ángulo para futuras voxelizaciones.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            rotation = EditorGUILayout.Vector3Field("Rotación de la foto", rotation);
            if (EditorGUI.EndChangeCheck()) needsPreview = true;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frente")) { rotation = Vector3.zero; needsPreview = true; }
                if (GUILayout.Button("Costado")) { rotation = new Vector3(0,90,0); needsPreview = true; }
                if (GUILayout.Button("Ángulo guardado")) Select(weapon);
            }
            Rect area = GUILayoutUtility.GetAspectRect(640f/480f);
            int control = GUIUtility.GetControlID(FocusType.Passive, area);
            EditorGUI.DrawRect(area,new Color(.12f,.13f,.16f));
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && area.Contains(evt.mousePosition))
            { GUIUtility.hotControl = control; evt.Use(); }
            if (evt.type == EventType.MouseDrag && evt.button == 0 && GUIUtility.hotControl == control)
            { rotation += new Vector3(evt.delta.y,-evt.delta.x,0)*.5f; needsPreview = true; evt.Use(); Repaint(); }
            if (evt.type == EventType.MouseUp && evt.button == 0 && GUIUtility.hotControl == control)
            { GUIUtility.hotControl = 0; evt.Use(); }
            if (needsPreview && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                preview ??= new InventoryPreview();
                preview.Show(weapon.visualPrefab,rotation);
                needsPreview = false;
            }
            if (preview != null && preview.HasModel) GUI.DrawTexture(area,preview.Texture,ScaleMode.ScaleToFit,true);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Guardar ángulo y actualizar icono",GUILayout.Height(30)))
                {
                    Undo.RecordObject(weapon,"Guardar ángulo del icono");
                    weapon.inventoryPreviewRotation = rotation;
                    InventoryPresentationAssets.RegenerateWeaponIcon(weapon);
                    AssetDatabase.SaveAssetIfDirty(weapon);
                    ShowNotification(new GUIContent("Foto actualizada"));
                }
        }
    }
}
