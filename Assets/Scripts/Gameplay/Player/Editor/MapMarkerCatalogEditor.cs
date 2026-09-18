using UnityEditor;
using UnityEngine;
using Mismo.Gameplay.Player.Presentation;

namespace Mismo.Gameplay.Player.Editor
{
    public sealed class MapMarkerCatalogWindow : EditorWindow
    {
        UnityEditor.Editor inspector;
        Vector2 scroll;
        [MenuItem("Mismo/Mapa/Editar marcadores")]
        [MenuItem("Mismo/Mapa/Ajustar vista y minimapa")]
        public static void Open() => GetWindow<MapMarkerCatalogWindow>("Mapa y minimapa");
        void OnGUI()
        {
            var catalog=Resources.Load<MapMarkerCatalog>("MapMarkerCatalog");
            if(catalog==null)
            {
                EditorGUILayout.HelpBox("Creá el catálogo para personalizar los iconos disponibles en el mapa.",MessageType.Info);
                if(GUILayout.Button("Crear catálogo"))
                {catalog=CreateInstance<MapMarkerCatalog>();AssetDatabase.CreateAsset(catalog,"Assets/Resources/MapMarkerCatalog.asset");AssetDatabase.SaveAssets();}
                return;
            }
            EditorGUILayout.HelpBox("Distancia visible: bajala para acercar el minimapa; subila para ver más terreno. Los ajustes se aplican en vivo. Guardá el catálogo para conservarlos. En Marcadores podés agregar o quitar iconos; conservá los ID usados en partidas.",MessageType.Info);
            if(inspector==null||inspector.target!=catalog){if(inspector!=null)DestroyImmediate(inspector);inspector=UnityEditor.Editor.CreateEditor(catalog);}
            scroll=EditorGUILayout.BeginScrollView(scroll);inspector.OnInspectorGUI();EditorGUILayout.EndScrollView();
            var error=catalog.ValidationError();if(error!=null)EditorGUILayout.HelpBox(error,MessageType.Error);
            using(new EditorGUI.DisabledScope(error!=null))if(GUILayout.Button("Guardar catálogo"))AssetDatabase.SaveAssets();
        }
        void OnDisable(){if(inspector!=null)DestroyImmediate(inspector);}
    }
}
