using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>
    /// Converts a weapon or model mesh into one optimized voxel mesh asset and prefab.
    /// It is intentionally editor-only: the original object is never modified.
    /// </summary>
    public sealed class WeaponVoxelizerWindow : EditorWindow
    {
        private const string WeaponOutputFolder = "Assets/Art/Prefabs/Weapons/Voxelized";
        private const string ModelOutputFolder = "Assets/Art/Prefabs/Voxelized";
        private const string MaterialFolder = "Assets/Art/Materials";

        private GameObject sourceWeapon;
        [SerializeField] private Equipment.WeaponDefinition targetWeapon;
        private bool genericModelMode;
        private bool preserveRig = true;
        private DefaultAsset animationFolder;
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
        private static void OpenWeapon() => OpenWindow(false);

        [MenuItem("Mismo/Modelos/Voxelizar modelo")]
        private static void OpenModel() => OpenWindow(true);

        private static void OpenWindow(bool genericModel)
        {
            WeaponVoxelizerWindow window = GetWindow<WeaponVoxelizerWindow>(genericModel ? "Voxelizar modelo" : "Voxelizar arma");
            window.genericModelMode = genericModel;
            window.outputName = genericModel ? "VoxelModel" : "VoxelWeapon";
            window.resolution = genericModel ? 64 : 32;
            window.Show();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            string objectLabel = genericModelMode ? "modelo" : "arma";
            EditorGUILayout.LabelField($"Voxelizador de {objectLabel}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Convierte un {objectLabel} con MeshRenderer o SkinnedMeshRenderer en una malla voxelizada combinada. " +
                "El objeto fuente permanece intacto y no se crea un GameObject por voxel.", MessageType.Info);

            sourceWeapon = (GameObject)EditorGUILayout.ObjectField($"{(genericModelMode ? "Modelo" : "Arma")} fuente", sourceWeapon, typeof(GameObject), true);
            if (!genericModelMode)
            {
                targetWeapon = (Equipment.WeaponDefinition)EditorGUILayout.ObjectField("Arma del inventario", targetWeapon, typeof(Equipment.WeaponDefinition), false);
                EditorGUILayout.HelpBox("Al elegir un arma, el prefab generado reemplaza su modelo visual y actualiza su foto. Ajustá el ángulo en Mismo → Armas → Taller de iconos.", MessageType.Info);
            }
            outputName = EditorGUILayout.TextField("Nombre de salida", outputName);
            resolution = Mathf.Clamp(EditorGUILayout.IntSlider("Resolución máxima", resolution, 8, 128), 8, 128);
            if (genericModelMode)
                EditorGUILayout.HelpBox("Usá 64–128 para alas, dedos y cuernos. La resolución indica los voxeles sobre el eje más largo del modelo.", MessageType.Info);
            fillInterior = EditorGUILayout.Toggle(new GUIContent("Rellenar interior", "Cierra huecos internos para que el objeto sea sólido"), fillInterior);
            preserveSourceMaterials = EditorGUILayout.Toggle(new GUIContent("Conservar materiales", "Mantiene materiales, texturas y colores de las piezas del objeto"), preserveSourceMaterials);
            if (genericModelMode && preserveSourceMaterials)
                EditorGUILayout.HelpBox("Usá el prefab original con los materiales correctos ya asignados. Un material o una textura del modelo equivocado también se copiará al voxelizado.", MessageType.Info);
            weaponColor = EditorGUILayout.ColorField("Color del voxel", weaponColor);

            if (genericModelMode)
            {
                preserveRig = EditorGUILayout.Toggle("Conservar rig y animaciones", preserveRig);
                if (preserveRig)
                {
                    animationFolder = (DefaultAsset)EditorGUILayout.ObjectField("Carpeta de animaciones", animationFolder, typeof(DefaultAsset), false);
                    EditorGUILayout.HelpBox("Seleccioná la raíz completa del modelo original con sus huesos. Conserva rig y pesos; en Humanoid conserva también el Avatar y el Controller asignado. Usá clips del mismo tipo de rig. Sin clips, podés asignarlos después en el taller de enemigos. Usa más geometría para cerrar los voxeles al animarlos.", MessageType.Info);
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Prefab generado", EditorStyles.boldLabel);
            bool animated = genericModelMode && preserveRig;
            addCollider = EditorGUILayout.Toggle(animated ? "Agregar collider de cuerpo" : "Agregar MeshCollider", addCollider);
            using (new EditorGUI.DisabledScope(!addCollider || animated))
                convexCollider = EditorGUILayout.Toggle(new GUIContent("Collider convexo", "Útil si el objeto tendrá Rigidbody"), convexCollider);

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(sourceWeapon == null))
                if (GUILayout.Button($"Voxelizar {objectLabel} y generar prefab", GUILayout.Height(30))) Generate();
            if (!string.IsNullOrEmpty(lastOutput))
                EditorGUILayout.HelpBox(lastOutput, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        public static GameObject ExportModel(GameObject source, string name, int maximumResolution = 64,
            bool keepRig = true, DefaultAsset animations = null)
        {
            var window = CreateInstance<WeaponVoxelizerWindow>();
            try
            {
                window.sourceWeapon = source; window.outputName = name; window.resolution = maximumResolution;
                window.genericModelMode = true; window.preserveRig = keepRig; window.animationFolder = animations;
                return window.Generate() ?? throw new System.InvalidOperationException(window.lastOutput);
            }
            finally { DestroyImmediate(window); }
        }

        // Refresh geometry without changing the existing prefab, mesh asset or controller.
        // The caller owns the returned transient mesh.
        public static Mesh RebuildRigGeometry(GameObject source, GameObject target, int resolution=64)
        {
            var plan=VoxelRigExporter.Prepare(source,null);
            if(plan.SourceAnimator==null || plan.AnimationRoot!=source.transform)
                throw new System.InvalidOperationException("La actualización necesita el Animator en la raíz del modelo original.");
            var sample=VoxelSurfaceSampler.Sample(source,resolution,true,true,true);
            FillInterior(sample.Occupied,sample.Dimensions);
            var animator=target.GetComponentInChildren<Animator>();var rig=animator.transform;
            foreach(var t in source.GetComponentsInChildren<Transform>())
            {
                string path=AnimationUtility.CalculateTransformPath(t,source.transform);
                var destination=path.Length==0?rig:rig.Find(path);
                if(destination==null)throw new System.InvalidOperationException("Falta hueso: "+path);
                destination.localPosition=t.localPosition;destination.localRotation=t.localRotation;destination.localScale=t.localScale;
            }
            rig.localPosition=-sample.Bounds.center;rig.localRotation=Quaternion.identity;rig.localScale=source.transform.lossyScale;
            animator.avatar=plan.SourceAnimator.avatar;
            var skin=target.GetComponentInChildren<SkinnedMeshRenderer>();
            skin.bones=sample.Bones.Select(b=>{string path=AnimationUtility.CalculateTransformPath(b,source.transform);return path.Length==0?rig:rig.Find(path);}).ToArray();
            skin.rootBone=rig;
            var mesh=BuildMesh(sample.Occupied,sample.Dimensions,sample.VoxelSize,sample.Bounds.min,sample.Bounds.center,Color.white,target.name,true,sample.MaterialIndices,sample.UVs,sample.Materials.Count+1,sample.BoneWeights);
            mesh.bindposes=skin.bones.Select(b=>b.worldToLocalMatrix*skin.transform.localToWorldMatrix).ToArray();
            return mesh;
        }

        private GameObject Generate()
        {
            if (sourceWeapon == null)
            {
                EditorUtility.DisplayDialog("Voxelizar modelo", "Seleccioná primero un prefab u objeto con MeshRenderer o SkinnedMeshRenderer.", "Aceptar");
                return null;
            }

            string outputFolder = genericModelMode ? ModelOutputFolder : WeaponOutputFolder;
            EnsureFolder("Assets/Art");
            EnsureFolder(outputFolder);
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialFolder);

            try
            {
                bool animated = genericModelMode && preserveRig;
                string animationsPath = animationFolder != null ? AssetDatabase.GetAssetPath(animationFolder) : null;
                var rigPlan = animated ? VoxelRigExporter.Prepare(sourceWeapon, animationsPath) : null;
                VoxelSurfaceSampler.Result sample = VoxelSurfaceSampler.Sample(sourceWeapon, resolution, preserveSourceMaterials, true, animated);
                Bounds bounds = sample.Bounds;
                Vector3Int dimensions = sample.Dimensions;
                float voxelSize = sample.VoxelSize;
                List<Material> sourceMaterials = sample.Materials;
                int[] voxelMaterials = sample.MaterialIndices;
                Vector2[] voxelUVs = sample.UVs;
                bool[] occupied = sample.Occupied;
                if (fillInterior) FillInterior(occupied, dimensions);
                if (!occupied.Any(value => value))
                {
                    EditorUtility.DisplayDialog("Voxelizar modelo", "No se encontraron voxeles ocupados. Probá aumentar la resolución.", "Aceptar");
                    return null;
                }

                Mesh mesh = BuildMesh(occupied, dimensions, voxelSize, bounds.min, bounds.center, weaponColor, outputName, preserveSourceMaterials, voxelMaterials, voxelUVs, sourceMaterials.Count + 1, sample.BoneWeights);
                string safeName = Sanitize(outputName);
                string meshFolder = outputFolder.Replace("/Prefabs/", "/Meshes/");
                EnsureFolder(meshFolder);
                string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{meshFolder}/{safeName}.asset");
                AssetDatabase.CreateAsset(mesh, meshPath);
                Material[] materials = BuildMaterialPalette(safeName, weaponColor, sourceMaterials);
                string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{safeName}.prefab");
                GameObject prefab = animated
                    ? VoxelRigExporter.Create(sourceWeapon, sample, mesh, materials, prefabPath, addCollider,
                        animationsPath, rigPlan)
                    : CreatePrefab(mesh, materials, prefabPath, safeName, addCollider, convexCollider);

                if (!genericModelMode)
                {
                    if (targetWeapon != null) InventoryPresentationAssets.RegenerateWeaponIcon(targetWeapon, prefab);
                    else InventoryIconCapture.Capture(prefab,
                        "Assets/Art/UI/Inventory/weapon-" + AssetDatabase.AssetPathToGUID(prefabPath) + ".png", Vector3.zero);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                lastOutput = $"Generado: {prefabPath}\n" +
                    $"Resolución: {dimensions.x} × {dimensions.y} × {dimensions.z} · Voxeles: {occupied.Count(value => value)}";
                Debug.Log($"Modelo voxelizado: {prefabPath} (mesh: {meshPath})");
                return prefab;
            }
            catch (System.OperationCanceledException)
            {
                lastOutput = "Voxelización cancelada.";
                return null;
            }
            catch (System.Exception exception)
            {
                lastOutput = "No se pudo voxelizar: " + exception.Message;
                Debug.LogException(exception);
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
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

        internal static Mesh BuildMesh(bool[] occupied, Vector3Int dimensions, float voxelSize, Vector3 boundsMin, Vector3 pivot, Color color, string name, bool preserveMaterials, int[] voxelMaterials, Vector2[] voxelUVs, int submeshCount, BoneWeight[] voxelWeights = null)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<List<int>> submeshTriangles = new List<List<int>>();
            for (int i = 0; i < Mathf.Max(1, preserveMaterials ? submeshCount : 1); i++) submeshTriangles.Add(new List<int>());
            List<Vector3> normals = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<Vector2> uvs = new List<Vector2>();
            List<BoneWeight> weights = voxelWeights != null ? new List<BoneWeight>() : null;
            var cornerWeights = weights != null ? new Dictionary<Vector3Int, BoneWeight>() : null;
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
                        if (weights != null && directions.All(direction =>
                        {
                            int ax = x + direction.x, ay = y + direction.y, az = z + direction.z;
                            return ax >= 0 && ay >= 0 && az >= 0 && ax < dimensions.x && ay < dimensions.y && az < dimensions.z && occupied[Index(ax, ay, az, dimensions)];
                        })) continue;
                        int materialIndex = preserveMaterials && voxelMaterials != null && voxelMaterials[voxelIndex] >= 0 ? voxelMaterials[voxelIndex] + 1 : 0;
                        materialIndex = Mathf.Clamp(materialIndex, 0, submeshTriangles.Count - 1);
                        Vector2 uv = voxelUVs != null && voxelIndex < voxelUVs.Length ? voxelUVs[voxelIndex] : Vector2.zero;
                        Vector3 center = boundsMin + new Vector3((x + .5f) * voxelSize, (y + .5f) * voxelSize, (z + .5f) * voxelSize) - pivot;
                        for (int face = 0; face < directions.Length; face++)
                        {
                            int nx = x + directions[face].x, ny = y + directions[face].y, nz = z + directions[face].z;
                            if (weights == null && nx >= 0 && ny >= 0 && nz >= 0 && nx < dimensions.x && ny < dimensions.y && nz < dimensions.z && occupied[Index(nx, ny, nz, dimensions)]) continue;
                            int start = vertices.Count;
                            for (int corner = 0; corner < 4; corner++)
                            {
                                vertices.Add(center + Vector3.Scale(faces[face][corner], half));
                                normals.Add(directions[face]);
                                colors.Add(color);
                                uvs.Add(uv);
                                if (weights != null)
                                {
                                    Vector3 offset = faces[face][corner];
                                    var key = new Vector3Int(x + (offset.x > 0 ? 1 : 0), y + (offset.y > 0 ? 1 : 0), z + (offset.z > 0 ? 1 : 0));
                                    if (!cornerWeights.TryGetValue(key, out BoneWeight weight))
                                    {
                                        var adjacent = new List<BoneWeight>(8);
                                        for (int az = key.z - 1; az <= key.z; az++)
                                            for (int ay = key.y - 1; ay <= key.y; ay++)
                                                for (int ax = key.x - 1; ax <= key.x; ax++)
                                                {
                                                    if (ax < 0 || ay < 0 || az < 0 || ax >= dimensions.x || ay >= dimensions.y || az >= dimensions.z) continue;
                                                    int neighbor = Index(ax, ay, az, dimensions);
                                                    if (occupied[neighbor] && voxelWeights[neighbor].weight0 > 0) adjacent.Add(voxelWeights[neighbor]);
                                                }
                                        weight = VoxelSurfaceSampler.AverageWeights(adjacent);
                                        cornerWeights[key] = weight;
                                    }
                                    weights.Add(weight);
                                }
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
            if (weights != null) mesh.boneWeights = weights.ToArray();
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

        private static Material[] BuildMaterialPalette(string name, Color fallbackColor, List<Material> sourceMaterials)
        {
            List<Material> result = new List<Material> { GetOrCreateMaterial(name, fallbackColor) };
            foreach (Material source in sourceMaterials) result.Add(PrepareMaterial(name, source));
            return result.ToArray();
        }

        private static Material PrepareMaterial(string name, Material source)
        {
            Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
            bool recovered = false;
            if (texture == null)
            {
                string path = AssetDatabase.GetAssetPath(source);
                string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parent))
                {
                    string modelFolder = System.IO.Path.GetDirectoryName(parent)?.Replace('\\', '/');
                    foreach (string folder in new[] { parent, parent.Replace("/Materials/", "/Textures/"), modelFolder + "/Texture", modelFolder + "/Textures" })
                        foreach (string extension in new[] { ".png", ".tga", ".jpg" })
                            if (texture == null) texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + source.name + extension);
                    recovered = texture != null;
                }
            }
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            bool upgrade = urp != null && source.shader != null &&
                (source.shader.name == "Standard" || source.shader.name == "Standard (Specular setup)");
            bool hasNormalMap = source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null;
            if (!upgrade && !recovered && !hasNormalMap) return source;
            Material material = upgrade ? new Material(urp) : new Material(source);
            material.name = name + "_" + source.name;
            Color tint = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            material.color = tint;
            material.mainTexture = texture;
            material.mainTextureScale = source.mainTextureScale; material.mainTextureOffset = source.mainTextureOffset;
            if (upgrade)
            {
                if (source.HasProperty("_Metallic")) material.SetFloat("_Metallic", source.GetFloat("_Metallic"));
                if (source.HasProperty("_Glossiness")) material.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
                foreach (string property in new[] { "_MetallicGlossMap", "_SpecGlossMap", "_OcclusionMap", "_EmissionMap" })
                    if (source.HasProperty(property)) material.SetTexture(property, source.GetTexture(property));
                foreach (string property in new[] { "_OcclusionStrength", "_Cutoff", "_GlossMapScale", "_SmoothnessTextureChannel" })
                    if (source.HasProperty(property) && material.HasProperty(property)) material.SetFloat(property, source.GetFloat(property));
                if (source.HasProperty("_SpecColor")) material.SetColor("_SpecColor", source.GetColor("_SpecColor"));
                if (source.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
                bool specular = source.shader.name == "Standard (Specular setup)";
                material.SetFloat("_WorkflowMode", specular ? 0 : 1);
                if (specular) material.EnableKeyword("_SPECULAR_SETUP");
                if (material.GetTexture(specular ? "_SpecGlossMap" : "_MetallicGlossMap") != null) material.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (material.GetTexture("_OcclusionMap") != null) material.EnableKeyword("_OCCLUSIONMAP");
                if (source.IsKeywordEnabled("_EMISSION")) material.EnableKeyword("_EMISSION");
            }
            UseVoxelSurfaceNormals(material);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath($"{MaterialFolder}/{Sanitize(material.name)}.mat"));
            if (recovered) Debug.Log($"Textura recuperada para {source.name}: {AssetDatabase.GetAssetPath(texture)}");
            return material;
        }

        internal static void UseVoxelSurfaceNormals(Material material)
        {
            // Each cube samples one source UV; it has no source tangent basis. Tangent-space
            // normal maps would shade it incorrectly. Keep the flat voxel face normals.
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
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
