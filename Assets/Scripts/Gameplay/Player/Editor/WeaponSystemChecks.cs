using System;
using System.Collections;
using System.IO;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class WeaponSystemChecks
    {
        const string Pending = "Mismo.WeaponChecks";
        static IEnumerator routine;
        static int frame = -1;
        static double deadline;
        static int checks;
        public static void RunBatch()
        {
            WeaponSystemBuilder.Apply();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Floor"; floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(70, 1, 70);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VoxelCharacterIntegration.PrefabPath));
            player.transform.position = Vector3.zero;
            player.GetComponent<PlayerController>().enabled = false;
            var light = new GameObject("Test light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientLight = new Color(.7f, .7f, .7f);
            SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            deadline = EditorApplication.timeSinceStartup + 100; EditorApplication.update += Tick;
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout"); return; }
            if (!Application.isPlaying || Time.frameCount < 5 || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try { if (routine == null) routine = Run(); if (!routine.MoveNext()) Finish(true, checks + " checks passed"); }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void Require(bool value, string message)
        { if (!value) throw new Exception(message); checks++; Debug.Log("WEAPON_CHECK " + message); }
        static void Finish(bool success, string message)
        {
            SessionState.SetBool(Pending, false); EditorApplication.update -= Tick;
            Directory.CreateDirectory("Docs/Validation"); File.WriteAllText("Docs/Validation/WeaponSystem-checks.txt", (success ? "PASS " : "FAIL ") + message);
            Debug.Log("WEAPON_CHECKS_" + (success ? "OK " : "FAILED ") + message); EditorApplication.Exit(success ? 0 : 1);
        }
        static Health Target(Vector3 position, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule); go.name = name; go.transform.position = position;
            var health = go.AddComponent<Health>(); go.AddComponent<DamageReceiver>(); Physics.SyncTransforms(); return health;
        }
        static IEnumerator Run()
        {
            var player = Object.FindFirstObjectByType<PlayerController>(); player.enabled = false;
            var loadout = player.GetComponent<EquipmentLoadout>(); loadout.Initialize(); var runner = loadout.Runner;
            var motor = player.GetComponent<PlayerMotor>(); var health = player.GetComponent<Health>();
            var sword = loadout.ActiveDefinition; var bow = loadout.SecondaryDefinition;
            Require(sword != null && bow != null && bow.isBow, "Sword and requested bow are equipped");
            Require(bow.GetAbility(AbilitySlot.Basic).actions[0] is ProjectileAction, "Bow uses shared projectile action");
            Require(runner.TryUse(AbilitySlot.Basic, Vector3.forward, Vector3.zero), "Sword basic starts");
            Require(!loadout.TrySwap(), "Swap rejected while attack active");
            runner.Tick(.4f);
            Require(!loadout.TrySwap(), "Swap rejected through combo recovery");
            runner.Tick(.3f); Require(loadout.TrySwap(), "Swap accepted after recovery");
            yield return null;
            yield return null;
            Capture(player.transform, "bow-equipped.png");
            var target = Target(new Vector3(0, 1.2f, 6), "Arrow target");
            player.GetComponent<CombatState>().Reward(20,"TEST");
            Require(runner.TryUse(AbilitySlot.Q, Vector3.forward, Vector3.zero), "Bow power starts");
            runner.Tick(.7f);
            Require(Object.FindObjectsByType<ProjectileInstance>(FindObjectsSortMode.None).Length == 1, "Exactly one arrow released across windup");
            Require(!loadout.TrySwap(), "Bow recovery blocks swap");
            foreach (var arrow in Object.FindObjectsByType<ProjectileInstance>(FindObjectsSortMode.None)) arrow.Step(.25f);
            Require(target.Current < target.Maximum, "Swept arrow hits target at speed");
            runner.Tick(.3f); float before = runner.Remaining(bow.GetAbility(AbilitySlot.Q));
            Require(loadout.TrySwap() && loadout.TrySwap(), "Can alternate twice when idle");
            Require(runner.Remaining(bow.GetAbility(AbilitySlot.Q)) >= before - .1f && !runner.TryUse(AbilitySlot.Q, Vector3.forward, Vector3.zero), "Cooldown survives inactive weapon and prevents reuse");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Arrow wall"; wall.transform.position = new Vector3(0, 1.2f, 3); wall.transform.localScale = new Vector3(3, 3, .1f); Physics.SyncTransforms();
            float hp = target.Current;
            var blocked = ProjectileInstance.Spawn(player.gameObject, Vector3.up * 1.2f, Vector3.forward, 40, 40, 25, .09f, null);
            blocked.Step(.3f); Require(Mathf.Approximately(target.Current, hp), "Thin wall stops arrow before target");
            Object.Destroy(wall); Object.Destroy(target.gameObject); yield return null;
            Vector3 point = new Vector3(5, 0, 5);
            Require(runner.TryUse(AbilitySlot.R, Vector3.forward, point), "Ground area starts");
            runner.Tick(1);
            var area = Object.FindFirstObjectByType<AreaInstance>(); Require(area != null, "Area spawns at fixed ground point");
            Require(loadout.TrySwap(), "Swap allowed after area release");
            motor.ResetPosition(new Vector3(-4, 0, 0));
            Require(Vector3.Distance(area.transform.position, point) < .001f, "Area stays in world after movement and swap");
            var low = Target(point + new Vector3(.5f, .9f, 0), "Area target");
            var high = Target(point + new Vector3(0, 3, 0), "Other floor target");
            area.Pulse(); Require(low.Current < low.Maximum && high.Current == high.Maximum, "Area damages local floor only");
            yield return null;
            yield return null;
            Capture(player.transform, "sword-equipped.png");
            Require(!loadout.TryEquip(0, bow), "Replacing equipped weapon blocked in combat");
            Require(runner.TryUse(AbilitySlot.E, Vector3.forward, Vector3.zero), "Reusable parry starts"); runner.Tick(.01f);
            Require(runner.Parry.IsWindowOpen, "Parry window opened");
            var attacker = Target(player.transform.position + Vector3.forward, "Parry attacker");
            attacker.gameObject.AddComponent<DamageDealer>();
            float playerHp = health.Current;
            Require(!player.GetComponent<DamageReceiver>().ReceiveDamage(new DamageInfo(20, attacker.gameObject, player.transform.position, Vector3.back)) && health.Current == playerHp, "Parry intercepts actual incoming damage");
            runner.Cancel(); Require(!runner.Parry.IsWindowOpen && !runner.IsBusy, "Cancellation closes defensive window");
            Object.Destroy(attacker.gameObject);
            yield return null;
            motor.ResetPosition(new Vector3(0, 0, -6));
            Require(runner.TryUse(AbilitySlot.Q, Vector3.forward, Vector3.zero), "Sword lunge uses shared movement");
            Vector3 start = motor.transform.position; runner.Tick(.08f); motor.Tick(Vector3.zero, false, false, false, .08f);
            Require(motor.transform.position.z > start.z, "Lunge moves through character motor: " + start + " -> " + motor.transform.position + " flags=" + motor.LastCollisionFlags + " controlled=" + motor.LastMovementWasControlled);
            runner.Cancel(); start = motor.transform.position; motor.Tick(Vector3.zero, false, false, false, .08f);
            Require(Mathf.Abs(motor.transform.position.z - start.z) < .01f, "Cancel leaves no pending lunge movement");
            var second = Object.Instantiate(player.gameObject, new Vector3(20, 0, 20), Quaternion.identity);
            second.GetComponent<PlayerController>().enabled = false;
            var otherLoadout = second.GetComponent<EquipmentLoadout>(); otherLoadout.Initialize();
            Require(otherLoadout.Runner.Remaining(sword.GetAbility(AbilitySlot.Q)) == 0 && runner.Remaining(sword.GetAbility(AbilitySlot.Q)) > 0, "Shared definitions do not share player cooldown state");
            Object.Destroy(second);
            Require(loadout.TrySwap(), "Bow reactivated");
            Require(runner.TryUse(AbilitySlot.Basic, Vector3.forward, Vector3.zero), "Basic arrow prepares");
            int arrowsBefore = Object.FindObjectsByType<ProjectileInstance>(FindObjectsSortMode.None).Length;
            health.ApplyDamage(new DamageInfo(1000, null, player.transform.position, Vector3.forward)); runner.Tick(1);
            Require(!runner.IsBusy && !loadout.TrySwap(), "Death cancels cast and blocks swap");
            Require(Object.FindObjectsByType<ProjectileInstance>(FindObjectsSortMode.None).Length == arrowsBefore, "Death before release creates no arrow");
        }
        static void Capture(Transform player, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Directory.CreateDirectory("Docs/Validation");
            var go = new GameObject("Weapon test camera"); var camera = go.AddComponent<UnityEngine.Camera>();
            camera.transform.position = player.position + new Vector3(3, 2.2f, 3); camera.transform.LookAt(player.position + Vector3.up);
            camera.orthographic = true; camera.orthographicSize = 1.6f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .16f, .2f);
            var rt = new RenderTexture(720, 720, 24); var old = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(720, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 720, 720), 0, 0); image.Apply();
            File.WriteAllBytes("Docs/Validation/" + name, image.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; rt.Release(); Object.Destroy(image); Object.Destroy(rt); Object.Destroy(go);
        }
    }
}
