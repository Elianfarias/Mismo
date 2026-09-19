using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mismo.Gameplay.Player.World;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class EnvironmentFixChecks
    {
        public static void RunBatch()
        {
            try
            {
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/VoxelRegion_7319.unity");
                foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(PrefabUtility.GetPrefabInstanceStatus(t.gameObject)==PrefabInstanceStatus.MissingAsset)throw new Exception("Missing prefab: "+t.name);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var settings=Object.Instantiate(Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.preserveAuthoredCenter=false;
                int villages=0;
                foreach(int seed in new[]{7319,12,654321})
                {
                    settings.seed=seed;var field=new ExplorationTerrain(settings);
                    for(int cz=-3;cz<=3;cz++)for(int cx=-3;cx<=3;cx++)
                    {
                        var site=field.Site(new Vector2Int(cx,cz));if(site.kind!=WorldSiteKind.Village)continue;villages++;
                        float floor=field.Height(site.position.x,site.position.z)+.08f;
                        for(int z=-24;z<=24;z++)for(int x=-23;x<=23;x++)
                            if(field.Height(site.position.x+x,site.position.z+z)>=floor)throw new Exception("Terrain intersects village "+seed+" "+site.cell);
                    }
                }
                settings.preserveAuthoredCenter=true;var legacy=new ExplorationTerrain(settings);
                for(int z=-24;z<=24;z++)for(int x=-23;x<=23;x++)if(legacy.Height(-50+x,-70+z)>4)throw new Exception("Legacy village terrain intersection");
                settings.preserveAuthoredCenter=false;
                var terrain=new ExplorationTerrain(settings);var town=terrain.Site(Vector2Int.zero);var center=ExplorationChunks.Coordinate(town.position);
                var holder=new GameObject("Test terrain");var material=Mismo.Core.ProjectAssets.Load<Material>("TerrainSurface");
                for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
                {
                    var go=new GameObject("Terrain");go.transform.SetParent(holder.transform);go.AddComponent<MeshFilter>().sharedMesh=ExplorationChunks.BuildTerrain(terrain,center+new Vector2Int(x,z));go.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
                new ExplorationContent(settings,terrain).Decorate(center,holder.transform,material);
                var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";camera.transform.position=town.position+new Vector3(30,12,-40);camera.transform.LookAt(town.position+Vector3.up*4);camera.farClipPlane=500;
                var cycle=new GameObject("Cycle").AddComponent<DayNightCycle>();cycle.Initialize(Mismo.Core.ProjectAssets.Load<DayNightSettings>("DayNightSettings"));cycle.SetHour(12);
                var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
                Directory.CreateDirectory("Docs/Validation/EnvironmentFix");File.WriteAllBytes("Docs/Validation/EnvironmentFix/Town.png",image.EncodeToPNG());
                File.WriteAllText("Docs/Validation/EnvironmentFix/Checks.txt","PASS: scene opens without missing prefabs; "+villages+" village footprints across 3 seeds clear terrain; town rendered with actual procedural terrain and cubemap sky.");
                Debug.Log("ENVIRONMENT_FIX_PASS "+villages);EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}


