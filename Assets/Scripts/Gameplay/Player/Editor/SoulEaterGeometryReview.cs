using System;
using System.IO;
using System.Linq;
using System.Text;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
 public static class SoulEaterGeometryReview
 {
  public static void Repair(){SoulEaterMouthRepair.Run();Run();}
  public static void Run()=>Review(true);
  public static void Capture()=>Review(false);
  static void Review(bool quit)
  {
   try {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   Directory.CreateDirectory("output/soul-eater-refinement");
   RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
   var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(45,-30,0);
   var report=new StringBuilder();
   foreach(var entry in new[]{("current",SoulEaterGreenVariant.PrefabPath),("base",SoulEaterGreenVariant.SourcePath)})
   {
    var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(entry.Item2));
    var rig=root.GetComponent<VoxelRigInstance>();var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
    var animator=root.GetComponentInChildren<Animator>();animator.enabled=false;
    var clips=AssetDatabase.LoadAssetAtPath<GameObject>(SoulEaterGreenVariant.PrefabPath).GetComponent<VoxelRigInstance>().clips;
    var mesh=new Mesh();var go=new GameObject("Baked");go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
    var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.22f,.24f);camera.fieldOfView=38;
    clips.First(c=>c.name=="Idle").SampleAnimation(animator.gameObject,0);
    foreach(var bone in skin.bones)if(bone.name.Contains("Jaw"))report.AppendLine(entry.Item1+" REST "+bone.name+" local="+skin.transform.InverseTransformPoint(bone.position));
    foreach(var name in new[]{"Idle","Walk","Run","Scream","Fireball Shoot"})
    foreach(var time in new[]{0f,.5f,.8f})
    {
     var clip=clips.First(c=>c.name==name);clip.SampleAnimation(animator.gameObject,clip.length*time);skin.BakeMesh(mesh,true);mesh.RecalculateBounds();
     go.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);skin.enabled=false;
     var points=mesh.vertices.Select(v=>go.transform.TransformPoint(v)).ToArray();report.AppendLine(entry.Item1+" "+name+" "+time+" minY="+points.Min(v=>v.y)+" bounds="+mesh.bounds);
     camera.transform.position=new Vector3(5,2,14);camera.transform.LookAt(new Vector3(0,.1f,1));
     Shot(camera,"output/soul-eater-refinement/"+entry.Item1+"-"+name.Replace(" ","-")+"-"+time.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".png");
     if(entry.Item1=="current" && name!="Walk" && name!="Run")
     {
      camera.transform.position=new Vector3(3,1.5f,9);camera.transform.LookAt(new Vector3(0,.8f,4));camera.fieldOfView=45;
      Shot(camera,"output/soul-eater-refinement/neck-side-"+name.Replace(" ","-")+"-"+time.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".png");
      camera.transform.position=new Vector3(0,1.5f,9);camera.transform.LookAt(new Vector3(0,.8f,4));
      Shot(camera,"output/soul-eater-refinement/neck-front-"+name.Replace(" ","-")+"-"+time.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".png");
      camera.fieldOfView=38;
     }
    }
    if(entry.Item1=="current")foreach(var bone in skin.bones)report.AppendLine("BONE "+bone.name+" "+bone.position.ToString("F4"));
    Object.DestroyImmediate(root);Object.DestroyImmediate(go);Object.DestroyImmediate(mesh);Object.DestroyImmediate(camera.gameObject);
   }
   File.WriteAllText("output/soul-eater-refinement/geometry.txt",report.ToString());
   Debug.Log("SOUL_GEOMETRY_REVIEW_OK");if(quit)EditorApplication.Exit(0);
   }catch(Exception e){if(!quit)throw;Debug.LogException(e);EditorApplication.Exit(1);}
  }
  internal static void Shot(UnityEngine.Camera camera,string path)
  {
   var rt=RenderTexture.GetTemporary(1000,760,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
   var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
   camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);
  }
 }
}
