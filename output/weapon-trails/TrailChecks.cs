using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Equipment;
using Object=UnityEngine.Object;
public static class TrailChecks
{
 public static void Run(){var log=new List<string>();Directory.CreateDirectory("output/trails");Action<bool,string> check=(pass,msg)=>{if(!pass)throw new Exception(msg);log.Add("PASS "+msg);};try{
 var owner=new GameObject("Owner");var blade=new GameObject("Blade");var p=ScriptableObject.CreateInstance<WeaponPoseProfile>();p.meleeTrail=true;p.proceduralTrail=true;p.trailBase=new Vector3(.4f,0,0);p.trailTip=new Vector3(2,0,0);p.trailDuration=1;p.trailStartColor=new Color(.2f,.9f,1,.95f);p.trailEndColor=new Color(.1f,.3f,1,0);
 var material=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/TrailTest.shader"));
 using(var trail=new WeaponTrailRibbon(owner.transform,material)){
 trail.SampleBlade(blade.transform,p,0,true);blade.transform.position=Vector3.up;trail.SampleBlade(blade.transform,p,.1f,true);check(trail.Mesh.vertexCount==4&&trail.Mesh.triangles.Length==6,"Two blade samples build one ribbon quad");
 check(Vector3.Distance(trail.Mesh.vertices[3],blade.transform.TransformPoint(p.trailTip))<.001f,"Newest edge matches blade tip");
 trail.SampleBlade(blade.transform,p,.2f,false);blade.transform.position=Vector3.up*2;trail.SampleBlade(blade.transform,p,.3f,true);check(trail.Mesh.triangles.Length==6,"Separate strokes are not bridged");
 trail.SampleBlade(blade.transform,p,2,false);check(trail.Mesh.vertexCount==0,"Trail expires after lifetime");
 trail.SampleBlade(blade.transform,p,2.1f,true);trail.SampleBlade(blade.transform,p,0,true);check(trail.SampleCount==1,"Scrubbing backwards clears history");
 trail.Clear();p.trailDuration=10;for(int i=0;i<300;i++){blade.transform.position=Vector3.up*i*.01f;trail.SampleBlade(blade.transform,p,i*.01f,true);}check(trail.SampleCount<=192,"Sample budget is bounded");
 p.meleeTrail=false;trail.SampleBlade(blade.transform,p,4,false);check(trail.SampleCount==0,"Disabling trail clears geometry");p.meleeTrail=true;p.trailDuration=1;
 blade.transform.position=Vector3.zero;
 for(int i=0;i<=60;i++){blade.transform.rotation=Quaternion.Euler(0,0,-80+i*2.6f);trail.SampleBlade(blade.transform,p,i/60f,true);}
 var cameraObject=new GameObject("Camera");var camera=cameraObject.AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-6);camera.orthographic=true;camera.orthographicSize=2.5f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.045f,.065f);camera.cullingMask=~0;
 var rt=new RenderTexture(800,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(800,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,800),0,0);image.Apply();File.WriteAllBytes("output/trails/preview.png",image.EncodeToPNG());
 var pixels=image.GetPixels();int bright=0;foreach(var pixel in pixels)if(pixel.g>.15f)bright++;check(bright>500,"Rendered ribbon is visible on camera ("+bright+" pixels)");
 camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(cameraObject);
 }
 Object.DestroyImmediate(material);Object.DestroyImmediate(p);Object.DestroyImmediate(blade);Object.DestroyImmediate(owner);File.WriteAllLines("output/trails/checks.txt",log);EditorApplication.Exit(0);
 }catch(Exception e){log.Add("FAIL "+e);File.WriteAllLines("output/trails/checks.txt",log);Debug.LogException(e);EditorApplication.Exit(1);}}
}
