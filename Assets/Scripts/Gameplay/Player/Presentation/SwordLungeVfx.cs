using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Presentation only, owned and cancelled by WeaponAbilityVfx. Never alters shared sword materials.</summary>
    [DefaultExecutionOrder(320)]
    public sealed class SwordLungeVfx : MonoBehaviour
    {
        public Material bladeMaterial;
        public Material ribbonMaterial;
        public ParticleSystem fragments;
        public Color tint = new Color(.12f,.88f,1f,.8f);
        public float radius = .48f;
        public float duration = .38f;
        public float age;
        readonly List<Transform> sources = new List<Transform>();
        readonly List<MeshRenderer> overlays = new List<MeshRenderer>();
        readonly List<Vector3> vertices = new List<Vector3>(512);
        readonly List<Color> colors = new List<Color>(512);
        readonly List<int> indices = new List<int>(2304);
        MaterialPropertyBlock properties;
        Mesh ribbon;
        WeaponPresentation presentation;
        EquipmentLoadout loadout;
        PlayerMotor motor;
        Transform visual;
        float emission;
        struct WakeSample { public Vector3 center, right, bladeStart, bladeEnd; public float time; }
        readonly List<WakeSample> wake = new List<WakeSample>(48);
        public int WakeSampleCount => wake.Count;
        const float WakeLifetime = .18f;

        void Start() => Initialize();
        public void Initialize()
        {
            if(ribbon!=null)return;
            presentation=GetComponentInParent<WeaponPresentation>();
            loadout=GetComponentInParent<EquipmentLoadout>();
            motor=GetComponentInParent<PlayerMotor>();
            if(presentation==null||loadout==null)return;
            visual=presentation.ActiveVisual;
            if(visual==null)return;
            properties=new MaterialPropertyBlock();
            ribbon=new Mesh{name="Estocada · estela voxel"};ribbon.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh=ribbon;
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=ribbonMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                var source=filter.GetComponent<MeshRenderer>();
                if(source==null||!source.enabled||filter.sharedMesh==null)continue;
                var copy=new GameObject("Estocada · luz de hoja");copy.transform.SetParent(transform,false);
                copy.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var overlay=copy.AddComponent<MeshRenderer>();
                var materials=new Material[filter.sharedMesh.subMeshCount];
                for(int i=0;i<materials.Length;i++)materials[i]=bladeMaterial;
                overlay.sharedMaterials=materials;overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;
                sources.Add(filter.transform);overlays.Add(overlay);
            }
        }
        void LateUpdate(){if(GameplayPause.IsPaused)return;Sample(Time.deltaTime);}
        public void Sample(float dt)
        {
            Initialize();
            if(ribbon==null||visual==null||loadout.ActiveDefinition?.poseProfile==null)return;
            age+=Mathf.Max(0,dt);
            var profile=loadout.ActiveDefinition.poseProfile;
            Vector3 bladeStart=visual.TransformPoint(profile.trailBase),bladeEnd=visual.TransformPoint(profile.trailTip);
            float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration*.62f,duration,age));
            properties.SetVector("_BladeStart",bladeStart);properties.SetVector("_BladeEnd",bladeEnd);
            properties.SetColor("_Color",new Color(tint.r,tint.g,tint.b,tint.a*fade));
            for(int i=0;i<overlays.Count;i++)
            {
                if(sources[i]==null)continue;
                var target=overlays[i].transform;target.SetPositionAndRotation(sources[i].position,sources[i].rotation);
                Vector3 parentScale=transform.lossyScale,s=sources[i].lossyScale;
                target.localScale=new Vector3(s.x/parentScale.x,s.y/parentScale.y,s.z/parentScale.z);
                overlays[i].SetPropertyBlock(properties);
            }
            Vector3 forward=motor!=null?motor.Facing:loadout.transform.forward;
            Vector3 center=loadout.transform.position+Vector3.up*.95f;
            Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
            while(wake.Count>0&&age-wake[0].time>WakeLifetime)wake.RemoveAt(0);
            bool moved=wake.Count==0||(center-wake[wake.Count-1].center).sqrMagnitude>.000225f;
            if(wake.Count>0&&(center-wake[wake.Count-1].center).sqrMagnitude>16)wake.Clear();
            if(moved&&dt>0&&age<duration*.7f)
            {
                if(wake.Count>=48)wake.RemoveAt(0);
                wake.Add(new WakeSample{center=center,right=right,bladeStart=bladeStart,bladeEnd=bladeEnd,time=age});
            }
            vertices.Clear();colors.Clear();indices.Clear();
            // Positions are stored in world space: old sections stay behind as the actor advances.
            // No sampled displacement means no ribbon; there is no prebuilt arc around the body.
            for(int band=0;band<3;band++)
            {
                int offset=vertices.Count;
                for(int i=0;i<wake.Count;i++)
                {
                    var sample=wake[i];float freshness=Mathf.Clamp01(1-(age-sample.time)/WakeLifetime);
                    float taper=freshness*freshness;
                    Vector3 a,b;
                    if(band==0)
                    {
                        Vector3 middle=Vector3.Lerp(sample.bladeStart,sample.bladeEnd,.55f);
                        Vector3 span=(sample.bladeEnd-sample.bladeStart)*(.38f*taper);
                        a=middle-span;b=middle+span;
                    }
                    else
                    {
                        Vector3 middle=sample.center+sample.right*(band==1?-.43f:.43f);
                        Vector3 span=(Vector3.up*.07f+sample.right*.045f)*taper;
                        a=middle-span;b=middle+span;
                    }
                    vertices.Add(transform.InverseTransformPoint(a));vertices.Add(transform.InverseTransformPoint(b));
                    Color color=tint;color.a*=fade*taper*(band==0?.55f:.32f);
                    colors.Add(color);colors.Add(color);
                    if(i>0){int n=offset+i*2;indices.Add(n-2);indices.Add(n);indices.Add(n-1);indices.Add(n-1);indices.Add(n);indices.Add(n+1);}
                }
            }
            ribbon.Clear();ribbon.SetVertices(vertices);ribbon.SetColors(colors);ribbon.SetTriangles(indices,0);ribbon.RecalculateBounds();
            emission+=Mathf.Max(0,dt);
            if(fragments!=null&&moved&&wake.Count>1&&emission>=.025f&&age<duration*.65f)
            {
                emission=0;
                var emit=new ParticleSystem.EmitParams{position=Vector3.Lerp(bladeStart,bladeEnd,Random.value),velocity=-forward*.35f,startColor=tint,startSize=.025f};
                fragments.Emit(emit,2);
            }
        }
        void OnDestroy(){if(ribbon!=null){if(Application.isPlaying)Destroy(ribbon);else DestroyImmediate(ribbon);}}
    }
}
