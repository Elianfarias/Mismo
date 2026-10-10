using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public enum SoulEaterCue { Bite, Tail, Inhale, Roar, Jump, Charge, Land, Wing, Footstep, Hurt, Death, Emerge }
    public sealed class SoulEaterEffects : MonoBehaviour
    {
        [SerializeField] SoulEaterFlameVfx flame;
        [SerializeField] Light mouthLight;
        [SerializeField] SkinnedMeshRenderer body;
        [SerializeField] ParticleSystem groundDust;
        [SerializeField] AudioSource voice, fire;
        [SerializeField] AudioClip[] cues;
        [SerializeField] SoulEaterAudioProfile audioProfile;
        [SerializeField] AudioSource foley, reaction;
        MaterialPropertyBlock eyeBlock;
        bool audioPaused;
        float groundTimer;
        bool breathing, fireFading, fireLooping;
        float fireFade, nextHurt, nextWing;
        AnimationClip movementClip;
        float movementPhase;
        int movementKind;
        Vector3 movementPosition;
        public SoulEaterAudioProfile AudioProfile => audioProfile;
        public event System.Action<SoulEaterCue, AudioClip> CuePlayed;
        public SoulEaterFlameVfx Flame => flame;
        public SoulEaterDustVfx Dust {get;private set;}
        public void ConfigureDust(SoulEaterPhaseOneSettings settings)
        {Dust=GetComponent<SoulEaterDustVfx>()??gameObject.AddComponent<SoulEaterDustVfx>();Dust.Configure(settings);}
        public void DustImpact(Vector3 point,SoulEaterDustKind kind)
        {Dust?.Play(point,kind);if(Time.time>=groundTimer){Cue(SoulEaterCue.Land);groundTimer=Time.time+.3f;}}
        public void Configure(SoulEaterFlameVfx f, Light light, SkinnedMeshRenderer skin, ParticleSystem dust, AudioSource vocal, AudioSource loop, AudioClip[] clips)
        { flame=f;mouthLight=light;body=skin;groundDust=dust;voice=vocal;fire=loop;cues=clips; }
        public void ConfigureAudio(SoulEaterAudioProfile profile, AudioSource movement, AudioSource hurt)
        {audioProfile=profile;foley=movement;reaction=hurt;}
        void Awake()
        {
            var group=AudioRuntime.SfxGroup;
            foreach(var source in new[]{voice,fire,foley,reaction})
                if(source!=null){source.outputAudioMixerGroup=group;source.dopplerLevel=0;}
        }
        public void Prepare(Vector3 p, Vector3 d, float heat, float dt)
        {
            flame?.Show(p,d,0,0,heat,dt);
            if(mouthLight!=null){mouthLight.transform.position=p;mouthLight.intensity=heat*2;mouthLight.enabled=heat>.02f;}
            if(heat>0)EyeIntensity(1+heat);
        }
        public void Breath(Vector3 p, Vector3 d, float reach, float angle, float dt, float floorY=float.NaN, float baseWidth=0, float backreach=0)
        {
            SynchronizePause();
            flame?.Show(p,d,reach,angle,1,dt,floorY,baseWidth,backreach);
            if(mouthLight!=null){mouthLight.transform.position=p+d*.3f;mouthLight.intensity=2.8f;mouthLight.enabled=true;}
            if(fire==null||audioPaused||Time.timeScale<=0)return;
            if(audioProfile==null){if(!fire.isPlaying)fire.Play();return;}
            if(!breathing)
            {
                breathing=true;fireFading=false;fireLooping=audioProfile.fireStart==null;
                fire.Stop();fire.clip=fireLooping?audioProfile.fireLoop:audioProfile.fireStart;
                fire.loop=fireLooping;fire.volume=audioProfile.fireVolume;
                // The windup recording must not overlap a shorter aerial warning.
                if(voice!=null&&audioProfile.TryGet(SoulEaterCue.Inhale,out var inhale)&&voice.clip==inhale.clip)voice.Stop();
                if(fire.clip!=null)fire.Play();
            }
            else if(!fire.isPlaying&&!fireLooping)
            {fireLooping=true;fire.clip=audioProfile.fireLoop;fire.loop=true;if(fire.clip!=null)fire.Play();}
        }
        public void StopBreath()
        {
            flame?.Hide();if(mouthLight!=null)mouthLight.enabled=false;
            if(fire!=null)
            {
                if(audioProfile!=null&&breathing&&fire.isPlaying){fireFading=true;fireFade=0;}
                else if(!fireFading)fire.Stop();
            }
            breathing=false;
        }
        public void EyeIntensity(float amount)
        {
            if(body==null)return;eyeBlock??=new MaterialPropertyBlock();
            eyeBlock.SetColor("_EmissionColor",new Color(.12f,.25f,.01f)*amount);
            body.SetPropertyBlock(eyeBlock,body.sharedMaterials.Length-1);
        }
        public void Cue(SoulEaterCue cue)
        {
            SynchronizePause();
            if(audioProfile==null)
            {if(voice!=null&&cues!=null&&(int)cue<cues.Length&&cues[(int)cue]!=null){voice.pitch=1;voice.PlayOneShot(cues[(int)cue]);}return;}
            if(audioPaused||Time.timeScale<=0||!audioProfile.TryGet(cue,out var sound)||sound.volume<=0)return;
            bool wings=cue==SoulEaterCue.Wing||cue==SoulEaterCue.Jump;
            if(wings&&Time.time<nextWing)return;
            if(cue==SoulEaterCue.Hurt&&Time.time<nextHurt)return;
            var source=cue==SoulEaterCue.Hurt?reaction:
                wings||cue==SoulEaterCue.Footstep||cue==SoulEaterCue.Land?foley:voice;
            if(source==null)return;
            if(wings)nextWing=Time.time+.35f;
            if(cue==SoulEaterCue.Hurt)nextHurt=Time.time+audioProfile.hurtCooldown;
            source.pitch=1;source.volume=sound.volume;source.clip=sound.clip;source.loop=false;source.Play();
            CuePlayed?.Invoke(cue,sound.clip);
        }
        public void CancelVoice(){if(voice!=null)voice.Stop();}
        public void AnimateMovement(AnimationClip clip,float phase,bool wings,bool steps)
        {
            if(audioProfile==null)return;
            int kind=wings?1:steps?2:0;
            bool moved=(transform.position-movementPosition).sqrMagnitude>.000001f;
            if(kind!=0&&kind==movementKind&&clip==movementClip)
            {
                bool Crossed(float at)=>phase>=movementPhase?movementPhase<at&&phase>=at:movementPhase<at||phase>=at;
                if(wings&&Crossed(audioProfile.wingBeatPhase))Cue(SoulEaterCue.Wing);
                else if(steps&&moved&&(Crossed(audioProfile.firstFootstepPhase)||Crossed(audioProfile.secondFootstepPhase)))Cue(SoulEaterCue.Footstep);
            }
            movementClip=clip;movementPhase=phase;movementKind=kind;movementPosition=transform.position;
        }
        public void GroundImpact(Vector3 position,float strength)
        {
            if(groundDust!=null){groundDust.transform.position=position+Vector3.up*.12f;groundDust.Emit(Mathf.RoundToInt(22*strength));}
            if(Time.time>=groundTimer){Cue(SoulEaterCue.Land);groundTimer=Time.time+.3f;}
        }
        public void StopAll()
        {
            Dust?.Clear();StopBreath();breathing=fireFading=fireLooping=false;movementClip=null;movementKind=0;
            nextWing=nextHurt=groundTimer=0;
            foreach(var source in new[]{voice,fire,foley,reaction})if(source!=null)source.Stop();
            if(groundDust!=null)groundDust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);EyeIntensity(1);
        }
        void SynchronizePause()
        {
            bool paused=Time.timeScale<=0;
            if(paused!=audioPaused)
            {
                audioPaused=paused;
                foreach(var source in new[]{voice,fire,foley,reaction})
                    if(source!=null){if(paused)source.Pause();else source.UnPause();}
            }
        }
        void Update()
        {
            SynchronizePause();
            if(!audioPaused&&fireFading&&fire!=null)
            {
                fireFade+=Time.deltaTime;float p=Mathf.Clamp01(fireFade/Mathf.Max(.02f,audioProfile.fireFadeOut));
                fire.volume=audioProfile.fireVolume*(1-p);
                if(p>=1){fire.Stop();fireFading=false;}
            }
        }
        void OnDisable()=>StopAll();
    }
}
