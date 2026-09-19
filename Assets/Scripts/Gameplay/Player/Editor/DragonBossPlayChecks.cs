using System;
using System.Collections;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class DragonBossPlayChecks
    {
        const string Pending = "Mismo.DragonBossPlayChecks";
        public static void Begin()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Estas pruebas de combate se ejecutan en un proyecto batch aislado.");
            DragonVoxelIntegration.RefreshBossesFromExportedModel();
            DragonVoxelChecks.Run();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartProbe()
        {
            if (!Application.isBatchMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            new GameObject("Dragon combat probe").AddComponent<DragonCombatProbe>();
        }
    }

    public sealed class DragonCombatProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor"; floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(200, 1, 200);
            foreach (string color in new[] { "Red", "Blue", "Green", "Purple" })
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(DragonVoxelIntegration.BossFolder + "/Firyx_" + color + ".prefab");
                GameObject model = Object.Instantiate(source);
                var boss = model.GetComponent<DragonBossController>();
                var library = model.GetComponentInChildren<VoxelRigInstance>();
                var config = Object.Instantiate(boss.Settings);
                config.flightCooldown = 8; config.flightDuration = .6f; config.breathCooldown = 1; config.defendChance = 0;
                boss.Configure(config, library.animator, Find(model.transform, "BreathOrigin"));
                var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = "Combat target"; dummy.transform.position = new Vector3(0, 0, 14);
                dummy.GetComponent<CapsuleCollider>().center = Vector3.up;
                var health = dummy.AddComponent<Health>(); health.ConfigureMaximum(10000); health.Revive(); dummy.AddComponent<DamageReceiver>();
                yield return null;
                try
                {
                    boss.SetTarget(dummy.transform);
                    var visited = new HashSet<DragonBossState>();
                    float before = health.Current;
                    for (int i = 0; i < 330; i++) Step(boss, library, visited);
                    Require(visited.Contains(DragonBossState.Roaring) && visited.Contains(DragonBossState.Attacking), color + " engagement");
                    Require(visited.Contains(DragonBossState.Flying) && visited.Contains(DragonBossState.Landing), color + " flight and landing");
                    Require(health.Current < before, color + " breath damage");

                    config.flightCooldown = 10000; config.flightHeight = 0; config.breathCooldown = 10000;
                    dummy.transform.position = model.transform.position + model.transform.forward * 5;
                    before = health.Current;
                    for (int i = 0; i < 170; i++) Step(boss, library, visited);
                    Require(health.Current < before, color + " melee damage");
                    config.defendChance = 1;
                    for (int i = 0; i < 120 && boss.State != DragonBossState.Defending; i++) Step(boss, library, visited);
                    Require(boss.State == DragonBossState.Defending, color + " defensive stance");
                    float bossBefore = boss.Health.Current;
                    var receiver = model.GetComponent<DamageReceiver>();
                    receiver.ReceiveDamage(new DamageInfo(10, dummy, model.transform.position, -model.transform.forward, AttackIdentity.Next()));
                    Require(receiver.LastResult.Outcome == HitOutcome.Block && boss.Health.Current == bossBefore, color + " frontal guard");
                    config.defendChance = 0;
                    boss.OnAttackParried(default);
                    Require(boss.State == DragonBossState.Staggered || boss.State == DragonBossState.Landing, color + " parry response");

                    boss.Health.ApplyDamage(new DamageInfo(config.health * .55f, dummy, model.transform.position, Vector3.back));
                    for (int i = 0; i < 130; i++) Step(boss, library, visited);
                    Require(boss.Enraged, color + " enrage phase");
                    dummy.transform.position = new Vector3(0, 0, 150);
                    for (int i = 0; i < 300; i++) Step(boss, library, visited);
                    Require(boss.State == DragonBossState.Sleeping && boss.Health.Normalized > .99f, color + " leash/reset");
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = new Vector3(0, 4, 9); wall.transform.localScale = new Vector3(30, 8, 1);
                    dummy.transform.position = new Vector3(0, 0, 14); boss.SetTarget(dummy.transform); before = health.Current;
                    for (int i = 0; i < 90; i++) Step(boss, library, visited);
                    Require(health.Current == before && boss.State == DragonBossState.Sleeping, color + " occluded target");
                    wall.SetActive(false); Object.Destroy(wall);

                    int deaths = 0; boss.Defeated += () => deaths++;
                    boss.Health.ApplyDamage(new DamageInfo(999999, dummy, model.transform.position, Vector3.back));
                    Require(boss.State == DragonBossState.Dead && deaths == 1, color + " death once");
                    Require(library.surface.enabled, color + " death clip remains visible before disintegration");
                    before = health.Current; for (int i = 0; i < 30; i++) Step(boss, library, visited);
                    Require(health.Current == before && !model.GetComponent<CapsuleCollider>().enabled, color + " no corpse damage/collider");
                    model.SetActive(false); model.SetActive(true);
                    Require(boss.State == DragonBossState.Sleeping && boss.Health.Normalized > .99f, color + " pooled respawn");
                    Debug.Log("PASS BOSS " + color + ": engagement, breath, melee, guard, flight/landing, parry, enrage, leash, wall occlusion, death, respawn.");
                }
                catch (Exception error)
                { Debug.LogException(error); EditorApplication.Exit(1); yield break; }
                Object.Destroy(model); Object.Destroy(dummy); Object.Destroy(config);
                yield return null;
            }
            Debug.Log("DRAGON_COMBAT_CHECKS_OK"); EditorApplication.Exit(0);
        }
        static Transform Find(Transform root, string name)
        { foreach (var t in root.GetComponentsInChildren<Transform>()) if (t.name == name) return t; throw new Exception(name); }
        static void Step(DragonBossController boss, VoxelRigInstance library, HashSet<DragonBossState> visited)
        {
            Physics.SyncTransforms(); boss.Tick(.1f); library.animator.Update(.1f);
            boss.GetComponent<CombatState>().Tick(.1f); boss.GetComponent<DefenseWindow>().Tick(.1f); visited.Add(boss.State);
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
