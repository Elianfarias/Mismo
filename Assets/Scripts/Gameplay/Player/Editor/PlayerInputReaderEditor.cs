using Mismo.Gameplay.Player.Input;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>
    /// Displays live input values for a PlayerInputReader during Play Mode.
    /// </summary>
    [CustomEditor(typeof(PlayerInputReader))]
    public sealed class PlayerInputReaderEditor : UnityEditor.Editor
    {
        /// <summary>
        /// Draws the live values exposed by the selected input reader.
        /// </summary>
        public override void OnInspectorGUI()
        {
            PlayerInputReader inputReader = (PlayerInputReader)target;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Entrá en Play Mode para observar los valores del input.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Move", inputReader.Move.ToString("F2"));
            EditorGUILayout.LabelField("Look", inputReader.Look.ToString("F2"));
            EditorGUILayout.Toggle("Sprint Held", inputReader.SprintHeld);
            EditorGUILayout.Toggle("Jump Held", inputReader.JumpHeld);
            EditorGUILayout.Toggle("Dash Held", inputReader.DashHeld);

            Repaint();
        }
    }
}
