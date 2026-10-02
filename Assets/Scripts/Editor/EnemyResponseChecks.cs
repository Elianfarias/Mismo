using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real prefab damage, navigation and animation checks in a disposable scene.</summary>
public static class EnemyResponseChecks
{
    const string Active = "Mismo.EnemyResponseChecks";
    const string ScenePath = "Assets/Scenes/EnemyResponseValidation.unity";
    const string Report = "output/enemy-response/checks.txt";
    static readonly string[] Prefabs = { "Assets/Art/Prefabs/Enemies/Goblin.prefab", EnemyControllerMigration.ImpPath };
    [Serializable] sealed class SavedScenes { public SceneSetup[] scenes; }
    static IEnumerator routine;
    static int frame = -1;
    static double deadline;
    static NavMeshData data;
    static NavMeshDataInstance nav;

    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged -= PlayState; EditorApplication.playModeStateChanged += PlayState;
        deadline = EditorApplication.timeSinceStartup + 150;
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the editor menu for an interactive session.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        Run();
    }

    [MenuItem("Mismo/Enemigos/Verificar respuesta a combos y retroceso")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty || string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                throw new InvalidOperationException("Open scenes have unsaved changes; preserved.");
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Existing validation scene preserved.");
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, "Enemy combat response / " + DateTime.Now.ToString("s") + "\n");
        // Existing regression includes super armor, custom interrupt limits, parry and posture breaks.
        CombatFeedbackChecks.RunBatch();
        Check(true, "Existing combat feedback and interrupt resistance regression");
        SessionState.SetString(Active + ".Scenes", JsonUtility.ToJson(new SavedScenes { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }

    static void Poll()
    {
        if (!SessionState.GetBool(Active, false) || !Application.isPlaying ||
            EditorApplication.isCompiling || Time.frameCount < 4 || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Enemy response Play Mode checks");
            routine ??= PlayChecks();
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        File.AppendAllText(Report, "PASS " + label + "\n");
    }

    static HitResult Hit(EnemyController enemy, GameObject source, float posture = 1)
        => enemy.GetComponent<DamageReceiver>().Resolve(new DamageInfo(1, source, enemy.transform.position + Vector3.up,
            Vector3.back, AttackIdentity.Next(), posture));

    static void TwoHits(EnemyController enemy, GameObject source)
    {
        Check(Hit(enemy, source).HealthDamage > 0 && enemy.State == EnemyState.Stagger, enemy.name + ": first basic interrupts");
        enemy.Tick(.1f); enemy.GetComponent<CombatState>().Tick(.1f);
        Check(Hit(enemy, source).HealthDamage > 0 && enemy.GetComponent<CombatState>().InterruptImmune,
            enemy.name + ": second basic activates resistance");
        enemy.Tick(enemy.Settings.comboResponseStun + .01f);
    }

    static IEnumerator PlayChecks()
    {
        Time.timeScale = 0;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = Vector3.down * .5f; floor.transform.localScale = new Vector3(80, 1, 80);
        var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = floor.transform.localToWorldMatrix, size = Vector3.one };
        data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource> { source },
            new Bounds(Vector3.zero, new Vector3(80, 10, 80)), Vector3.zero, Quaternion.identity);
        Check(data != null, "Isolated navigation surface"); nav = NavMesh.AddNavMeshData(data);
        var target = new GameObject("Response target");
        var capsule = target.AddComponent<CapsuleCollider>(); capsule.height = 2; capsule.center = Vector3.up;
        var targetLife = target.AddComponent<Health>(); targetLife.ConfigureMaximum(10000); targetLife.Revive();
        target.AddComponent<DamageReceiver>();
        var sword = new GameObject("Basic combo source"); sword.AddComponent<BoxCollider>().isTrigger = true;
        sword.AddComponent<AttackHitbox>();
        foreach (string path in Prefabs)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), Vector3.zero, Quaternion.identity);
            yield return null;
            var enemy = root.GetComponent<EnemyController>(); var agent = root.GetComponent<NavMeshAgent>();
            Check(enemy.Settings.positioningSpeed >= 3.5f, root.name + ": serialized lateral movement is brisk");
            float acceleration = agent.acceleration; bool braking = agent.autoBraking;
            target.transform.position = Vector3.forward * 1.4f; Physics.SyncTransforms(); enemy.SetTarget(target.transform);
            enemy.Tick(.4f);
            Check(enemy.State == EnemyState.Telegraph, root.name + ": normal melee telegraph before interruption");
            TwoHits(enemy, sword);
            Check(enemy.IsBackstepping && enemy.State == EnemyState.Position, root.name + ": escape starts within 0.13 seconds of second basic");
            Check(agent.speed >= 8 && agent.acceleration >= 100, root.name + ": burst does not inherit walking speed/acceleration");
            for (int i = 0; i < 2; i++)
                Check(Hit(enemy, sword).HealthDamage > 0 && enemy.IsBackstepping && enemy.State != EnemyState.Stagger,
                    root.name + ": further basic damages without restarting flinch");
            Vector3 start = root.transform.position; float peak = 0; bool backwardClip = false;
            Time.timeScale = 1;
            double timeout = EditorApplication.timeSinceStartup + 3;
            using (var playback = new EnemyActionPlayback())
            {
                while (enemy.IsBackstepping && EditorApplication.timeSinceStartup < timeout)
                {
                    peak = Mathf.Max(peak, agent.velocity.magnitude);
                    var equipment = root.GetComponent<EnemyEquipment>();
                    if (equipment != null && equipment.backwardClip != null && agent.velocity.magnitude > 1)
                    {
                        playback.Tick(equipment.ResolveAnimator(), null, EnemyAttackPhase.Active, 0, 2, 0, agent.velocity.magnitude, .02f);
                        backwardClip |= playback.ActionClip == equipment.backwardClip;
                    }
                    yield return null;
                }
            }
            Time.timeScale = 0;
            float travelled = Vector3.Distance(start, root.transform.position);
            Check(!enemy.IsBackstepping && travelled > 1.4f && travelled < 2.9f && peak > 6,
                root.name + ": real quick retreat, distance " + travelled.ToString("F2") + ", peak speed " + peak.ToString("F2"));
            Check(agent.acceleration == acceleration && agent.autoBraking == braking, root.name + ": temporary navigation settings restored");
            if (enemy is ImpController) Check(backwardClip, "Imp plays backward motion during escape instead of full-body hit flinch");
            for (int i = 0; i < 10 && enemy.CurrentAttack == null; i++) enemy.Tick(.02f);
            Check(enemy.State == EnemyState.Telegraph && enemy.CurrentAttack != null,
                root.name + ": resumes an attack immediately after separation");
            Check((enemy.CurrentAttack.kind == CreatureAttackKind.Projectile) == (enemy is ImpController),
                root.name + ": melee re-engages, Imp takes ranged opportunity");
            Check(enemy.StateProgress < .5f, root.name + ": counter still has its authored telegraph");
            enemy.GetComponent<CombatState>().DamagePosture(10000);
            Check(enemy.State == EnemyState.Stagger && !enemy.IsBackstepping && agent.isStopped,
                root.name + ": posture break still overrides resistance");
            Object.Destroy(root); yield return null;
        }

        // Physics walls must be respected even before NavMesh obstacle carving updates.
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(0, 1, -1.2f); wall.transform.localScale = new Vector3(8, 2, .4f);
        foreach (string path in Prefabs)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), Vector3.zero, Quaternion.identity);
            yield return null;
            var enemy = root.GetComponent<EnemyController>();
            target.transform.position = Vector3.forward * 1.4f; Physics.SyncTransforms(); enemy.SetTarget(target.transform);
            TwoHits(enemy, sword); enemy.Tick(.02f);
            Check(!enemy.IsBackstepping && enemy.State == EnemyState.Telegraph && enemy.CurrentAttack.kind != CreatureAttackKind.Projectile,
                root.name + ": cornered enemy counters with melee instead of moving through wall");
            Object.Destroy(root); yield return null;
        }
        Object.Destroy(wall); yield return null;
        foreach (bool breakPosture in new[] { true, false })
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyControllerMigration.ImpPath), Vector3.zero, Quaternion.identity);
            yield return null;
            var enemy = root.GetComponent<EnemyController>(); var agent = root.GetComponent<NavMeshAgent>();
            float acceleration = agent.acceleration;
            enemy.SetTarget(target.transform); TwoHits(enemy, sword);
            Check(enemy.IsBackstepping, "Interruption test starts an escape");
            if (breakPosture) enemy.GetComponent<CombatState>().DamagePosture(10000);
            else enemy.enabled = false;
            Check(!enemy.IsBackstepping && agent.isStopped && agent.acceleration == acceleration,
                breakPosture ? "Posture break cancels escape immediately" : "Disable cancels escape and restores navigation");
            Object.Destroy(root); yield return null;
        }
        // Keep the previous real movement, blocked retreat, sight and animation regression.
        var tactics = ImpTacticsChecks.PlayChecks(target);
        while (tactics.MoveNext()) yield return tactics.Current;
        Check(true, "Existing Imp tactical navigation and directional animation regression");
        Check(true, "ALL_RESPONSE_CHECKS_COMPLETE");
    }

    static void Finish(Exception error)
    {
        if (error != null) { File.AppendAllText(Report, "FAIL\n" + error); Debug.LogException(error); }
        SessionState.SetBool(Active + ".Passed", error == null);
        if (nav.valid) nav.Remove(); if (data != null) Object.Destroy(data);
        routine = null; Time.timeScale = 1; EditorApplication.ExitPlaymode();
    }

    static void PlayState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Active, false)) return;
        SessionState.SetBool(Active, false);
        var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Active + ".Scenes", ""));
        EditorSceneManager.RestoreSceneManagerSetup(saved.scenes); AssetDatabase.DeleteAsset(ScenePath);
        SessionState.EraseString(Active + ".Scenes");
        if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Active + ".Passed", false) ? 0 : 1);
    }
}
