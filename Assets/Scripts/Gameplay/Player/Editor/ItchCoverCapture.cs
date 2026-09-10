using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class ItchCoverCapture
    {
        static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>().Where(r=>!(r is ParticleSystemRenderer)&&r.GetComponent<TextMesh>()==null).ToArray();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        static GameObject Model(string path,Vector3 position,float height,float yaw)
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))canvas.gameObject.SetActive(false);
            foreach(var text in root.GetComponentsInChildren<TextMesh>(true))text.gameObject.SetActive(false);
            foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
            var animator=root.GetComponentInChildren<Animator>();
            if(animator!=null&&animator.runtimeAnimatorController!=null)
            {var clip=animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.ToLower().Contains("idle"));if(clip!=null)clip.SampleAnimation(animator.gameObject,.2f);animator.enabled=false;}
            var b=BoundsOf(root);root.transform.localScale*=height/b.size.y;root.transform.rotation=Quaternion.Euler(0,yaw,0);b=BoundsOf(root);root.transform.position+=position-new Vector3(b.center.x,b.min.y,b.center.z);return root;
        }
        static void Box(string name,Vector3 position,Vector3 scale,Color color)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;var m=new Material(Shader.Find("Standard")){color=color};m.SetFloat("_Glossiness",0);go.GetComponent<Renderer>().sharedMaterial=m;}
        static void Label(Transform parent,string value,int size,Vector2 pos,Color color)
        {var go=new GameObject("Title",typeof(RectTransform));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,1);rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=pos;rt.sizeDelta=new Vector2(1200,260);var text=go.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontStyle=FontStyle.Bold;text.fontSize=size;text.alignment=TextAnchor.UpperCenter;text.text=value;text.color=color;text.raycastTarget=false;}
        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.9f,.94f,.9f);RenderSettings.ambientEquatorColor=new Color(.65f,.7f,.64f);RenderSettings.ambientGroundColor=new Color(.2f,.24f,.2f);
                var sun=new GameObject("Key light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.4f;sun.color=new Color(1,.89f,.69f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(45,150,0);
                var fill=new GameObject("Rim light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.65f;fill.color=new Color(.58f,.8f,1);fill.transform.rotation=Quaternion.Euler(25,-30,0);
                Box("Earth",new Vector3(0,-.5f,0),new Vector3(10,1,8),new Color(.18f,.22f,.13f));
                Box("Grass terrace",new Vector3(-.5f,-.13f,-.3f),new Vector3(9,.25f,7),new Color(.29f,.4f,.17f));
                Model("Assets/Prefabs/Enemies/Goblin.prefab",new Vector3(-1.25f,-.4f,0),4.3f,18);
                Model("Assets/Prefabs/Enemies/ForestCreatures/Spider_Standard.prefab",new Vector3(1.7f,0,1),1.9f,-12);
                Model("Assets/Prefabs/World/Nature/Tree_0.prefab",new Vector3(-4.2f,0,-3.5f),5.5f,0);
                Model("Assets/Prefabs/World/Nature/Tree_2.prefab",new Vector3(4.3f,0,-3.5f),5.5f,20);
                Model("Assets/Prefabs/World/Nature/rock_0.prefab",new Vector3(-3,0,2),.7f,20);
                Model("Assets/Prefabs/World/Nature/grass_1.prefab",new Vector3(2.9f,0,2.6f),.5f,30);
                var camera=new GameObject("Cover camera").AddComponent<UnityEngine.Camera>();camera.transform.position=new Vector3(3,4.5f,13);camera.transform.LookAt(new Vector3(0,2.2f,0));camera.orthographic=true;camera.orthographicSize=3.6f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.115f,.105f);camera.farClipPlane=100;
                var canvas=new GameObject("Typography").AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;var scaler=canvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1260,1000);scaler.matchWidthOrHeight=.5f;
                Label(canvas.transform,"MISMO",218,new Vector2(3,-42),new Color(.03f,.07f,.065f));
                Label(canvas.transform,"MISMO",218,new Vector2(0,-32),new Color(1,.91f,.66f));
                Label(canvas.transform,"EXPLORACIÓN · COMBATE · AVENTURA",25,new Vector2(0,-265),new Color(.92f,.94f,.82f));
                Directory.CreateDirectory("Docs/Art/ItchCover");
                foreach(int width in new[]{1260,630})
                {
                    int height=width*250/315;var rt=new RenderTexture(width,height,24);rt.antiAliasing=4;camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Docs/Art/ItchCover/Mismo-Cover-"+width+".png",image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
                }
                Debug.Log("ITCH_COVER_PASS");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
