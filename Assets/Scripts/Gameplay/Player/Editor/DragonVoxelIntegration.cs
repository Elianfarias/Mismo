using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Batch client of the same voxelizer used by the editor window.</summary>
    public static class DragonVoxelIntegration
    {
        public const string MonsterFolder = "Assets/Art/FBX/Monsters";
        public const string BossFolder = "Assets/Art/Prefabs/DragonBosses";
        public const string BossDataFolder = "Assets/Data/Enemies/DragonBosses";
        public const string FiryxFolder = MonsterFolder + "/Dragon Firyx/Dragon Usurper";
        public const string FullPackage = "Monsters/FourEvilDragonsPBR";
        public const string FullModels = "Assets/Art/FBX/" + FullPackage;
        public const string FullMaterials = "Assets/Art/Materials/" + FullPackage;
        public const string FullAnimations = "Assets/Art/Animations/" + FullPackage;
        public const string FullPrefabs = "Assets/Art/Prefabs/" + FullPackage;

        [MenuItem("Mismo/Modelos/Generar dragones animados y bosses Firyx")]
        public static void BuildAll()
        {
            EnsureBossFolders();
            GameObject firyx = Export(FiryxFolder + "/Mesh/DragonUsurperMesh.fbx", FiryxFolder, "Firyx_Animated", "RedHP");
            ExportNightmare();
            Export(MonsterFolder + "/Dragon SoulEater/Mesh/DragonSoulEaterMesh.fbx", MonsterFolder + "/Dragon SoulEater", "SoulEater_Animated", "RedHP");
            BuildBosses(firyx);
            AssetDatabase.SaveAssets();
            Debug.Log("DRAGON_BUILD_OK: tres modelos animados y cuatro bosses Firyx generados con WeaponVoxelizerWindow.ExportModel.");
        }

        public static GameObject ExportNightmare()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(FullModels + "/DragonTheNightmareMesh.fbx");
            var material = RequireNightmareMaterial();
            if (source == null) throw new InvalidOperationException("Falta el modelo Nightmare del paquete completo.");
            var instance = Object.Instantiate(source);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMesh.subMeshCount).ToArray();
                return WeaponVoxelizerWindow.ExportModel(instance, "Nightmare_Animated", 128, true,
                    AssetDatabase.LoadAssetAtPath<DefaultAsset>(FullAnimations + "/DragonNightMare"));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        // The complete package has the same Nightmare geometry/UVs and clips. Updating the
        // existing material preserves every prefab, mesh, animation and scene reference.
        [MenuItem("Mismo/Modelos/Actualizar material Nightmare desde paquete completo")]
        public static void UpdateNightmareMaterial()
        {
            var source = RequireNightmareMaterial();
            var target = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Nightmare_Animated_BlueHP.mat");
            if (target == null) throw new InvalidOperationException("No se encontró el material del Nightmare voxelizado existente.");
            Undo.RecordObject(target, "Actualizar material Nightmare");
            string name = target.name;
            EditorUtility.CopySerialized(source, target);
            target.name = name;
            WeaponVoxelizerWindow.UseVoxelSurfaceNormals(target);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            Debug.Log("NIGHTMARE_MATERIAL_OK: albedo, metallic/smoothness y AO del paquete completo; rig, malla y clips conservados.", target);
        }

        static Material RequireNightmareMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FullMaterials + "/DragonNightmare/BluePBR.mat");
            if (material == null || !material.HasProperty("_BaseMap") || material.GetTexture("_BaseMap") == null)
                throw new InvalidOperationException("Falta BluePBR o su albedo. Importá el paquete FourEvilDragonsPBR completo y convertí sus materiales a URP.");
            return material;
        }

        public static void RefreshBossesFromExportedModel()
        {
            BuildBosses(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Voxelized/Firyx_Animated.prefab"));
            AssetDatabase.SaveAssets();
        }
        static void EnsureBossFolders()
        {
            foreach (string folder in new[] { BossFolder, BossDataFolder, "Assets/Art/Materials/DragonBosses", "Assets/Art/Meshes/DragonBosses" }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        static void BuildBosses(GameObject firyx)
        {
            EnsureBossFolders();
            BuildBoss(firyx, "Red", "FIRYX · BRASA", new Color(1, .25f, .06f), 1700, 5, 16, .06f, .6f);
            BuildBoss(firyx, "Blue", "FIRYX · LLAMA AZUL", new Color(.1f, .65f, 1), 1550, 4.5f, 12, .08f, .35f);
            BuildBoss(firyx, "Green", "FIRYX · LLAMA ESMERALDA", new Color(.4f, 1, .1f), 2300, 3.2f, 30, .4f, .4f);
            BuildBoss(firyx, "Purple", "FIRYX · LLAMA VIOLETA", new Color(.7f, .25f, 1), 1850, 4.2f, 18, .15f, .7f);
        }

        static GameObject Export(string modelPath, string folder, string name, string materialName)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) throw new InvalidOperationException("No se encontró " + modelPath);
            GameObject instance = Object.Instantiate(source);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var material = AssetDatabase.LoadAssetAtPath<Material>(folder.Replace("/FBX/", "/Materials/") + "/" + materialName + ".mat");
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (material != null) skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMesh.subMeshCount).ToArray();
                return WeaponVoxelizerWindow.ExportModel(instance, name, 128, true, AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder.Replace("/FBX/", "/Animations/")));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static void BuildBoss(GameObject prefab, string colorName, string displayName, Color accent,
            float health, float speed, float flightCooldown, float guard, float bite)
        {
            string prefix = BossDataFolder + "/Firyx_" + colorName;
            var settings = AssetDatabase.LoadAssetAtPath<DragonBossSettings>(prefix + ".asset");
            if (settings == null) { settings = ScriptableObject.CreateInstance<DragonBossSettings>(); AssetDatabase.CreateAsset(settings, prefix + ".asset"); }
            settings.displayName = displayName; settings.accent = accent; settings.health = health; settings.moveSpeed = speed;
            settings.flightCooldown = flightCooldown; settings.defendChance = guard; settings.biteChance = bite;
            settings.recovery = colorName == "Purple" ? .65f : colorName == "Green" ? 2 : 1.2f;
            settings.breathCooldown = colorName == "Red" ? 5 : colorName == "Blue" ? 7 : 10;
            settings.flightHeight = colorName == "Blue" ? 7 : 4;
            settings.flightDuration = colorName == "Blue" ? 5 : 3;
            settings.comboChance = colorName == "Purple" ? .85f : colorName == "Red" ? .3f : .1f;
            var root = new GameObject("Firyx_" + colorName + "_Boss");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                model.transform.SetParent(root.transform, false);
                var library = model.GetComponent<VoxelRigInstance>();
                foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                model.transform.localPosition = Vector3.up * -library.surface.sharedMesh.bounds.min.y;
                var material = AssetDatabase.LoadAssetAtPath<Material>(FiryxFolder.Replace("/FBX/", "/Materials/") + "/" + colorName + "HP.mat");
                library.surface.sharedMaterials = Enumerable.Repeat(material, library.surface.sharedMesh.subMeshCount).ToArray();
                foreach (DragonMotion motion in Enum.GetValues(typeof(DragonMotion)))
                {
                    var clip = library.clips.FirstOrDefault(c => string.Equals(c.name, motion.ToString(), StringComparison.OrdinalIgnoreCase));
                    if (clip == null) throw new InvalidOperationException("Falta la animación Firyx: " + motion);
                    settings.clips[(int)motion] = clip;
                }
                var head = model.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
                var muzzle = new GameObject("BreathOrigin").transform;
                muzzle.SetParent(head, false);
                Bounds bounds = library.surface.sharedMesh.bounds;
                muzzle.position = model.transform.TransformPoint(new Vector3(0, bounds.size.y * .1f, bounds.min.z));
                var body = root.AddComponent<CapsuleCollider>(); body.radius = 1.25f; body.height = 4; body.center = Vector3.up * 2;
                var rigidbody = root.AddComponent<Rigidbody>(); rigidbody.isKinematic = true; rigidbody.useGravity = false;
                root.AddComponent<Health>(); root.AddComponent<CombatState>(); root.AddComponent<DefenseWindow>(); root.AddComponent<DamageReceiver>();
                var agent = root.AddComponent<NavMeshAgent>(); agent.radius = 1.25f; agent.height = 4; agent.speed = speed; agent.angularSpeed = 85; agent.enabled = false;
                root.AddComponent<DragonBossController>().Configure(settings, library.animator, muzzle);
                root.AddComponent<EnemyNameplate>(); root.AddComponent<EnemyProgressionReward>();
                AddBreath(root, muzzle, accent);
                EditorUtility.SetDirty(settings);
                PrefabUtility.SaveAsPrefabAsset(root, BossFolder + "/Firyx_" + colorName + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void AddBreath(GameObject root, Transform muzzle, Color color)
        {
            string materialPath = "Assets/Art/Materials/DragonBosses/VoxelFlame.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            string cubePath = "Assets/Art/Meshes/DragonBosses/FlameCube.asset";
            var cube = AssetDatabase.LoadAssetAtPath<Mesh>(cubePath);
            if (cube == null)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube = Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(primitive); AssetDatabase.CreateAsset(cube, cubePath);
            }
            var go = new GameObject("VoxelBreath"); go.transform.SetParent(muzzle, false);
            var particles = go.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.playOnAwake = false; main.loop = true; main.startLifetime = .8f; main.startSpeed = 24;
            main.startSize = new ParticleSystem.MinMaxCurve(.12f, .35f); main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 220;
            var emission = particles.emission; emission.rateOverTime = 130; emission.enabled = false;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 13; shape.radius = .15f;
            var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = cube; renderer.sharedMaterial = material;
            root.AddComponent<DragonBreathVfx>().Configure(particles);
        }
    }
}
