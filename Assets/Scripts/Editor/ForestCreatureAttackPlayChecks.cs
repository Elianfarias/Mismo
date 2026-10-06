using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Batch-only fixture: uses current prefabs and a temporary floor, never an authored scene.</summary>
public static class ForestCreatureAttackPlayChecks
{
    const string Pending = "Mismo.CreatureAttackPolish.Play";
    const string Report = "output/creature-attack-polish/play-checks.txt";
    static IEnumerator routine;
    static int lastFrame = -1;
    static double deadline;
    static NavMeshData navigation;
    static NavMeshDataInstance nav;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this isolated check in batch mode.");
        ForestCreatureAttackPolish.VerifyAndPreview();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        File.WriteAllText(Report, "");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Step;
    }

    static void Step()
    {
        if (!Application.isPlaying || Time.frameCount < 4 || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Creature attack Play Mode checks");
            routine ??= Run();
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    static void Finish(Exception error)
    {
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Step;
        if (nav.valid) nav.Remove();
        if (navigation != null) Object.Destroy(navigation);
        Time.captureDeltaTime = 0;
        File.AppendAllText(Report, error == null ? "PASS COMPLETE\n" : "FAIL " + error + "\n");
        Debug.Log(error == null ? "CREATURE_ATTACK_PLAY_PASS" : "CREATURE_ATTACK_PLAY_FAIL " + error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        File.AppendAllText(Report, "PASS " + message + "\n");
    }

    static IEnumerator Run()
    {
        Time.captureDeltaTime = 1f / 60;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = Vector3.down * .5f;
        floor.transform.localScale = new Vector3(50, 1, 50);
        navigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),
            new List<NavMeshBuildSource> { new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = floor.transform.localToWorldMatrix, size = Vector3.one } },
            new Bounds(Vector3.zero, new Vector3(50, 10, 50)), Vector3.zero, Quaternion.identity);
        Check(navigation != null, "Temporary navigation surface");
        nav = NavMesh.AddNavMeshData(navigation);
        var target = new GameObject("Attack target");
        var collider = target.AddComponent<CapsuleCollider>(); collider.height = 2; collider.center = Vector3.up;
        var life = target.AddComponent<Health>(); life.ConfigureMaximum(10000); life.Revive();
        target.AddComponent<DamageReceiver>();
        foreach (string species in new[] { "Boar", "Spider" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ForestCreatureIntegration.Prefabs + "/" + species + "_Standard.prefab");
            foreach (var sourceAttack in prefab.GetComponent<CreatureController>().Settings.attacks.Where(a => a.enabled))
            {
                var root = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                var enemy = root.GetComponent<CreatureController>();
                var settings = Object.Instantiate(enemy.Settings);
                settings.attacks = new[] { sourceAttack }; settings.decisionPause = 100;
                enemy.Configure(settings, root.GetComponent<DamageDealer>());
                yield return null;
                var animator = root.GetComponentInChildren<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var body = animator.GetComponentsInChildren<Transform>().Single(t => t.name == "Body");
                float restHeight = body.position.y - root.transform.position.y;
                Vector3 restBody = root.transform.InverseTransformPoint(body.position);
                var head = animator.GetComponentsInChildren<Transform>().Single(t => t.name == "Head");
                Quaternion restHead = head.localRotation;
                target.transform.position = Vector3.forward * (sourceAttack.minimumRange > 0 ? 4 : 1.6f);
                life.Revive(); Physics.SyncTransforms(); enemy.SetTarget(target.transform);
                int frames = 0;
                while (enemy.State != EnemyState.Telegraph && frames++ < 120) yield return null;
                Check(enemy.State == EnemyState.Telegraph, species + " " + sourceAttack.label + " enters preparation");
                bool moved = false, preparationSafe = true, contactSafe = true; float highest = restHeight;
                while (enemy.State == EnemyState.Telegraph && frames++ < 240)
                {
                    preparationSafe &= life.Current == life.Maximum;
                    // Bone local coordinates include the FBX's centimetre conversion.
                    moved |= Vector3.Distance(root.transform.InverseTransformPoint(body.position), restBody) > .04f || Quaternion.Angle(head.localRotation, restHead) > 10;
                    yield return null;
                }
                Check(moved, species + " " + sourceAttack.label + " actual playback shows anticipation");
                Check(preparationSafe, "No preparation damage");
                while (enemy.State == EnemyState.Attack && frames++ < 360)
                {
                    highest = Mathf.Max(highest, body.position.y - root.transform.position.y);
                    if (enemy.StateProgress < sourceAttack.damageStartsAt - .05f)
                        contactSafe &= life.Current == life.Maximum;
                    yield return null;
                }
                Check(enemy.State == EnemyState.Recovery, species + " " + sourceAttack.label + " finishes execution");
                Check(contactSafe, "No damage before contact window");
                Check(Mathf.Abs(life.Maximum - life.Current - sourceAttack.damage) < .01f, "Exactly one contact with configured damage");
                if (sourceAttack.label == "Jump") Check(highest - restHeight > .45f, "Actual pounce playback lifts the body before landing");
                float after = life.Current, recoveryStart = Time.time;
                while (enemy.State == EnemyState.Recovery && frames++ < 400) yield return null;
                Check(Time.time - recoveryStart <= .14f && enemy.State == EnemyState.Position, "Recovery exits within 0.14 seconds including frame rounding");
                Check(life.Current == after, "Recovery applies no extra damage");
                // Interrupt a fresh windup using the real posture/death callbacks.
                enemy.enabled = false; enemy.enabled = true; enemy.SetTarget(target.transform);
                root.GetComponent<NavMeshAgent>().Warp(Vector3.zero); target.transform.position = Vector3.forward * (sourceAttack.minimumRange > 0 ? 4 : 1.6f);
                Physics.SyncTransforms(); frames = 0;
                while (enemy.State != EnemyState.Telegraph && frames++ < 120) yield return null;
                Check(enemy.State == EnemyState.Telegraph, "Starts a new attack for cancellation");
                root.GetComponent<CombatState>().DamagePosture(10000);
                Check(enemy.State == EnemyState.Stagger && enemy.CurrentAttack == null, "Posture break cancels prepared attack");
                root.GetComponent<Health>().ApplyDamage(new DamageInfo(100000, target, root.transform.position, Vector3.back));
                Check(enemy.State == EnemyState.Dead, "Death cancels attack");
                yield return null;
                Check(life.Current == after, "Cancelled attack cannot damage later");
                Object.Destroy(root); Object.Destroy(settings); yield return null;
            }
        }
        Object.Destroy(target); Object.Destroy(floor);
    }
}
