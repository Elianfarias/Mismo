using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Changes only the script reference, preserving the component's local ID and prefab overrides.</summary>
    public static class EnemyControllerMigration
    {
        public const string ImpPath = "Assets/Art/Prefabs/Enemies/ForestCreatures/Imp.prefab";

        public static void UseController<T>(GameObject root) where T : EnemyController
        {
            if (root.GetComponent<T>() != null) return;
            var current = root.GetComponent<EnemyController>();
            if (current == null) { root.AddComponent<T>(); return; }
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Gameplay/Enemies/" + typeof(T).Name + ".cs");
            if (script == null || script.GetClass() != typeof(T)) throw new InvalidOperationException("Missing controller script: " + typeof(T));
            var serialized = new SerializedObject(current);
            serialized.FindProperty("m_Script").objectReferenceValue = script;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Mismo/Enemigos/Migrar controladores compartidos")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode antes de migrar.");
            var report = new List<string>();
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Art/Prefabs/Enemies" })
                         .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var old = asset.GetComponent<GoblinController>();
                if (old == null || (path != ImpPath && !(old.Settings is CreatureSettings))) continue;
                var settings = old.Settings;
                var serialized = new SerializedObject(old);
                var weapon = serialized.FindProperty("weapon").objectReferenceValue;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(old, out string guid, out long id);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(weapon, out string weaponGuid, out long weaponId);
                byte[] original = File.ReadAllBytes(path);
                string backup = ".validation/enemy-controllers/prefabs/" + Path.GetFileName(path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.WriteAllBytes(backup, original);
                try
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        if (path == ImpPath) UseController<ImpController>(root);
                        else UseController<CreatureController>(root);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var migrated = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<EnemyController>();
                    var expected = path == ImpPath ? typeof(ImpController) : typeof(CreatureController);
                    if (migrated == null || migrated.GetType() != expected || migrated.Settings != settings)
                        throw new InvalidOperationException("Controller/settings mismatch: " + path);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(migrated, out string newGuid, out long newId);
                    var newWeapon = new SerializedObject(migrated).FindProperty("weapon").objectReferenceValue;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(newWeapon, out string newWeaponGuid, out long newWeaponId);
                    if (guid != newGuid || id != newId || weaponGuid != newWeaponGuid || weaponId != newWeaponId)
                        throw new InvalidOperationException("Component or weapon identity changed: " + path);
                    report.Add(path + " -> " + expected.Name + " | GUID, component ID, settings and weapon preserved");
                }
                catch
                {
                    File.WriteAllBytes(path, original);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    throw;
                }
            }
            Directory.CreateDirectory("output/enemy-controllers");
            if (report.Count > 0) File.WriteAllLines("output/enemy-controllers/migration.txt", report);
            else if (!File.Exists("output/enemy-controllers/migration.txt"))
                File.WriteAllText("output/enemy-controllers/migration.txt", "No pending controller migrations.\n");
            Debug.Log("ENEMY_CONTROLLER_MIGRATION_PASS " + report.Count);
        }
    }
}
