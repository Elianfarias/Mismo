using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Core;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RecipeUIIntegration
{
    [Serializable] sealed class Manifest { public Entry[] entries; }
    [Serializable] sealed class Entry { public string key,path; public int[] border; }
    [Serializable] sealed class Sources { public Icon[] icons; }
    [Serializable] sealed class Icon { public string key,path; public int codepoint; }
    public const string ThemePath="Assets/Data/UI/RecipeUITheme.asset";
    [MenuItem("Mismo/UI/Registrar radial y recetas")]
    public static void Register()
    {
        AssetDatabase.Refresh();
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Art/UI/Recipes/RecipeUIManifest.json"));
        var icons=JsonUtility.FromJson<Sources>(File.ReadAllText("Assets/Art/UI/Flaticon/Recipes/Sources.json"));
        var pieces=new List<RecipeUITheme.Piece>();
        foreach(var item in manifest.entries)
            pieces.Add(new RecipeUITheme.Piece{key=item.key,texture=Texture(item.path),border=new Vector4(item.border[0],item.border[1],item.border[2],item.border[3])});
        // Existing inventory icon references stay in InventoryUIIcons and remain editable there.
        foreach(var item in icons.icons.Where(i=>i.codepoint>0))pieces.Add(new RecipeUITheme.Piece{key=item.key,texture=Texture(item.path)});
        var theme=AssetDatabase.LoadAssetAtPath<RecipeUITheme>(ThemePath);
        if(theme==null){theme=ScriptableObject.CreateInstance<RecipeUITheme>();AssetDatabase.CreateAsset(theme,ThemePath);}
        theme.pieces=pieces.ToArray();
        theme.radialSurface=Texture("Assets/Art/UI/PixelFrames/RadialSurface.png");
        theme.radialOutline=Texture("Assets/Art/UI/PixelFrames/RadialOutline.png");
        theme.radialSelected=Enumerable.Range(0,7).Select(i=>Texture("Assets/Art/UI/PixelFrames/RadialSelected"+i+".png")).ToArray();
        EditorUtility.SetDirty(theme);
        var catalog=AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        var entries=catalog.entries.Where(e=>e.key!="UI/Recipes").ToList();
        entries.Add(new RuntimeAssetCatalog.Entry{key="UI/Recipes",assets=new UnityEngine.Object[]{theme}});
        catalog.entries=entries.ToArray();catalog.Invalidate();EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        RecipeUIAssetChecks.Run();
        if(RecipeUITheme.Load()!=theme||theme.radialSelected.Length!=7)throw new InvalidOperationException("Recipe theme registration failed.");
        Debug.Log("RECIPE_UI_INTEGRATION_OK: shared recipe view and seven radial sectors registered.");
    }
    static Texture2D Texture(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path)??throw new InvalidOperationException("Missing UI texture: "+path);
    public static void Build()
    {
        Register();
        string output="output/recipe-ui-build/Mismo.exe";Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/VoxelRegion_7319.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Recipe build failed: "+report.summary.result);
        Debug.Log("RECIPE_UI_BUILD_OK "+report.summary.totalSize);
    }
}
