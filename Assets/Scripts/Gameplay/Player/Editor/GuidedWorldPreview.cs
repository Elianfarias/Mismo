using System;
using System.IO;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Isolated batch capture of the actual procedural geometry, with no scene or profile writes.
    public static class GuidedWorldPreview
    {
        public static void CaptureBatch()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run this capture in an isolated batch project.");
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var settings=Object.Instantiate(Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));
                settings.generationVersion=2;settings.preserveAuthoredCenter=false;settings.seed=7319;
                var field=new ExplorationTerrain(settings);
                var world=new GameObject("Generated continent");
                var horizon=world.AddComponent<FiniteWorldHorizon>();horizon.Initialize(field,Mismo.Core.ProjectAssets.Load<Material>("TerrainSurface"));
                if(world.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Distant terrain must not add collision");
                horizon.SetChunkLoaded(Vector2Int.zero,true);horizon.SetChunkLoaded(Vector2Int.zero,false);
                var sun=new GameObject("Preview sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.85f;sun.transform.rotation=Quaternion.Euler(38,-35,0);
                RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.22f,.28f,.35f);RenderSettings.fog=false;
                var camera=new GameObject("Preview camera").AddComponent<UnityEngine.Camera>();
                camera.tag="MainCamera";camera.farClipPlane=200;horizon.UpdateView();
                if(camera.farClipPlane<field.Plan.Bounds.size.magnitude)throw new Exception("Camera cannot see neighbouring regions");
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.48f,.69f,.82f);camera.nearClipPlane=.1f;camera.farClipPlane=12000;
                var bounds=field.Plan.Bounds;var center=new Vector3(bounds.center.x,70,bounds.center.y);
                camera.orthographic=true;camera.orthographicSize=1650;
                camera.transform.position=center+new Vector3(0,2700,-1500);camera.transform.LookAt(center);
                Directory.CreateDirectory("output/finite-world");
                Capture(camera,"output/finite-world/guided-overview.png");
                float x=400,z=field.Plan.Layout.NorthBorder(x)-270;
                camera.orthographic=false;camera.fieldOfView=65;
                camera.transform.position=new Vector3(x,field.Height(x,z)+5,z);
                camera.transform.LookAt(new Vector3(x+40,100,field.Plan.Layout.NorthBorder(x)+80));
                Capture(camera,"output/finite-world/ice-from-meadow.png");
                Object.DestroyImmediate(world);Object.DestroyImmediate(settings);
                Debug.Log("GUIDED_WORLD_PREVIEW_PASS");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Capture(UnityEngine.Camera camera,string path)
        {
            var target=new RenderTexture(1400,1000,24);var previous=RenderTexture.active;
            var image=new Texture2D(1400,1000,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(image);Object.DestroyImmediate(target);}
        }
    }
}
