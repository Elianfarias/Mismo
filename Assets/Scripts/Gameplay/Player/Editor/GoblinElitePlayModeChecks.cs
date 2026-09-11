using System;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Comprueba la diferenciación visual del tier Elite en la escena de playtest.</summary>
    public static class GoblinElitePlayModeChecks
    {
        private const string Pending = "Mismo.GoblinEliteChecks.Pending";
        private static double deadline;
        private static GoblinController elite;
        private static GoblinEliteVisual visual;
        private static GoblinVisualStyle style;
        private static Health health;
        private static DamageReceiver receiver;
        private static bool damaged;

        public static void RunBatch()
        {
            if (!Application.isBatchMode) return;
            GoblinPrototypeTool.Build();
            EditorSceneManager.OpenScene(GoblinPrototypeTool.EliteScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!Application.isBatchMode || !SessionState.GetBool(Pending, false)) return;
            deadline = EditorApplication.timeSinceStartup + 45d;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || !Application.isPlaying || Time.frameCount < 3) return;
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout de validación Elite"); return; }
            if (elite == null)
            {
                elite = Object.FindAnyObjectByType<GoblinController>();
                visual = elite != null ? elite.GetComponent<GoblinEliteVisual>() : null;
                style = elite != null ? elite.GetComponent<GoblinVisualStyle>() : null;
                health = elite != null ? elite.GetComponent<Health>() : null;
                receiver = elite != null ? elite.GetComponent<DamageReceiver>() : null;
                Require(elite != null && visual != null && style != null && receiver != null, "Prefab Elite con modifier y estilo");
                Require(Mathf.Abs(visual.ScaleMultiplier - 1.28f) < 0.001f, "Escala Elite configurada");
                Require(style.LabelPrefix == "ELITE" && style.TintStrength > 0.7f, "Etiqueta y tinte Elite configurados");
                Require(style.Tint.r > 0.55f && style.Tint.b > style.Tint.g, "Tinte púrpura Elite");
                Require(elite.transform.Find("Elite Aura") != null && elite.transform.Find("Elite Feedback") != null, "Aura y feedback Elite creados");
            }
            if (!damaged)
            {
                receiver.ReceiveDamage(new DamageInfo(1f, null, elite.transform.position, Vector3.up));
                damaged = true;
                Require(visual.IsHitFlashing, "Feedback de impacto Elite");
            }
            TextMesh label = elite.transform.Find("Status/State Label")?.GetComponent<TextMesh>();
            Require(label != null && label.text.Contains("ELITE"), "Label visible de Elite");
            Finish(true, "escala, color, aura, label y feedback de impacto");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Pending, false);
            Debug.Log((success ? "GOBLIN ELITE CHECKS PASSED: " : "GOBLIN ELITE CHECKS FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
