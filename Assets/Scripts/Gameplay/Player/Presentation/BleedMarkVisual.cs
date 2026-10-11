using Mismo.Gameplay.Player.Equipment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Tajo sangrante's mark: three drops orbit like the shared buff symbols and the axe head glows with a heartbeat.
    /// Presentation only; it never alters the weapon's own materials or hierarchy.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(560)]
    public sealed class BleedMarkVisual : MonoBehaviour
    {
        const int DropCount=3;
        readonly MeshRenderer[] drops=new MeshRenderer[DropCount];
        readonly WeaponHeadGlow glow=new WeaponHeadGlow();
        GameObject root; MeshRenderer arc;
        WeaponSkillEffects skills; WeaponPresentation presentation; EquipmentLoadout loadout; Collider body; UnityEngine.Camera viewer;
        BuffPresentation profile; MaterialPropertyBlock symbolProperties;
        float presence, elapsed;
        static readonly int ColorId=Shader.PropertyToID("_Color");
        public int VisibleDrops {get;private set;}
        public int GlowRenderers {get;private set;}
        void Awake(){skills=GetComponent<WeaponSkillEffects>();body=GetComponent<Collider>();}
        void LateUpdate()=>Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            dt=Mathf.Max(0,dt);
            if(profile==null)profile=BuffPresentation.Current;
            if(profile==null||profile.material==null||profile.bleedMarkSymbol==null){Clear();return;}
            float remaining=0,duration=0;
            bool marked=skills!=null&&skills.isActiveAndEnabled&&skills.BleedMarkTimer(out remaining,out duration);
            presence=Mathf.MoveTowards(presence,marked?1:0,dt/Mathf.Max(.05f,profile.fadeSeconds));
            if(presence<=0){Clear();return;}
            if(root==null)Create();
            if(root.scene!=gameObject.scene)SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            if(viewer==null||!viewer.isActiveAndEnabled||!viewer.CompareTag("MainCamera"))viewer=UnityEngine.Camera.main;
            elapsed+=dt;
            // The last second of the mark blinks: the basic has to land now.
            float alpha=presence*(marked&&remaining<1&&Mathf.Sin(remaining*22)<=0?.35f:1);
            var bounds=body!=null&&body.enabled?body.bounds:new Bounds(transform.position+Vector3.up*.9f,new Vector3(.6f,1.8f,.6f));
            float height=Mathf.Clamp(bounds.size.y,.5f,5),scale=height/1.8f;
            float radius=Mathf.Clamp(Mathf.Max(bounds.extents.x,bounds.extents.z)+.23f*scale,.4f*scale,.75f*scale);
            Vector3 feet=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            float distanceFade=viewer!=null?1-Mathf.InverseLerp(18,30,Vector3.Distance(viewer.transform.position,bounds.center)):1;
            root.transform.position=feet;
            VisibleDrops=0;
            for(int i=0;i<DropCount;i++)
            {
                float angle=(elapsed*profile.orbitDegreesPerSecond+i*360f/DropCount)*Mathf.Deg2Rad;
                var drop=drops[i];
                drop.transform.position=feet+new Vector3(Mathf.Sin(angle)*radius,height*(.5f+.07f*Mathf.Sin(angle*2)),Mathf.Cos(angle)*radius);
                drop.transform.rotation=viewer!=null?Quaternion.LookRotation(viewer.transform.position-drop.transform.position,Vector3.up):Quaternion.identity;
                drop.transform.localScale=Vector3.one*profile.bleedMarkSize*scale;
                SetColor(drop,profile.bleedMarkColor,alpha*profile.symbolOpacity*distanceFade);
                drop.enabled=true;VisibleDrops++;
            }
            arc.enabled=profile.footArc!=null;
            if(arc.enabled)
            {
                arc.transform.SetPositionAndRotation(feet+Vector3.up*.065f*scale,Quaternion.Euler(0,elapsed*8,0));
                arc.transform.localScale=Vector3.one*radius*1.1f;
                SetColor(arc,profile.bleedMarkColor,alpha*profile.footOpacity*distanceFade);
            }
            Glow(alpha);
        }
        void Glow(float alpha)
        {
            // Resolved late: the runner adds this component before the loadout adds its presentation.
            if(presentation==null)presentation=GetComponent<WeaponPresentation>();
            if(loadout==null)loadout=GetComponent<EquipmentLoadout>();
            var color=profile.bleedMarkColor;color.a=Mathf.Clamp01(profile.bladeGlowIntensity*alpha*(.65f+.45f*Heartbeat.At(elapsed,1.1f)));
            glow.Update(root.transform,presentation!=null?presentation.ActiveVisual:null,loadout!=null&&loadout.ActiveDefinition!=null?loadout.ActiveDefinition.poseProfile:null,
                profile.bladeGlowMaterial,profile.bladeGlowFrom,color);
            GlowRenderers=glow.Renderers;
        }
        void Create()
        {
            symbolProperties=new MaterialPropertyBlock();
            root=new GameObject("Tajo sangrante · marca"){hideFlags=HideFlags.DontSave};
            SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            for(int i=0;i<DropCount;i++)drops[i]=MeshObject("Gota "+(i+1),profile.bleedMarkSymbol,profile.material);
            arc=MeshObject("Arco de tobillos",profile.footArc,profile.material);
        }
        MeshRenderer MeshObject(string label,Mesh mesh,Material material)
        {
            var go=new GameObject(label);go.transform.SetParent(root.transform,false);go.layer=gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;renderer.enabled=false;return renderer;
        }
        void SetColor(Renderer renderer,Color color,float alpha){color.a*=alpha;symbolProperties.SetColor(ColorId,color);renderer.SetPropertyBlock(symbolProperties);}
        static void Dispose(GameObject go){if(go==null)return;go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
        public void Clear()
        {
            glow.Clear();Dispose(root);root=null;arc=null;
            for(int i=0;i<DropCount;i++)drops[i]=null;
            VisibleDrops=GlowRenderers=0;presence=0;elapsed=0;
        }
        void OnDisable()=>Clear();
        void OnDestroy()=>Clear();
    }
}
