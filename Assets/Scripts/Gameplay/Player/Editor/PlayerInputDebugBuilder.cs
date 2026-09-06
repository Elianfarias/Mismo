using Mismo.Gameplay.Player.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>
    /// Creates a temporary scene object for validating the player input configuration.
    /// </summary>
    public static class PlayerInputDebugBuilder
    {
        private const string InputAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string DebugObjectName = "Player Input Debug";

        /// <summary>
        /// Creates and configures a temporary PlayerInputReader in the active scene.
        /// </summary>
        [MenuItem("Mismo/Prototype/Build Player Input Debugger")]
        public static void Build()
        {
            if (GameObject.Find(DebugObjectName) != null)
            {
                Debug.Log("Player Input Debugger: ya existe en la escena.");
                return;
            }

            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            if (inputActions == null)
            {
                Debug.LogError($"Player Input Debugger: no se encontró el asset en {InputAssetPath}.");
                return;
            }

            GameObject debugObject = new GameObject(DebugObjectName);
            Undo.RegisterCreatedObjectUndo(debugObject, "Build Player Input Debugger");

            PlayerInput playerInput = Undo.AddComponent<PlayerInput>(debugObject);
            playerInput.actions = inputActions;
            playerInput.defaultActionMap = "Player";
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            Undo.AddComponent<PlayerInputReader>(debugObject);

            Selection.activeGameObject = debugObject;
            EditorGUIUtility.PingObject(debugObject);
            Debug.Log("Player Input Debugger: creado. Entrá en Play Mode y observá Player Input Reader en el Inspector.");
        }

        /// <summary>
        /// Removes the temporary input debugger from the active scene.
        /// </summary>
        [MenuItem("Mismo/Prototype/Delete Player Input Debugger")]
        public static void Delete()
        {
            GameObject debugObject = GameObject.Find(DebugObjectName);
            if (debugObject == null)
            {
                Debug.Log("Player Input Debugger: no existe en la escena.");
                return;
            }

            Undo.DestroyObjectImmediate(debugObject);
            Debug.Log("Player Input Debugger: eliminado.");
        }

        /// <summary>
        /// Enables the delete command only while the temporary debugger exists.
        /// </summary>
        [MenuItem("Mismo/Prototype/Delete Player Input Debugger", true)]
        private static bool CanDelete()
        {
            return GameObject.Find(DebugObjectName) != null;
        }
    }
}
