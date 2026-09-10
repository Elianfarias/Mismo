using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Mismo.Menu.Editor
{
    public static class WebBuildBuilder
    {
        public static void Build()
        {
            string output=Environment.GetEnvironmentVariable("MISMO_WEBGL_OUTPUT");
            if(string.IsNullOrEmpty(output))output="Builds/Mismo-WebGL";
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback=true;
            PlayerSettings.WebGL.dataCaching=true;
            PlayerSettings.WebGL.initialMemorySize=256;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{MainMenuBuilder.ScenePath,"Assets/Scenes/VoxelRegion_7319.unity"},
                locationPathName=output,target=BuildTarget.WebGL,options=BuildOptions.None
            });
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Web build failed: "+report.summary.result);
            string index=Path.Combine(output,"index.html");
            string html=File.ReadAllText(index);
            const string config="var config = {";
            if(!html.Contains(config))throw new Exception("Web template configuration was not found");
            html=html.Replace(config,config+"\n        autoSyncPersistentDataPath: true,");
            // Keep the exported player visible on narrow browser windows.
            html=html.Replace("</head>","<style>#unity-container.unity-desktop{width:min(100vw,1280px)}#unity-canvas{width:100%!important;height:auto!important;aspect-ratio:8/5}#unity-footer{max-width:100%}body{margin:0;overflow-x:hidden}</style></head>");
            File.WriteAllText(index,html);
            UnityEngine.Debug.Log("WEB_BUILD_OK "+report.summary.totalSize);
        }
    }
}
