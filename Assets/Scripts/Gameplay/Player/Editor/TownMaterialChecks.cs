using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    public static class TownMaterialChecks
    {
        public static void RunBatch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Town/TownVoxelPalette.mat");
            if(material==null || material.mainTexture==null)throw new System.Exception("Town palette missing");
            int index=0;
            foreach(string path in Directory.GetFiles("Assets/Art/Models/Town","*.obj"))
            {
                var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var renderers=model.GetComponentsInChildren<MeshRenderer>();
                var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    foreach(var assigned in renderer.sharedMaterials)
                        if(assigned!=material)throw new System.Exception("Wrong town material: "+path);
                }
                float scale=2.3f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                model.transform.localScale*=scale;
                model.transform.position=new Vector3((index%5)*3,0,(index/5)*3)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                index++;
            }
            RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
            var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(45,-30,0);
            var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();
            camera.transform.position=new Vector3(13,12,-14);camera.transform.LookAt(new Vector3(6,.4f,1.5f));
            camera.orthographic=true;camera.orthographicSize=5.5f;camera.backgroundColor=new Color(.12f,.15f,.2f);camera.clearFlags=CameraClearFlags.SolidColor;
            var rt=new RenderTexture(1400,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var pixels=new Texture2D(1400,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1400,800),0,0);pixels.Apply();
            File.WriteAllBytes("town-materials.png",pixels.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(pixels);rt.Release();Object.DestroyImmediate(rt);
            Debug.Log("TOWN_MATERIALS_OK models="+index);
        }
    }
}
