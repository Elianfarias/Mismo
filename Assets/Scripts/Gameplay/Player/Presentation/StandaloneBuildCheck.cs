using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Opt-in verification of the shipped executable. Uses a separate disposable profile.</summary>
    public sealed class StandaloneBuildCheck : MonoBehaviour
    {
        string output;
        readonly List<string> checks = new List<string>();
        readonly List<string> errors = new List<string>();
        bool finished;
        float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-mismo-build-check");
            if (index < 0 || index + 1 >= args.Length) return;
            string directory = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(directory);
            World.WorldSession.VerificationDirectory=directory;
            PlayerInventory.BuildCheckRepository = new ProtectedProfileRepository(Path.Combine(directory, "test-profile-" + Guid.NewGuid().ToString("N") + ".mismo"));
            var go = new GameObject("Standalone build verification");
            DontDestroyOnLoad(go);
            var check = go.AddComponent<StandaloneBuildCheck>(); check.output = directory;
            check.deadline = Time.realtimeSinceStartup + 90;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.logMessageReceived += check.OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        void Update() { if (!finished && Time.realtimeSinceStartup > deadline) Finish(false, "Verification timeout"); }
        IEnumerator Start()
        {
            var routine = Run();
            while (!finished)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception error) { Finish(false, error.ToString()); yield break; }
                if (!next) { Finish(errors.Count == 0, string.Join("\n", errors)); yield break; }
                yield return routine.Current;
            }
        }
        void Check(bool success, string message)
        { if (!success) throw new InvalidOperationException(message); checks.Add(message); Debug.Log("BUILD_CHECK " + message); }

        IEnumerator Run()
        {
            yield return null;
            Check(SceneManager.GetActiveScene().name == "MainMenu", "Starts in MainMenu");
            yield return new WaitForEndOfFrame();
            CaptureCamera("01-menu.png");
            yield return new WaitForSecondsRealtime(.4f);
            var menu = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(item => item.GetType().FullName == "Mismo.Menu.MainMenuView");
            Check(menu != null, "Menu controller exists");
            menu.SendMessage("Begin");
            float until = Time.realtimeSinceStartup + 30;
            while (SceneManager.GetActiveScene().name != "VoxelRegion_7319" && Time.realtimeSinceStartup < until) yield return null;
            yield return null; yield return null;
            Check(SceneManager.GetActiveScene().name == "VoxelRegion_7319", "Menu loads the actual region");
            var player = FindFirstObjectByType<PlayerController>();
            Check(player != null, "Region player exists");
            var inventory = player.GetComponent<PlayerInventory>();
            var panel = player.GetComponent<InventoryPanel>();
            var equipment = player.GetComponent<EquipmentLoadout>();
            Check(player.GetComponent<World.RegionRespawn>() != null, "Region respawn component included");
            Check(inventory != null && inventory.IsReady && !inventory.HasSaveProblem, "Single-player inventory initializes and saves successfully");
            Check(panel != null, "Inventory panel included in the player build");
            until = Time.realtimeSinceStartup + 10;
            while (!equipment.CanChangeEquipment && Time.realtimeSinceStartup < until) yield return null;
            Debug.Log("BUILD_INVENTORY_STATE combat=" + equipment.InCombat + " busy=" + equipment.Runner.IsBusy + " scale=" + Time.timeScale);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            player.GetComponent<Input.PlayerInputReader>().SendMessage("Start");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Check(panel.IsOpen && Cursor.visible, "I opens inventory in the executable");
            yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(output, "02-inventory.txt"), "I opened inventory. Owned weapons: " + inventory.Count + ". Cursor visible: " + Cursor.visible);
            yield return new WaitForSecondsRealtime(.4f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Check(!panel.IsOpen, "I closes inventory in the executable");
            int inspected = 0;
            var invalidMaterials = new List<string>();
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null || !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader")
                        invalidMaterials.Add(renderer.name + " / " + (material != null ? material.name : "MISSING"));
                    inspected++;
                }
            }
            Check(inspected > 20, "Inspected region materials: " + inspected);
            Check(invalidMaterials.Count == 0, "No missing or unsupported materials: " + string.Join(", ", invalidMaterials));
            player.enabled = false;
            var camera = UnityEngine.Camera.main;
            yield return new WaitForEndOfFrame();
            CaptureCamera("03-region.png");
            var goblin = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(item => item.GetType().Name == "GoblinController");
            Check(goblin != null, "Goblin exists in actual region");
            var cameraController = camera.GetComponent<Camera.ThirdPersonCamera>();
            if (cameraController != null) cameraController.enabled = false;
            camera.transform.position = goblin.transform.position + new Vector3(3, 2, 3);
            camera.transform.LookAt(goblin.transform.position + Vector3.up);
            yield return null;
            goblin.GetComponent<Health>().ApplyDamage(new DamageInfo(10000, player.gameObject, goblin.transform.position, Vector3.forward));
            Check(goblin.GetComponent<Health>().IsDead && !goblin.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, "Goblin death completes gameplay callbacks");
            var cubes = GameObject.Find("Death cubes");
            Check(cubes != null && cubes.GetComponent<ParticleSystem>().particleCount > 0, "Goblin emits death cubes");
            var particles = cubes.GetComponent<ParticleSystemRenderer>();
            Check(particles.sharedMaterial != null && particles.sharedMaterial.shader.isSupported && particles.mesh != null, "Death cubes have a supported material and mesh");
            yield return new WaitForSecondsRealtime(.15f);
            yield return new WaitForEndOfFrame();
            CaptureCamera("04-death-cubes.png");
            yield return new WaitForSecondsRealtime(.5f);
            Check(errors.Count == 0, "No runtime errors during menu, inventory and death checks");
        }

        void CaptureCamera(string filename)
        {
            var camera = UnityEngine.Camera.main;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24);
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, filename), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                target.Release(); Destroy(target); Destroy(texture);
            }
        }

        void Finish(bool success, string detail)
        {
            if (finished) return;
            finished = true;
            Application.logMessageReceived -= OnLog;
            string report = (success ? "PASS" : "FAIL") + "\n" + string.Join("\n", checks) + "\n" + detail;
            File.WriteAllText(Path.Combine(output, "result.txt"), report);
            Debug.Log("BUILD_CHECK_RESULT " + (success ? "PASS" : "FAIL") + " " + detail);
            Application.Quit(success ? 0 : 1);
        }
    }
}
