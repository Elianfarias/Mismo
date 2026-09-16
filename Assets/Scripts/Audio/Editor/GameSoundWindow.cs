using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class GameSoundWindow : EditorWindow
{
    const string Path = "Assets/Resources/Audio/GameSounds.asset";
    GameSoundCatalog catalog;
    SerializedObject serialized;
    Vector2 scroll;
    string search = "";
    static readonly string[] Labels = {
        "Abrir menú", "Cerrar menú", "Pasar cursor / seleccionar", "Pulsar botón", "Cambiar pestaña",
        "Crafteo exitoso", "Crafteo fallido", "Recibir experiencia", "Subir de nivel", "Drop de arma",
        "Recoger botín", "Equipar / cambiar arma", "Usar consumible", "Recolectar recurso",
        "Descubrir región", "Descubrir especie", "Error al guardar",
        "Paso al caminar", "Paso al correr", "Saltar", "Aterrizar"
    };

    [MenuItem("Mismo/Audio/Configurar sonidos")]
    public static void Open() => GetWindow<GameSoundWindow>("Sonidos de Mismo");
    void OnEnable()
    {
        catalog = AssetDatabase.LoadAssetAtPath<GameSoundCatalog>(Path);
        if (catalog == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/Audio");
            AssetDatabase.Refresh();
            catalog = CreateInstance<GameSoundCatalog>();
            catalog.explorationMusic = Resources.Load<AudioClip>("Audio/Music/Exploration");
            catalog.combatMusic = Resources.Load<AudioClip>("Audio/Music/Combat");
            catalog.EnsureEvents();
            AssetDatabase.CreateAsset(catalog, Path);
        }
        catalog.EnsureEvents();
        EditorUtility.SetDirty(catalog);
        serialized = new SerializedObject(catalog);
    }
    void OnGUI()
    {
        if (catalog == null) { if (GUILayout.Button("Cargar catálogo")) OnEnable(); return; }
        EditorGUILayout.LabelField("Sonidos del juego", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Arrastrá un AudioClip a cada evento. Sin clip = silencio. Movimiento usa el volumen SFX; interfaz y recompensas usan Interfaz. Para pasos, usá un clip de un solo paso, sin bucle.", MessageType.Info);
        EditorGUILayout.HelpBox("Probar reproduce el clip original en el editor. En Play, Disparar evento prueba también volumen, mezclador e intervalo.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField("Buscar", search);
            if (GUILayout.Button("Ver asset", GUILayout.Width(85))) Selection.activeObject = catalog;
            if (GUILayout.Button("Detener", GUILayout.Width(75))) StopPreview();
        }
        serialized.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Música", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Elegí las pistas para exploración, combate y menú principal. Sin pista = silencio. Usan el volumen Música del juego y se repiten en bucle. Exploración y combate cambian con un fundido de 1,5 segundos.", MessageType.Info);
        MusicField("explorationMusic", "Exploración");
        MusicField("combatMusic", "Combate con jefe");
        MusicField("menuMusic", "Menú principal");
        EditorGUILayout.LabelField("Biomas y pueblos", EditorStyles.boldLabel);
        MusicField("meadowMusic", "Pradera");
        MusicField("forestMusic", "Bosque");
        MusicField("highlandsMusic", "Tierras altas");
        MusicField("villageMusic", "Cerca del pueblo");
        EditorGUILayout.PropertyField(serialized.FindProperty("villageMusicMargin"), new GUIContent("Distancia desde el borde (m)"));
        EditorGUILayout.PropertyField(serialized.FindProperty("musicZoneDelay"), new GUIContent("Confirmar cambio de zona (s)"));
        EditorGUILayout.HelpBox("Prioridad: pelea con jefe > pueblo > bioma > exploración. Los enemigos comunes mantienen la música de zona. El jefe debe estar peleando con el jugador; al morir o abandonar la pelea vuelve la zona. Un bioma o pueblo sin pista usa exploración. La demora evita alternar canciones en los límites.", MessageType.None);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Movimiento", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("walkStepDistance"), new GUIContent("Distancia entre pasos caminando (m)"));
        EditorGUILayout.PropertyField(serialized.FindProperty("runStepDistance"), new GUIContent("Distancia entre pasos corriendo (m)"));
        EditorGUILayout.HelpBox("El dash se configura en el asset del especial equipado: Activation Sfx y Activation Sfx Volume. Las habilidades de arma conservan sus sonidos de preparación y ejecución.", MessageType.Info);
        EditorGUILayout.LabelField("Eventos de sonido", EditorStyles.boldLabel);
        var entries = serialized.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            int id = entry.FindPropertyRelative("sound").intValue;
            string label = id >= 0 && id < Labels.Length ? Labels[id] : ((GameSound)id).ToString();
            if (search.Length > 0 && label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && ((GameSound)id).ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                var clip = entry.FindPropertyRelative("clip");
                EditorGUILayout.PropertyField(clip, new GUIContent("Sonido"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("volume"), new GUIContent("Volumen"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("cooldown"), new GUIContent("Intervalo mínimo (s)"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(clip.objectReferenceValue == null))
                        if (GUILayout.Button("Probar clip")) Preview((AudioClip)clip.objectReferenceValue);
                    using (new EditorGUI.DisabledScope(!Application.isPlaying || clip.objectReferenceValue == null))
                        if (GUILayout.Button("Disparar evento")) { serialized.ApplyModifiedProperties(); GameAudio.Play((GameSound)id); }
                }
            }
        }
        EditorGUILayout.EndScrollView();
        if (serialized.ApplyModifiedProperties()) EditorUtility.SetDirty(catalog);
        if (GUILayout.Button("Guardar configuración")) AssetDatabase.SaveAssetIfDirty(catalog);
    }

    void MusicField(string property, string label)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var clip = serialized.FindProperty(property);
            EditorGUILayout.PropertyField(clip, new GUIContent(label));
            using (new EditorGUI.DisabledScope(clip.objectReferenceValue == null))
                if (GUILayout.Button("Probar", GUILayout.Width(70))) Preview((AudioClip)clip.objectReferenceValue);
        }
    }

    static Type AudioUtil => typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
    static void StopPreview() => AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(null, null);
    static void Preview(AudioClip clip)
    {
        StopPreview();
        var method = AudioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        if (method == null) { Debug.LogWarning("La vista previa de audio no está disponible en esta versión. Probá el evento en Play."); return; }
        method.Invoke(null, new object[] { clip, 0, false });
    }
    void OnDisable() { StopPreview(); if (catalog != null) AssetDatabase.SaveAssetIfDirty(catalog); }
}
