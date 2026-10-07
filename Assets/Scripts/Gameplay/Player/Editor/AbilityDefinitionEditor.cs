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
        // Same as DrawPropertiesExcluding, but passiveValue only appears on the passives that read it, with its own label.
        void DrawFields(params string[] excluded)
        {
            var definition = (AbilityDefinition)target;
            var iterator = serializedObject.GetIterator();
            for (bool enter = true; iterator.NextVisible(enter); enter = false)
            {
                if (Array.IndexOf(excluded, iterator.name) >= 0) continue;
                if (iterator.name == "passiveValue")
                {
                    if (definition.UsesPassiveValue)
                    {
                        string label, tooltip;
                        switch (definition.passive)
                        {
                            case WeaponPassive.Verdugo: label = "Daño extra contra objetivos sangrando"; tooltip = "0,2 = +20 % al daño de los básicos."; break;
                            case WeaponPassive.DoubleEdge: label = "Probabilidad de golpe doble"; tooltip = "0,5 = 50 % por cada básico. Con Potencia no pasa de 100 %."; break;
                            case WeaponPassive.Bloodthirst: label = "Restauración al derrotar o abrir"; tooltip = "0,2 = 20 % de la estamina y del Focus máximos."; break;
                            default: label = "Focus por básico contra objetivos sangrando"; tooltip = "Focus que da cada básico que impacta a un objetivo sangrando."; break;
                        }
                        EditorGUILayout.PropertyField(iterator, new GUIContent(label, tooltip + " Potencia lo multiplica por rango."));
                    }
                    continue;
                }
                using (new EditorGUI.DisabledScope(iterator.name == "m_Script"))
                    EditorGUILayout.PropertyField(iterator, true);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var definition = (AbilityDefinition)target;
            EditorGUILayout.HelpBox("VFX: Anchor permite emitir desde Weapon (socket), Character o Above Head. Face Camera orienta el efecto hacia la cámara, sin modificarla. Preparation acompaña la carga; Execution aparece al soltar; Active dura la fase activa. Los modificadores pueden agregar sus propios efectos. Estas fases corresponden a habilidades activas, no a pasivas.", MessageType.Info);
            if (definition.usesSwordCombo)
            {
                EditorGUILayout.HelpBox("Golpes del combo: duración en segundos; ventanas de impacto y encadenado en porcentajes (0–100). Mayor duración reproduce el clip más lento. El volumen sigue al personaje durante el avance. Los clips y su desplazamiento se configuran en las animaciones de la familia. Cambiar este asset afecta a todas las armas que lo comparten.", MessageType.Info);
                DrawFields("preparation", "active", "recovery", "actions");
                if (definition.comboSteps == null || definition.comboSteps.Length == 0)
                    EditorGUILayout.HelpBox("Agregá al menos un golpe para poder ejecutar este combo.", MessageType.Warning);
                else foreach (var step in definition.comboSteps)
                    if (step != null && step.ImpactEnd <= step.ImpactStart)
                        EditorGUILayout.HelpBox("Un golpe tiene una ventana de impacto vacía: no hará daño.", MessageType.Warning);
                if (definition.comboOrder == ComboOrder.AlternateHands && (definition.comboSteps == null || definition.comboSteps.Length != 4))
                    EditorGUILayout.HelpBox("Alternar manos necesita cuatro golpes: derecha, izquierda, doble que abre la derecha y doble que abre la izquierda.", MessageType.Warning);
            }
            else if (definition.RecastCount > 0)
            {
                EditorGUILayout.HelpBox("Reactivación: cada etapa es una pulsación con sus propios tiempos (reemplazan Preparation, Active y Recovery) y reproduce el clip de combo del mismo índice en las animaciones de la familia. Focus y estamina se cobran en la primera pulsación; el cooldown empieza en la última o al vencer la ventana. Recast Strike Action necesita un golpe por etapa.", MessageType.Info);
                DrawFields("comboSteps", "comboOrder", "preparation", "active", "recovery");
                var strikes = definition.actions?.OfType<RecastStrikeAction>().FirstOrDefault();
                if (strikes == null)
                    EditorGUILayout.HelpBox("Ninguna acción usa la etapa: agregá Recast Strike Action para que cada pulsación golpee.", MessageType.Warning);
                else if (strikes.strikes == null || strikes.strikes.Length != definition.RecastCount)
                    EditorGUILayout.HelpBox("Recast Strike Action tiene " + (strikes.strikes?.Length ?? 0) + " golpes y la habilidad " + definition.RecastCount + " etapas: deben coincidir.", MessageType.Warning);
            }
            else
            {
                DrawFields("comboSteps", "comboOrder");
                if (definition.recastStages != null && definition.recastStages.Length > 0)
                    EditorGUILayout.HelpBox(definition.chargeable ? "La reactivación no se combina con la carga: la habilidad se ejecuta una sola vez." : "Una sola etapa no reactiva: hacen falta al menos dos.", MessageType.Warning);
            }
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
