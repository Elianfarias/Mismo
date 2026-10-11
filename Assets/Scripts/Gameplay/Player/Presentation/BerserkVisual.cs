using Mismo.Gameplay.Player.Equipment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Modo Berserker: both axe heads lit and shedding embers, rings on the ground and a red screen edge (drawn by
    /// PlayerHUD). Everything grows with the fury: the share of the mode already spent.
    /// Presentation only; the weapons' own materials and hierarchy are never touched.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(560)]
    public sealed class BerserkVisual : MonoBehaviour
    {
        sealed class Ring { public MeshRenderer renderer; public float age, duration; public bool entry; public float fury; }
        const int RingCount=4, AxeCount=2;
        const float EndingSeconds=1.5f, EntryKickSeconds=.35f;
        // Where along the handle (trailBase → trailTip) the embers leave the axe: its head.
        const float HeadAlongHandle=.9f;
        readonly Ring[] rings=new Ring[RingCount];
        readonly WeaponHeadGlow[] glows={new WeaponHeadGlow(),new WeaponHeadGlow()};
        readonly ParticleSystem[] embers=new ParticleSystem[AxeCount];
        GameObject root;
        WeaponSkillEffects skills; WeaponPresentation presentation; EquipmentLoadout loadout; Collider body;
        BuffPresentation profile; MaterialPropertyBlock ringProperties;
        float presence, elapsed, ringTimer, kick, lastRemaining, beatClock; bool wasActive;
        BerserkAction mode; AudioSource embersAudio; int lastBeat;
        static readonly int ColorId=Shader.PropertyToID("_Color");
        public float Fury {get;private set;}
        // Opacity of the red screen edge; PlayerHUD draws it.
        public float ScreenEdge {get;private set;}
        // Embers per second over both axes.
        public float EmberRate {get;private set;}
        // Axes currently shedding embers.
        public int EmberEmitters {get;private set;}
        public int GlowRenderers {get;private set;}
        public int ActiveRings {get;private set;}
        public int HeartbeatsPlayed {get;private set;}
        public int EndsPlayed {get;private set;}
        // The volume the embers loop is asked to play at (0 when stopped); batchmode may run without an audio device.
        public float EmbersVolume {get;private set;}
        public static float FuryAt(float remaining,float duration)=>duration>0?Mathf.Clamp01(1-remaining/duration):1;
        public static float EmberRateAt(BuffPresentation profile,float fury)=>Mathf.Lerp(profile.emberRate.x,profile.emberRate.y,fury);
        public static float RingIntervalAt(BuffPresentation profile,float fury)=>Mathf.Max(.1f,Mathf.Lerp(profile.ringInterval.x,profile.ringInterval.y,fury));
        void Awake()=>skills=GetComponent<WeaponSkillEffects>();
        void LateUpdate()=>Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            dt=Mathf.Max(0,dt);
            if(profile==null)profile=BuffPresentation.Current;
            if(profile==null||profile.material==null){Clear();return;}
            float remaining=0,duration=0;
            bool active=skills!=null&&skills.isActiveAndEnabled&&skills.BerserkTimer(out remaining,out duration);
            if(active)mode=skills.ActiveBerserk;
            // Only a mode that ran out sounds its end; dying or switching weapons cuts it silently.
            if(wasActive&&!active&&lastRemaining<.3f&&mode!=null&&mode.endSfx!=null&&mode.endVolume>0)
            {AudioEvents.RaisePlayAbilitySFX(mode.endSfx,mode.endVolume);EndsPlayed++;}
            lastRemaining=active?remaining:0;
            presence=Mathf.MoveTowards(presence,active?1:0,dt/Mathf.Max(.05f,profile.fadeSeconds));
            // Once the mode is over, the embers already in the air finish their flight.
            if(presence<=0&&(root==null||!EmbersAlive())){Clear();return;}
            if(root==null)Create();
            if(root.scene!=gameObject.scene)SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            elapsed+=dt;
            if(active)Fury=FuryAt(remaining,duration);
            if(active&&!wasActive){Spawn(true);kick=EntryKickSeconds;ringTimer=0;}
            wasActive=active;
            float strength=presence*Ending(active,remaining);
            // The pulse integrates its own tempo, so the axes' glow and the heartbeat sound stay on the same count as it speeds up.
            beatClock+=dt*(1.1f+.9f*Fury);
            float fury=Mathf.Lerp(.35f,1,Fury),beat=Heartbeat.At(beatClock,1);
            Sound(active,strength);
            if(body==null)body=GetComponent<Collider>();
            var bounds=body!=null&&body.enabled?body.bounds:new Bounds(transform.position+Vector3.up*.9f,new Vector3(.6f,1.8f,.6f));
            Vector3 feet=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            root.transform.position=feet;
            Axes(strength,beat);
            if(active){ringTimer+=dt;if(ringTimer>=RingIntervalAt(profile,Fury)){ringTimer=0;Spawn(false);}}
            Rings(dt,feet,strength);
            kick=Mathf.Max(0,kick-dt);
            float edge=strength*fury*Mathf.Clamp01(.55f+.45f*beat);
            ScreenEdge=profile.screenEdgeMax*Mathf.Clamp01(Mathf.Max(edge,kick/EntryKickSeconds));
        }
        void Sound(bool active,float strength)
        {
            int count=Mathf.FloorToInt(beatClock);
            if(count!=lastBeat)
            {
                lastBeat=count;
                if(active&&mode!=null&&mode.heartbeatSfx!=null&&mode.heartbeatVolume>0&&strength>0)
                {AudioEvents.RaisePlayAbilitySFX(mode.heartbeatSfx,mode.heartbeatVolume*Mathf.Clamp01(strength));HeartbeatsPlayed++;}
            }
            var clip=mode!=null?mode.embersSfx:null;
            float volume=clip!=null?mode.embersVolume*strength*Mathf.Lerp(.2f,1,Fury):0;
            EmbersVolume=Mathf.Clamp01(volume);
            if(volume<=0){if(embersAudio!=null&&embersAudio.isPlaying)embersAudio.Stop();return;}
            if(embersAudio==null)
            {
                embersAudio=gameObject.AddComponent<AudioSource>();
                embersAudio.playOnAwake=false;embersAudio.loop=true;embersAudio.spatialBlend=0;
                embersAudio.outputAudioMixerGroup=AudioRuntime.SfxGroup;
            }
            if(embersAudio.clip!=clip)embersAudio.clip=clip;
            embersAudio.volume=Mathf.Clamp01(volume);
            if(!embersAudio.isPlaying)embersAudio.Play();
        }
        // The last seconds dim and flicker: the mode is about to end.
        static float Ending(bool active,float remaining)
        {
            if(!active||remaining>=EndingSeconds)return 1;
            float r=1-remaining/EndingSeconds;
            return (1-r*.6f)*(Mathf.Sin(remaining*30)>-.2f?1:.3f);
        }
        // Both axe heads glow and shed embers; an axe out of the hand (thrown) does neither.
        void Axes(float strength,float beat)
        {
            // Resolved late: the runner adds this component before the loadout adds its presentation.
            if(presentation==null)presentation=GetComponent<WeaponPresentation>();
            if(loadout==null)loadout=GetComponent<EquipmentLoadout>();
            var pose=loadout!=null&&loadout.ActiveDefinition!=null?loadout.ActiveDefinition.poseProfile:null;
            var color=profile.axeGlowColor;color.a=Mathf.Clamp01(profile.axeGlowIntensity*strength*(.45f+.45f*beat)*(.5f+.5f*Fury));
            GlowRenderers=EmberEmitters=0;EmberRate=0;
            float rate=EmberRateAt(profile,Fury)*strength/AxeCount;
            for(int i=0;i<AxeCount;i++)
            {
                var visual=presentation==null?null:i==0?presentation.ActiveVisual:presentation.ActiveSecondVisual;
                glows[i].Update(root.transform,visual,pose,profile.bladeGlowMaterial,profile.bladeGlowFrom,color);
                GlowRenderers+=glows[i].Renderers;
                var system=embers[i];
                if(system==null)continue;
                bool shedding=visual!=null&&pose!=null&&visual.gameObject.activeInHierarchy&&rate>0;
                var emission=system.emission;emission.rateOverTime=shedding?rate:0;
                if(!shedding)continue;
                system.transform.SetPositionAndRotation(visual.TransformPoint(Vector3.Lerp(pose.trailBase,pose.trailTip,HeadAlongHandle)),Quaternion.identity);
                var velocity=system.velocityOverLifetime;velocity.speedModifierMultiplier=Mathf.Lerp(.7f,1.5f,Fury);
                EmberRate+=rate;EmberEmitters++;
            }
        }
        bool EmbersAlive()
        {
            foreach(var system in embers)if(system!=null&&system.particleCount>0)return true;
            return false;
        }
        void Spawn(bool entry)
        {
            if(profile.furyRing==null)return;
            Ring slot=null;
            foreach(var ring in rings)if(slot==null||ring.age>=ring.duration||slot.age<slot.duration&&ring.age>slot.age)slot=ring;
            slot.age=0;slot.entry=entry;slot.fury=Fury;slot.duration=entry?.7f:.45f;
        }
        void Rings(float dt,Vector3 feet,float strength)
        {
            ActiveRings=0;
            foreach(var ring in rings)
            {
                ring.age+=dt;float p=ring.duration>0?ring.age/ring.duration:1;
                ring.renderer.enabled=p<1&&strength>0;
                if(!ring.renderer.enabled)continue;
                float radius=ring.entry?Mathf.Lerp(.25f,1.8f,p):Mathf.Lerp(.2f,.6f+.35f*ring.fury,p);
                ring.renderer.transform.SetPositionAndRotation(feet+Vector3.up*.05f,Quaternion.identity);
                ring.renderer.transform.localScale=Vector3.one*radius;
                var color=ring.entry?new Color(1,.55f,.16f,1):profile.furyColor;
                color.a=(1-p)*(ring.entry?1:.4f+.4f*ring.fury)*(ring.entry?1:strength);
                ringProperties.SetColor(ColorId,color);ring.renderer.SetPropertyBlock(ringProperties);ActiveRings++;
            }
        }
        void Create()
        {
            ringProperties=new MaterialPropertyBlock();
            root=new GameObject("Modo Berserker · furia"){hideFlags=HideFlags.DontSave};
            SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            if(profile.furyEmbers!=null)
                for(int i=0;i<AxeCount;i++)
                {
                    var go=Instantiate(profile.furyEmbers,root.transform);go.name="Brasas del hacha "+(i+1);
                    embers[i]=go.GetComponentInChildren<ParticleSystem>();
                    if(embers[i]==null)continue;
                    var emission=embers[i].emission;emission.rateOverTime=0;embers[i].Play(true);
                }
            for(int i=0;i<RingCount;i++)
            {
                var go=new GameObject("Onda "+(i+1));go.transform.SetParent(root.transform,false);go.layer=gameObject.layer;
                go.AddComponent<MeshFilter>().sharedMesh=profile.furyRing;var renderer=go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=profile.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;renderer.enabled=false;
                rings[i]=new Ring{renderer=renderer,age=1,duration=1};
            }
        }
        public void Clear()
        {
            foreach(var glow in glows)glow.Clear();
            if(root!=null){root.SetActive(false);if(Application.isPlaying)Destroy(root);else DestroyImmediate(root);}
            root=null;
            for(int i=0;i<AxeCount;i++)embers[i]=null;
            for(int i=0;i<RingCount;i++)rings[i]=null;
            if(embersAudio!=null&&embersAudio.isPlaying)embersAudio.Stop();
            EmbersVolume=0;
            presence=elapsed=ringTimer=kick=lastRemaining=beatClock=0;lastBeat=0;wasActive=false;mode=null;
            Fury=ScreenEdge=EmberRate=0;EmberEmitters=GlowRenderers=ActiveRings=0;
        }
        void OnDisable()=>Clear();
        void OnDestroy()=>Clear();
    }
}
