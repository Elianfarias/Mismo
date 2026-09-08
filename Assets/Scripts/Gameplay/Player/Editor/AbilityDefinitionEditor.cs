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
            DrawDefaultInspector();
            if (GUILayout.Button("Agregar comportamiento"))
            {
                var menu = new GenericMenu();
                foreach (var type in TypeCache.GetTypesDerivedFrom<AbilityAction>().Where(t => !t.IsAbstract))
                {
                    Type selected = type;
                    menu.AddItem(new GUIContent(type.Name), false, () =>
                    {
                        var definition = (AbilityDefinition)target;
                        Undo.RecordObject(definition, "Agregar comportamiento");
                        definition.actions = (definition.actions ?? Array.Empty<AbilityAction>()).Concat(new[] { (AbilityAction)Activator.CreateInstance(selected) }).ToArray();
                        EditorUtility.SetDirty(definition);
                    });
                }
                menu.ShowAsContext();
            }
        }
    }
}
