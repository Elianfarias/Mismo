using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    [CreateAssetMenu(menuName="Mismo/World/Voxel Region Settings")]
    public sealed class VoxelRegionSettings : ScriptableObject
    {
        public int seed = 7319;
        [Range(256,384)] public int size = 256;
        [Range(4f,24f)] public float relief = 14f;
        [Range(.125f,.25f)] public float stepHeight = .25f;
        [Range(0f,1f)] public float vegetationDensity = .65f;
        public const int GeneratorVersion = 1;
    }

    public sealed class VoxelRegionWindow : EditorWindow
    {
        private VoxelRegionSettings settings;
        [MenuItem("Mismo/World/Voxel Region Generator")]
        private static void Open() => GetWindow<VoxelRegionWindow>("Región voxel");
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Paisaje procedural · Scope 0",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Genera una escena nueva. La semilla cambia el paisaje; pueblo, rutas y encuentros mantienen un diseño controlado. No sobrescribe escenas anteriores. El tamaño se ajusta a múltiplos de 32 m.",MessageType.Info);
            settings=(VoxelRegionSettings)EditorGUILayout.ObjectField("Configuración",settings,typeof(VoxelRegionSettings),false);
            if(settings==null)
            {
                if(GUILayout.Button("Crear o cargar configuración")) settings=VoxelRegionGenerator.DefaultSettings();
                return;
            }
            var serialized=new SerializedObject(settings);
            serialized.Update();
            string[] fields={"seed","size","relief","stepHeight","vegetationDensity"};
            string[] labels={"Semilla","Tamaño en metros","Relieve","Altura del escalón","Densidad de vegetación"};
            for(int i=0;i<fields.Length;i++)EditorGUILayout.PropertyField(serialized.FindProperty(fields[i]),new GUIContent(labels[i]));
            serialized.ApplyModifiedProperties();
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if(GUILayout.Button("Generar nueva región")) VoxelRegionGenerator.Generate(settings);
        }
    }
}
