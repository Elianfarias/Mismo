using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Editor;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit setup and regression checks; does not rewrite attacks or player saves.</summary>
public static class ImpTacticsChecks
{
    const string Output = "output/imp-tactics";
    const string CatalogPath = "Assets/Data/World/WorldContentCatalog.asset";
    const string Clips = "Assets/Art/Animations/WeaponCombat/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_";

    public static void RunBatch()
    {
        try
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("RunBatch is only for a dedicated Unity process.");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            Configure();
            SessionState.SetBool("Mismo.ImpTactics.Batch", true);
            EnemyControllerRefactorChecks.Run();
            if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.Exit(1);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    [InitializeOnLoadMethod]
    static void RegisterBatchCompletion()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("Mismo.ImpTactics.Batch", false)) return;
            SessionState.SetBool("Mismo.ImpTactics.Batch", false);
            EditorApplication.delayCall += () =>
            {
                bool passed = File.ReadAllText("output/enemy-controllers/play-checks.txt").Contains("PLAY_MODE_COMPLETE");
                if (passed) { try { BuildContent(); } catch (Exception e) { Debug.LogException(e); passed = false; } }
                EditorApplication.Exit(passed ? 0 : 1);
            };
        };
    }

    public static void BuildContent()
    {
        const string folder = ".validation/imp-tactics/content";
        Directory.CreateDirectory(folder);
        var manifest = BuildPipeline.BuildAssetBundles(folder,
            new[] { new AssetBundleBuild { assetBundleName = "imp-tactics", assetNames = new[] { EnemyControllerMigration.ImpPath, CatalogPath } } },
            BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new Exception("Imp content build failed");
        var bundle = AssetBundle.LoadFromFile(folder + "/imp-tactics");
        if (bundle == null) throw new Exception("Could not load Imp bundle");
        try
        {
            var catalog = bundle.LoadAsset<WorldContentCatalog>(CatalogPath);
            var entry = catalog.encounters.Single(e => e.id == "forest.Imp");
            var equipment = entry.prefab.GetComponent<EnemyEquipment>();
            Check(WorldContentCatalog.Allows(entry.biomes, WorldBiome.Meadow) && entry.prefab.GetComponent<ImpController>() != null,
                "Built world catalog includes Imp in meadows");
            Check(equipment.strafeLeftClip != null && equipment.strafeRightClip != null && equipment.backwardClip != null,
                "Built Imp retains all directional clips");
            File.WriteAllText(Output + "/content-build.txt", "PASS Windows content bundle: real world catalog, Imp controller, attack settings and three directional clips.\n");
        }
        finally { bundle.Unload(true); }
    }
    static void Check(bool ok, string label)
    {
        if (!ok) throw new InvalidOperationException(label);
        File.AppendAllText(Output + "/checks.txt", "PASS " + label + "\n");
    }

    [MenuItem("Mismo/Enemigos/Configurar movimiento del Imp y aparición en praderas")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode antes de configurar.");
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory(".validation/imp-tactics/before");
        foreach (string path in new[] { CatalogPath, EnemyControllerMigration.ImpPath })
        {
            string backup = ".validation/imp-tactics/before/" + Path.GetFileName(path);
            if (!File.Exists(backup)) File.Copy(path, backup);
        }
        var root = PrefabUtility.LoadPrefabContents(EnemyControllerMigration.ImpPath);
        try
        {
            var equipment = root.GetComponent<EnemyEquipment>();
            if (equipment == null || !equipment.ResolveAnimator().isHuman) throw new Exception("Imp needs compatible Humanoid equipment");
            // Never replace an artist's assigned directional clip on a repeat run.
            if (equipment.strafeLeftClip == null) equipment.strafeLeftClip = Clip("Left");
            if (equipment.strafeRightClip == null) equipment.strafeRightClip = Clip("Right");
            if (equipment.backwardClip == null) equipment.backwardClip = Clip("Backward");
            PrefabUtility.SaveAsPrefabAsset(root, EnemyControllerMigration.ImpPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var catalog = AssetDatabase.LoadAssetAtPath<WorldContentCatalog>(CatalogPath);
        var imp = catalog.encounters.Single(e => e.id == "forest.Imp");
        if (!WorldContentCatalog.Allows(imp.biomes, WorldBiome.Meadow))
        {
            imp.biomes = imp.biomes.Concat(new[] { WorldBiome.Meadow }).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
    }

    static AnimationClip Clip(string direction)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(Clips + direction + ".fbx")
            .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null || !clip.isHumanMotion || !clip.isLooping) throw new Exception("Invalid directional clip: " + direction);
        return clip;
    }

    public static void EditorChecks()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/checks.txt", "Imp tactics / " + DateTime.Now.ToString("s") + "\n");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyControllerMigration.ImpPath);
        var equipment = prefab.GetComponent<EnemyEquipment>();
        Check(equipment.strafeLeftClip != null && equipment.strafeRightClip != null && equipment.backwardClip != null,
            "Imp has three serialized directional clips");
        Check(new[] { equipment.strafeLeftClip, equipment.strafeRightClip, equipment.backwardClip }.All(c => c.isHumanMotion && c.isLooping),
            "Directional clips are looping Humanoid animations");
        Check(equipment.DirectionalMovement(Vector3.left) == equipment.strafeLeftClip &&
              equipment.DirectionalMovement(Vector3.right) == equipment.strafeRightClip &&
              equipment.DirectionalMovement(Vector3.back) == equipment.backwardClip &&
              equipment.DirectionalMovement(Vector3.forward) == null, "Local movement selects left, right and backward with forward fallback");
        var catalog = AssetDatabase.LoadAssetAtPath<WorldContentCatalog>(CatalogPath);
        var settings = Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings");
        Check(settings.content == catalog, "Runtime world uses the edited catalog");
        var entry = catalog.encounters.Single(e => e.id == "forest.Imp");
        Check(entry.prefab == prefab && entry.prefab.GetComponent<ImpController>() != null, "Catalog references the complete Imp enemy");
        Check(WorldContentCatalog.Allows(entry.biomes, WorldBiome.Forest) && WorldContentCatalog.Allows(entry.biomes, WorldBiome.Meadow),
            "Imp eligible in forests and meadows");
        Check(!WorldContentCatalog.Allows(entry.biomes, WorldBiome.Desert), "Other biome restrictions retained");
        foreach (WorldBiome biome in new[] { WorldBiome.Forest, WorldBiome.Meadow })
        {
            int count = 0;
            for (int seed = 0; seed < 10000; seed++)
                if (catalog.Encounter(WorldSiteKind.Clearing, biome, 1, 10, seed) == entry) count++;
            Check(count > 1000, biome + ": Imp selected in " + count + "/10000 catalog rolls at level 1");
        }
        Check(catalog.Encounter(WorldSiteKind.Village, WorldBiome.Meadow, 1, 10, 17) == null, "Village remains safe");
        SpawnChecks(settings);
    }

    static void SpawnChecks(ExplorationWorldSettings original)
    {
        var previous = SceneManager.GetActiveScene();
        var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(preview);
        var settings = Object.Instantiate(original);
        var roots = new List<GameObject>();
        var meshes = new List<Mesh>();
        NavMeshData data = null;
        NavMeshDataInstance nav = default;
        try
        {
            settings.preserveAuthoredCenter = false;
            var terrain = new ExplorationTerrain(settings);
            var content = new ExplorationContent(settings, terrain);
            WorldSite selected = default;
            bool found = false;
            for (int z = -12; z <= 12 && !found; z++)
            for (int x = -12; x <= 12 && !found; x++)
            foreach (var site in content.Encounters(new Vector2Int(x, z)))
            {
                if (site.kind != WorldSiteKind.Clearing || terrain.Biome(site.position.x, site.position.z) != WorldBiome.Meadow) continue;
                var density = new System.Random(ExplorationTerrain.Hash(settings.seed, site.cell.x, site.cell.y, 200));
                if (!site.roaming && density.NextDouble() > settings.content.clearingChance) continue;
                int seed = ExplorationTerrain.Hash(settings.seed, site.cell.x, site.cell.y, site.roaming ? 411 : 201);
                if (settings.content.Encounter(site.kind, WorldBiome.Meadow, 1, site.position.y, seed)?.id != "forest.Imp") continue;
                selected = site; found = true; break;
            }
            Check(found, "Natural meadow site selects Imp using actual density and selection seeds");
            var chunk = ExplorationChunks.Coordinate(selected.position);
            var sources = new List<NavMeshBuildSource>();
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
            {
                var mesh = ExplorationChunks.BuildTerrain(terrain, chunk + new Vector2Int(x, z)); meshes.Add(mesh);
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = mesh, transform = Matrix4x4.identity });
            }
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources, new Bounds(selected.position, new Vector3(100, 100, 100)), Vector3.zero, Quaternion.identity);
            nav = NavMesh.AddNavMeshData(data);
            var player = new GameObject("Temporary spawn inventory"); roots.Add(player); SceneManager.MoveGameObjectToScene(player, preview);
            player.transform.position = selected.position + Vector3.right * 60;
            var inventory = player.AddComponent<PlayerInventory>();
            var profile = new InventoryProfile();
            typeof(PlayerInventory).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, profile);
            profile.regions.Add(new DiscoveredRegion { id = terrain.RegionId(selected.position.x, selected.position.z), minimumLevel = 1 });
            var root = new GameObject("Temporary meadow encounter"); roots.Add(root); SceneManager.MoveGameObjectToScene(root, preview);
            content.Populate(chunk, root.transform, inventory);
            var imps = root.GetComponentsInChildren<ImpController>();
            Check(imps.Length > 0, "World population actually instantiates Imps on generated meadow NavMesh");
            var actors = root.GetComponentsInChildren<WorldEnemyIdentity>();
            content.Populate(chunk, root.transform, inventory);
            Check(root.GetComponentsInChildren<WorldEnemyIdentity>().Length == actors.Length, "Streaming does not duplicate encounters");
            foreach (var actor in actors) profile.defeatedEnemies.Add(actor.Id);
            Object.DestroyImmediate(root); roots.Remove(root);
            root = new GameObject("Temporary reload"); roots.Add(root); SceneManager.MoveGameObjectToScene(root, preview);
            content.Unload(chunk); content.Populate(chunk, root.transform, inventory);
            Check(root.GetComponentsInChildren<WorldEnemyIdentity>().Length == 0, "Catalog adjustment does not resurrect defeated enemies");
        }
        finally
        {
            foreach (var root in roots) if (root != null) Object.DestroyImmediate(root);
            if (nav.valid) nav.Remove();
            if (data != null) Object.DestroyImmediate(data);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(settings);
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(preview, true);
        }
    }

    public static IEnumerator PlayChecks(GameObject target)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyControllerMigration.ImpPath);
        var root = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        yield return null;
        var imp = root.GetComponent<ImpController>();
        var agent = root.GetComponent<NavMeshAgent>();
        target.transform.position = Vector3.forward * 12; Physics.SyncTransforms(); imp.SetTarget(target.transform);
        imp.Tick(.31f);
        Check(imp.Tactic == ImpTactic.Approach && imp.State == EnemyState.Position && imp.CurrentAttack == null,
            "Far Imp approaches a firing position instead of firing immediately or chasing into melee");
        Check(Vector3.Distance(imp.TacticalDestination, target.transform.position) > 5, "Approach destination stops outside melee range");
        target.transform.position = Vector3.forward * 6; Physics.SyncTransforms(); imp.Tick(.4f);
        Check(imp.State == EnemyState.Telegraph && imp.CurrentAttack.kind == CreatureAttackKind.Projectile, "Imp fires from comfortable range");
        CompleteAttack(imp);
        imp.Tick(.4f);
        Check(imp.State == EnemyState.Position && imp.Tactic == ImpTactic.Strafe && Mathf.Abs(imp.TacticalDestination.x) > .5f,
            "Fireball cooldown produces lateral movement");
        var equipment = root.GetComponent<EnemyEquipment>();
        imp.enabled = false; // Isolate locomotion sampling from new AI destinations during these three passes.
        Time.timeScale = 1f;
        using (var playback = new EnemyActionPlayback())
        {
            foreach (var velocity in new[] { Vector3.left, Vector3.right, Vector3.back })
            {
                agent.ResetPath(); agent.isStopped = false; agent.speed = 1.6f;
                var path = new NavMeshPath();
                Check(agent.CalculatePath(root.transform.position + root.transform.TransformDirection(velocity) * 4, path) &&
                    path.status == NavMeshPathStatus.PathComplete && agent.SetPath(path), "Complete locomotion path for " + velocity);
                double until = EditorApplication.timeSinceStartup + 3;
                while (EditorApplication.timeSinceStartup < until)
                {
                    yield return null;
                    var local = root.transform.InverseTransformDirection(agent.velocity);
                    if (!agent.pathPending && local.magnitude > .2f && Vector3.Dot(local.normalized, velocity) > .9f) break;
                }
                Check(agent.velocity.magnitude > .2f, "NavMesh advances " + velocity + " (dt " + Time.deltaTime + ", stopped " + agent.isStopped +
                    ", enabled " + agent.enabled + ", onMesh " + agent.isOnNavMesh + ", hasPath " + agent.hasPath + ", status " + agent.pathStatus +
                    ", desired " + agent.desiredVelocity + ", speed " + agent.speed + ", remaining " + agent.remainingDistance + ", position " + root.transform.position + ")");
                playback.Tick(equipment.ResolveAnimator(), null, EnemyAttackPhase.Active, 0, 1, 0, agent.velocity.magnitude, .1f);
                Check(playback.ActionClip == equipment.DirectionalMovement(velocity), "Actual playback selects " + velocity + " directional clip (velocity " + agent.velocity + ")");
            }
        }
        Time.timeScale = 0f;
        Object.Destroy(root);
        foreach (var projectile in Object.FindObjectsByType<ProjectileInstance>()) Object.Destroy(projectile.gameObject);
        yield return null;
        root = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity); yield return null;
        imp = root.GetComponent<ImpController>(); agent = root.GetComponent<NavMeshAgent>();
        target.transform.position = Vector3.forward * 3.5f; Physics.SyncTransforms(); imp.SetTarget(target.transform); imp.Tick(.4f);
        Check(imp.Tactic == ImpTactic.Retreat && imp.State == EnemyState.Position && imp.TacticalDestination.z < -.5f, "Pressure inside firing band produces a reachable backward retreat");
        imp.Tick(imp.retreatDuration + .1f);
        Check(imp.Tactic != ImpTactic.Retreat && agent.isStopped, "Retreat duration is bounded and leaves a punishable pause (" + imp.Tactic + ", " + imp.State + ", stopped " + agent.isStopped + ")");
        Object.Destroy(root);
        foreach (var projectile in Object.FindObjectsByType<ProjectileInstance>()) Object.Destroy(projectile.gameObject);
        yield return null;

        root = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity); yield return null;
        imp = root.GetComponent<ImpController>();
        target.transform.position = Vector3.forward * 1.2f; Physics.SyncTransforms(); imp.SetTarget(target.transform); imp.Tick(.31f);
        Check(imp.State == EnemyState.Telegraph && imp.CurrentAttack.kind != CreatureAttackKind.Projectile, "Imp uses melee when caught at close range");
        CompleteAttack(imp); imp.Tick(.4f);
        Check(imp.Tactic == ImpTactic.Retreat && imp.State == EnemyState.Position, "Melee finishes recovery before disengaging");
        Object.Destroy(root); yield return null;

        root = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity); yield return null;
        imp = root.GetComponent<ImpController>();
        agent = root.GetComponent<NavMeshAgent>();
        var backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backWall.transform.position = new Vector3(0, 1, -1.6f); backWall.transform.localScale = new Vector3(8, 2, .5f);
        var obstacle = backWall.AddComponent<NavMeshObstacle>(); obstacle.size = Vector3.one; obstacle.carveOnlyStationary = false; obstacle.carving = true;
        Physics.SyncTransforms(); Time.timeScale = 1f;
        double carveUntil = EditorApplication.timeSinceStartup + 3;
        while (!agent.Raycast(Vector3.back * 2.5f, out _) && EditorApplication.timeSinceStartup < carveUntil) yield return null;
        Time.timeScale = 0f;
        Check(agent.Raycast(Vector3.back * 2.5f, out _), "Test obstacle blocks navigation behind Imp");
        target.transform.position = Vector3.forward * 1.2f; Physics.SyncTransforms(); imp.SetTarget(target.transform); imp.Tick(.31f);
        Check(imp.CurrentAttack != null && imp.CurrentAttack.kind != CreatureAttackKind.Projectile, "Cornered Imp can defend with melee");
        CompleteAttack(imp); imp.Tick(.4f);
        Check(imp.Tactic == ImpTactic.Hold && agent.isStopped, "Imp rejects retreat paths through a wall and holds when cornered");
        Object.Destroy(backWall); Object.Destroy(root); yield return null;

        root = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity); yield return null;
        imp = root.GetComponent<ImpController>();
        target.transform.position = Vector3.forward * 6; Physics.SyncTransforms(); imp.SetTarget(target.transform); imp.Tick(.01f);
        Check(imp.State != EnemyState.Telegraph, "New target has a reaction delay");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(0, 1, 3); wall.transform.localScale = new Vector3(3, 2, .5f); Physics.SyncTransforms();
        imp.Tick(.4f);
        Check(imp.State != EnemyState.Telegraph && imp.Tactic == ImpTactic.Reposition, "Blocked sight repositions without shooting through walls");
        Object.Destroy(wall);
        root.GetComponent<CombatState>().DamagePosture(10000);
        Check(imp.State == EnemyState.Stagger && root.GetComponent<NavMeshAgent>().isStopped, "Posture break stops tactical navigation");
        Object.Destroy(root); yield return null;
        Check(true, "TACTICS_PLAY_COMPLETE");
    }

    static void CompleteAttack(EnemyController enemy)
    {
        for (int i = 0; i < 500 && EnemyActionPlayback.IsPerformingAttack(enemy); i++) enemy.Tick(.02f);
        Check(!EnemyActionPlayback.IsPerformingAttack(enemy), "Attack respects windup, active and recovery completion");
    }
}
