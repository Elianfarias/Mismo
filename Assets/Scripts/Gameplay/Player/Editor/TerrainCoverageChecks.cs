using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class TerrainCoverageChecks
    {
        const string Output = "output/terrain-coverage";

        [MenuItem("Mismo/World/Verify terrain coverage")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/TerrainSurface.mat");
            Require(source != null && source.HasProperty("_DistantTerrain"), "Terrain shader supports horizon coverage");
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                CheckRoad(source);
                CheckPasses(source);
            }
            finally { ShaderUtil.allowAsyncCompilation = asyncCompilation; }
            var errors = ShaderUtil.GetShaderMessages(source.shader).Where(m => m.severity.ToString() == "Error").ToArray();
            Require(errors.Length == 0, string.Join("\n", errors.Select(e => e.message)));
            File.WriteAllText(Output + "/Checks.txt", "PASS: forward, depth and normal coverage; negative chunk coordinates; loaded/unloaded transition; nearby terrain unaffected; production road with horizon matches terrain alone.\n");
            Debug.Log("TERRAIN_COVERAGE_CHECKS_OK");
        }

        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        static Color32[] Read(RenderTexture rt, Texture2D image, string path = null)
        {
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            if (path != null) File.WriteAllBytes(path, image.EncodeToPNG());
            return image.GetPixels32();
        }

        static int Differences(Color32[] a, Color32[] b)
        {
            int count = 0;
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i].r-b[i].r) > 2 || Math.Abs(a[i].g-b[i].g) > 2 || Math.Abs(a[i].b-b[i].b) > 2) count++;
            return count;
        }

        static void CheckPasses(Material source)
        {
            var material = new Material(source);
            var coverage = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            var geometry = new VoxelRegionGeometry();
            geometry.Quad(new Vector3(-32,0,-32), new Vector3(-32,0,32), new Vector3(32,0,32), new Vector3(32,0,-32), Color.green);
            var mesh = geometry.Mesh("Coverage regression quad");
            var rt = new RenderTexture(64,64,24);
            rt.Create();
            var image = new Texture2D(64,64,TextureFormat.RGBA32,false);
            var camera = new GameObject("Coverage test camera").AddComponent<UnityEngine.Camera>(); camera.enabled = false;
            var previous = RenderTexture.active;
            try
            {
                camera.orthographic = true; camera.orthographicSize = 32; camera.aspect = 1;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.transform.position = new Vector3(0,50,0); camera.transform.rotation = Quaternion.Euler(90,0,0);
                material.SetTexture("_TerrainCoverage",coverage); material.SetVector("_CoverageGrid",new Vector4(-1,-1,2,2));
                foreach (string pass in new[]{"ForwardLit","DepthOnly","DepthNormals"})
                {
                    int index = material.FindPass(pass); Require(index >= 0, "Missing " + pass);
                    Color32[] Draw(bool draw)
                    {
                        using (var commands = new CommandBuffer())
                        {
                            commands.SetRenderTarget(rt); commands.ClearRenderTarget(true,true,new Color(.8f,.1f,.7f));
                            commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(camera.projectionMatrix,false));
                            if (draw) commands.DrawMesh(mesh,Matrix4x4.identity,material,0,index);
                            Graphics.ExecuteCommandBuffer(commands);
                        }
                        return Read(rt,image);
                    }
                    var empty = Draw(false);
                    material.SetFloat("_DistantTerrain",1);
                    coverage.SetPixels(new[]{Color.black,Color.black,Color.black,Color.black}); coverage.Apply();
                    var visible = Draw(true);
                    File.WriteAllBytes(Output + "/Pass_" + pass + ".png",image.EncodeToPNG());
                    Require(Differences(empty,visible) > 3900, pass + " renders unloaded terrain: " + Differences(empty,visible));
                    // Mask a negative-coordinate chunk. Exactly one quarter must disappear.
                    coverage.SetPixel(0,0,Color.white); coverage.Apply();
                    var partial = Draw(true);
                    Require(Differences(visible,partial) == 1024, pass + " masks exactly the loaded negative chunk");
                    coverage.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white}); coverage.Apply();
                    Require(Differences(empty,Draw(true)) == 0, pass + " removes all loaded terrain");
                    material.SetFloat("_DistantTerrain",0);
                    Require(Differences(visible,Draw(true)) == 0, pass + " keeps nearby terrain regardless of coverage");
                    material.SetFloat("_DistantTerrain",1);
                    coverage.SetPixels(new[]{Color.black,Color.black,Color.black,Color.black}); coverage.Apply();
                    Require(Differences(visible,Draw(true)) == 0, pass + " restores unloaded terrain");
                }
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(mesh); Object.DestroyImmediate(material);
                Object.DestroyImmediate(coverage); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
            }
        }

        static void CheckRoad(Material material)
        {
            var settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationWorldSettings>("Assets/Data/World/ExplorationWorldSettings.asset"));
            settings.generationVersion = 2; settings.preserveAuthoredCenter = false; settings.seed = 7319;
            var field = new ExplorationTerrain(settings);
            var point = Vector2.Lerp(field.Plan.RoutePoint(1),field.Plan.RoutePoint(2),.35f);
            var scene = EditorSceneManager.NewPreviewScene();
            var meshes = new List<Mesh>();
            var rt = new RenderTexture(960,640,24); var image = new Texture2D(960,640,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            float fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
            try
            {
                var camera = new GameObject("Road coverage camera").AddComponent<UnityEngine.Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject,scene); camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
                camera.orthographic = true; camera.orthographicSize = 24; camera.farClipPlane = 500; camera.targetTexture = rt;
                var target = new Vector3(point.x,field.Height(point.x,point.y),point.y);
                camera.transform.position = target + new Vector3(32,40,-32); camera.transform.LookAt(target);
                var light = new GameObject("Road test sun").AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = 1.3f; light.transform.rotation = Quaternion.Euler(45,-35,0); SceneManager.MoveGameObjectToScene(light.gameObject,scene);
                var distant = new GameObject("Road test horizon"); SceneManager.MoveGameObjectToScene(distant,scene);
                var horizon = distant.AddComponent<FiniteWorldHorizon>(); horizon.Initialize(field,material);
                var center = ExplorationChunks.Coordinate(target);
                for (int z=-2;z<=2;z++) for (int x=-2;x<=2;x++)
                {
                    var id = center + new Vector2Int(x,z);
                    var tile = new GameObject("Road test " + id); SceneManager.MoveGameObjectToScene(tile,scene);
                    var mesh = ExplorationChunks.BuildTerrain(field,id); meshes.Add(mesh);
                    tile.AddComponent<MeshFilter>().sharedMesh = mesh; tile.AddComponent<MeshRenderer>().sharedMaterial = material;
                    horizon.SetChunkLoaded(id,true);
                }
                var horizonMaterial = distant.GetComponent<Renderer>().sharedMaterial;
                horizonMaterial.SetFloat("_DistantTerrain",0); camera.Render();
                var before = Read(rt,image,Output + "/RoadBefore.png");
                horizonMaterial.SetFloat("_DistantTerrain",1); camera.Render();
                var after = Read(rt,image,Output + "/RoadAfter.png");
                distant.SetActive(false); camera.Render();
                var reference = Read(rt,image,Output + "/RoadWithoutHorizon.png");
                int oldDifference = Differences(before,reference), newDifference = Differences(after,reference);
                File.WriteAllText(Output + "/Comparison.txt", "Pixels affected by overlapping horizon: " + oldDifference + "\nPixels different after correction: " + newDifference + "\n");
                Require(oldDifference > 100, "Road sample reproduces horizon overlap");
                Require(newDifference == 0, "Masked horizon must not change the loaded road: " + newDifference);
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
                foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(settings);
            }
        }
    }
}
