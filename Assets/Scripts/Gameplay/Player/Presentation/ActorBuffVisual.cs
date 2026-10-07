using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>A shared orbit: four symbols for one/two/four kinds, three for three kinds.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(550)]
    public sealed class ActorBuffVisual : MonoBehaviour
    {
        sealed class Slot { public MeshRenderer renderer; public MeshFilter filter; public BuffKind kind; public float opacity; }
        readonly Slot[] slots=new Slot[4];
        readonly bool[] active=new bool[4];
        readonly BuffKind[] kinds=new BuffKind[4];
        GameObject root; MeshRenderer arc; Color arcColor; float arcOpacity;
        ActorBuffFeedback feedback; BuffPresentation profile; Collider body; UnityEngine.Camera viewer;
        MaterialPropertyBlock properties; float elapsed;
        static readonly int ColorId=Shader.PropertyToID("_Color");
        public int ActiveKinds {get;private set;}
        public int SymbolCount {get;private set;}
        public int RendererCount {get;private set;}
        public float AnimationTime=>elapsed;
        void Awake(){feedback=GetComponent<ActorBuffFeedback>();body=GetComponent<Collider>();}
        void LateUpdate()=>Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if(feedback==null||!feedback.isActiveAndEnabled){Clear();return;}
            if(profile==null)profile=BuffPresentation.Current;
            if(profile==null||profile.material==null)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            if(viewer==null||!viewer.isActiveAndEnabled||!viewer.CompareTag("MainCamera"))viewer=UnityEngine.Camera.main;
            elapsed+=Mathf.Max(0,dt);
            for(int i=0;i<4;i++)active[i]=false;
            foreach(var view in feedback.Views)if(view.worldVisible)active[(int)view.kind]=true;
            ActiveKinds=0;
            for(int i=0;i<4;i++)if(active[i]&&profile.For((BuffKind)i)?.symbol!=null)kinds[ActiveKinds++]=(BuffKind)i;
            int count=ActiveKinds==0?0:ActiveKinds==3?3:4;
            if(root==null)
            {
                if(count==0){Clear();return;}
                Create();
            }
            if(root.scene!=gameObject.scene)SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            var bounds=body!=null&&body.enabled?body.bounds:new Bounds(transform.position+Vector3.up*.9f,new Vector3(.6f,1.8f,.6f));
            float height=Mathf.Clamp(bounds.size.y,.5f,5),scale=height/1.8f;
            float radius=Mathf.Clamp(Mathf.Max(bounds.extents.x,bounds.extents.z)+.23f*scale,.4f*scale,.75f*scale);
            Vector3 feet=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            float distanceFade=viewer!=null?1-Mathf.InverseLerp(18,30,Vector3.Distance(viewer.transform.position,bounds.center)):1;
            RendererCount=SymbolCount=0;root.transform.position=feet;
            for(int n=0;n<slots.Length;n++)
            {
                var slot=slots[n];bool wanted=n<count;
                if(wanted)
                {
                    var kind=kinds[n%ActiveKinds];var next=profile.For(kind);
                    if(slot.kind!=kind||slot.filter.sharedMesh!=next.symbol){slot.kind=kind;slot.filter.sharedMesh=next.symbol;slot.opacity=0;}
                }
                // Reassign the same four slots; transitions can never double the symbol budget.
                if(count>0&&!wanted)slot.opacity=0;
                else slot.opacity=Mathf.MoveTowards(slot.opacity,wanted?1:0,Mathf.Max(0,dt)/Mathf.Max(.05f,profile.fadeSeconds));
                slot.renderer.enabled=slot.opacity>0;
                if(!slot.renderer.enabled)continue;
                var style=profile.For(slot.kind);
                float angle=(elapsed*profile.orbitDegreesPerSecond+n*360f/Mathf.Max(1,count==0?4:count))*Mathf.Deg2Rad;
                var glyph=slot.renderer;float level=.50f+.07f*Mathf.Sin(angle*2);
                glyph.transform.position=feet+new Vector3(Mathf.Sin(angle)*radius,height*level,Mathf.Cos(angle)*radius);
                glyph.transform.rotation=viewer!=null?Quaternion.LookRotation(viewer.transform.position-glyph.transform.position,Vector3.up):Quaternion.identity;
                glyph.transform.localScale=Vector3.one*style.size*scale;
                SetColor(glyph,style.color,slot.opacity*profile.symbolOpacity*distanceFade*(ActiveKinds>1?.75f:1));
                SymbolCount++;RendererCount++;
            }
            arcOpacity=Mathf.MoveTowards(arcOpacity,count>0?1:0,Mathf.Max(0,dt)/Mathf.Max(.05f,profile.fadeSeconds));
            if(count>0)arcColor=profile.For(kinds[0]).color;
            arc.enabled=arcOpacity>0&&profile.footArc!=null;
            if(arc.enabled)
            {
                arc.transform.SetPositionAndRotation(feet+Vector3.up*.065f*scale,Quaternion.Euler(0,elapsed*8,0));
                arc.transform.localScale=Vector3.one*radius*1.1f;
                SetColor(arc,arcColor,arcOpacity*profile.footOpacity*distanceFade);RendererCount++;
            }
            if(count==0&&SymbolCount==0&&arcOpacity<=0)Clear();
        }
        void Create()
        {
            root=new GameObject("Buff VFX · Shared orbit"){hideFlags=HideFlags.DontSave};
            SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            for(int i=0;i<slots.Length;i++)
            {
                var renderer=MeshObject(root,"Orbit symbol "+(i+1),null);renderer.enabled=false;
                slots[i]=new Slot{renderer=renderer,filter=renderer.GetComponent<MeshFilter>()};
            }
            arc=MeshObject(root,"Ankle arc",profile.footArc);arc.enabled=false;arcOpacity=0;
        }
        MeshRenderer MeshObject(GameObject root,string label,Mesh mesh)
        {
            var go=new GameObject(label);go.transform.SetParent(root.transform,false);go.layer=gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=profile.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;return renderer;
        }
        void SetColor(Renderer renderer,Color color,float alpha){color.a*=alpha;properties.SetColor(ColorId,color);renderer.SetPropertyBlock(properties);}
        static void Dispose(GameObject go){if(go==null)return;go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
        public void Clear(){Dispose(root);root=null;arc=null;for(int i=0;i<4;i++)slots[i]=null;ActiveKinds=RendererCount=SymbolCount=0;arcOpacity=0;elapsed=0;}
        void OnDisable()=>Clear();
        void OnDestroy()=>Clear();
    }
}
