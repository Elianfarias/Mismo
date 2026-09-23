using System;
using System.Reflection;
using Mismo.Core;
using TMPro;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>TMP has no public settings injection API. Bind our preloaded catalog instead of a Resources folder.</summary>
    public static class ProjectTextSettings
    {
        static readonly FieldInfo Instance = typeof(TMP_Settings).GetField("s_Instance",BindingFlags.Static|BindingFlags.NonPublic);
        public static void Use(TMP_Settings settings)
        {
            if(settings==null)throw new InvalidOperationException("Falta UI/TextSettings en RuntimeAssetCatalog.");
            if(Instance==null)throw new InvalidOperationException("Cambió la API de TMP Settings; revisar ProjectTextSettings.");
            Instance.SetValue(null,settings);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()=>Use(ProjectAssets.Load<TMP_Settings>("UI/TextSettings"));
    }
}
