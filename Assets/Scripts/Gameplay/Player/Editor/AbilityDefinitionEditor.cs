using System;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    [CustomEditor(typeof(AbilityDefinition))]
    public sealed class AbilityDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var definition = (AbilityDefinition)target;
            if (definition.usesSwordCombo)
            {
                EditorGUILayout.HelpBox("Golpes del combo: duración en segundos; ventanas de impacto y encadenado en porcentajes (0–100). Mayor duración reproduce el clip más lento. El volumen sigue al personaje durante el avance. Los clips y su desplazamiento se configuran en las animaciones de la familia. Cambiar este asset afecta a todas las armas que lo comparten.", MessageType.Info);
                DrawPropertiesExcluding(serializedObject, "preparation", "active", "recovery", "actions");
                if (definition.comboSteps == null || definition.comboSteps.Length == 0)
                    EditorGUILayout.HelpBox("Agregá al menos un golpe para poder ejecutar este combo.", MessageType.Warning);
                else foreach (var step in definition.comboSteps)
                    if (step != null && step.ImpactEnd <= step.ImpactStart)
                        EditorGUILayout.HelpBox("Un golpe tiene una ventana de impacto vacía: no hará daño.", MessageType.Warning);
            }
            else DrawPropertiesExcluding(serializedObject, "comboSteps");
            serializedObject.ApplyModifiedProperties();
            if (definition.usesSwordCombo) return;
            if (GUILayout.Button("Agregar comportamiento"))
            {
                var menu = new GenericMenu();
                foreach (var type in TypeCache.GetTypesDerivedFrom<AbilityAction>().Where(t => !t.IsAbstract))
                {
                    Type selected = type;
                    menu.AddItem(new GUIContent(type.Name), false, () =>
                    {
                        var selectedDefinition = (AbilityDefinition)target;
                        Undo.RecordObject(selectedDefinition, "Agregar comportamiento");
                        selectedDefinition.actions = (selectedDefinition.actions ?? Array.Empty<AbilityAction>()).Concat(new[] { (AbilityAction)Activator.CreateInstance(selected) }).ToArray();
                        EditorUtility.SetDirty(selectedDefinition);
                    });
                }
                menu.ShowAsContext();
            }
        }
    }
}
