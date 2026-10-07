using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterArrivalPerformanceChecks
    {
        public static void ProfileOnly(){Measure(false);if(Application.isBatchMode)EditorApplication.Exit(0);}
        public static void RunAndWorld(){Measure(true);DragonArcChecks.Run();}
        public static void Measure(bool validate)
        {
            const string output="output/soul-eater-arrival";Directory.CreateDirectory(output);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SoulEaterPhaseOneSetup.PrefabPath);
            var source=prefab.GetComponentInChildren<VoxelRigInstance>();
            var first=Object.Instantiate(source.gameObject);var second=Object.Instantiate(source.gameObject);
            try
            {
                var rig=first.GetComponent<VoxelRigInstance>();var next=second.GetComponent<VoxelRigInstance>();
                var type=typeof(SoulEaterPhaseOneController).Assembly.GetType("Mismo.Gameplay.Enemies.SoulEaterFootSupport");
                type.GetMethod("ClearCache",BindingFlags.Static|BindingFlags.NonPublic)?.Invoke(null,null);
                Func<Vector3,float> flat=_=>0f;var watch=new Stopwatch();watch.Start();
                var support=Activator.CreateInstance(type,new object[]{rig,flat});watch.Stop();
                double cold=watch.Elapsed.TotalMilliseconds;
                watch.Restart();var warm=Activator.CreateInstance(type,new object[]{next,flat});watch.Stop();
                double reused=watch.Elapsed.TotalMilliseconds;
                var apply=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),support,type.GetMethod("Apply"));apply(.025f);
                watch.Restart();for(int i=0;i<120;i++)apply(.025f);watch.Stop();
                var text="Cold initialization: "+cold+" ms\n"+
                    "Second rig initialization: "+reused+" ms\n"+
                    "120 support updates: "+watch.Elapsed.TotalMilliseconds+" ms\n"+
                    "Mesh vertices: "+rig.surface.sharedMesh.vertexCount+"; bones: "+rig.surface.bones.Length+"\n";
                File.WriteAllText(output+"/performance.txt",text);UnityEngine.Debug.Log("SOUL_ARRIVAL_PROFILE\n"+text);
                if(validate)
                {
                    if(cold>250 || reused>25)throw new Exception("Arrival preparation exceeds its CPU budget");
                    var soles=type.GetField("soles",BindingFlags.Instance|BindingFlags.NonPublic);
                    if(!ReferenceEquals(soles.GetValue(support),soles.GetValue(warm)))throw new Exception("Arrival and combat do not share immutable sole data");
                    File.AppendAllText(output+"/performance.txt","PASS: shared geometry and bounded preparation.\n");
                }
            }
            finally{Object.DestroyImmediate(first);Object.DestroyImmediate(second);}
        }
    }
}
