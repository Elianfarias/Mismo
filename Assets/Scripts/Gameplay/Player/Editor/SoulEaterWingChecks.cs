using System;
using System.IO;
using System.Linq;
using System.Text;
using Mismo.Gameplay.Player.Voxels;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterWingChecks
    {
        public static void RunAndWorld(){Run();SoulEaterMouthChecks.Run();SoulEaterPhaseOneChecks.RunAndWorld();}
        public static void Run()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SoulEaterGreenVariant.PrefabPath));var baked=new Mesh();
            try
            {
                var rig=root.GetComponent<VoxelRigInstance>();rig.animator.enabled=false;var skin=rig.surface;var bones=skin.bones;var mesh=skin.sharedMesh;var weights=mesh.boneWeights;var triangles=mesh.triangles;
                bool Wing(int v)=>bones[weights[v].boneIndex0].name.StartsWith("Wing")||weights[v].weight1>0&&bones[weights[v].boneIndex1].name.StartsWith("Wing");
                var wingTriangles=Enumerable.Range(0,triangles.Length/3).Where(t=>Wing(triangles[t*3])||Wing(triangles[t*3+1])||Wing(triangles[t*3+2])).ToArray();
                if(wingTriangles.Length<1000)throw new Exception("Missing wing geometry");
                var report=new StringBuilder();
                foreach(var clip in rig.clips)
                {
                    float largest=0;
                    for(int frame=0;frame<=20;frame++)
                    {
                        clip.SampleAnimation(rig.animator.gameObject,clip.length*frame/20f);skin.BakeMesh(baked,true);var points=baked.vertices;
                        foreach(int t in wingTriangles)for(int edge=0;edge<3;edge++)
                        {
                            var a=points[triangles[t*3+edge]];var b=points[triangles[t*3+(edge+1)%3]];
                            float length=Vector3.Distance(a,b);if(float.IsNaN(length)||float.IsInfinity(length))throw new Exception("Invalid wing in "+clip.name);
                            largest=Mathf.Max(largest,length);
                        }
                    }
                    // Original Run reached 0.767 m, Defend 2.408 m; voxel width is about 0.08 m.
                    float limit=clip.name=="Run"?.4f:.75f;
                    if(largest>limit)throw new Exception("Overstretched wing: "+clip.name+" / "+largest+" m");
                    report.AppendLine("PASS "+clip.name+": 21 poses, both wings finite; longest triangle edge="+largest.ToString("F4")+" m");
                }
                Directory.CreateDirectory("output/soul-wing-fix");File.WriteAllText("output/soul-wing-fix/geometry.txt",report.ToString());Debug.Log("SOUL_WING_CHECKS_OK");
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(root);}
        }
        public static void CheckRetreat(SoulEaterPhaseOneController boss,Action<bool,string> check)
        {
            var rig=boss.GetComponentInChildren<VoxelRigInstance>();var mesh=rig.surface.sharedMesh;var weights=mesh.boneWeights;var bones=rig.surface.bones;
            var feet=Enumerable.Range(0,weights.Length).Where(i=>{var n=bones[weights[i].boneIndex0].name;return n.StartsWith("Hand")||n.StartsWith("Feet")||n.StartsWith("Toe");}).ToArray();
            var baked=new Mesh();float maxFloating=0;int samples=0;
            try
            {
                while(boss.State==SoulEaterState.RetreatJump)
                {
                    Physics.SyncTransforms();boss.Tick(1f/60);
                    if(boss.State!=SoulEaterState.RetreatJump||boss.Progress<.6f)continue;
                    rig.surface.BakeMesh(baked,false);var v=baked.vertices;float bottom=float.PositiveInfinity;
                    foreach(int i in feet)bottom=Mathf.Min(bottom,(rig.surface.transform.position+rig.surface.transform.rotation*v[i]).y);
                    maxFloating=Mathf.Max(maxFloating,bottom-boss.transform.position.y);samples++;
                }
                check(samples>8&&maxFloating<.9f,"Salto de retirada: las patas siguen el arco sin la altura extra del vuelo vertical; offset máximo="+maxFloating+" m");
            }
            finally{Object.DestroyImmediate(baked);}
        }
    }
}
