using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Player.Camera;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class CameraCheatChecks
    {
        const string Key = "Mismo.CameraCheatChecks";
        static IEnumerator routine;
        static double deadline;
        static int frame = -1, count;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void RunBatch()
        {
            if (!Application.isBatchMode || !Directory.GetCurrentDirectory().Replace('\\', '/').Contains("/.validation/"))
                throw new InvalidOperationException("Run in the isolated validation project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));
            Object.DestroyImmediate(player.GetComponent<World.RegionRespawn>());
            player.transform.position = new Vector3(0, 100, 0);
            new GameObject("Camera").AddComponent<UnityEngine.Camera>().tag = "MainCamera";
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update += Step;
        }
        static void Step()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout"); return; }
            if (!Application.isPlaying || Time.frameCount < 10 || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try { if (routine == null) routine = Run(); if (!routine.MoveNext()) Finish(true, count + " checks"); }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void Finish(bool pass, string message)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Step;
            Directory.CreateDirectory("output/camera-cheats");
            File.WriteAllText("output/camera-cheats/checks.txt", (pass ? "PASS " : "FAIL ") + message);
            Debug.Log("CAMERA_CHEATS_" + (pass ? "PASS " : "FAIL ") + message);
            EditorApplication.Exit(pass ? 0 : 1);
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); count++; Debug.Log("CAMERA_CHEATS_CHECK " + message); }
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static GameObject Box(string name, Vector3 position, Vector3 scale, Transform parent = null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.SetParent(parent); box.transform.position = position; box.transform.localScale = scale;
            return box;
        }
        sealed class Memory : IProfileRepository
        {
            public string Payload; public bool Fail; public int Writes;
            public ProfileReadResult Read(Func<string, bool> validate, out string payload)
            { payload = Payload; return payload == null ? ProfileReadResult.Missing : validate(payload) ? ProfileReadResult.Loaded : ProfileReadResult.Invalid; }
            public void Write(string payload) { if (Fail) throw new IOException("Expected write failure"); Payload = payload; Writes++; }
        }
        static IEnumerator Run()
        {
            var player = Object.FindAnyObjectByType<PlayerController>(); player.enabled = false;
            var motor = player.GetComponent<PlayerMotor>();
            var cheats = player.GetComponent<PlayerCheats>();
            Check(cheats != null && !cheats.Active && !motor.IsFlying, "Cheats attach to the existing player and default off");
            var loadout = player.GetComponent<EquipmentLoadout>(); loadout.Runner.Cancel(); loadout.Belt?.Cancel();

            var target = new GameObject("Camera test target"); target.transform.position = new Vector3(1000, 1000, 1000);
            var cameraObject = new GameObject("Camera collision check");
            var view = cameraObject.AddComponent<UnityEngine.Camera>(); view.nearClipPlane = .1f;
            var camera = cameraObject.AddComponent<ThirdPersonCamera>(); camera.Configure(target.transform, null);
            camera.transform.rotation = Quaternion.identity;
            Vector3 origin = target.transform.position + Vector3.up * 1.3f;
            var weapon = Box("Own equipped collider on Default layer", origin + Vector3.back * .8f, Vector3.one * .3f, target.transform);
            Physics.SyncTransforms();
            Check(Physics.SphereCast(origin, .25f, Vector3.back, out var oldHit, 9, ~(1 << 2), QueryTriggerInteraction.Ignore) && oldHit.distance < .5f,
                "Reproduces the original camera collapse against equipped geometry");
            float Distance() => (float)Invoke(camera, "ObstacleDistance", origin, Vector3.back);
            Check(Mathf.Abs(Distance() - 9) < .01f, "Own colliders do not shorten the camera distance");
            var wall = Box("Real wall", origin + Vector3.back * 5, new Vector3(8, 8, .5f));
            Physics.SyncTransforms();
            Check(Distance() > 4 && Distance() < 4.5f, "A wall behind an ignored own collider still stops the camera");
            wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms();
            Check(Mathf.Abs(Distance() - 9) < .01f, "Triggers do not zoom the camera");
            wall.GetComponent<Collider>().isTrigger = false;
            for (int i = 0; i < 36; i++) Box("Crowded own collider", origin + Vector3.back * (1 + i * .08f), Vector3.one * .1f, target.transform);
            Physics.SyncTransforms();
            Check(Distance() > 4 && Distance() < 4.5f, "Growing the cast buffer preserves real obstacles in crowded equipment");
            Invoke(camera, "LateUpdate"); float close = Vector3.Distance(camera.transform.position, origin);
            wall.SetActive(false); Physics.SyncTransforms();
            Invoke(camera, "LateUpdate");
            Check(Vector3.Distance(camera.transform.position, origin) >= close && Vector3.Distance(camera.transform.position, origin) < 9,
                "Camera recovers outward smoothly after the wall clears");
            wall.SetActive(true); wall.transform.position = origin + Vector3.back; Physics.SyncTransforms();
            Invoke(camera, "LateUpdate");
            Check(Vector3.Distance(camera.transform.position, origin) < .5f, "New close obstacles take priority over smoothing");
            var visible = weapon.GetComponent<Renderer>();
            var alreadyHidden = target.transform.GetChild(1).GetComponent<Renderer>(); alreadyHidden.forceRenderingOff = true;
            Invoke(camera, "BeforeCamera", default(ScriptableRenderContext), view);
            Check(visible.forceRenderingOff, "A wall forcing the camera inside the avatar hides that avatar for this render");
            Invoke(camera, "AfterCamera", default(ScriptableRenderContext), view);
            Check(!visible.forceRenderingOff && alreadyHidden.forceRenderingOff, "Renderer state is restored without unhiding other hidden objects");
            Invoke(camera, "BeforeCamera", default(ScriptableRenderContext), UnityEngine.Camera.main);
            Check(!visible.forceRenderingOff, "The minimap and other cameras keep the character visible");
            Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target); Object.DestroyImmediate(wall);

            motor.ResetPosition(new Vector3(0, 100, 0));
            motor.MovementConstraint = (from, to) => from;
            motor.SetFlight(true); motor.TickFlight(Vector3.up, false, .1f);
            Check(player.transform.position.y > 101 && !motor.IsGrounded, "Flight ascends and bypasses the progression movement constraint");
            Vector3 hovering = player.transform.position;
            for (int i = 0; i < 30; i++) motor.Tick(Vector3.zero, false, false, false, .02f);
            Check(Vector3.Distance(player.transform.position, hovering) < .001f, "Inventory/menu locomotion ticks do not apply gravity during flight");
            motor.TickFlight(Vector3.forward, false, .1f); float slow = player.transform.position.z;
            motor.TickFlight(Vector3.forward, true, .1f);
            Check(player.transform.position.z - slow > slow * 2, "Sprint accelerates flight");
            motor.ResetPosition(new Vector3(0, 100, 0)); motor.SetFlight(true);
            var floor = Box("Flight terrain", new Vector3(0, 99, 0), new Vector3(20, 1, 20)); Physics.SyncTransforms();
            for (int i = 0; i < 20; i++) motor.TickFlight(Vector3.down, false, .02f);
            Check(player.transform.position.y >= 99.4f && player.GetComponent<CharacterController>().enabled, "Flight descent respects terrain collision");
            motor.ResetPosition(new Vector3(0, 105, 0)); motor.MovementConstraint = null;
            motor.SetFlight(true); motor.SetFlight(false); motor.Tick(Vector3.zero, false, false, false, .1f);
            Check(player.transform.position.y < 105 && !motor.IsFlying, "Leaving flight restores normal gravity");

            var inventory = player.gameObject.AddComponent<PlayerInventory>();
            var storage = new Memory();
            var catalog = Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog");
            inventory.Initialize(catalog, storage);
            var initial = JsonUtility.FromJson<InventoryProfile>(storage.Payload);
            int initialCount = inventory.Count;
            storage.Fail = true;
            Check(!inventory.TryGrantCheatWeapons() && inventory.Count == initialCount, "Failed saves do not partially grant weapons");
            storage.Fail = false;
            Check(cheats.SetActive(true) && motor.IsFlying && cheats.Active, "Enabling cheats starts flight and grants the arsenal");
            var all = JsonUtility.FromJson<InventoryProfile>(storage.Payload);
            Check(catalog.weapons.All(w => all.weapons.Any(owned => owned.definitionId == w.Id)), "Every catalog weapon, including the boss weapon, is saved");
            int writes = storage.Writes;
            Check(inventory.TryGrantCheatWeapons() && storage.Writes == writes, "Repeated grants are idempotent");
            Check(cheats.SetActive(false) && !motor.IsFlying && inventory.Count >= initialCount, "Disabling cheats restores walking and retains granted weapons");
            Check(all.equipped.SequenceEqual(initial.equipped) && all.claimedRewards.SequenceEqual(initial.claimedRewards), "The arsenal preserves equipped items and boss reward progress");

            var profileField = typeof(PlayerInventory).GetField("profile", Private);
            var full = initial.Copy();
            while (full.weapons.Count < 256) full.weapons.Add(new OwnedWeapon { instanceId = Guid.NewGuid().ToString("N"), definitionId = initial.weapons[0].definitionId });
            profileField.SetValue(inventory, full);
            Check(inventory.TryGrantCheatWeapons(), "A full inventory still accepts the arsenal through persistent pending loot");
            var overflow = JsonUtility.FromJson<InventoryProfile>(storage.Payload);
            Check(overflow.weapons.Count == 256 && overflow.pendingLoot.Count > 0 && catalog.weapons.All(w =>
                overflow.weapons.Any(owned => owned.definitionId == w.Id) || overflow.pendingLoot.Any(p => p.HasWeapon && p.weapon.definitionId == w.Id)),
                "Overflow preserves existing possessions and every granted definition");
            Check(overflow.pendingLoot.All(p => p.y < 100), "Weapons requested in flight fall back to loot at ground level");
            writes = storage.Writes;
            Check(inventory.TryGrantCheatWeapons() && storage.Writes == writes, "Pending weapons are not duplicated by repeat grants");
            Object.DestroyImmediate(floor);
            Type.GetType("ProjectOrganizationChecks, Assembly-CSharp-Editor", true).GetMethod("Run").Invoke(null, null);
            yield break;
        }
    }
}
