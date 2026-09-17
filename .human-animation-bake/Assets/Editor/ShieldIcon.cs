using UnityEngine;
using UnityEditor;
using System.IO;
public static class ShieldIcon
{
 public static void Run()
 {
  var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shield.fbx"));
  var material=new Material(Shader.Find("Standard"));material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/weapons_bits_texture.png");material.SetFloat("_Glossiness",.25f);
  var bounds=new Bounds();bool first=true;
  foreach(var r in model.GetComponentsInChildren<Renderer>()){r.sharedMaterial=material;if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
  var camera=new GameObject("Icon camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x)*1.25f;camera.nearClipPlane=.0001f;camera.farClipPlane=100;
  camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
  var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(35,155,0);RenderSettings.ambientLight=Color.gray;
  var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;
  for(int side=0;side<2;side++){
   camera.transform.position=bounds.center+new Vector3(.15f,.08f,side==0?1:-1)*Mathf.Max(bounds.size.magnitude*3,.1f);camera.transform.LookAt(bounds.center);camera.Render();RenderTexture.active=rt;
   var image=new Texture2D(512,512,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();File.WriteAllBytes("shield-icon-"+side+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
  }
  EditorApplication.Exit(0);
 }
}
