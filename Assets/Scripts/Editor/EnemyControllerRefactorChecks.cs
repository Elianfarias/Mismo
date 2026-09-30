using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Editor;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Asset migration and actual prefab lifecycle checks in an isolated, temporary scene.</summary>
public static class EnemyControllerRefactorChecks
{
    const string Output = "output/enemy-controllers";
    const string Request = "Temp/EnemyControllerChecks.request";
    const string Active = "Mismo.EnemyControllerChecks.Active";
    const string TestScene = "Assets/Scenes/EnemyControllerValidation.unity";
    [Serializable] class SavedScenes { public SceneSetup[] scenes; }
    static IEnumerator routine;
    static int frame = -1;
    static double deadline;
    static NavMeshData navData;
    static NavMeshDataInstance nav;

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged -= PlayState;
        EditorApplication.playModeStateChanged += PlayState;
        deadline = EditorApplication.timeSinceStartup + 150;
    }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Request))
        {
            string command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            if (command == "build") BuildContent();
            else { if (command == "tactics") ImpTacticsChecks.Configure(); Run(); }
            return;
        }
        if (!SessionState.GetBool(Active, false) || !Application.isPlaying || Time.frameCount < 4 || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Enemy Play Mode checks");
            if (routine == null) routine = PlayChecks();
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }

    static void Check(bool ok, string message, string file = "editor-checks.txt")
    {
        if (!ok) throw new InvalidOperationException(message);
        File.AppendAllText(Output + "/" + file, "PASS " + message + "\n");
    }

    [MenuItem("Mismo/Enemigos/Verificar separación de controladores")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/editor-checks.txt", "");
        bool organization = SessionState.GetBool("Mismo.Organization.Running", false);
        SessionState.SetBool("Mismo.Organization.Running", true);
        try
        {
            EnemyControllerMigration.Run();
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Art/Prefabs/Enemies" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var controller = root.GetComponent<EnemyController>();
                if (controller == null) continue;
                Check(root.GetComponents<EnemyController>().Length == 1, "One controller: " + path);
                Check(controller.Settings != null && new SerializedObject(controller).FindProperty("weapon").objectReferenceValue != null,
                    "Settings and damage source: " + path);
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                    Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "No missing script: " + path + "/" + item.name);
                if (path == EnemyControllerMigration.ImpPath) Check(controller is ImpController, "Imp uses its own humanoid controller");
                else if (controller.Settings is CreatureSettings) Check(controller is CreatureController && !(controller is HumanoidEnemyController), "Creature stays outside humanoid branch: " + path);
                string json = EditorJsonUtility.ToJson(controller.Settings);
                var copy = ScriptableObject.CreateInstance(controller.Settings.GetType());
                try { EditorJsonUtility.FromJsonOverwrite(json, copy); Check(EditorJsonUtility.ToJson(copy) == json, "Inherited settings round trip: " + path); }
                finally { Object.DestroyImmediate(copy); }
            }
            SelectionChecks();
            ImpTacticsChecks.EditorChecks();
            CombatFeedbackChecks.RunBatch();
            Check(true, "Existing CombatFeedbackChecks passed, including combo immunity, posture, parry and death");
            typeof(EnemyAnimationChecks).GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Check(true, "Existing EnemyAnimationChecks passed");
        }
        catch (Exception e) { File.AppendAllText(Output + "/editor-checks.txt", "FAIL\n" + e); Debug.LogException(e); return; }
        finally { SessionState.SetBool("Mismo.Organization.Running", organization); }
        try { ProjectOrganizationChecks.Run(); File.WriteAllText(Output + "/organization.txt", "PASS\n"); }
        catch (Exception e) { File.WriteAllText(Output + "/organization.txt", "FAIL\n" + e); }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty || string.IsNullOrEmpty(scene.path))
            { File.WriteAllText(Output + "/play-checks.txt", "BLOCKED: open scene has unsaved changes; preserved."); return; }
        }
        if (File.Exists(TestScene)) { File.WriteAllText(Output + "/play-checks.txt", "BLOCKED: test scene already exists; preserved."); return; }
        SessionState.SetString(Active + ".Scenes", JsonUtility.ToJson(new SavedScenes { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        var test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(test, TestScene);
        File.WriteAllText(Output + "/play-checks.txt", "Running actual prefab Play Mode checks\n");
        SessionState.SetBool(Active, true);
        deadline = EditorApplication.timeSinceStartup + 150;
        EditorApplication.EnterPlaymode();
    }

    static void SelectionChecks()
    {
        var settings = ScriptableObject.CreateInstance<EnemySettings>();
        var visual = new GameObject("Projectile eligibility check");
        var random = UnityEngine.Random.state;
        try
        {
            var selection = new EnemyAttackSelection();
            Check(selection.SelectLegacyMelee(settings, 1f, false) == settings.slash, "Legacy close slash");
            Check(selection.SelectLegacyMelee(settings, 4f, false) == settings.charge, "Legacy midrange charge");
            selection.Commit(settings.charge, settings);
            Check(selection.SelectLegacyMelee(settings, 4f, true) == null, "Charge cooldown blocks pressure attack");
            selection.Tick(settings.chargeCooldown);
            Check(selection.SelectLegacyMelee(settings, 4f, true) == settings.charge, "Charge cooldown expires");
            var spell = new GoblinAttack { kind = CreatureAttackKind.Projectile, minimumRange = 3, range = 12, projectileVisual = visual };
            settings.attacks = new[] { spell };
            Check(selection.SelectWeighted(settings, 2) == null && selection.SelectWeighted(settings, 6) == spell, "Projectile minimum range and selection");
            selection.Commit(spell, settings);
            selection.Tick(spell.windup + spell.active + spell.recovery + spell.cooldown - .02f);
            Check(!selection.Eligible(spell, 6), "Cooldown includes all action phases");
            selection.Tick(.03f);
            Check(selection.Eligible(spell, 6), "Projectile cooldown expires");
            spell.projectileVisual = null;
            Check(!selection.Eligible(spell, 6), "Missing projectile visual is ineligible");
            selection.Reset();
            Check(selection.SelectLegacyMelee(settings, 4f, false) == settings.charge, "New life clears action clocks");
        }
        finally { UnityEngine.Random.state = random; Object.DestroyImmediate(settings); Object.DestroyImmediate(visual); }
    }

    static IEnumerator PlayChecks()
    {
        Time.timeScale = 0f; // Deterministic manual ticks; Unity still runs actual lifecycle and animation callbacks.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = Vector3.down * .5f;
        floor.transform.localScale = new Vector3(80, 1, 80);
        var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = floor.transform.localToWorldMatrix, size = Vector3.one };
        navData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource> { source }, new Bounds(Vector3.zero, new Vector3(80, 10, 80)), Vector3.zero, Quaternion.identity);
        if (navData == null) throw new Exception("Could not build isolated navigation surface");
        nav = NavMesh.AddNavMeshData(navData);
        var target = new GameObject("Combat check target");
        var capsule = target.AddComponent<CapsuleCollider>(); capsule.height = 2; capsule.center = Vector3.up;
        var life = target.AddComponent<Health>(); life.ConfigureMaximum(10000); life.Revive();
        target.AddComponent<DamageReceiver>();
        string[] paths = { "Assets/Art/Prefabs/Enemies/Goblin.prefab", EnemyControllerMigration.ImpPath,
            "Assets/Art/Prefabs/Enemies/ForestCreatures/Boar_Standard.prefab", "Assets/Art/Prefabs/Enemies/ForestCreatures/Spider_Standard.prefab",
            "Assets/Art/Prefabs/Enemies/ForestCreatures/Forest_Golem_Stylized.prefab" };
        foreach (string path in paths)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), Vector3.zero, Quaternion.identity);
            yield return null; // Awake, OnEnable and inherited Start run normally.
            var enemy = root.GetComponent<EnemyController>();
            var agent = root.GetComponent<NavMeshAgent>();
            Check(enemy.State == EnemyState.Idle && agent.isOnNavMesh, "Inherited lifecycle and navigation: " + root.name, "play-checks.txt");
            Check((root.GetComponent<GoblinHitReaction>() != null) == (enemy is HumanoidEnemyController), "Species reaction policy: " + root.name, "play-checks.txt");
            target.transform.position = Vector3.forward * 1.2f; life.Revive(); Physics.SyncTransforms();
            enemy.SetTarget(target.transform); enemy.Tick(enemy is ImpController impMelee ? impMelee.reactionDelay + .01f : .01f);
            Check(enemy.State == EnemyState.Telegraph && life.Current == life.Maximum, "Telegraph without premature damage: " + root.name, "play-checks.txt");
            float damage = enemy.CurrentAttack.damage;
            Advance(enemy, EnemyState.Telegraph);
            Advance(enemy, EnemyState.Attack);
            Check(enemy.State == EnemyState.Recovery && Mathf.Approximately(life.Maximum - life.Current, damage), "One melee impact: " + root.name, "play-checks.txt");
            float after = life.Current; enemy.Tick(.01f);
            Check(life.Current == after, "No recovery damage: " + root.name, "play-checks.txt");
            root.GetComponent<CombatState>().DamagePosture(10000);
            Check(enemy.State == EnemyState.Stagger && enemy.CurrentAttack == null, "Posture break cancels action: " + root.name, "play-checks.txt");
            root.GetComponent<Health>().ApplyDamage(new DamageInfo(100000, target, root.transform.position, Vector3.back));
            Check(enemy.State == EnemyState.Dead && !agent.enabled && !root.GetComponentsInChildren<Collider>().Any(c => c.enabled), "Death cancels navigation and contacts: " + root.name, "play-checks.txt");
            enemy.Tick(10); Check(life.Current == after, "Dead enemy cannot damage: " + root.name, "play-checks.txt");
            Object.Destroy(root); yield return null;
        }
        var impRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyControllerMigration.ImpPath), Vector3.zero, Quaternion.identity);
        yield return null;
        var imp = impRoot.GetComponent<ImpController>();
        target.transform.position = Vector3.forward * 6; life.Revive(); Physics.SyncTransforms();
        imp.SetTarget(target.transform); imp.Tick(imp.reactionDelay + .01f);
        Check(imp.CurrentAttack?.kind == CreatureAttackKind.Projectile, "Imp selects its spell at range", "play-checks.txt");
        Check(EnemyActionPlayback.IsPerformingAttack(imp), "Shared playback recognizes Imp attack", "play-checks.txt");
        yield return null;
        Advance(imp, EnemyState.Telegraph);
        var projectiles = Object.FindObjectsByType<ProjectileInstance>();
        Check(projectiles.Length == 1, "Imp releases exactly one projectile", "play-checks.txt");
        var projectile = projectiles[0];
        for (int i = 0; i < 200 && projectile != null && projectile.enabled; i++) projectile.Step(.02f);
        Check(life.Current < life.Maximum, "Imp projectile applies damage with actual socket and visual", "play-checks.txt");
        Object.Destroy(impRoot);
        yield return null;
        impRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyControllerMigration.ImpPath), Vector3.zero, Quaternion.identity);
        yield return null;
        imp = impRoot.GetComponent<ImpController>();
        imp.SetTarget(target.transform); imp.Tick(imp.reactionDelay + .01f);
        Check(imp.State == EnemyState.Telegraph && imp.CurrentAttack?.kind == CreatureAttackKind.Projectile, "Imp begins second spell", "play-checks.txt");
        impRoot.GetComponent<CombatState>().DamagePosture(10000);
        imp.Tick(20);
        Check(imp.State == EnemyState.Stagger && imp.CurrentAttack == null, "Posture cancels spell before release", "play-checks.txt");
        Check(Object.FindObjectsByType<ProjectileInstance>().Length == 0, "No delayed projectile after interruption", "play-checks.txt");
        Object.Destroy(impRoot);
        yield return null;
        var tactics = ImpTacticsChecks.PlayChecks(target);
        while (tactics.MoveNext()) yield return tactics.Current;
        Check(true, "PLAY_MODE_COMPLETE", "play-checks.txt");
    }

    static void Advance(EnemyController enemy, EnemyState phase)
    {
        for (int i = 0; i < 2000 && enemy.State == phase; i++) enemy.Tick(.02f);
        if (enemy.State == phase) throw new Exception("Action did not leave " + phase);
    }

    static void Finish(Exception error)
    {
        if (error != null) File.AppendAllText(Output + "/play-checks.txt", "FAIL\n" + error);
        if (nav.valid) nav.Remove();
        if (navData != null) Object.Destroy(navData);
        routine = null; Time.timeScale = 1f;
        EditorApplication.ExitPlaymode();
    }

    static void PlayState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Active, false)) return;
        SessionState.SetBool(Active, false);
        var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Active + ".Scenes", ""));
        EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
        AssetDatabase.DeleteAsset(TestScene);
        SessionState.EraseString(Active + ".Scenes");
    }

    static void BuildContent()
    {
        Directory.CreateDirectory(Output);
        try
        {
            const string folder = ".validation/enemy-controllers/content";
            Directory.CreateDirectory(folder);
            string[] paths = { "Assets/Art/Prefabs/Enemies/Goblin.prefab", EnemyControllerMigration.ImpPath,
                "Assets/Art/Prefabs/Enemies/ForestCreatures/Boar_Standard.prefab" };
            var manifest = BuildPipeline.BuildAssetBundles(folder, new[] { new AssetBundleBuild { assetBundleName = "enemy-controllers", assetNames = paths } },
                BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new Exception("Enemy content build failed");
            var bundle = AssetBundle.LoadFromFile(folder + "/enemy-controllers");
            if (bundle == null) throw new Exception("Enemy content could not be loaded");
            try
            {
                var expected = new[] { typeof(GoblinController), typeof(ImpController), typeof(CreatureController) };
                for (int i = 0; i < paths.Length; i++)
                {
                    string path = paths[i];
                    var prefab = bundle.LoadAsset<GameObject>(path);
                    var controller = prefab != null ? prefab.GetComponent<EnemyController>() : null;
                    if (controller == null || controller.GetType() != expected[i] || controller.Settings == null)
                        throw new Exception("Missing or incorrect controller/settings in built content: " + path);
                }
                File.WriteAllText(Output + "/content-build.txt", "PASS: Goblin, Imp and creature controllers and settings loaded from Windows asset bundle.\n");
            }
            finally { bundle.Unload(true); }
        }
        catch (Exception e) { File.WriteAllText(Output + "/content-build.txt", "FAIL\n" + e); }
    }
}
