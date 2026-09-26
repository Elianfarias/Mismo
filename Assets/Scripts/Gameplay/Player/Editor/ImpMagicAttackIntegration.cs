using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>
    /// Configures the Imp's authored melee/projectile actions without replacing its GoblinController.
    /// The request file makes the operation safe to run from the already open Unity editor.
    /// </summary>
    public static class ImpMagicAttackIntegration
    {
        const string Request = "Temp/ImpMagicAttackIntegration.request";
        const string SettingsPath = "Assets/Data/Enemies/ForestCreatures/Imp.asset";
        const string EnemyPrefabPath = "Assets/Art/Prefabs/Enemies/ForestCreatures/Imp.prefab";
        const string ProjectilePrefabPath = "Assets/Art/Prefabs/Enemies/ForestCreatures/FireballProjectile.prefab";
        const string ProjectileMaterialPath = "Assets/Art/Materials/Creatures/Imp/FireballProjectile.mat";
        const string AnimationFolder = "Assets/Art/Animations/WeaponCombat/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Directional";
        const string LoadPath = AnimationFolder + "/HumanM@MagicAttackDirect1H01_L - Load.fbx";
        const string CastPath = AnimationFolder + "/HumanM@MagicAttackDirect1H01_L - Cast.fbx";

        static double nextPoll;

        [InitializeOnLoadMethod]
        static void RegisterPoll()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            nextPoll = EditorApplication.timeSinceStartup + 2d;
            if (!File.Exists(Request)) return;

            File.Delete(Request);
            try { Apply(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Mismo/Enemies/Configure Imp fireball attack")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Salí de Play Mode antes de configurar el ataque del Imp.");

            var load = LoadClip(LoadPath, "Load");
            var cast = LoadClip(CastPath, "Cast");
            var fireball = BuildFireballPrefab();
            var settings = AssetDatabase.LoadAssetAtPath<GoblinSettings>(SettingsPath);
            if (settings == null) throw new InvalidOperationException("No se encontró Imp.asset en " + SettingsPath);

            ConfigureSettings(settings, load, cast, fireball);
            ConfigurePrefab();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"IMP_MAGIC_ATTACK_PASS: Load={load.name} ({load.length:F2}s), Cast={cast.name} ({cast.length:F2}s), Fireball={fireball.name}");
        }

        static AnimationClip LoadClip(string path, string semantic)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("No se encontró la animación", path);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var clip = clips.FirstOrDefault(candidate => candidate.name.IndexOf(semantic, StringComparison.OrdinalIgnoreCase) >= 0) ?? clips.FirstOrDefault();
            if (clip == null) throw new InvalidOperationException("El FBX no contiene un AnimationClip utilizable: " + path);
            return clip;
        }

        static void ConfigureSettings(GoblinSettings settings, AnimationClip load, AnimationClip cast, GameObject fireball)
        {
            var melee = settings.slash ?? new GoblinAttack();
            melee.label = "Golpe cuerpo a cuerpo";
            melee.kind = CreatureAttackKind.Melee;
            melee.enabled = true;
            melee.minimumRange = 0f;
            melee.range = Mathf.Max(2.2f, melee.range);
            melee.weight = Mathf.Max(1f, melee.weight);
            settings.slash = melee;

            float preparationDuration = Mathf.Max(.01f, load.length);
            float castDuration = Mathf.Max(.01f, cast.length);
            var fireballAttack = new GoblinAttack
            {
                label = "Bola de fuego",
                enabled = true,
                minimumRange = 3.2f,
                range = Mathf.Min(settings.detectionRange, 14f),
                windup = preparationDuration + castDuration,
                active = .10f,
                recovery = .85f,
                damage = 18f,
                cooldown = 4.5f,
                weight = 2f,
                kind = CreatureAttackKind.Projectile,
                hitHeight = 1.0f,
                damageStartsAt = 1f,
                animation = new EnemyAttackAnimation
                {
                    clip = cast,
                    activeStartsAt = 1f,
                    recoveryStartsAt = 1f,
                    blendSeconds = .04f
                },
                preparationClip = load,
                preparationDuration = preparationDuration,
                preparationEndNormalized = 1f,
                projectileVisual = fireball,
                projectileSocket = "hand_l",
                grabAt = preparationDuration * .45f,
                projectileSpeed = 12f,
                projectileRange = 24f,
                projectileRadius = .32f
            };

            // A non-empty attacks array activates the generic creature path. Keep melee and
            // fireball together so the same GoblinController can choose by distance.
            settings.preferredRange = 6.5f;
            settings.attacks = new[] { melee, fireballAttack };
            EditorUtility.SetDirty(settings);
        }

        static void ConfigurePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                var oldDriver = root.GetComponent<GoblinAnimationDriver>();
                if (oldDriver != null) Object.DestroyImmediate(oldDriver);

                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException("Imp.prefab no contiene un Animator visual.");

                var driver = root.GetComponent<CreatureAnimationDriver>() ?? root.AddComponent<CreatureAnimationDriver>();
                driver.Configure(animator);
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static GameObject BuildFireballPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            if (existing != null) return existing;

            EnsureFolder("Assets/Art/Materials/Creatures/Imp");
            var material = AssetDatabase.LoadAssetAtPath<Material>(ProjectileMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("No se encontró un shader para la bola de fuego.");
                material = new Material(shader) { name = "FireballProjectile" };
                material.color = new Color(1f, .12f, .015f, 1f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(1f, .12f, .015f, 1f));
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(1f, .025f, .001f, 1f) * 4f);
                }
                AssetDatabase.CreateAsset(material, ProjectileMaterialPath);
            }

            EnsureFolder("Assets/Art/Prefabs/Enemies/ForestCreatures");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("FireballProjectile");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "Fireball";
                orb.transform.SetParent(root.transform, false);
                orb.transform.localScale = Vector3.one * .28f;
                Object.DestroyImmediate(orb.GetComponent<Collider>());
                orb.GetComponent<Renderer>().sharedMaterial = material;

                var sparks = new GameObject("Fire sparks");
                sparks.transform.SetParent(root.transform, false);
                var particles = sparks.AddComponent<ParticleSystem>();
                var main = particles.main;
                main.loop = true;
                main.startLifetime = .28f;
                main.startSpeed = .35f;
                main.startSize = .07f;
                main.startColor = new Color(1f, .28f, .02f, 1f);
                var emission = particles.emission;
                emission.rateOverTime = 18f;
                var shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = .16f;
                var particleRenderer = sparks.GetComponent<ParticleSystemRenderer>();
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                particleRenderer.sharedMaterial = material;

                return PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) throw new InvalidOperationException("Ruta de carpeta inválida: " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
