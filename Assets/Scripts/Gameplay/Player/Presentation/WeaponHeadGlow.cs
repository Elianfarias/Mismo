using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Draws a weapon visual again with an additive material (Mismo/Sword Blade Glow), clipped to its head along the
    /// pose's handle. The copies live under the caller's own root and follow the weapon; its materials and hierarchy stay untouched.</summary>
    public sealed class WeaponHeadGlow
    {
        readonly List<Transform> sources=new List<Transform>();
        readonly List<MeshRenderer> glows=new List<MeshRenderer>();
        // Created on first use: owners build this helper in a field initializer, where Unity forbids native allocations.
        MaterialPropertyBlock properties;
        Transform visual,parent; Material material;
        static readonly int ColorId=Shader.PropertyToID("_Color"),StartId=Shader.PropertyToID("_BladeStart"),EndId=Shader.PropertyToID("_BladeEnd");
        public int Renderers {get;private set;}
        // `from`: fraction of the handle (trailBase → trailTip) where the glow starts; color.a is the final opacity.
        public void Update(Transform parent,Transform visual,WeaponPoseProfile pose,Material material,float from,Color color)
        {
            if(visual!=this.visual||parent!=this.parent||material!=this.material)Build(parent,visual,material);
            Renderers=0;
            bool show=visual!=null&&pose!=null&&material!=null&&color.a>0;
            if(show)
            {
                if(properties==null)properties=new MaterialPropertyBlock();
                Vector3 axis=pose.trailTip-pose.trailBase;
                properties.SetVector(StartId,visual.TransformPoint(pose.trailBase+axis*from));
                // Generous past the tip: the head can rise above trailTip and the shader clips at the end.
                properties.SetVector(EndId,visual.TransformPoint(pose.trailTip+axis*.6f));
                properties.SetColor(ColorId,color);
            }
            for(int i=0;i<glows.Count;i++)
            {
                var source=sources[i];var glow=glows[i];
                if(glow==null)continue;
                glow.enabled=show&&source!=null&&source.gameObject.activeInHierarchy;
                if(!glow.enabled)continue;
                glow.transform.SetPositionAndRotation(source.position,source.rotation);
                glow.transform.localScale=source.lossyScale;
                glow.SetPropertyBlock(properties);Renderers++;
            }
        }
        void Build(Transform parent,Transform visual,Material material)
        {
            Clear();this.parent=parent;this.visual=visual;this.material=material;
            if(parent==null||visual==null||material==null)return;
            foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                var source=filter.GetComponent<MeshRenderer>();
                if(source==null||!source.enabled||filter.sharedMesh==null)continue;
                var go=new GameObject("Brillo del arma");go.transform.SetParent(parent,false);go.layer=visual.gameObject.layer;
                go.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var glow=go.AddComponent<MeshRenderer>();
                var materials=new Material[filter.sharedMesh.subMeshCount];
                for(int i=0;i<materials.Length;i++)materials[i]=material;
                glow.sharedMaterials=materials;glow.shadowCastingMode=ShadowCastingMode.Off;glow.receiveShadows=false;
                glow.lightProbeUsage=LightProbeUsage.Off;glow.reflectionProbeUsage=ReflectionProbeUsage.Off;
                glow.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;glow.enabled=false;
                sources.Add(filter.transform);glows.Add(glow);
            }
        }
        public void Clear()
        {
            foreach(var glow in glows)
            {
                if(glow==null)continue;
                glow.gameObject.SetActive(false);
                if(Application.isPlaying)Object.Destroy(glow.gameObject);else Object.DestroyImmediate(glow.gameObject);
            }
            glows.Clear();sources.Clear();visual=parent=null;material=null;Renderers=0;
        }
    }

    public static class Heartbeat
    {
        // Two beats per cycle, like a pulse: 0 between beats, up to ~1.6 on the first.
        public static float At(float time,float beatsPerSecond)
        {
            float p=time*beatsPerSecond%1;
            return Mathf.Exp(-Mathf.Pow((p-.05f)/.05f,2))+.6f*Mathf.Exp(-Mathf.Pow((p-.25f)/.05f,2));
        }
    }
}
