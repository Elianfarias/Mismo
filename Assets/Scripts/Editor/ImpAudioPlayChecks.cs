using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Batch-only playback checks with the real Imp prefab in an unsaved test scene.</summary>
public static class ImpAudioPlayChecks
{
    const string Pending = "Mismo.ImpAudio.Play";
    const string Report = ImpAudioIntegration.Output + "/play-checks.txt";
    static IEnumerator routine;
    static int lastFrame = -1;
    static double deadline;
    static NavMeshData navigation;
    static NavMeshDataInstance nav;
    static readonly List<AudioClip> Heard = new List<AudioClip>();
    static void Listen(AudioClip clip, float volume) { if (volume > 0) Heard.Add(clip); }
    static void Check(bool ok, string label)
    {
        if (!ok) throw new InvalidOperationException(label);
        File.AppendAllText(Report, "PASS " + label + "\n");
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Dedicated batch process required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        File.WriteAllText(Report, "Actual Imp prefab / Play Mode\n");
        SessionState.SetBool(Pending, true);
        Resume(); EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update -= Step; EditorApplication.update += Step;
    }

    static void Step()
    {
        if (!Application.isPlaying || Time.frameCount < 4 || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Imp audio playback checks");
            routine ??= Run();
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    static void Finish(Exception error)
    {
        AudioEvents.OnPlayAbilitySFX -= Listen;
        SessionState.SetBool(Pending, false); EditorApplication.update -= Step;
        if (nav.valid) nav.Remove();
        if (navigation != null) Object.Destroy(navigation);
        CombatTimeFeedback.CancelForPause(); Time.timeScale = 1;
        File.AppendAllText(Report, error == null ? "PASS IMP_AUDIO_PLAY_COMPLETE\n" : "FAIL " + error + "\n");
        Debug.Log(error == null ? "IMP_AUDIO_PLAY_COMPLETE" : "IMP_AUDIO_PLAY_FAILED " + error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    static AudioSource[] WorldSources() => Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
        .Where(s => s.gameObject.name == "World SFX").ToArray();

    static IEnumerator Run()
    {
        Time.timeScale = 0; // Real lifecycle, manually advanced combat/physics queries.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = Vector3.down * .5f; floor.transform.localScale = new Vector3(80, 1, 80);
        navigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource> {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = floor.transform.localToWorldMatrix, size = Vector3.one }
        }, new Bounds(Vector3.zero, new Vector3(80, 10, 80)), Vector3.zero, Quaternion.identity);
        Check(navigation != null, "Temporary navigation surface"); nav = NavMesh.AddNavMeshData(navigation);
        new GameObject("Test listener").AddComponent<AudioListener>();
        var target = new GameObject("Imp audio target");
        var collider = target.AddComponent<CapsuleCollider>(); collider.height = 2; collider.center = Vector3.up;
        var life = target.AddComponent<Health>(); life.ConfigureMaximum(10000); life.Revive(); target.AddComponent<DamageReceiver>();
        AudioEvents.OnPlayAbilitySFX += Listen;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImpAudioIntegration.PrefabPath);
        foreach (bool ranged in new[] { false, true })
        {
            Vector3 origin = new Vector3(10, 0, 6);
            target.transform.position = origin + Vector3.forward * (ranged ? 6.5f : 1.2f);
            var root = Object.Instantiate(prefab, origin, Quaternion.identity);
            yield return null; // Awake/OnEnable/Start on the actual prefab.
            var enemy = root.GetComponent<ImpController>();
            Check(root.GetComponent<NavMeshAgent>().isOnNavMesh, "Imp initialized on navigation");
            Heard.Clear(); life.Revive(); Physics.SyncTransforms(); enemy.SetTarget(target.transform);
            enemy.Tick(enemy.reactionDelay + .01f);
            var action = enemy.CurrentAttack;
            Check(enemy.State == EnemyState.Telegraph && action.kind == (ranged ? CreatureAttackKind.Projectile : CreatureAttackKind.Melee),
                (ranged ? "Fireball" : "Melee") + " enters its own telegraph");
            Check(Heard.Count == 1 && Heard[0] == action.preparationSfx, "Preparation sound once at telegraph start");
            enemy.Tick(action.windup - .01f);
            Check(Heard.Count == 1 && enemy.State == EnemyState.Telegraph, "No launch sound before the end of preparation");
            Check(Object.FindObjectsByType<ProjectileInstance>(FindObjectsSortMode.None).Length == 0, "No premature projectile");
            enemy.Tick(.02f);
            Check(Heard.Count == 2 && Heard[1] == action.executionSfx, "Execution sound once at the transition to attack");
            if (ranged)
            {
                var projectile = Object.FindObjectsByType<ProjectileInstance>(FindObjectsSortMode.None).Single();
                Check(WorldSources().Length == 0, "No explosion at launch");
                for (int i = 0; i < 30 && projectile.enabled; i++) projectile.Step(.025f);
                Check(!projectile.enabled && life.Current < life.Maximum, "Real fireball collides and deals damage");
                var sources = WorldSources();
                Check(sources.Length == 1, "One explosion on collision");
                Check(Vector3.Distance(sources[0].transform.position, collider.ClosestPoint(sources[0].transform.position)) < .05f &&
                    sources[0].transform.position.sqrMagnitude > 25, "Explosion audio originates at the actual contact, away from world origin");
                Check(sources[0].spatialBlend == 1 && Mathf.Approximately(sources[0].volume, action.projectileImpactSfxVolume) &&
                    sources[0].outputAudioMixerGroup == AudioRuntime.SfxGroup, "Explosion uses 3D SFX routing and authored volume");
                projectile.Step(.1f); Check(WorldSources().Length == 1, "Repeated projectile step does not repeat explosion");
                var miss = ProjectileInstance.Spawn(root, new Vector3(25, 10, 25), Vector3.up, 1, 10, .5f, .1f, null,
                    new ProjectileImpactSettings { impactSfx = action.projectileImpactSfx });
                miss.Step(1); Check(!miss.enabled && WorldSources().Length == 1, "Range expiry does not fake an impact explosion");
            }
            enemy.Tick(action.active + .01f); enemy.Tick(.01f);
            Check(Heard.Count == 2, "No duplicate voice during active/recovery frames");
            var receiver = root.GetComponent<DamageReceiver>();
            receiver.Resolve(new DamageInfo(1, target, root.transform.position, Vector3.back, AttackIdentity.Next(), 0));
            Check(Heard.Count == 3 && Heard.Last() == enemy.Settings.hitSfx[0], "Confirmed Imp hit plays approved B voice");
            receiver.Resolve(new DamageInfo(1, target, root.transform.position, Vector3.back, AttackIdentity.Next(), 0, statusEffect: StatusEffectType.Burn));
            Check(Heard.Count == 3, "Burn tick does not add a hurt voice");
            CombatTimeFeedback.CancelForPause(); Object.Destroy(root); yield return null;
        }
        var cancelRoot = Object.Instantiate(prefab, new Vector3(10, 0, 6), Quaternion.identity);
        yield return null;
        var cancelled = cancelRoot.GetComponent<ImpController>();
        Heard.Clear(); cancelled.SetTarget(target.transform); cancelled.Tick(cancelled.reactionDelay + .01f);
        Check(cancelled.State == EnemyState.Telegraph && Heard.Count == 1, "Cancellation fixture entered fire preparation");
        cancelRoot.GetComponent<CombatState>().DamagePosture(10000);
        cancelled.Tick(1.21f);
        Check(Heard.Count == 1 && Object.FindObjectsByType<ProjectileInstance>(FindObjectsSortMode.None).Length == 0,
            "Posture cancellation prevents launch audio and projectile");
        int count = WorldSources().Length;
        AudioRuntime.PlayWorldSFX(null, Vector3.one);
        AudioRuntime.PlayWorldSFX(cancelled.Settings.attacks[1].projectileImpactSfx, Vector3.one, 0);
        Check(WorldSources().Length == count, "Null and muted impact sounds create no sources");
        Object.Destroy(cancelRoot); Object.Destroy(target);
    }
}
