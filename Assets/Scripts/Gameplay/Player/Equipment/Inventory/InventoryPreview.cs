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
        Vector3 orbitCenter;
        float orbitDistance, yaw, pitch;
        readonly List<Mesh> baked=new List<Mesh>();
        readonly List<Material> previewMaterials=new List<Material>();
        public Texture Texture=>texture;
        public void Zoom(float factor){if(camera!=null)camera.orthographicSize*=Mathf.Clamp(factor,.2f,2);}
        public bool HasModel {get;private set;}
        public void Show(GameObject source,Vector3 rotation=default,bool portrait=false,System.Predicate<Renderer> filter=null,bool mapLighting=false)
        {
            Dispose();if(source==null)return;
            root=new GameObject("Inventory preview geometry");root.transform.position=new Vector3(0,-20000,0);
            var renderers=source.GetComponentsInChildren<Renderer>();
            foreach(var renderer in renderers)
            {
                if(filter!=null&&!filter(renderer))continue;
                if(!renderer.enabled||(!renderer.gameObject.activeInHierarchy&&source.scene.IsValid())||renderer is ParticleSystemRenderer||renderer is TrailRenderer||renderer is LineRenderer)continue;
                Mesh mesh=null;
                if(renderer is SkinnedMeshRenderer skin){if(skin.sharedMesh==null)continue;mesh=new Mesh();baked.Add(mesh);skin.BakeMesh(mesh,false);mesh.RecalculateBounds();}
                // TextMesh also has a MeshRenderer but no MeshFilter. Unity's missing-component
                // objects must be checked with its null semantics, not the ?. operator.
                else if(renderer is MeshRenderer&&renderer.TryGetComponent<MeshFilter>(out var meshFilter))mesh=meshFilter.sharedMesh;
                if(mesh==null)continue;
                var go=new GameObject("Preview mesh");go.layer=30;go.transform.SetParent(root.transform,false);
                go.transform.localPosition=Quaternion.Inverse(source.transform.rotation)*(renderer.transform.position-source.transform.position);
                go.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*renderer.transform.rotation;
                go.transform.localScale=renderer.transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var materials=renderer.sharedMaterials;
                if(mapLighting)
                {
                    for(int i=0;i<materials.Length;i++)
                    {
                        var sourceMaterial=materials[i];var copy=new Material(Shader.Find("Mismo/Map Portrait"));
                        if(sourceMaterial!=null)
                        {
                            copy.color=sourceMaterial.HasProperty("_BaseColor")?sourceMaterial.GetColor("_BaseColor"):sourceMaterial.HasProperty("_Color")?sourceMaterial.color:Color.white;
                            if(sourceMaterial.HasProperty("_BaseMap"))copy.mainTexture=sourceMaterial.GetTexture("_BaseMap");
                            else if(sourceMaterial.HasProperty("_MainTex"))copy.mainTexture=sourceMaterial.mainTexture;
                        }
                        materials[i]=copy;previewMaterials.Add(copy);
                    }
                }
                go.AddComponent<MeshRenderer>().sharedMaterials=materials;
            }
            root.transform.rotation=Quaternion.Euler(rotation);
            var copies=root.GetComponentsInChildren<Renderer>();if(copies.Length==0){Dispose();return;}
            HasModel=true;
            Bounds bounds=copies[0].bounds;foreach(var r in copies)bounds.Encapsulate(r.bounds);
            var cameraObject=new GameObject("Inventory preview camera");cameraObject.transform.SetParent(root.transform,false);
            camera=cameraObject.AddComponent<UnityEngine.Camera>();camera.enabled=false;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.orthographic=true;camera.orthographicSize=Mathf.Max(.1f,bounds.extents.magnitude)*1.12f;
            camera.nearClipPlane=.01f;camera.farClipPlane=Mathf.Max(100,bounds.size.magnitude*6);
            camera.transform.position=bounds.center+new Vector3(0,bounds.extents.y*.12f,Mathf.Max(3,bounds.size.magnitude*2));
            camera.transform.LookAt(bounds.center);camera.allowHDR=false;
            orbitCenter=bounds.center;
            orbitDistance=Vector3.Distance(camera.transform.position,orbitCenter);
            yaw=0;pitch=Mathf.Asin((camera.transform.position.y-orbitCenter.y)/orbitDistance)*Mathf.Rad2Deg;
            texture=new RenderTexture(640,portrait?800:480,24);texture.Create();camera.targetTexture=texture;
            camera.aspect=640f/(portrait?800:480);
            if(portrait)camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x/camera.aspect)*1.18f;
            var lightObject=new GameObject("Inventory preview light");lightObject.transform.SetParent(root.transform,false);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.cullingMask=1<<30;
            light.transform.rotation=Quaternion.Euler(35,145,0);
            camera.Render();
        }
        public void Render(){if(camera!=null&&HasModel)camera.Render();}
        public void Rotate(Vector2 delta)
        {
            if(camera==null||!HasModel)return;
            yaw-=delta.x*.5f;pitch=Mathf.Clamp(pitch+delta.y*.4f,-70,70);
            camera.transform.position=orbitCenter+Quaternion.Euler(-pitch,yaw,0)*Vector3.forward*orbitDistance;
            camera.transform.LookAt(orbitCenter);
        }
        static void Release(Object value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
        public void Dispose()
        {
            HasModel=false;
            if(root!=null)Release(root);root=null;camera=null;
            if(texture!=null){texture.Release();Release(texture);}texture=null;
            foreach(var mesh in baked)Release(mesh);baked.Clear();
            foreach(var material in previewMaterials)Release(material);previewMaterials.Clear();
        }
    }
}



