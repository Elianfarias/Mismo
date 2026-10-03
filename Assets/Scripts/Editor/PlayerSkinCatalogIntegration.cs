using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Core;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit skin dependencies shared by editor registration and player builds.</summary>
public static class PlayerSkinCatalogIntegration
{
    static readonly Dictionary<string,string> Paths = new Dictionary<string,string>
    {
        {PlayerAppearance.Mage, AshenWarriorIntegration.Mage},
        {PlayerAppearance.Knight, AshenWarriorIntegration.Warrior},
        {PlayerAppearance.NinjaFrog, NinjaFrogIntegration.Skin}
    };
    public static string[] SkinPaths => PlayerAppearance.Skins.Select(id=>Paths[id]).ToArray();

    [MenuItem("Mismo/Character/Skins/Registrar en el selector")]
    public static void Register()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        UpdateEntries(catalog);
        catalog.Invalidate();EditorUtility.SetDirty(catalog);
        RuntimeCatalogBuilder.RegisterPreloaded(catalog);AssetDatabase.SaveAssets();
    }

    public static void UpdateEntries(RuntimeAssetCatalog catalog)
    {
        if(catalog==null)throw new InvalidOperationException("Falta RuntimeAssetCatalog.");
        // Resolve everything before writing, so a missing prefab leaves the catalog intact.
        var skins=PlayerAppearance.Skins.Select(id=>
        {
            if(!Paths.TryGetValue(id,out string path))throw new InvalidOperationException("Falta registrar la skin: "+id);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)throw new InvalidOperationException("Falta el prefab de skin: "+path);
            return new RuntimeAssetCatalog.Entry{key="PlayerSkins/"+id,assets=new Object[]{prefab}};
        }).ToArray();
        var keys=new HashSet<string>(skins.Select(e=>e.key));
        catalog.entries=catalog.entries.Where(e=>e==null||!keys.Contains(e.key)).Concat(skins)
            .OrderBy(e=>e?.key,StringComparer.Ordinal).ToArray();
    }
}
