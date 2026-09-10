using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    public sealed class WeaponFamilyWindow : EditorWindow
    {
        [SerializeField] private WeaponDefinition weapon,source;
        private UnityEditor.Editor familyEditor,animationEditor;
        private Vector2 scroll;
        [MenuItem("Mismo/Armas/Familias y animaciones")]
        public static void Open()=>GetWindow<WeaponFamilyWindow>("Familias de armas");
        public static void Open(WeaponDefinition selected){var window=GetWindow<WeaponFamilyWindow>("Familias de armas");window.weapon=selected;}
        private void OnDisable(){if(familyEditor!=null)DestroyImmediate(familyEditor);if(animationEditor!=null)DestroyImmediate(animationEditor);}
        private void Assign(WeaponFamilyDefinition family)
        {Undo.RecordObject(weapon,"Asignar familia");weapon.family=family;EditorUtility.SetDirty(weapon);}
        private void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("La familia comparte habilidades y animaciones. El taller de poses mantiene el agarre y el modelo de cada arma. Los cambios en una familia afectan a todas sus armas.",MessageType.Info);
            weapon=(WeaponDefinition)EditorGUILayout.ObjectField("Arma",weapon,typeof(WeaponDefinition),false);
            if(weapon==null){EditorGUILayout.EndScrollView();return;}
            EditorGUI.BeginChangeCheck();
            var family=(WeaponFamilyDefinition)EditorGUILayout.ObjectField("Familia",weapon.family,typeof(WeaponFamilyDefinition),false);
            if(EditorGUI.EndChangeCheck())Assign(family);
            source=(WeaponDefinition)EditorGUILayout.ObjectField("Tomar familia de",source,typeof(WeaponDefinition),false);
            using(new EditorGUI.DisabledScope(source==null || source.family==null))
                if(GUILayout.Button("Compartir familia de esa arma"))Assign(source.family);
            if(weapon.family==null && GUILayout.Button("Crear familia desde esta arma"))
            {
                string path=EditorUtility.SaveFilePanelInProject("Nueva familia",weapon.name+"Family","asset","Guardar familia");
                if(!string.IsNullOrEmpty(path))
                {var created=CreateInstance<WeaponFamilyDefinition>();created.abilities=weapon.abilities!=null?(AbilityDefinition[])weapon.abilities.Clone():new AbilityDefinition[4];AssetDatabase.CreateAsset(created,path);Assign(created);}
            }
            family=weapon.family;
            if(family!=null)
            {
                UnityEditor.Editor.CreateCachedEditor(family,null,ref familyEditor);familyEditor.OnInspectorGUI();
                if(family.animations==null && GUILayout.Button("Crear conjunto de animaciones"))
                {
                    string path=EditorUtility.SaveFilePanelInProject("Animaciones",family.name+"Animations","asset","Guardar animaciones");
                    if(!string.IsNullOrEmpty(path))
                    {var set=CreateInstance<WeaponAnimationSet>();AssetDatabase.CreateAsset(set,path);Undo.RecordObject(family,"Asignar animaciones");family.animations=set;EditorUtility.SetDirty(family);}
                }
                if(family.animations!=null)
                {
                    EditorGUILayout.HelpBox("Actions: asociá una habilidad y sus clips. Mask Mode permite heredar la máscara de familia, usar Full Body o una Custom Mask. Se configura una vez por acción, sin editar los clips.",MessageType.Info);
                    UnityEditor.Editor.CreateCachedEditor(family.animations,null,ref animationEditor);animationEditor.OnInspectorGUI();
                    if(family.animations.actions!=null && System.Array.Exists(family.animations.actions,a=>a!=null && a.maskMode==ActionMaskMode.Custom && a.customMask==null))
                        EditorGUILayout.HelpBox("Hay una acción en Custom sin máscara asignada. Mientras esté vacía heredará la máscara de la familia.",MessageType.Warning);
                }
                if(GUILayout.Button("Duplicar familia y animaciones para esta arma"))
                {
                    string path=EditorUtility.SaveFilePanelInProject("Duplicar familia",family.name+"Variant","asset","Guardar variante independiente");
                    if(!string.IsNullOrEmpty(path))
                    {
                        var copy=Instantiate(family);
                        if(family.animations!=null)
                        {copy.animations=Instantiate(family.animations);AssetDatabase.CreateAsset(copy.animations,AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.ChangeExtension(path,null)+"Animations.asset"));}
                        AssetDatabase.CreateAsset(copy,path);Assign(copy);
                    }
                }
                if(GUILayout.Button("Guardar")){AssetDatabase.SaveAssetIfDirty(weapon);AssetDatabase.SaveAssetIfDirty(family);if(family.animations!=null)AssetDatabase.SaveAssetIfDirty(family.animations);}
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
