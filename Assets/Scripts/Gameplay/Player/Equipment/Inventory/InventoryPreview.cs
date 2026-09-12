using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    // Copy renderable geometry only. Never instantiate gameplay scripts, colliders or audio.
    public sealed class InventoryPreview : System.IDisposable
    {
        GameObject root;
        UnityEngine.Camera camera;
        RenderTexture texture;
        readonly List<Mesh> baked=new List<Mesh>();
        public Texture Texture=>texture;
        public bool HasModel {get;private set;}
        public void Show(GameObject source,Vector3 rotation=default)
        {
            Dispose();if(source==null)return;
            root=new GameObject("Inventory preview geometry");root.transform.position=new Vector3(0,-20000,0);
            var renderers=source.GetComponentsInChildren<Renderer>();
            foreach(var renderer in renderers)
            {
                if(!renderer.enabled||(!renderer.gameObject.activeInHierarchy&&source.scene.IsValid())||renderer is ParticleSystemRenderer||renderer is TrailRenderer||renderer is LineRenderer)continue;
                Mesh mesh=null;
                if(renderer is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh,false);mesh.RecalculateBounds();baked.Add(mesh);}
                else if(renderer is MeshRenderer)mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if(mesh==null)continue;
                var go=new GameObject("Preview mesh");go.layer=30;go.transform.SetParent(root.transform,false);
                go.transform.localPosition=Quaternion.Inverse(source.transform.rotation)*(renderer.transform.position-source.transform.position);
                go.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*renderer.transform.rotation;
                go.transform.localScale=renderer.transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            }
            root.transform.rotation=Quaternion.Euler(rotation);
            var copies=root.GetComponentsInChildren<Renderer>();if(copies.Length==0)return;
            HasModel=true;
            Bounds bounds=copies[0].bounds;foreach(var r in copies)bounds.Encapsulate(r.bounds);
            var cameraObject=new GameObject("Inventory preview camera");cameraObject.transform.SetParent(root.transform,false);
            camera=cameraObject.AddComponent<UnityEngine.Camera>();camera.enabled=false;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.orthographic=true;camera.orthographicSize=Mathf.Max(.1f,bounds.extents.magnitude)*1.12f;
            camera.nearClipPlane=.01f;camera.farClipPlane=Mathf.Max(100,bounds.size.magnitude*6);
            camera.transform.position=bounds.center+new Vector3(0,bounds.extents.y*.12f,Mathf.Max(3,bounds.size.magnitude*2));
            camera.transform.LookAt(bounds.center);camera.allowHDR=false;
            texture=new RenderTexture(640,480,24);texture.Create();camera.targetTexture=texture;
            camera.aspect=640f/480;
            var lightObject=new GameObject("Inventory preview light");lightObject.transform.SetParent(root.transform,false);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.cullingMask=1<<30;
            light.transform.rotation=Quaternion.Euler(35,145,0);
            camera.Render();
        }
        public void Render(){if(camera!=null&&HasModel)camera.Render();}
        static void Release(Object value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
        public void Dispose()
        {
            HasModel=false;
            if(root!=null)Release(root);root=null;camera=null;
            if(texture!=null){texture.Release();Release(texture);}texture=null;
            foreach(var mesh in baked)Release(mesh);baked.Clear();
        }
    }
}
