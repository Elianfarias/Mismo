using System.IO;
using Mismo.Gameplay.Player.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Mismo.Gameplay.Player.Editor
{
    public static class TranslationTables
    {
        [MenuItem("Mismo/Languages/Import Spanish and English tables")]
        public static void Import()
        {
            const string folder="Assets/Resources/Localization";
            var data=JsonUtility.FromJson<GameLanguage.Catalog>(File.ReadAllText(folder+"/Translations.json"));
            var seen=new System.Collections.Generic.HashSet<string>();
            foreach(var row in data.entries)if(string.IsNullOrWhiteSpace(row.key)||!seen.Add(row.key)||string.IsNullOrWhiteSpace(row.es)||string.IsNullOrWhiteSpace(row.en))throw new System.InvalidOperationException("Invalid translation: "+row.key);
            Directory.CreateDirectory("Assets/Localization");AssetDatabase.Refresh();
            if(LocalizationEditorSettings.ActiveLocalizationSettings==null)
            {
                var settings=ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings,"Assets/Localization/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings=settings;
            }
            foreach(string code in new[]{"es","en"})if(LocalizationEditorSettings.GetLocale(code)==null)
            {
                var locale=Locale.CreateLocale(code);AssetDatabase.CreateAsset(locale,"Assets/Localization/"+code+".asset");LocalizationEditorSettings.AddLocale(locale);
            }
            var collection=LocalizationEditorSettings.GetStringTableCollection("Game")??LocalizationEditorSettings.CreateStringTableCollection("Game",folder);
            foreach(string code in new[]{"es","en"})
            {
                var table=collection.GetTable(code) as StringTable;
                if(table==null)table=collection.AddNewTable(code) as StringTable;
                foreach(var row in data.entries)table.AddEntry(row.key,code=="es"?row.es:row.en);
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);AssetDatabase.SaveAssets();
            Debug.Log("TRANSLATIONS_IMPORTED "+data.entries.Length+" entries in Spanish and English");
        }
    }
}
