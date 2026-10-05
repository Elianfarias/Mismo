using UnityEngine;
namespace Mismo.Gameplay.Enemies
{
    public enum SoulEaterDustKind { Dive, Charge, Landing }
    /// <summary>World-space, bounded one-shot instances of the authored dust explosion.</summary>
    public sealed class SoulEaterDustVfx : MonoBehaviour
    {
        sealed class Cloud
        {
            public GameObject root;
            public ParticleSystem[] systems;
            public ParticleSystem.Burst[][] bursts;
            public float expires;
        }
        readonly System.Collections.Generic.Dictionary<Material,Material> materials=new System.Collections.Generic.Dictionary<Material,Material>();
        Cloud[] clouds;
        Transform poolRoot;
        SoulEaterPhaseOneSettings settings;
        Vector3 sourceScale;Quaternion sourceRotation;
        int next;
        public int BurstCount {get;private set;}
        public SoulEaterDustKind LastKind {get;private set;}
        public Vector3 LastPosition {get;private set;}
        public GameObject LastCloud {get;private set;}
        public int Capacity=>clouds?.Length??0;
        public void Configure(SoulEaterPhaseOneSettings data,int capacity=2)
        {
            if(poolRoot!=null){poolRoot.gameObject.SetActive(false);Destroy(poolRoot.gameObject);}
            ReleaseMaterials();settings=data;clouds=null;next=0;
            if(data==null||data.dustExplosionPrefab==null)return;
            var prefab=data.dustExplosionPrefab;sourceScale=prefab.transform.localScale;sourceRotation=prefab.transform.localRotation;
            poolRoot=new GameObject("Soul Eater dust pool").transform;poolRoot.gameObject.SetActive(false);
            clouds=new Cloud[Mathf.Clamp(capacity,1,2)];
            for(int i=0;i<clouds.Length;i++)
            {
                var go=Instantiate(prefab,poolRoot);go.name="DustExplosion (pooled)";
                var systems=go.GetComponentsInChildren<ParticleSystem>(true);
                var cloud=new Cloud{root=go,systems=systems,bursts=new ParticleSystem.Burst[systems.Length][]};
                for(int s=0;s<systems.Length;s++)
                {
                    var ps=systems[s];ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=ps.main;main.loop=false;main.prewarm=false;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;
                    main.useUnscaledTime=false;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                    main.maxParticles=Mathf.Clamp(data.dustMaxParticlesPerSystem,16,128);
                    var emission=ps.emission;emission.rateOverTime=0;emission.rateOverDistance=0;
                    cloud.bursts[s]=new ParticleSystem.Burst[emission.burstCount];emission.GetBursts(cloud.bursts[s]);
                    var collision=ps.collision;collision.enabled=false;
                    var lights=ps.lights;lights.enabled=false;
                    var renderer=ps.GetComponent<ParticleSystemRenderer>();
                    if(renderer!=null)
                    {
                        // The source wave faces the camera like a disk. Align it to the authored
                        // emitter plane instead: local Z is world up, so the ring lies on the ground.
                        if(ps.name=="Shockwave")renderer.alignment=ParticleSystemRenderSpace.Local;
                        var sources=renderer.sharedMaterials;
                        for(int m=0;m<sources.Length;m++)
                        {
                            var source=sources[m];if(source==null)continue;
                            if(!materials.TryGetValue(source,out var material))
                            {
                                material=new Material(source){name=source.name+" (Soul Eater dust)"};materials.Add(source,material);
                                // Imported soft-particle fading assumes a camera depth texture. This camera
                                // can render without one. Neutralize fades on the owned copy; retain the
                                // shader keywords so the serialized prefab's build variants remain valid.
                                if(material.HasProperty("_SoftParticleFadeParams"))material.SetVector("_SoftParticleFadeParams",Vector4.zero);
                                if(material.HasProperty("_CameraFadeParams"))material.SetVector("_CameraFadeParams",new Vector4(-1000,1,0,0));
                                if(ps.name=="Shockwave")foreach(var colorName in new[]{"_BaseColor","_Color"})
                                    if(material.HasProperty(colorName)){var color=material.GetColor(colorName);color.a*=data.dustWaveOpacity;material.SetColor(colorName,color);}
                            }
                            sources[m]=material;
                        }
                        renderer.sharedMaterials=sources;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                    }
                }
                go.SetActive(false);clouds[i]=cloud;
            }
            poolRoot.gameObject.SetActive(true);
        }
        public void Play(Vector3 point,SoulEaterDustKind kind)
        {
            if(clouds==null||clouds.Length==0)return;
            var cloud=clouds[next];next=(next+1)%clouds.Length;
            Stop(cloud);
            float scale=kind==SoulEaterDustKind.Dive?settings.diveDustScale:kind==SoulEaterDustKind.Landing?settings.landingDustScale:settings.chargeDustScale;
            float density=kind==SoulEaterDustKind.Dive?settings.diveDustDensity:kind==SoulEaterDustKind.Landing?settings.landingDustDensity:settings.chargeDustDensity;
            cloud.root.transform.SetPositionAndRotation(point+Vector3.up*settings.dustGroundOffset,sourceRotation);
            cloud.root.transform.localScale=sourceScale*Mathf.Max(.01f,scale);
            for(int s=0;s<cloud.systems.Length;s++)
            {
                var ps=cloud.systems[s];var emission=ps.emission;
                // Rebuild each burst from the source values, never multiply a previous playback.
                for(int b=0;b<cloud.bursts[s].Length;b++)
                {
                    var burst=cloud.bursts[s][b];burst.cycleCount=1;
                    burst.count=new ParticleSystem.MinMaxCurve(Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(burst.count.constantMax,ps.main.maxParticles)*density),1,ps.main.maxParticles));
                    emission.SetBurst(b,burst);
                }
            }
            cloud.root.SetActive(true);foreach(var ps in cloud.systems)ps.Play(false);
            cloud.expires=Time.time+Mathf.Max(1,settings.dustMaximumLifetime);
            BurstCount++;LastKind=kind;LastPosition=point;LastCloud=cloud.root;
        }
        void Update()
        {
            if(clouds==null||Time.timeScale<=0)return;
            foreach(var cloud in clouds)
            {
                if(cloud.root==null||!cloud.root.activeSelf)continue;
                bool alive=false;foreach(var ps in cloud.systems)if(ps!=null)alive|=ps.IsAlive(false);
                if(!alive||Time.time>=cloud.expires)Stop(cloud);
            }
        }
        static void Stop(Cloud cloud)
        {
            // Scene unload can destroy the independent cloud root before its owning boss.
            foreach(var ps in cloud.systems)if(ps!=null)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(cloud.root!=null)cloud.root.SetActive(false);
        }
        public void Clear(){if(clouds!=null)foreach(var cloud in clouds)Stop(cloud);}
        void OnDisable()=>Clear();
        void ReleaseMaterials(){foreach(var material in materials.Values)Destroy(material);materials.Clear();}
        void OnDestroy(){if(poolRoot!=null)Destroy(poolRoot.gameObject);ReleaseMaterials();}
    }
}
