using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterPhaseOneBuild
    {
        [MenuItem("Mismo/Bosses/Soul Eater/Compilar arena Windows")]
        public static void Run()
        {
            const string output="output/soul-eater-phase-one";
            Directory.CreateDirectory(output);var organization=new List<string>();
            void Collect(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||message.Contains("ORGANIZATION"))organization.Add(message);}
            Application.logMessageReceived+=Collect;
            try
            {
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("ProjectOrganizationChecks")).First(t=>t!=null);
                try{type.GetMethod("Run").Invoke(null,null);}catch(Exception e){organization.Add(e.InnerException?.Message??e.Message);}
            }
            finally{Application.logMessageReceived-=Collect;File.WriteAllLines(output+"/organization.txt",organization);}
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SoulEaterPhaseOneSetup.ScenePath},locationPathName=".validation/SoulEaterPhaseOneBuild/SoulEaterArena.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            string result=$"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nSize: {report.summary.totalSize}\nScene: {SoulEaterPhaseOneSetup.ScenePath}\nBuild: .validation/SoulEaterPhaseOneBuild/SoulEaterArena.exe\n";
            foreach(var step in report.steps)foreach(var message in step.messages)if(message.type==LogType.Error||message.type==LogType.Exception)result+="Diagnostic: "+message.content+"\n";
            File.WriteAllText(output+"/build.txt",result);Debug.Log("SOUL_PHASE_ONE_BUILD: "+result);
            if(Application.isBatchMode)EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
        }
    }
}
