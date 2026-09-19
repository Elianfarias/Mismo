using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>
    /// Converts a weapon mesh into one optimized voxel mesh asset and prefab.
    /// It is intentionally editor-only: the original weapon is never modified.
    /// </summary>
    public sealed class WeaponVoxelizerWindow : EditorWindow
    {
        private const int SamplingLayer = 31;
        private const string OutputFolder = "Assets/Data/Weapons/Voxelized";
        private const string MaterialFolder = "Assets/Art/Materials";

        private GameObject sourceWeapon;
        private string outputName = "VoxelWeapon";
        private int resolution = 32;
        private bool fillInterior = true;
        private bool preserveSourceMaterials = true;
        private bool addCollider = true;
        private bool convexCollider;
        private Color weaponColor = Color.white;
        private Vector2 scroll;
        private string lastOutput;

        [MenuItem("Mismo/Armas/Voxelizar arma")]
        private static void Open() => GetWindow<WeaponVoxelizerWindow>("Voxelizar arma");

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Voxelizador de armas", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Convierte una espada, arco, escudo u otra arma en una malla voxelizada combinada. " +
                "El modelo fuente permanece intacto y no se crea un GameObject por voxel.", MessageType.Info);

            sourceWeapon = (GameObject)EditorGUILayout.ObjectField("Arma fuente", sourceWeapon, typeof(GameObject), true);
            outputName = EditorGUILayout.TextField("Nombre de salida", outputName);
            resolution = Mathf.Clamp(EditorGUILayout.IntSlider("Resolución máxima", resolution, 8, 128), 8, 128);
            fillInterior = EditorGUILayout.Toggle(new GUIContent("Rellenar interior", "Cierra huecos internos para que el arma sea sólida"), fillInterior);
            preserveSourceMaterials = EditorGUILayout.Toggle(new GUIContent("Conservar materiales", "Mantiene materiales, texturas y colores de las piezas del arma"), preserveSourceMaterials);
            weaponColor = EditorGUILayout.ColorField("Color del voxel", weaponColor);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Prefab generado", EditorStyles.boldLabel);
            addCollider = EditorGUILayout.Toggle("Agregar MeshCollider", addCollider);
            using (new EditorGUI.DisabledScope(!addCollider))
                convexCollider = EditorGUILayout.Toggle(new GUIContent("Collider convexo", "Útil si el arma tendrá Rigidbody"), convexCollider);

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(sourceWeapon == null))
                if (GUILayout.Button("Voxelizar y generar prefab", GUILayout.Height(30))) Generate();
            if (!string.IsNullOrEmpty(lastOutput))
                EditorGUILayout.HelpBox(lastOutput, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void Generate()
        {
            if (sourceWeapon == null)
            {
                EditorUtility.DisplayDialog("Voxelizar arma", "Seleccioná primero un prefab u objeto con MeshRenderer o SkinnedMeshRenderer.", "Aceptar");
                return;
            }

            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Weapons");
            EnsureFolder(OutputFolder);
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialFolder);

            GameObject samplingClone = null;
            try
            {
                samplingClone = CreateSamplingClone(sourceWeapon);
                Renderer[] renderers = samplingClone.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
                if (renderers.Length == 0)
                {
                    EditorUtility.DisplayDialog("Voxelizar arma", "El objeto no contiene un renderer activo.", "Aceptar");
                    return;
                }

                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                List<Material> sourceMaterials = preserveSourceMaterials ? CollectSourceMaterials(renderers) : new List<Material>();
                float largestAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                float voxelSize = Mathf.Max(0.0001f, largestAxis / resolution);
                Vector3Int dimensions = new Vector3Int(
                    Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / voxelSize)),
                    Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / voxelSize)),
                    Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / voxelSize)));

                int[] voxelMaterials = new int[dimensions.x * dimensions.y * dimensions.z];
                for (int i = 0; i < voxelMaterials.Length; i++) voxelMaterials[i] = -1;
                Color[] voxelColors = new Color[voxelMaterials.Length];
                Vector2[] voxelUVs = new Vector2[voxelMaterials.Length];
                bool[] occupied = SampleSurface(bounds, dimensions, voxelSize, sourceMaterials, voxelMaterials, voxelColors, voxelUVs);
                if (fillInterior) FillInterior(occupied, dimensions);
                if (!occupied.Any(value => value))
                {
                    EditorUtility.DisplayDialog("Voxelizar arma", "No se encontraron voxeles ocupados. Probá aumentar la resolución.", "Aceptar");
                    return;
                }

                Mesh mesh = BuildMesh(occupied, dimensions, voxelSize, bounds.min, bounds.center, weaponColor, outputName, preserveSourceMaterials, voxelMaterials, voxelUVs, sourceMaterials.Count + 1);
                string safeName = Sanitize(outputName);
                string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{safeName}.asset");
                AssetDatabase.CreateAsset(mesh, meshPath);
                Material[] materials = BuildMaterialPalette(safeName, weaponColor, sourceMaterials);
                string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{safeName}.prefab");
                GameObject prefab = CreatePrefab(mesh, materials, prefabPath, safeName, addCollider, convexCollider);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                lastOutput = $"Generado: {prefabPath}\n" +
                    $"Resolución: {dimensions.x} × {dimensions.y} × {dimensions.z} · Voxeles: {occupied.Count(value => value)}";
                Debug.Log($"Arma voxelizada: {prefabPath} (mesh: {meshPath})");
            }
            finally
            {
                if (samplingClone != null) DestroyImmediate(samplingClone);
                EditorUtility.ClearProgressBar();
            }
        }

        private bool[] SampleSurface(Bounds bounds, Vector3Int dimensions, float voxelSize, List<Material> sourceMaterials, int[] voxelMaterials, Color[] voxelColors, Vector2[] voxelUVs)
        {
            int total = dimensions.x * dimensions.y * dimensions.z;
            bool[] occupied = new bool[total];
            int mask = 1 << SamplingLayer;
            Dictionary<Material, int> materialIndices = new Dictionary<Material, int>();
            for (int i = 0; i < sourceMaterials.Count; i++) materialIndices[sourceMaterials[i]] = i;
            Collider[] hits = new Collider[32];
            Vector3[] rayDirections = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int z = 0; z < dimensions.z; z++)
                for (int y = 0; y < dimensions.y; y++)
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        int index = Index(x, y, z, dimensions);
                        Vector3 center = bounds.min + new Vector3(x + .5f, y + .5f, z + .5f) * voxelSize;
                        if (sourceMaterials.Count == 0)
                        {
                            occupied[index] = Physics.CheckBox(center, Vector3.one * voxelSize * .49f, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                        }
                        else
                        {
                            int hitCount = Physics.OverlapBoxNonAlloc(center, Vector3.one * voxelSize * .49f, hits, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                            occupied[index] = hitCount > 0;
                            for (int hit = 0; hit < hitCount; hit++)
                            {
                                Renderer renderer = hits[hit].GetComponent<Renderer>() ?? hits[hit].GetComponentInParent<Renderer>();
                                Material material = renderer != null && renderer.sharedMaterials.Length > 0 ? renderer.sharedMaterials[0] : null;
                                if (material == null || !materialIndices.TryGetValue(material, out int materialIndex)) continue;
                                voxelMaterials[index] = materialIndex;
                                voxelColors[index] = material.color;
                                break;
                            }

                            if (occupied[index])
                            {
                                // The original weapon materials use texture atlases. A generated
                                // voxel mesh has no original UV layout, so sample one UV from the
                                // source surface and reuse it on the voxel faces. This keeps the
                                // local wood/metal/color region instead of showing one atlas pixel.
                                bool uvSampled = false;
                                for (int direction = 0; direction < rayDirections.Length && !uvSampled; direction++)
                                {
                                    Vector3 rayDirection = rayDirections[direction];
                                    Ray ray = new Ray(center + rayDirection * voxelSize * 4f, -rayDirection);
                                    if (!Physics.Raycast(ray, out RaycastHit surfaceHit, voxelSize * 8f, mask, QueryTriggerInteraction.Ignore)) continue;
                                    Renderer surfaceRenderer = FindRenderer(surfaceHit.collider);
                                    if (surfaceRenderer == null || surfaceRenderer.sharedMaterials == null) continue;
                                    Material[] surfaceMaterials = surfaceRenderer.sharedMaterials;
                                    for (int materialSlot = 0; materialSlot < surfaceMaterials.Length; materialSlot++)
                                    {
                                        Material surfaceMaterial = surfaceMaterials[materialSlot];
                                        if (surfaceMaterial == null || !materialIndices.ContainsKey(surfaceMaterial)) continue;
                                        voxelMaterials[index] = materialIndices[surfaceMaterial];
                                        voxelUVs[index] = surfaceHit.textureCoord;
                                        uvSampled = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if ((index & 2047) == 0)
                            EditorUtility.DisplayProgressBar("Voxelizar arma", "Muestreando la superficie...", index / (float)total);
                    }
            return occupied;
        }

        private static void FillInterior(bool[] occupied, Vector3Int dimensions)
        {
            bool[] outside = new bool[occupied.Length];
            Queue<int> pending = new Queue<int>();
            for (int z = 0; z < dimensions.z; z++)
                for (int y = 0; y < dimensions.y; y++)
                    for (int x = 0; x < dimensions.x; x++)
                        if ((x == 0 || y == 0 || z == 0 || x == dimensions.x - 1 || y == dimensions.y - 1 || z == dimensions.z - 1) && TryMarkOutside(x, y, z, occupied, outside, dimensions))
                            pending.Enqueue(Index(x, y, z, dimensions));

            Vector3Int[] directions = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                Unpack(index, dimensions, out int x, out int y, out int z);
                foreach (Vector3Int direction in directions)
                {
                    int nx = x + direction.x, ny = y + direction.y, nz = z + direction.z;
                    if (nx < 0 || ny < 0 || nz < 0 || nx >= dimensions.x || ny >= dimensions.y || nz >= dimensions.z) continue;
                    if (TryMarkOutside(nx, ny, nz, occupied, outside, dimensions)) pending.Enqueue(Index(nx, ny, nz, dimensions));
                }
            }

            for (int i = 0; i < occupied.Length; i++)
                if (!occupied[i] && !outside[i]) occupied[i] = true;
        }

        private static bool TryMarkOutside(int x, int y, int z, bool[] occupied, bool[] outside, Vector3Int dimensions)
        {
            int index = Index(x, y, z, dimensions);
            if (occupied[index] || outside[index]) return false;
            outside[index] = true;
            return true;
        }

        private static Mesh BuildMesh(bool[] occupied, Vector3Int dimensions, float voxelSize, Vector3 boundsMin, Vector3 pivot, Color color, string name, bool preserveMaterials, int[] voxelMaterials, Vector2[] voxelUVs, int submeshCount)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<List<int>> submeshTriangles = new List<List<int>>();
            for (int i = 0; i < Mathf.Max(1, preserveMaterials ? submeshCount : 1); i++) submeshTriangles.Add(new List<int>());
            List<Vector3> normals = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<Vector2> uvs = new List<Vector2>();
            Vector3 half = Vector3.one * voxelSize * .5f;
            Vector3Int[] directions = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
            Vector3[][] faces =
            {
                new[]{new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(1,1,1),new Vector3(1,-1,1)},
                new[]{new Vector3(-1,-1,-1),new Vector3(-1,-1,1),new Vector3(-1,1,1),new Vector3(-1,1,-1)},
                new[]{new Vector3(-1,1,-1),new Vector3(-1,1,1),new Vector3(1,1,1),new Vector3(1,1,-1)},
                new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,-1,1),new Vector3(-1,-1,1)},
                new[]{new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)},
                new[]{new Vector3(-1,-1,-1),new Vector3(-1,1,-1),new Vector3(1,1,-1),new Vector3(1,-1,-1)}
            };

            for (int z = 0; z < dimensions.z; z++)
                for (int y = 0; y < dimensions.y; y++)
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        int voxelIndex = Index(x, y, z, dimensions);
                        if (!occupied[voxelIndex]) continue;
                        int materialIndex = preserveMaterials && voxelMaterials != null && voxelMaterials[voxelIndex] >= 0 ? voxelMaterials[voxelIndex] + 1 : 0;
                        materialIndex = Mathf.Clamp(materialIndex, 0, submeshTriangles.Count - 1);
                        Vector2 uv = voxelUVs != null && voxelIndex < voxelUVs.Length ? voxelUVs[voxelIndex] : Vector2.zero;
                        Vector3 center = boundsMin + new Vector3((x + .5f) * voxelSize, (y + .5f) * voxelSize, (z + .5f) * voxelSize) - pivot;
                        for (int face = 0; face < directions.Length; face++)
                        {
                            int nx = x + directions[face].x, ny = y + directions[face].y, nz = z + directions[face].z;
                            if (nx >= 0 && ny >= 0 && nz >= 0 && nx < dimensions.x && ny < dimensions.y && nz < dimensions.z && occupied[Index(nx, ny, nz, dimensions)]) continue;
                            int start = vertices.Count;
                            for (int corner = 0; corner < 4; corner++)
                            {
                                vertices.Add(center + Vector3.Scale(faces[face][corner], half));
                                normals.Add(directions[face]);
                                colors.Add(color);
                                uvs.Add(uv);
                            }
                            submeshTriangles[materialIndex].Add(start); submeshTriangles[materialIndex].Add(start + 1); submeshTriangles[materialIndex].Add(start + 2);
                            submeshTriangles[materialIndex].Add(start); submeshTriangles[materialIndex].Add(start + 2); submeshTriangles[materialIndex].Add(start + 3);
                        }
                    }

            Mesh mesh = new Mesh { name = string.IsNullOrWhiteSpace(name) ? "VoxelWeapon" : name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.subMeshCount = submeshTriangles.Count;
            for (int i = 0; i < submeshTriangles.Count; i++) mesh.SetTriangles(submeshTriangles[i], i);
            mesh.SetNormals(normals); mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject CreatePrefab(Mesh mesh, Material[] materials, string path, string name, bool addCollider, bool convex)
        {
            GameObject root = new GameObject(name);
            MeshFilter filter = root.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            if (addCollider)
            {
                MeshCollider collider = root.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; collider.convex = convex;
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateSamplingClone(GameObject source)
        {
            GameObject clone = Instantiate(source);
            clone.name = "Weapon Voxel Sampling Source";
            clone.hideFlags = HideFlags.HideAndDontSave;
            foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = SamplingLayer;
                child.gameObject.SetActive(true);
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && child.GetComponent<MeshCollider>() == null)
                {
                    MeshCollider collider = child.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh;
                }
                SkinnedMeshRenderer skinned = child.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null && skinned.sharedMesh != null && child.GetComponent<MeshCollider>() == null)
                {
                    Mesh baked = new Mesh { name = "Weapon Voxel Sampling Mesh" };
                    skinned.BakeMesh(baked);
                    MeshCollider collider = child.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = baked;
                }
            }
            Physics.SyncTransforms();
            return clone;
        }

        private static List<Material> CollectSourceMaterials(IEnumerable<Renderer> renderers)
        {
            List<Material> result = new List<Material>();
            foreach (Renderer renderer in renderers)
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && !result.Contains(material)) result.Add(material);
            return result;
        }

        private static Renderer FindRenderer(Collider collider)
        {
            if (collider == null) return null;
            return collider.GetComponent<Renderer>() ??
                   collider.GetComponentInParent<Renderer>() ??
                   collider.GetComponentInChildren<Renderer>();
        }

        private static Material[] BuildMaterialPalette(string name, Color fallbackColor, List<Material> sourceMaterials)
        {
            List<Material> result = new List<Material> { GetOrCreateMaterial(name, fallbackColor) };
            result.AddRange(sourceMaterials);
            return result.ToArray();
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{Sanitize(name)}_Voxel.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { color = color };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.color = color;
            return material;
        }

        private static int Index(int x, int y, int z, Vector3Int dimensions) => x + dimensions.x * (y + dimensions.y * z);

        private static void Unpack(int index, Vector3Int dimensions, out int x, out int y, out int z)
        {
            x = index % dimensions.x; int row = index / dimensions.x; y = row % dimensions.y; z = row / dimensions.y;
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
            if (string.IsNullOrWhiteSpace(value)) return "VoxelWeapon";
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Trim();
        }
    }
}
