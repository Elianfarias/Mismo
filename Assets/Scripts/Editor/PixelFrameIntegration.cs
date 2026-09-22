using System;
using System.IO;
using System.Linq;
using Mismo.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Register only the shared UI textures that the game uses, preserving existing assets.</summary>
[InitializeOnLoad]
public static class PixelFrameIntegration
{
    const string Request="Temp/PixelFrameRegistration.request";
    static PixelFrameIntegration(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        File.Delete(Request);
        try{Register();File.WriteAllText("Temp/PixelFrameRegistration.result","PASS");}
        catch(Exception error){File.WriteAllText("Temp/PixelFrameRegistration.result",error.ToString());Debug.LogException(error);}
    }
    [MenuItem("Mismo/UI/Registrar marcos pixelados")]
    public static void Register()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        if(catalog==null)throw new InvalidOperationException("Missing runtime asset catalog");
        var entries=catalog.entries.ToList();
        foreach(var name in new[]{"Frame","Panel","Crosshair"})
        {
            string path="Assets/Art/UI/PixelFrames/"+name+".png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture==null)throw new InvalidOperationException("Missing pixel UI texture: "+path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(importer.filterMode!=FilterMode.Point||importer.mipmapEnabled)
                throw new InvalidOperationException("Pixel UI needs Point sampling and no mipmaps: "+name);
            string key="UI/PixelFrames/"+name;
            entries.RemoveAll(e=>e.key==key);
            entries.Add(new RuntimeAssetCatalog.Entry{key=key,assets=new UnityEngine.Object[]{texture}});
        }
        catalog.entries=entries.ToArray();catalog.Invalidate();EditorUtility.SetDirty(catalog);
        RuntimeCatalogBuilder.Refresh();
        ProjectOrganizationChecks.Run();
        Debug.Log("PIXEL_FRAME_REGISTRATION_OK");
    }

    public static void Build()
    {
        Register();
        string output="output/pixel-ui-build/Mismo.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes=new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/VoxelRegion_7319.unity"},
            locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
        });
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Pixel UI build failed: "+report.summary.result);
        Debug.Log("PIXEL_FRAME_BUILD_OK "+report.summary.totalSize);
    }
}
