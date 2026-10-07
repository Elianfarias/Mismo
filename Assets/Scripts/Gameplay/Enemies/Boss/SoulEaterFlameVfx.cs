using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Reuses the configured particle prefab; procedural ribbons remain a fallback for older assets.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SoulEaterFlameVfx : MonoBehaviour
    {
        [SerializeField] Material flameMaterial;
        PrefabBreath prefabBreath;
        public GameObject PrefabInstance=>prefabBreath?.Root;
        public void ConfigurePrefab(GameObject prefab,int budget)
        {
            if(prefabBreath!=null)Destroy(prefabBreath.Root);
            prefabBreath=prefab!=null?new PrefabBreath(prefab,transform,budget):null;
        }
        Mesh mesh;
        MeshRenderer surface;
        readonly List<Vector3> vertices = new List<Vector3>(2600);
        readonly List<Color> colors = new List<Color>(2600);
        readonly List<Vector2> uv = new List<Vector2>(2600);
        readonly List<int> triangles = new List<int>(6000);
        float time;
        public bool Emitting { get; private set; }
        public float Reach { get; private set; }
        public Material Material => flameMaterial;
        public void Configure(Material material) { flameMaterial = material; GetComponent<MeshRenderer>().sharedMaterial = material; }
        void Awake()
        {
            mesh = new Mesh { name = "SoulEater flowing flame" }; mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh; surface = GetComponent<MeshRenderer>();
            surface.sharedMaterial = flameMaterial; surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; surface.receiveShadows = false; surface.enabled = false;
        }
        public void Show(Vector3 origin, Vector3 direction, float reach, float halfAngle, float heat, float dt, float floorY = float.NaN, float baseWidth = 0, float backreach = 0)
        {
            if (mesh == null) return;
            time += Mathf.Max(0, dt); Emitting = reach > .05f; Reach = Mathf.Max(0, reach);
            surface.enabled = heat > .01f || Emitting;
            if (!surface.enabled) { prefabBreath?.Hide(); return; }
            if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            if(prefabBreath!=null)
            {
                if(Emitting){surface.enabled=false;prefabBreath.Show(reach,halfAngle,dt,floorY,baseWidth,backreach);return;}
                prefabBreath.Hide();
            }
            vertices.Clear(); colors.Clear(); uv.Clear(); triangles.Clear();
            if (Emitting) BuildFlame(reach, Mathf.Tan(halfAngle * Mathf.Deg2Rad));
            else BuildHeat(heat);
            if(Emitting && !float.IsNaN(floorY))
            {
                // Fan-shaped ground breath: its lower ribbons reach the floor beneath the mouth.
                // The same width, backreach and floor are used by the damage volume.
                float cone=Mathf.Tan(halfAngle*Mathf.Deg2Rad);
                for(int i=0;i<vertices.Count;i++)
                {
                    var p=vertices[i];float envelope=.22f+Mathf.Max(0,p.z)*cone;
                    float lower=Mathf.Clamp01(-p.y/envelope);
                    p.x*=1+baseWidth/envelope;
                    var world=transform.TransformPoint(p);
                    world.y=Mathf.Lerp(world.y,floorY+.03f,lower);
                    world-=Vector3.ProjectOnPlane(direction,Vector3.up).normalized*(backreach*lower);
                    vertices[i]=transform.InverseTransformPoint(world);
                }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }
        public void Hide() { prefabBreath?.Hide(); Emitting = false; Reach = 0; if (surface != null) surface.enabled = false; }
        void BuildFlame(float reach, float cone)
        {
            const int count = 24, segments = 28;
            for (int strand = 0; strand < count; strand++)
            {
                float angle = strand * 2.399963f;
                float ring = strand < 6 ? .13f : .3f + .65f * Hash(strand * 7 + 2);
                Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                Vector3 widthAxis = new Vector3(-radial.y, radial.x, 0);
                float length = reach * (strand < 6 ? .94f : .63f + .37f * Hash(strand + 5));
                int start = vertices.Count;
                for (int j = 0; j <= segments; j++)
                {
                    float t = j / (float)segments, z = t * length;
                    float envelope = .2f + cone * z;
                    float wave = Mathf.Sin(z * 2.7f - time * 15 + strand * 1.7f);
                    float curl = Mathf.Sin(z * 4.5f - time * 22 + strand * .8f);
                    Vector3 center = radial * (envelope * ring + wave * envelope * .12f) + widthAxis * curl * envelope * .08f + Vector3.forward * z;
                    center.y += Mathf.Pow(t, 2) * .3f;
                    float width = envelope * (strand < 6 ? .52f : .33f) * (1 - Mathf.Pow(t, 5)) * (.8f + .2f * wave);
                    Color hot = new Color(.85f, 1.25f, .32f, .25f);
                    Color green = new Color(.12f, .8f, .02f, .22f);
                    Color color = Color.Lerp(hot, green, Mathf.Clamp01(t * 1.8f + ring * .35f));
                    color.a *= Mathf.Clamp01((1 - t) * 4) * Mathf.Lerp(.45f, 1, Mathf.Abs(wave));
                    vertices.Add(center - widthAxis * width); vertices.Add(center + widthAxis * width);
                    uv.Add(new Vector2(0,t));uv.Add(new Vector2(1,t));
                    colors.Add(color); colors.Add(color);
                    if (j > 0) Quad(start + (j - 1) * 2, start + j * 2);
                }
            }
            for (int i = 0; i < 60; i++)
            {
                float p = Mathf.Repeat(time * (1.5f + Hash(i) * .8f) + Hash(i + 40), 1);
                float a = Hash(i + 10) * Mathf.PI * 2;
                float radius = (.12f + p * reach * cone) * (.3f + Hash(i + 60) * .75f);
                var center = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius + p * p * .4f, p * reach);
                Gem(center, (.025f + .045f * Hash(i + 90)) * (1 - p * .65f), new Color(.65f, 1.1f, .06f, (1 - p) * .75f));
            }
        }
        void BuildHeat(float heat)
        {
            float r = .06f + heat * .23f;
            Gem(Vector3.zero, r, new Color(.55f, 1.1f, .14f, heat * .6f));
            for (int i = 0; i < 18; i++)
            {
                float p = Mathf.Repeat(time * 1.3f + i / 18f, 1), a = i * 2.399963f + time * 2;
                float radius = (.12f + p * .5f) * heat;
                Gem(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, p * .5f), .025f * heat, new Color(.6f, 1, .1f, (1-p)*heat*.7f));
            }
        }
        void Quad(int a, int b) { triangles.Add(a); triangles.Add(b); triangles.Add(a+1); triangles.Add(a+1); triangles.Add(b); triangles.Add(b+1); }
        void Gem(Vector3 p, float r, Color c)
        {
            int first = vertices.Count;
            vertices.Add(p + Vector3.up*r); vertices.Add(p + Vector3.right*r); vertices.Add(p - Vector3.up*r); vertices.Add(p - Vector3.right*r);
            vertices.Add(p + Vector3.forward*r); vertices.Add(p - Vector3.forward*r);
            for (int i=0;i<6;i++){colors.Add(c);uv.Add(new Vector2(.5f,.5f));}
            for (int i=0;i<4;i++) { int n=(i+1)%4; triangles.Add(first+i); triangles.Add(first+n); triangles.Add(first+4); triangles.Add(first+n); triangles.Add(first+i); triangles.Add(first+5); }
        }
        static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f, 1);
        void OnDisable() => Hide();
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
    /// <summary>One pooled emitter, no mesh rebuilding or allocations during an active breath.
    /// Particle color, material and flipbook come from the authored prefab; positions fill the damage fan.</summary>
    internal sealed class PrefabBreath
    {
        public GameObject Root {get;}
        readonly ParticleSystem[] systems;
        readonly ParticleSystem.Particle[][] particles;
        readonly Transform parent;
        bool active;
        public PrefabBreath(GameObject prefab,Transform parent,int budget)
        {
            this.parent=parent;Root=Object.Instantiate(prefab,parent);Root.name=prefab.name+" (Soul Eater breath)";
            Root.transform.localPosition=Vector3.zero;Root.transform.localRotation=Quaternion.identity;Root.transform.localScale=Vector3.one;
            systems=Root.GetComponentsInChildren<ParticleSystem>(true);particles=new ParticleSystem.Particle[systems.Length][];
            int perSystem=Mathf.Max(1,Mathf.Clamp(budget,16,128)/Mathf.Max(1,systems.Length));
            for(int i=0;i<systems.Length;i++)
            {
                var ps=systems[i];ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.playOnAwake=false;main.loop=true;main.stopAction=ParticleSystemStopAction.None;
                main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                main.maxParticles=perSystem;main.startSpeed=0;main.gravityModifier=0;
                var emission=ps.emission;emission.enabled=true;emission.rateOverTime=perSystem/Mathf.Max(.1f,main.startLifetime.constantMax);
                var shape=ps.shape;shape.enabled=false;
                var collision=ps.collision;collision.enabled=false;var lights=ps.lights;lights.enabled=false;
                particles[i]=new ParticleSystem.Particle[perSystem];
            }
            Root.SetActive(false);
        }
        public void Show(float reach,float angle,float dt,float floor,float baseWidth,float backreach)
        {
            bool start=!active;active=true;if(start)Root.SetActive(true);
            float cone=Mathf.Tan(angle*Mathf.Deg2Rad);
            for(int s=0;s<systems.Length;s++)
            {
                var ps=systems[s];var buffer=particles[s];
                // Explicit simulation also works in the editor's combat workshop and deterministic tests.
                ps.Simulate(start?Mathf.Max(.1f,ps.main.startLifetime.constantMax):Mathf.Max(0,dt),false,start,false);
                int count=ps.GetParticles(buffer);
                for(int i=0;i<count;i++)
                {
                    var p=buffer[i];float t=Mathf.Clamp01(1-p.remainingLifetime/Mathf.Max(.01f,p.startLifetime));
                    float seed=p.randomSeed%8191;float z=t*reach;
                    float width=Mathf.Max(.3f,baseWidth)+z*cone;
                    float x=(Hash(seed)*2-1)*width*.85f;
                    float vertical=Hash(seed+19);
                    var local=new Vector3(x,(vertical-.5f)*(.5f+z*cone),z);
                    var world=parent.TransformPoint(local);
                    if(!float.IsNaN(floor))
                    {
                        world.y=Mathf.Lerp(floor+.2f,Mathf.Max(floor+.3f,world.y+.4f),vertical);
                        world-=Vector3.ProjectOnPlane(parent.forward,Vector3.up).normalized*backreach*(1-vertical)*(1-t);
                    }
                    p.position=ps.transform.InverseTransformPoint(world);p.velocity=Vector3.zero;
                    float size=Mathf.Lerp(.8f,2.8f,t)*(.8f+Hash(seed+43)*.4f);
                    p.startSize3D=new Vector3(size,size,size*1.65f);
                    buffer[i]=p;
                }
                ps.SetParticles(buffer,count);
            }
        }
        static float Hash(float n)=>Mathf.Repeat(Mathf.Sin(n*127.1f+311.7f)*43758.5453f,1);
        public void Hide()
        {
            if(!active)return;active=false;
            foreach(var ps in systems)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            Root.SetActive(false);
        }
    }

}
