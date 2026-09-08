using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Voxels
{
    /// <summary>Panel y controles de ratón exclusivos de la escena de taller.</summary>
    public sealed class VoxelRuntimePanel : MonoBehaviour
    {
        private VoxelModelInstance model;
        private VoxelRuntimeCustomizer customizer;
        private Camera view;
        private int mode;
        private Vector3Int coordinate;
        private Color color = Color.white;
        private string slot = "mi_modelo";
        private string status = "Elegí una herramienta y hacé click sobre el modelo.";
        private readonly Stack<Edit> undo = new Stack<Edit>();
        private struct Edit { public Vector3Int cell; public bool filled; public Color color; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++) AddPanel(SceneManager.GetSceneAt(i));
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddPanel(scene);
        private static void AddPanel(Scene scene)
        {
            if (scene.name != "VoxelWorkshop") return;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<VoxelRuntimePanel>() != null) return;
            GameObject panel = new GameObject("Voxel Runtime Tools");
            SceneManager.MoveGameObjectToScene(panel, scene);
            panel.AddComponent<VoxelRuntimePanel>();
        }
        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (view == null) view = root.GetComponentInChildren<Camera>();
                if (model == null) model = root.GetComponentInChildren<VoxelModelInstance>();
            }
            if (model != null) Select(model);
        }
        private void Select(VoxelModelInstance selected)
        {
            model = selected;
            customizer = model.GetComponent<VoxelRuntimeCustomizer>();
            if (customizer == null) customizer = model.gameObject.AddComponent<VoxelRuntimeCustomizer>();
            slot = customizer.SaveSlot;
            undo.Clear();
        }
        private void Update()
        {
            if (model == null || model.Asset == null || view == null || Mouse.current == null) return;
            Mouse mouse = Mouse.current;
            Vector2 screen = mouse.position.ReadValue();
            if (screen.x < 310) return;
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                view.transform.RotateAround(model.transform.position, Vector3.up, delta.x * 0.3f);
                view.transform.RotateAround(model.transform.position, view.transform.right, -delta.y * 0.3f);
            }
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel != 0) view.transform.position = model.transform.position +
                (view.transform.position - model.transform.position) * (wheel > 0 ? 0.9f : 1.1f);
            if (!mouse.leftButton.wasPressedThisFrame) return;
            Ray world = view.ScreenPointToRay(screen);
            Ray ray = new Ray(model.transform.InverseTransformPoint(world.origin), model.transform.InverseTransformVector(world.direction).normalized);
            float nearest = float.PositiveInfinity;
            Vector3Int hit = default;
            Vector3 hitPoint = default;
            Vector3Int size = model.Dimensions;
            for (int z = 0; z < size.z; z++) for (int y = 0; y < size.y; y++) for (int x = 0; x < size.x; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, z);
                if (!model.GetRuntimeFilled(cell)) continue;
                Bounds bounds = new Bounds(model.Asset.GridToLocal(cell), Vector3.one * model.Asset.VoxelSize);
                if (bounds.IntersectRay(ray, out float distance) && distance < nearest)
                { nearest = distance; hit = cell; hitPoint = ray.GetPoint(distance); }
            }
            if (float.IsInfinity(nearest)) return;
            coordinate = hit;
            if (mode == 0)
            {
                Vector3 relative = hitPoint - model.Asset.GridToLocal(hit);
                Vector3 absolute = new Vector3(Mathf.Abs(relative.x), Mathf.Abs(relative.y), Mathf.Abs(relative.z));
                if (absolute.x >= absolute.y && absolute.x >= absolute.z) coordinate.x += relative.x >= 0 ? 1 : -1;
                else if (absolute.y >= absolute.z) coordinate.y += relative.y >= 0 ? 1 : -1;
                else coordinate.z += relative.z >= 0 ? 1 : -1;
            }
            Apply();
        }
        private void Apply()
        {
            if (!model.Asset.IsInside(coordinate)) { status = "Límite de la grilla alcanzado."; return; }
            undo.Push(new Edit { cell = coordinate, filled = model.GetRuntimeFilled(coordinate), color = model.GetRuntimeColor(coordinate) });
            model.SetVoxelRuntime(coordinate, mode != 1, color);
            status = "Editado " + coordinate;
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 290, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("TALLER VOXEL · PLAY MODE");
            if (model == null || model.Asset == null)
            {
                GUILayout.Label("Falta un modelo voxel. Fuera de Play, voxelizá tu modelo y pulsá Mostrar preview en esta escena. Después entrá en Play.");
                GUILayout.EndArea(); return;
            }
            GUILayout.Label(model.Asset.name);
            mode = GUILayout.Toolbar(mode, new[] { "Agregar", "Quitar", "Pintar" });
            GUILayout.Label("Color RGB");
            color.r = GUILayout.HorizontalSlider(color.r, 0, 1);
            color.g = GUILayout.HorizontalSlider(color.g, 0, 1);
            color.b = GUILayout.HorizontalSlider(color.b, 0, 1);
            GUILayout.Label("Coordenada (también permite empezar vacío)");
            coordinate.x = Mathf.RoundToInt(GUILayout.HorizontalSlider(coordinate.x, 0, model.Dimensions.x - 1));
            coordinate.y = Mathf.RoundToInt(GUILayout.HorizontalSlider(coordinate.y, 0, model.Dimensions.y - 1));
            coordinate.z = Mathf.RoundToInt(GUILayout.HorizontalSlider(coordinate.z, 0, model.Dimensions.z - 1));
            if (GUILayout.Button("Aplicar en " + coordinate)) Apply();
            if (GUILayout.Button("Deshacer") && undo.Count > 0)
            { Edit edit = undo.Pop(); model.SetVoxelRuntime(edit.cell, edit.filled, edit.color); }
            GUILayout.Label("Nombre del guardado");
            slot = GUILayout.TextField(slot);
            if (GUILayout.Button("Guardar")) { customizer.SelectSlot(slot); status = customizer.Save() ? "Guardado: " + customizer.SavePath : "No se pudo guardar."; }
            if (GUILayout.Button("Cargar")) { customizer.SelectSlot(slot); bool loaded = customizer.Load(); status = loaded ? "Cargado." : "No existe o es incompatible."; if (loaded) undo.Clear(); }
            GUILayout.Label("Click izquierdo: editar\nBotón derecho: girar cámara\nRueda: zoom");
            GUILayout.Label(status);
            GUILayout.EndArea();
        }
    }
}
