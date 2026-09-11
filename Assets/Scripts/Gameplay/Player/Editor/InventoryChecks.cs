using System;
using System.Collections;
using System.IO;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class InventoryChecks
    {
        const string Pending = "Mismo.InventoryChecks";
        static IEnumerator routine;
        static double deadline;
        static int lastFrame = -1, count;
        static string testDirectory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void IsolateProfile()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            foreach (var respawn in Object.FindObjectsByType<Mismo.Gameplay.Player.World.RegionRespawn>())
                Object.DestroyImmediate(respawn);
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/GoblinEliteArena.unity");
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var gameView = EditorWindow.GetWindow(gameViewType);
            gameView.Show(); gameView.Focus();
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update += Step;
        }
        static void Step()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout"); return; }
            if (!Application.isPlaying || Time.frameCount < 10 || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try { if (routine == null) routine = Run(); if (!routine.MoveNext()) Finish(true, count + " checks passed"); }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void Check(bool value, string description)
        { if (!value) throw new Exception(description); count++; Debug.Log("INVENTORY_CHECK " + description); }
        static void Finish(bool passed, string message)
        {
            SessionState.SetBool(Pending, false); EditorApplication.update -= Step;
            Directory.CreateDirectory("Docs/Validation");
            File.WriteAllText("Docs/Validation/Inventory-checks.txt", (passed ? "PASS " : "FAIL ") + message);
            Debug.Log("INVENTORY_" + (passed ? "PASS " : "FAIL ") + message);
            EditorApplication.Exit(passed ? 0 : 1);
        }
        sealed class FailingRepository : IProfileRepository
        {
            public bool Fail;
            readonly IProfileRepository inner;
            public FailingRepository(IProfileRepository source) { inner = source; }
            public ProfileReadResult Read(Func<string, bool> validate, out string payload) => inner.Read(validate, out payload);
            public void Write(string payload) { if (Fail) throw new IOException("Simulated disk failure"); inner.Write(payload); }
        }
        static IEnumerator Run()
        {
            Application.targetFrameRate = 60; Application.runInBackground = true;
            foreach (var enemy in Object.FindObjectsByType<GoblinController>()) enemy.enabled = false;
            foreach (var boss in Object.FindObjectsByType<BossController>()) boss.enabled = false;
            var player = Object.FindAnyObjectByType<PlayerController>(); player.enabled = false;
            var loadout = player.GetComponent<EquipmentLoadout>(); loadout.Runner.Cancel();
            loadout.Belt?.Cancel();
            var health = player.GetComponent<Health>(); health.Revive();
            float initialCombat = Time.time + 7;
            while (loadout.InCombat && Time.time < initialCombat) yield return null;
            Check(loadout.CanChangeEquipment, "Fixture has left initial arena combat");
            Check(player.GetComponent<PlayerInventory>() == null, "Arena does not load the user's single-player save");
            var catalog = Resources.Load<ItemCatalog>("ItemCatalog");
            Check(catalog != null && catalog.bossReward != null && catalog.weapons.Length == 3, "Catalog and reward included in Resources");
            Check(!string.IsNullOrEmpty(catalog.bossReward.inventoryDescription), "Reward description is imported");
            testDirectory = Path.Combine(Application.temporaryCachePath, "InventoryChecks", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(testDirectory, "profile.mismo");
            var storage = new FailingRepository(new ProtectedProfileRepository(path));
            var inventory = player.gameObject.AddComponent<PlayerInventory>(); inventory.Initialize(catalog, storage);
            Check(inventory.IsReady && inventory.Count == 2 && File.Exists(path), "Fresh profile contains and saves two starting weapons");
            Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("sword.basic"), "Saved payload does not expose weapon identifiers as plaintext");
            Mismo.Gameplay.Player.World.WeaponRewardPickup.Spawn(inventory, player.transform.position, ItemCatalog.BossRewardId);
            for (int i = 0; i < 12; i++) yield return null;
            Check(inventory.Count == 3, "Physical reward pickup enters inventory and saves it");
            Check(!inventory.TryClaimReward(ItemCatalog.BossRewardId) && inventory.Count == 3, "Repeated reward cannot duplicate an item");
            Check(!inventory.TryClaimReward("unknown.reward"), "Unknown reward rejected");
            string trophy = inventory.ItemId(2), original = inventory.EquippedId(0);
            Check(!inventory.TryEquip(0, Guid.NewGuid().ToString("N")), "Unowned item cannot be equipped");
            Check(!inventory.TryEquip(2, trophy), "Invalid equipment slot rejected");
            Check(inventory.TryEquip(0, trophy) && loadout.GetSlot(0) == catalog.bossReward, "Owned reward equips with its definition");
            Check(inventory.TryEquip(1, trophy) && inventory.EquippedId(0) != inventory.EquippedId(1), "Moving an equipped instance exchanges slots without duplication");
            storage.Fail = true;
            string unchanged = inventory.EquippedId(0);
            Check(!inventory.TryEquip(0, original) && inventory.EquippedId(0) == unchanged && inventory.HasSaveProblem, "Failed save rolls back equipment change");
            storage.Fail = false;
            Check(inventory.TryEquip(0, original), "Equipment can retry after a write failure");
            loadout.MarkCombat();
            Check(!inventory.TryEquip(0, trophy), "Combat rejects equipment replacement");
            Check(loadout.TrySwap(), "Active weapon can still alternate during combat");
            var panel = player.gameObject.AddComponent<InventoryPanel>();
            Check(panel.TryOpen() && !loadout.CanChangeEquipment, "Inventory opens read-only during combat");
            panel.Close();
            float until = Time.time + 7;
            while (loadout.InCombat && Time.time < until) yield return null;
            Check(panel.TryOpen() && panel.BlocksGameplay && Cursor.visible && Cursor.lockState == CursorLockMode.None, "Inventory opens and releases cursor outside combat");
            var input = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            var keyboard = UnityEngine.InputSystem.Keyboard.current ?? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            input.actions.FindActionMap("Player").Enable();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.Q, UnityEngine.InputSystem.Key.Tab));
            UnityEngine.InputSystem.InputSystem.Update();
            int activeBeforeInput = loadout.ActiveSlot;
            Vector3 beforeInput = player.transform.position;
            player.SendMessage("Update");
            Check(!loadout.Runner.IsBusy && loadout.ActiveSlot == activeBeforeInput && Vector3.ProjectOnPlane(player.transform.position - beforeInput, Vector3.up).magnitude < .01f,
                "Inventory consumes movement, attack and swap input");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            UnityEngine.InputSystem.InputSystem.Update();
            Directory.CreateDirectory("Docs/Validation");
            ScreenCapture.CaptureScreenshot("Docs/Validation/Inventory-screen.png");
            for (int i = 0; i < 8; i++) yield return null;
            Check(File.Exists("Docs/Validation/Inventory-screen.png"), "Inventory screen rendered");
            panel.Close();
            Check(!panel.IsOpen && panel.BlocksGameplay, "Closing consumes the input frame");
            yield return null;
            Check(!panel.BlocksGameplay, "Gameplay input resumes next frame");
            // Regression: a basic arrow must remain usable with zero Focus after closing inventory.
            Check(inventory.TryEquip(0, inventory.ItemId(1)), "Bow can be equipped from inventory");
            if (!loadout.ActiveDefinition.isBow) Check(loadout.TrySwap(), "Bow becomes active");
            var combat = player.GetComponent<CombatState>(); combat.ResetCombat();
            var basic = loadout.ActiveDefinition.GetAbility(AbilitySlot.Basic);
            Check(basic.focusCost == 0 && loadout.ActiveDefinition.GetAbility(AbilitySlot.Q).focusCost == 20,
                "Only the powerful shot costs Focus; basic arrows are free");
            Check(!loadout.Runner.TryUse(AbilitySlot.Q, Vector3.forward, Vector3.zero), "Powerful shot still requires earned Focus");
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            input.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            player.GetComponent<Mismo.Gameplay.Player.Input.PlayerInputReader>().SendMessage("Start");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            UnityEngine.InputSystem.InputSystem.Update();
            player.SendMessage("Update");
            Check(loadout.Runner.Current != null && loadout.Runner.Current.Definition == basic && combat.Focus == 0,
                "Actual left click starts a basic arrow with zero Focus after closing inventory");
            int arrows = Object.FindObjectsByType<ProjectileInstance>().Length;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState());
            UnityEngine.InputSystem.InputSystem.Update(); player.SendMessage("Update");
            loadout.Runner.Tick(.41f);
            Check(Object.FindObjectsByType<ProjectileInstance>().Length == arrows + 1 && combat.Focus == 0,
                "Tap releases one projectile after minimum preparation without spending Focus");
            loadout.Runner.Tick(.4f);
            float nextShot = Time.time + basic.cooldown + .1f;
            while (Time.time < nextShot) yield return null;
            combat.ResetCombat();
            arrows = Object.FindObjectsByType<ProjectileInstance>().Length;
            Check(loadout.Runner.TryUse(AbilitySlot.Basic, Vector3.forward, Vector3.zero, null, true), "Held basic starts with zero Focus");
            loadout.Runner.Tick(.6f);
            Check(Object.FindObjectsByType<ProjectileInstance>().Length == arrows, "Held basic waits for release while charging");
            loadout.Runner.SetHeld(false); loadout.Runner.Tick(.01f);
            Check(Object.FindObjectsByType<ProjectileInstance>().Length == arrows + 1 && combat.Focus == 0,
                "Charged basic releases one arrow without Focus");
            loadout.Runner.Tick(.4f);
            UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            string savedFirst = inventory.EquippedId(0), savedSecond = inventory.EquippedId(1);
            int active = loadout.ActiveSlot;
            health.ApplyDamage(new DamageInfo(10000, null, player.transform.position, Vector3.zero));
            Check(inventory.Count == 3, "Death preserves owned weapons");
            Object.DestroyImmediate(panel); Object.DestroyImmediate(inventory);
            health.Revive();
            inventory = player.gameObject.AddComponent<PlayerInventory>(); inventory.Initialize(catalog, new ProtectedProfileRepository(path));
            Check(inventory.Count == 3 && inventory.EquippedId(0) == savedFirst && inventory.EquippedId(1) == savedSecond && loadout.ActiveSlot == active,
                "New inventory instance restores collection, equipment and active slot");
            Check(!inventory.TryClaimReward(ItemCatalog.BossRewardId), "Unique reward remains claimed after reload");
            var bytes = File.ReadAllBytes(path); bytes[30] ^= 1; File.WriteAllBytes(path, bytes);
            Object.DestroyImmediate(inventory);
            inventory = player.gameObject.AddComponent<PlayerInventory>(); inventory.Initialize(catalog, new ProtectedProfileRepository(path));
            Check(inventory.Count == 3 && inventory.Notice.Contains("respaldo"), "Modified primary recovers authenticated valid backup");
            Check(inventory.TrySwap(), "Recovered profile can save again");
            Check(new ProtectedProfileRepository(path).Read(_ => true, out _) == ProfileReadResult.Loaded, "Recovery writes a valid primary file");
            bytes = File.ReadAllBytes(path); bytes[0] ^= 1; File.WriteAllBytes(path, bytes); File.WriteAllBytes(path + ".bak", bytes);
            Object.DestroyImmediate(inventory);
            inventory = player.gameObject.AddComponent<PlayerInventory>(); inventory.Initialize(catalog, new ProtectedProfileRepository(path));
            Check(inventory.HasSaveProblem && !inventory.TryClaimReward(ItemCatalog.BossRewardId), "Invalid primary and backup disable saving and rewards");
            Check(Convert.ToBase64String(bytes) == Convert.ToBase64String(File.ReadAllBytes(path)), "Unrecoverable save is preserved, not overwritten");
        }
    }
}
