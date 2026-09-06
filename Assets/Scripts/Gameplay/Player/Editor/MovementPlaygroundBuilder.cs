using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>
    /// Creates and removes a temporary scene used to test player movement.
    /// </summary>
    public static class MovementPlaygroundBuilder
    {
        private const string RootName = "Movement Playground";
        private const string BuildMenuPath = "Mismo/Prototype/Build Movement Playground";
        private const string DeleteMenuPath = "Mismo/Prototype/Delete Movement Playground";

        /// <summary>
        /// Creates the movement playground in the active scene.
        /// </summary>
        [MenuItem(BuildMenuPath)]
        public static void Build()
        {
            if (GameObject.Find(RootName) != null)
            {
                Debug.Log("Movement Playground: ya existe en la escena.");
                return;
            }

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Movement Playground");

            CreateBlock("Ground", root.transform, new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f));
            CreateBlock("Ramp", root.transform, new Vector3(-7f, 1f, 3f), new Vector3(7f, 0.5f, 4f), new Vector3(0f, 0f, -15f));

            CreateBlock("Low Platform", root.transform, new Vector3(5f, 0.5f, 3f), new Vector3(4f, 1f, 4f));
            CreateBlock("High Platform", root.transform, new Vector3(10f, 1.5f, 3f), new Vector3(4f, 3f, 4f));

            CreateSteps(root.transform, new Vector3(-3f, 0.25f, -5f), 5);

            CreateBlock("Narrow Obstacle", root.transform, new Vector3(3f, 0.75f, -5f), new Vector3(1f, 1.5f, 5f));
            CreateBlock("Tall Obstacle", root.transform, new Vector3(8f, 1.5f, -6f), new Vector3(3f, 3f, 1f));

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("Movement Playground: creado. Podés deshacerlo con Ctrl+Z o eliminarlo desde el menú Mismo/Prototype.");
        }

        /// <summary>
        /// Removes the movement playground from the active scene.
        /// </summary>
        [MenuItem(DeleteMenuPath)]
        public static void Delete()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.Log("Movement Playground: no existe en la escena.");
                return;
            }

            Undo.DestroyObjectImmediate(root);
            Debug.Log("Movement Playground: eliminado.");
        }

        /// <summary>
        /// Enables the delete command only while the movement playground exists.
        /// </summary>
        [MenuItem(DeleteMenuPath, true)]
        private static bool CanDelete()
        {
            return GameObject.Find(RootName) != null;
        }

        /// <summary>
        /// Creates a sequence of progressively taller steps.
        /// </summary>
        private static void CreateSteps(Transform parent, Vector3 start, int count)
        {
            for (int index = 0; index < count; index++)
            {
                float height = 0.5f * (index + 1);
                Vector3 position = start + new Vector3(index * 1.25f, height * 0.5f, 0f);
                Vector3 scale = new Vector3(1.25f, height, 3f);
                CreateBlock($"Step {index + 1}", parent, position, scale);
            }
        }

        /// <summary>
        /// Creates and positions a cube primitive under the supplied parent.
        /// </summary>
        private static GameObject CreateBlock(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Vector3 rotation = default)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.SetPositionAndRotation(position, Quaternion.Euler(rotation));
            block.transform.localScale = scale;
            return block;
        }
    }
}
