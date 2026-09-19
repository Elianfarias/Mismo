using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Mismo.Gameplay.Player.Localization
{
    public static class GameLanguage
    {
        [Serializable] public sealed class Row { public string key, es, en; }
        [Serializable] public sealed class Catalog { public Row[] entries; }
        static Dictionary<string,string> sourceKeys;
        static StringTable table, spanish;
        static string language;
        static string[] languages;
        public static event Action Changed;
        public static string[] Languages
        {
            get
            {
                if(languages==null)
                {
                    var codes=new List<string>();
                    foreach(var t in Resources.LoadAll<StringTable>("Localization"))if(!codes.Contains(t.LocaleIdentifier.Code))codes.Add(t.LocaleIdentifier.Code);
                    if(codes.Count==0)codes.Add("es");codes.Sort(StringComparer.Ordinal);languages=codes.ToArray();
                }
                return (string[])languages.Clone();
            }
        }
        public static string Code
        {
            get{if(language==null){var saved=PlayerPrefs.GetString("Mismo.Language","es");language=Array.IndexOf(Languages,saved)>=0?saved:"es";}return language;}
        }
        public static CultureInfo Culture => CultureInfo.GetCultureInfo(Code=="es"?"es-AR":Code);
        // Kept for menu/configuration integrations; the gameplay HUD does not display this selector.
        public static string LanguageLabel => Text("Idioma")+": "+Culture.NativeName;
        public static void Next(){var codes=Languages;Set(codes[(Array.IndexOf(codes,Code)+1)%codes.Length]);}
        public static void Set(string code,bool persist=true)
        {
            if(Array.IndexOf(Languages,code)<0)return;
            language=code;table=null;
            if(persist){PlayerPrefs.SetString("Mismo.Language",code);PlayerPrefs.Save();}
            Changed?.Invoke();
        }
        public static string Get(string key,string fallback)
        {
            if(table==null)table=Resources.Load<StringTable>("Localization/Game_"+Code);
            var value=table?.GetEntry(key)?.LocalizedValue;
            if(!string.IsNullOrEmpty(value))return value;
            if(spanish==null)spanish=Resources.Load<StringTable>("Localization/Game_es");
            var backup=spanish?.GetEntry(key)?.LocalizedValue;
            return string.IsNullOrEmpty(backup)?fallback??"":backup;
        }
        public static string Text(string source)
        {
            if(string.IsNullOrEmpty(source))return source??"";
            if(sourceKeys==null)
            {
                sourceKeys=new Dictionary<string,string>(StringComparer.Ordinal);
                var json=Resources.Load<TextAsset>("Localization/Translations");
                if(json!=null)foreach(var row in JsonUtility.FromJson<Catalog>(json.text).entries)sourceKeys[row.es]=row.key;
            }
            return sourceKeys.TryGetValue(source,out var key)?Get(key,source):source;
        }
        public static string Format(string source,params object[] values)=>string.Format(Culture,Text(source),values);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){sourceKeys=null;table=null;spanish=null;language=null;languages=null;Changed=null;}
    }
}
