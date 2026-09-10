using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mismo.Gameplay.Player.World;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class TownPolishIntegration
    {
        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                const string path="Assets/Prefabs/World/Villages/MedievalVillage.prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                var floor=root.transform.Find("Stone foundation");if(floor!=null)Object.DestroyImmediate(floor.gameObject);
                Vector3 entrance=Vector3.zero;bool found=false;
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;var materials=filter.GetComponent<Renderer>().sharedMaterials;var vertices=mesh.vertices;
                    for(int i=0;i<materials.Length;i++)
                    {
                        if(!materials[i].name.StartsWith("Entrance_Bridge_Open"))continue;
                        var bounds=new Bounds();bool first=true;
                        foreach(int index in mesh.GetTriangles(i))
                        {var v=root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[index]));if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}
                        entrance=bounds.center;found=true;
                    }
                }
                if(!found)throw new Exception("Open entrance not found");
                Vector3 outward=Mathf.Abs(entrance.x)>Mathf.Abs(entrance.z)?Vector3.right*Mathf.Sign(entrance.x):Vector3.forward*Mathf.Sign(entrance.z);
                var catalog=Resources.Load<WorldContentCatalog>("WorldContentCatalog");catalog.villageSpawnOffset=entrance+outward*11;catalog.villageSpawnOffset.y=0;catalog.villageSpawnYaw=Quaternion.LookRotation(-outward).eulerAngles.y;EditorUtility.SetDirty(catalog);
                PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
                const string surfacePath="Assets/Resources/TerrainSurface.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(surfacePath);
                if(material==null){material=new Material(Shader.Find("Mismo/Textured Terrain"));AssetDatabase.CreateAsset(material,surfacePath);}
                AssetDatabase.SaveAssets();
                Debug.Log("TOWN_POLISH_PASS entrance="+entrance+" spawn="+catalog.villageSpawnOffset+" yaw="+catalog.villageSpawnYaw);
                EnvironmentFixChecks.RunBatch();
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
