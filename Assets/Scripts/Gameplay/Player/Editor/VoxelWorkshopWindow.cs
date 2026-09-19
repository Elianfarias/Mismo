using System.Linq;
using Mismo.Gameplay.Voxels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>
    /// Taller de voxelización aislado. No modifica MovementPrototype ni prefabs existentes:
    /// crea sus resultados en Assets/Data/Voxels, Assets/Art/Prefabs/Voxel y VoxelWorkshop.unity.
    /// </summary>
    public sealed class VoxelWorkshopWindow : EditorWindow
    {
        private const string WorkshopScenePath = "Assets/Scenes/VoxelWorkshop.unity";
        private const string VoxelDataFolder = "Assets/Data/Voxels";
        private const string VoxelPrefabFolder = "Assets/Art/Prefabs/Voxel";
        private const string MaterialFolder = "Assets/Art/Materials";

        private GameObject sourceModel;
        private VoxelModelAsset currentAsset;
        private string assetName = "VoxelModel";
        private Vector3Int dimensions = new Vector3Int(16, 16, 16);
        private float voxelSize = 0.1f;
        private bool fitToBounds = true;
        private Color voxelColor = new Color(0.35f, 0.75f, 1f, 1f);
        private Vector3Int editCoordinate;
        private Vector2 scroll;

        [MenuItem("Mismo/Voxels/Open Voxel Workshop")]
        public static void OpenWorkshop()
        {
            EnsureFolders();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkshopScenePath) == null)
                CreateWorkshopScene();
            else
            {
                Scene workshop = SceneManager.GetSceneByPath(WorkshopScenePath);
                if (!workshop.IsValid() || !workshop.isLoaded)
                    workshop = EditorSceneManager.OpenScene(WorkshopScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(workshop);
            }
            GetWindow<VoxelWorkshopWindow>("Voxel Workshop");
        }

        [MenuItem("Mismo/Voxels/Create Voxel Workshop Scene")]
        public static void CreateWorkshopSceneMenu() => CreateWorkshopScene();

        private static void CreateWorkshopScene()
        {
            EnsureFolders();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameObject root = new GameObject("Voxel Workshop");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Workshop Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.position = new Vector3(0f, -0.08f, 0f);
            floor.transform.localScale = new Vector3(24f, 0.1f, 24f);

            GameObject cameraObject = new GameObject("Workshop Camera", typeof(UnityEngine.Camera));
            cameraObject.transform.position = new Vector3(0f, 4.5f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
            cameraObject.GetComponent<UnityEngine.Camera>().clearFlags = CameraClearFlags.SolidColor;
            cameraObject.GetComponent<UnityEngine.Camera>().backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            cameraObject.GetComponent<UnityEngine.Camera>().tag = "MainCamera";

            GameObject lightObject = new GameObject("Workshop Light", typeof(Light));
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, WorkshopScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Voxel Workshop creado en {WorkshopScenePath}. No se modificaron escenas existentes.");
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Voxel Workshop", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Arrastrá un modelo 3D, definí la grilla y generá un asset voxel. Este taller es independiente del prototipo de movimiento.", MessageType.Info);

            sourceModel = (GameObject)EditorGUILayout.ObjectField("Modelo fuente", sourceModel, typeof(GameObject), true);
            assetName = EditorGUILayout.TextField("Nombre del asset", assetName);
            dimensions = EditorGUILayout.Vector3IntField("Dimensiones", dimensions);
            fitToBounds = EditorGUILayout.Toggle("Ajustar al modelo", fitToBounds);
            using (new EditorGUI.DisabledScope(fitToBounds))
                voxelSize = Mathf.Max(0.001f, EditorGUILayout.FloatField("Tamaño de voxel", voxelSize));
            voxelColor = EditorGUILayout.ColorField("Color inicial", voxelColor);

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Voxelizar modelo")) VoxelizeSource();
            currentAsset = (VoxelModelAsset)EditorGUILayout.ObjectField("Asset actual", currentAsset, typeof(VoxelModelAsset), false);
            if (currentAsset != null)
            {
                EditorGUILayout.LabelField($"Celdas ocupadas: {currentAsset.CountFilled()} / {currentAsset.CellCount}");
                DrawVoxelEditingControls();
                EditorGUILayout.Space(4f);
                if (GUILayout.Button("Crear/actualizar prefab voxel")) CreatePrefab();
                if (GUILayout.Button("Mostrar preview en esta escena")) CreatePreview();
                if (GUILayout.Button("Reconstruir preview seleccionado")) RebuildSelected();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox("El mismo VoxelModelAsset puede usarse para un personaje, una espada u otra arma. Agregar y quitar voxeles no modifica el modelo fuente.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void DrawVoxelEditingControls()
        {
            EditorGUILayout.LabelField("Edición por coordenada", EditorStyles.boldLabel);
            editCoordinate = EditorGUILayout.Vector3IntField("Coordenada", editCoordinate);
            using (new EditorGUI.DisabledScope(!currentAsset.IsInside(editCoordinate)))
            {
                if (GUILayout.Button("Agregar voxel")) SetVoxel(true);
                if (GUILayout.Button("Quitar voxel")) SetVoxel(false);
            }
            if (!currentAsset.IsInside(editCoordinate))
                EditorGUILayout.HelpBox("La coordenada está fuera de las dimensiones actuales.", MessageType.Warning);
        }

        private void SetVoxel(bool filled)
        {
            Undo.RecordObject(currentAsset, filled ? "Add voxel" : "Remove voxel");
            currentAsset.SetVoxel(editCoordinate, filled, filled ? voxelColor : currentAsset.DefaultColor);
            EditorUtility.SetDirty(currentAsset);
            AssetDatabase.SaveAssets();
            RebuildSelected();
            Repaint();
        }

        private void VoxelizeSource()
        {
            if (sourceModel == null)
            {
                EditorUtility.DisplayDialog("Voxel Workshop", "Arrastrá primero un modelo 3D al campo Modelo fuente.", "Aceptar");
                return;
            }
            dimensions = new Vector3Int(Mathf.Max(1, dimensions.x), Mathf.Max(1, dimensions.y), Mathf.Max(1, dimensions.z));
            EnsureFolders();

            GameObject clone = InstantiateForSampling(sourceModel);
            try
            {
                Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    EditorUtility.DisplayDialog("Voxel Workshop", "El modelo no contiene Renderers para voxelizar.", "Aceptar");
                    return;
                }

                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                float cellSize = fitToBounds
                    ? Mathf.Max(bounds.size.x / dimensions.x, Mathf.Max(bounds.size.y / dimensions.y, bounds.size.z / dimensions.z))
                    : voxelSize;
                cellSize = Mathf.Max(0.001f, cellSize);

                string assetPath = $"{VoxelDataFolder}/{Sanitize(assetName)}.asset";
                VoxelModelAsset asset = AssetDatabase.LoadAssetAtPath<VoxelModelAsset>(assetPath);
                if (asset == null)
                {
                    asset = CreateInstance<VoxelModelAsset>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                }
                asset.SetDefaultColor(voxelColor);
                asset.Configure(dimensions, cellSize);
                Physics.SyncTransforms();

                int mask = 1 << 31;
                for (int z = 0; z < dimensions.z; z++)
                    for (int y = 0; y < dimensions.y; y++)
                        for (int x = 0; x < dimensions.x; x++)
                        {
                            Vector3Int coordinate = new Vector3Int(x, y, z);
                            Vector3 point = bounds.min + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, (z + 0.5f) * cellSize);
                            bool occupied = Physics.CheckBox(point, Vector3.one * cellSize * 0.46f, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                            asset.SetVoxel(coordinate, occupied, voxelColor);
                        }

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                currentAsset = asset;
                assetName = asset.name;
                Debug.Log($"Voxelizado {sourceModel.name}: {asset.CountFilled()} voxeles en {assetPath}.");
                Repaint();
            }
            finally
            {
                DestroyImmediate(clone);
            }
        }

        private static GameObject InstantiateForSampling(GameObject source)
        {
            GameObject clone = Object.Instantiate(source);
            clone.name = "Voxel Sampling Source";
            clone.hideFlags = HideFlags.HideAndDontSave;
            foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 31;
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && child.GetComponent<Collider>() == null)
                {
                    MeshCollider collider = child.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }
                SkinnedMeshRenderer skinned = child.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null && skinned.sharedMesh != null && child.GetComponent<Collider>() == null)
                {
                    Mesh baked = new Mesh { name = "Voxel Sampling Baked Mesh" };
                    skinned.BakeMesh(baked);
                    MeshCollider collider = child.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = baked;
                }
            }
            return clone;
        }

        private void CreatePrefab()
        {
            EnsureFolders();
            string prefabPath = $"{VoxelPrefabFolder}/{Sanitize(currentAsset.name)}.prefab";
            Material material = GetOrCreateMaterial();
            GameObject root = new GameObject(currentAsset.name);
            VoxelModelInstance instance = root.AddComponent<VoxelModelInstance>();
            instance.Configure(currentAsset, material);
            root.AddComponent<VoxelRuntimeCustomizer>();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            Debug.Log($"Prefab voxel creado en {prefabPath}. El modelo fuente no fue modificado.");
        }

        private void CreatePreview()
        {
            Material material = GetOrCreateMaterial();
            GameObject existing = FindPreview();
            if (existing != null) DestroyImmediate(existing);
            GameObject preview = new GameObject($"Voxel Preview - {currentAsset.name}");
            preview.transform.position = Vector3.zero;
            VoxelModelInstance instance = preview.AddComponent<VoxelModelInstance>();
            instance.Configure(currentAsset, material);
            preview.AddComponent<VoxelRuntimeCustomizer>();
            Selection.activeGameObject = preview;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void RebuildSelected()
        {
            VoxelModelInstance instance = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<VoxelModelInstance>()
                : null;
            if (instance != null) instance.Rebuild();
        }

        private static GameObject FindPreview()
        {
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject)
                .FirstOrDefault(go => go.name.StartsWith("Voxel Preview -"));
        }

        private static Material GetOrCreateMaterial()
        {
            string path = $"{MaterialFolder}/VoxelPrototype.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader) { color = new Color(0.35f, 0.75f, 1f, 1f) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(VoxelDataFolder);
            EnsureFolder("Assets/Art/Prefabs");
            EnsureFolder(VoxelPrefabFolder);
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "VoxelModel";
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Trim();
        }
    }
}
