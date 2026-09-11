using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public sealed class RotationRegressionProbe : MonoBehaviour
    {
        public Vector3 Direction;
        public int Ticks;
        void Update() { Ticks++; GetComponent<PlayerMotor>().Tick(Direction, true, false, false, Time.deltaTime); }
    }

    public static class BowPresentationChecks
    {
        const string Pending = "Mismo.BowPresentationChecks";
        static IEnumerator routine;
        static int frame = -1;
        static double deadline;
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(VoxelRegionGenerator.DefaultScene);
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
            if (!Application.isPlaying || Time.frameCount < 10 || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try { if (routine == null) routine = Run(); if (!routine.MoveNext()) Finish(true, "Completed"); }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void Finish(bool success, string result)
        {
            SessionState.SetBool(Pending, false); EditorApplication.update -= Tick;
            Debug.Log("BOW_PRESENTATION_" + (success ? "OK " : "FAILED ") + result);
            EditorApplication.Exit(success ? 0 : 1);
        }
        static string PathOf(Transform t) => t.parent != null ? PathOf(t.parent) + "/" + t.name : t.name;
        static IEnumerator Run()
        {
            var player = Object.FindAnyObjectByType<PlayerController>(); player.enabled = false;
            var loadout = player.GetComponent<EquipmentLoadout>(); var motor = player.GetComponent<PlayerMotor>();
            var animator = player.GetComponent<PlayerAnimationDriver>().Animator;
            Debug.Log("BOW_RIG animated=" + PathOf(animator.transform) + " motor=" + PathOf(motor.Visual));
            foreach (var hand in player.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Hand.L"))
                Debug.Log("BOW_HAND " + PathOf(hand) + " world=" + hand.position + " active=" + hand.gameObject.activeInHierarchy);
            var turning = player.gameObject.AddComponent<RotationRegressionProbe>();
            foreach (bool bow in new[] { false, true })
            {
                if (loadout.ActiveDefinition.isBow != bow) loadout.TrySwap();
                foreach (var direction in new[] { Vector3.right, Vector3.back, Vector3.left, Vector3.forward })
                {
                    turning.Direction = direction;
                    float turnEnd = Time.time + .8f;
                    while (Time.time < turnEnd) yield return null;
                    Require(Vector3.Dot(Vector3.ProjectOnPlane(motor.Facing, Vector3.up).normalized, direction) > .98f,
                        "Locomotion turns with lean enabled: " + (bow ? "bow " : "sword ") + direction + " actual=" + motor.Facing + " ticks=" + turning.Ticks);
                }
            }
            turning.enabled = false; Object.Destroy(turning);
            for (int i = 0; i < 5; i++) yield return null;
            var presentation = player.GetComponent<WeaponPresentation>();
            Require(presentation.ActiveHandAnchor != null && presentation.ActiveHandAnchor.IsChildOf(animator.transform), "Bow follows visible animated rig, not legacy avatar");
            foreach (var hand in player.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Hand.L"))
                Debug.Log("BOW_HAND_EQUIPPED " + PathOf(hand) + " world=" + hand.position);
            Capture(player.transform.position + Vector3.up, "bow-region-front.png", new Vector3(0, .3f, 3));
            motor.Face(Vector3.right);
            for (int i = 0; i < 4; i++) yield return null;
            Capture(player.transform.position + Vector3.up, "bow-region-turned.png", new Vector3(3, .3f, 0));
            for (int turn = 0; turn < 4; turn++)
            {
                Vector3 direction = Quaternion.Euler(0, turn * 90, 0) * Vector3.forward;
                motor.Face(direction);
                for (int i = 0; i < 10; i++)
                {
                    motor.Tick(direction, true, false, false, Time.deltaTime);
                    yield return null;
                    Require(Vector3.Distance(presentation.ActiveVisual.position, presentation.ActiveHandAnchor.position) < .15f, "Grip remains at hand while moving and turning");
                }
            }
            var visual = ((ProjectileAction)loadout.ActiveDefinition.GetAbility(AbilitySlot.Basic).actions[0]).visual;
            Vector3 ground = player.transform.position + motor.Facing * 3;
            if (Physics.Raycast(ground + Vector3.up * 5, Vector3.down, out var floor, 10)) ground = floor.point;
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule); target.transform.position = ground + Vector3.up * .9f;
            var targetHealth = target.AddComponent<Mismo.Gameplay.Combat.Health>(); target.AddComponent<Mismo.Gameplay.Combat.DamageReceiver>();
            Physics.SyncTransforms();
            var area = AreaInstance.Spawn(player.gameObject, ground, 2.5f, 3, .6f, 7, visual);
            area.Step(.01f);
            Require(targetHealth.Current == targetHealth.Maximum, "Rain waits for arrival before applying damage");
            var arrows = Object.FindObjectsByType<FallingArrowVisual>();
            Require(arrows.Length == 9, "Rain emits configured volley");
            var arrow = arrows[0]; Vector3 initial = arrow.transform.position;
            arrow.Step(.1f);
            Require(arrow.transform.position.y < initial.y && Mathf.Abs(arrow.transform.position.x - initial.x) < .001f && Mathf.Abs(arrow.transform.position.z - initial.z) < .001f, "Rain arrows travel vertically downward");
            Require(Vector3.Dot(arrow.transform.forward, Vector3.down) > .999f, "Arrow tips face down");
            foreach (var falling in arrows) falling.Step(.15f);
            Capture(ground + Vector3.up * 2, "bow-arrow-rain.png", new Vector3(5, 2, 5), 3.5f);
            area.Step(.43f);
            Require(Mathf.Approximately(targetHealth.Current, targetHealth.Maximum - 7), "One volley applies one damage pulse, not damage per visual arrow");
            loadout.TrySwap();
            for (int i = 0; i < 5; i++) yield return null;
            Require(area != null && Vector3.Distance(area.transform.position, ground) < .001f, "Rain remains at its ground target after swapping");
            float end = Time.time + 5;
            while (Time.time < end) yield return null;
            Require(Object.FindObjectsByType<FallingArrowVisual>().Length == 0 && area == null, "Rain arrows and area clean up at completion");
            if (!loadout.ActiveDefinition.isBow) loadout.TrySwap();
            Application.runInBackground = true;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var input = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            input.enabled = false; input.enabled = true;
            input.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            player.GetComponent<Mismo.Gameplay.Player.Input.PlayerInputReader>().SendMessage("Start");
            input.actions.FindActionMap("Player").Enable();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            UnityEngine.InputSystem.InputSystem.Update();
            Debug.Log("INPUT_STATE attack=" + input.actions.FindAction("Attack").WasPressedThisFrame() + " E=" + input.actions.FindAction("Parry").WasPressedThisFrame() + " reader=" + player.GetComponent<Mismo.Gameplay.Player.Input.PlayerInputReader>().enabled + " health=" + player.GetComponent<Mismo.Gameplay.Combat.Health>().Current);
            player.SendMessage("Update");
            Require(loadout.Runner.Current != null && loadout.Runner.Current.Definition == loadout.ActiveDefinition.GetAbility(AbilitySlot.Basic), "Actual left mouse input starts Basic, not E");
            Require(!loadout.Runner.Current.Definition.actions.Any(a => a is MoveCasterAction), "Basic has no retreat movement");
            loadout.Runner.Cancel();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState());
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.E));
            UnityEngine.InputSystem.InputSystem.Update();
            Debug.Log("INPUT_STATE attack=" + input.actions.FindAction("Attack").WasPressedThisFrame() + " E=" + input.actions.FindAction("Parry").WasPressedThisFrame() + " reader=" + player.GetComponent<Mismo.Gameplay.Player.Input.PlayerInputReader>().enabled + " health=" + player.GetComponent<Mismo.Gameplay.Combat.Health>().Current);
            player.SendMessage("Update");
            Require(loadout.Runner.Current != null && loadout.Runner.Current.Definition == loadout.ActiveDefinition.GetAbility(AbilitySlot.E), "Actual E input starts retreat");
            loadout.Runner.Cancel();
            UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
            targetHealth.Revive();
            targetHealth.ApplyDamage(new Mismo.Gameplay.Combat.DamageInfo(1000, player.gameObject, target.transform.position, Vector3.forward));
            Require(targetHealth.LastDamageApplied == 100, "Damage display uses actual health lost, including overkill");
            Require(DamageNumbers.ActiveCount > 0, "Successful damage creates floating numbers");
            Require(!target.GetComponent<Renderer>().enabled, "Death hides original mesh");
            Require(GameObject.Find("Death cubes") != null, "Death emits cube particles");
            yield return null;
            Capture(target.transform.position, "death-cubes.png", new Vector3(3,1,3), 2);
            targetHealth.Revive();
            Require(target.GetComponent<Renderer>().enabled, "Revive restores visible mesh");
            player.GetComponent<Mismo.Gameplay.Combat.Health>().ApplyDamage(new Mismo.Gameplay.Combat.DamageInfo(1000, target, player.transform.position, Vector3.forward));
            yield return null;
            Capture(player.transform.position + Vector3.up, "player-death-cubes.png", new Vector3(3,1,3), 2);
            Require(WeaponAim.Viewport.y > .5f, "Bow sight is above character center");
        }
        static void Require(bool value, string message) { if (!value) throw new Exception(message); Debug.Log("BOW_CHECK " + message); }
        internal static void Capture(Vector3 center, string filename, Vector3 offset, float size = 1.5f)
        {
            Directory.CreateDirectory("Docs/Validation");
            var go = new GameObject("Bow review camera"); var camera = go.AddComponent<UnityEngine.Camera>();
            camera.transform.position = center + offset; camera.transform.LookAt(center);
            camera.orthographic = true; camera.orthographicSize = size; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f, .2f, .24f);
            var rt = new RenderTexture(900, 900, 24); var old = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(900, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); image.Apply();
            File.WriteAllBytes("Docs/Validation/" + filename, image.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; rt.Release(); Object.Destroy(image); Object.Destroy(rt); Object.Destroy(go);
        }
    }
}

