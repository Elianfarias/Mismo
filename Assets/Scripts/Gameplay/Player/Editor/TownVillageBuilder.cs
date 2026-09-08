using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Seeded decoration inside fixed village lots, keeping the central route clear.</summary>
    public static class TownVillageBuilder
    {
        private const string Folder = "Assets/Art/fbx/Town/";
        public static void Build(Transform village, VoxelRegionSettings settings)
        {
            var previous = village.Find("Imported Town");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject("Imported Town").transform;
            root.SetParent(village, false);
            var random = new System.Random(settings.seed ^ 0x544F574E);
            var field = new VoxelRegionHeightfield(settings);
            var lots = new[] { new Vector2(-63,-75), new Vector2(-39,-78), new Vector2(-64,-61), new Vector2(-40,-61) };
            for (int i=0;i<lots.Length;i++)
            {
                var lot=lots[i];
                Place(root,field,"2-House",lot.x,lot.y,7.4f+(float)random.NextDouble()*.4f, i%2==0?-90:90,true);
                // Props sit behind or alongside each house, away from the central street.
                float outside=i%2==0?-1:1;
                Place(root,field, new[]{"0-Crate-0","1-Crate-1","5-Crate-2"}[random.Next(3)],lot.x+outside*5.8f,lot.y-2,1.15f,random.Next(4)*90,true);
                Place(root,field,"9-Logs",lot.x+outside*5.8f,lot.y+1,1.8f,90,true);
                Place(root,field,"8-Rack",lot.x,lot.y+5.3f,1.6f,180,true);
                for(int j=0;j<3;j++)
                    Place(root,field,j%2==0?"6-Grass-0":"7-Grass-1",lot.x+outside*(5.2f+(float)random.NextDouble()*1.5f),lot.y-4+j*4,.55f+(float)random.NextDouble()*.3f,random.Next(4)*90,false);
            }
            Place(root,field,"3-Trolley",-58,-84,2.5f,20,true);
            Place(root,field,"4-Rock",-71,-68,1.5f,random.Next(4)*90,true);
        }

        private static void Place(Transform parent,VoxelRegionHeightfield field,string suffix,float x,float z,float width,float yaw,bool solid)
        {
            string path=Folder+"Medieval Town - Free Sample-"+suffix+".obj";
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null)throw new InvalidOperationException("Falta el modelo Town: "+path);
            var slot=new GameObject(suffix).transform;slot.SetParent(parent,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,slot);
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0)throw new InvalidOperationException("Modelo sin geometría: "+path);
            Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float scale=width/Mathf.Max(bounds.size.x,bounds.size.z);
            model.transform.localScale*=scale;
            model.transform.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            // Uniform scaling and bottom alignment also handle OBJ origins far from their mesh.
            slot.position=new Vector3(x,field.Height(Mathf.Floor(x)+.5f,Mathf.Floor(z)+.5f),z);
            slot.rotation=Quaternion.Euler(0,yaw,0);
            if(solid)
            {
                var collider=slot.gameObject.AddComponent<BoxCollider>();
                collider.center=Vector3.up*bounds.size.y*scale*.5f;
                collider.size=bounds.size*scale;
            }
        }

        [MenuItem("Mismo/World/Update Town In Current Region")]
        public static void UpdateCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var village=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Village" && t.parent!=null && t.parent.name=="AuthoredGeometry");
            if(village==null)throw new InvalidOperationException("Abrí una región voxel antes de actualizar el pueblo.");
            var region=village.parent.parent.parent;
            string[] parts=region.name.Split(' ');
            int index=Array.IndexOf(parts,"seed");
            if(index<0 || !int.TryParse(parts[index+1],out int seed))throw new InvalidOperationException("No se pudo leer la semilla de la región.");
            var settings=ScriptableObject.CreateInstance<VoxelRegionSettings>();settings.seed=seed;
            // Village is an authored flat site; only decoration uses the seed here.
            try
            {
                Undo.RegisterFullObjectHierarchyUndo(village.gameObject,"Update Town");
                Build(village,settings);
                foreach(var child in village.Cast<Transform>().Where(t=>t.name.StartsWith("House_")).ToArray())
                    Object.DestroyImmediate(child.gameObject);
                EditorSceneManager.MarkSceneDirty(village.gameObject.scene);
            }
            finally{Object.DestroyImmediate(settings);}
        }

        public static void UpdateDefaultBatch()
        {
            EditorSceneManager.OpenScene(VoxelRegionGenerator.DefaultScene);
            UpdateCurrent();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("TOWN_INTEGRATION_OK");
        }
    }
}
