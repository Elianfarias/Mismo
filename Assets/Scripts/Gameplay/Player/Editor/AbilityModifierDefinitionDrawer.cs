using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    // Shows each mastery modifier with only the fields its evolution reads, so balancing never means hunting
    // through values that belong to another evolution.
    [CustomPropertyDrawer(typeof(AbilityModifierDefinition))]
    public sealed class AbilityModifierDefinitionDrawer : PropertyDrawer
    {
        struct Field
        {
            public readonly string Name, Label, Tooltip;
            public Field(string name, string label, string tooltip = null) { Name = name; Label = label; Tooltip = tooltip; }
        }

        static Field[] Common(bool passive) => new[]
        {
            new Field("id", "Id", "ID persistente dentro de esta habilidad. No cambiarlo: las partidas guardadas lo usan."),
            new Field("displayName", "Nombre"),
            new Field("description", "Descripción"),
            new Field("maxLevel", "Rangos máximos"),
            new Field("effectiveUsesPerLevel", "Usos efectivos por rango"),
            new Field("damagePerLevel", "Potencia por rango", passive
                ? "Se suma por rango entrenado. En una pasiva multiplica su magnitud (Verdugo, Filo cruel...); 0,25 = +25 % por rango."
                : "Se suma por rango entrenado al daño de la habilidad; 0,05 = +5 % por rango."),
            new Field("cooldownReductionPerLevel", "Recarga por rango", "Fracción de la recarga que se quita por rango; 0,05 = −5 %."),
            new Field("behavior", "Evolución", "Ninguna = modificador numérico. Las demás cambian cómo se juega la habilidad."),
            new Field("retired", "Retirado", "Conserva el id y el progreso, pero deja de ofrecerse y de aplicarse."),
        };

        static IEnumerable<Field> Specific(AbilityModifierBehavior behavior)
        {
            switch (behavior)
            {
                case AbilityModifierBehavior.ChargedCut:
                    yield return new Field("chargeSeconds", "Segundos de carga", "Cada rango la reduce 0,05 s.");
                    yield return new Field("chargedPostureMultiplier", "Multiplicador de postura con carga completa");
                    break;
                case AbilityModifierBehavior.DodgeChain:
                case AbilityModifierBehavior.ParryRiposte:
                case AbilityModifierBehavior.LungeFinisher:
                    yield return new Field("followupWindow", "Ventana para encadenar (segundos)");
                    yield return new Field("windowPerRank", "Ventana extra por rango (segundos)");
                    break;
                case AbilityModifierBehavior.FourthCut:
                    yield return new Field("hitsRequired", "Golpes seguidos para sangrar", "Básicos consecutivos sobre el mismo objetivo.");
                    yield return new Field("streakResetSeconds", "Segundos sin golpear que reinician la cuenta");
                    foreach (var field in Bleed()) yield return field;
                    break;
                case AbilityModifierBehavior.RawFlesh:
                    foreach (var field in Bleed()) yield return field;
                    break;
                case AbilityModifierBehavior.DeepRend:
                    yield return new Field("amount", "Reducción de armadura en rango 0", "Reemplaza la de la habilidad; 0,39 = 39 %.");
                    yield return new Field("amountPerRank", "Reducción extra por rango", "0,02 = +2 puntos porcentuales por rango.");
                    break;
                case AbilityModifierBehavior.RustyEdge:
                    yield return new Field("amount", "Daño extra del sangrado en rango 0", "0,5 = +50 %. Se suma al sangrado de Tajo sangrante.");
                    yield return new Field("amountPerRank", "Daño extra por rango");
                    break;
                case AbilityModifierBehavior.Escalation:
                    yield return new Field("amount", "Daño extra por golpe anterior acertado, en rango 0", "0,1 = +10 % del daño base por cada golpe previo de la cadena que acertó.");
                    yield return new Field("amountPerRank", "Daño extra por rango");
                    break;
                case AbilityModifierBehavior.Shatter:
                    yield return new Field("amount", "Segundos extra de rotura de postura en rango 0");
                    yield return new Field("amountPerRank", "Segundos extra por rango");
                    break;
                case AbilityModifierBehavior.LandingStrike:
                    yield return new Field("amount", "Daño del básico encadenado en rango 0", "1 = 100 % del básico. El básico sale gratis, sin estamina.");
                    yield return new Field("amountPerRank", "Daño extra por rango", "0,05 = +5 % por rango.");
                    break;
                case AbilityModifierBehavior.Whirlwind:
                    yield return new Field("amount", "Probabilidad de girar otra vez en rango 0", "0,15 = 15 %. Cada rango suma el extra.");
                    yield return new Field("amountPerRank", "Probabilidad extra por rango", "0,01 = +1 punto porcentual por rango.");
                    yield return new Field("maxRepeats", "Giros extra seguidos como máximo");
                    break;
                case AbilityModifierBehavior.Burst:
                    yield return new Field("hitsRequired", "Básicos seguidos para desatar la ráfaga", "Sobre el mismo objetivo.");
                    yield return new Field("streakResetSeconds", "Segundos sin golpear que reinician la cuenta");
                    yield return new Field("count", "Golpes extra de la ráfaga");
                    yield return new Field("interval", "Segundos entre golpes extra");
                    yield return new Field("amount", "Daño de cada golpe extra en rango 0", "0,4 = 40 % del básico.");
                    yield return new Field("amountPerRank", "Daño extra por rango");
                    break;
                case AbilityModifierBehavior.WanderingAxe:
                    yield return new Field("count", "Rebotes como máximo", "Enemigos distintos a los que salta el hacha después del impacto.");
                    yield return new Field("amount", "Daño que pierde cada rebote en rango 0", "0,3 = cada rebote pega un 30 % menos que el anterior.");
                    yield return new Field("amountPerRank", "Pérdida extra por rango", "Negativo = pierde menos: −0,0167 por rango lleva el 30 % al 25 % en rango 3.");
                    yield return new Field("distance", "Distancia máxima al siguiente enemigo (m)");
                    break;
                case AbilityModifierBehavior.Slaughter:
                    yield return new Field("amount", "Vida que cura cada baja en rango 0", "Fracción de la vida máxima (0,05 = 5 %).");
                    yield return new Field("amountPerRank", "Vida extra por rango");
                    break;
                case AbilityModifierBehavior.RisingFury:
                    yield return new Field("amount", "Velocidad de ataque por segundo en rango 0", "0,015 = +1,5 % por cada segundo que lleva el modo.");
                    yield return new Field("amountPerRank", "Velocidad extra por segundo y por rango");
                    break;
                case AbilityModifierBehavior.Reopen:
                    yield return new Field("amount", "Segundos que alarga el sangrado en rango 0", "Solo si el objetivo ya sangra.");
                    yield return new Field("amountPerRank", "Segundos extra por rango");
                    break;
                case AbilityModifierBehavior.Sweep:
                    yield return new Field("width", "Ancho del golpe (× el radio)", "2 = el doble de radio.");
                    yield return new Field("amount", "Daño a los costados en rango 0", "Para lo que queda fuera del área original; 0,6 = 60 %.");
                    yield return new Field("amountPerRank", "Daño extra a los costados por rango");
                    break;
                case AbilityModifierBehavior.ColdBlood:
                    yield return new Field("amount", "Focus por básico en rango 0", "Contra objetivos con la armadura reducida.");
                    yield return new Field("amountPerRank", "Focus extra por rango");
                    break;
                case AbilityModifierBehavior.Execution:
                    yield return new Field("amount", "Vida del objetivo que activa el bono, en rango 0", "Fracción de su vida máxima; 0,3 = 30 %.");
                    yield return new Field("amountPerRank", "Vida extra por rango");
                    yield return new Field("bonus", "Daño extra de los básicos", "0,4 = +40 % (reemplaza al de Verdugo cuando la vida es baja).");
                    break;
                case AbilityModifierBehavior.CrossCut:
                    yield return new Field("amount", "Daño extra del segundo corte en rango 0", "0,2 = +20 %.");
                    yield return new Field("amountPerRank", "Daño extra por rango");
                    break;
                case AbilityModifierBehavior.Insatiable:
                    yield return new Field("amount", "Vida que cura en rango 0", "Fracción de la vida máxima; 0,05 = 5 %.");
                    yield return new Field("amountPerRank", "Vida extra por rango");
                    yield return new Field("threshold", "Solo con la vida por debajo de", "0,5 = 50 % de la vida máxima.");
                    break;
            }
        }

        // Value = base + extra × rank, for the damage and for the seconds of the bleed.
        static IEnumerable<Field> Bleed()
        {
            yield return new Field("bleedDamagePerSecond", "Daño de sangrado por segundo en rango 0", "Antes de multiplicar por el nivel del arma.");
            yield return new Field("bleedDamagePerRank", "Daño extra por rango", "0,1667 = +1/18 del daño base por rango.");
            yield return new Field("bleedSeconds", "Duración del sangrado en rango 0 (segundos)");
            yield return new Field("bleedSecondsPerRank", "Duración extra por rango (segundos)", "0 = la duración no cambia con el rango.");
        }

        static AbilityModifierBehavior Behavior(SerializedProperty property)
        {
            var behavior = property.FindPropertyRelative("behavior");
            return behavior == null ? AbilityModifierBehavior.None : (AbilityModifierBehavior)behavior.enumValueIndex;
        }

        // On a passive, Potencia scales the effect's magnitude instead of damage, so its tooltip says so.
        static bool OwnerIsPassive(SerializedProperty property) =>
            property.serializedObject.targetObject is AbilityDefinition ability && ability.IsPassive;

        static List<Field> Visible(SerializedProperty property)
        {
            var fields = new List<Field>(Common(OwnerIsPassive(property)));
            // Evolution fields go right after the evolution selector.
            int at = fields.FindIndex(f => f.Name == "behavior") + 1;
            fields.InsertRange(at, Specific(Behavior(property)));
            fields.Add(new Field("weaponVfx", "VFX adicionales"));
            return fields;
        }

        // Serialized names this drawer shows for a modifier; checks use it to prove every one resolves.
        public static IEnumerable<string> FieldNames(SerializedProperty property)
        {
            foreach (var field in Visible(property)) yield return field.Name;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;
            foreach (var field in Visible(property))
            {
                var child = property.FindPropertyRelative(field.Name);
                if (child != null) height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            return height + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var name = property.FindPropertyRelative("displayName");
            string title = name != null && !string.IsNullOrEmpty(name.stringValue) ? name.stringValue : label.text;
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, title, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                foreach (var field in Visible(property))
                {
                    var child = property.FindPropertyRelative(field.Name);
                    if (child == null) continue;
                    float height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), child, new GUIContent(field.Label, field.Tooltip), true);
                    y += height + EditorGUIUtility.standardVerticalSpacing;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
