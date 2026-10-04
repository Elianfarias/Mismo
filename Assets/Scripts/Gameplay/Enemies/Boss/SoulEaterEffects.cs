using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public enum SoulEaterCue { Bite, Tail, Inhale, Roar, Jump, Charge, Land }
    public sealed class SoulEaterEffects : MonoBehaviour
    {
        [SerializeField] SoulEaterFlameVfx flame;
        [SerializeField] Light mouthLight;
        [SerializeField] SkinnedMeshRenderer body;
        [SerializeField] ParticleSystem groundDust;
        [SerializeField] AudioSource voice, fire;
        [SerializeField] AudioClip[] cues;
        MaterialPropertyBlock eyeBlock;
        bool audioPaused;
        float groundTimer;
        public SoulEaterFlameVfx Flame => flame;
        public void Configure(SoulEaterFlameVfx f, Light light, SkinnedMeshRenderer skin, ParticleSystem dust, AudioSource vocal, AudioSource loop, AudioClip[] clips)
        { flame=f;mouthLight=light;body=skin;groundDust=dust;voice=vocal;fire=loop;cues=clips; }
        public void Prepare(Vector3 p, Vector3 d, float heat, float dt)
        {
            flame?.Show(p,d,0,0,heat,dt);
            if(mouthLight!=null){mouthLight.transform.position=p;mouthLight.intensity=heat*2;mouthLight.enabled=heat>.02f;}
            if(heat>0)EyeIntensity(1+heat);
        }
        public void Breath(Vector3 p, Vector3 d, float reach, float angle, float dt, float floorY=float.NaN, float baseWidth=0, float backreach=0)
        {
            flame?.Show(p,d,reach,angle,1,dt,floorY,baseWidth,backreach);
            if(mouthLight!=null){mouthLight.transform.position=p+d*.3f;mouthLight.intensity=2.8f;mouthLight.enabled=true;}
            if(fire!=null&&!fire.isPlaying&&!audioPaused)fire.Play();
        }
        public void StopBreath(){flame?.Hide();if(mouthLight!=null)mouthLight.enabled=false;if(fire!=null)fire.Stop();}
        public void EyeIntensity(float amount)
        {
            if(body==null)return;eyeBlock??=new MaterialPropertyBlock();
            eyeBlock.SetColor("_EmissionColor",new Color(.12f,.25f,.01f)*amount);
            body.SetPropertyBlock(eyeBlock,body.sharedMaterials.Length-1);
        }
        public void Cue(SoulEaterCue cue)
        {if(voice!=null&&cues!=null&&(int)cue<cues.Length&&cues[(int)cue]!=null){voice.pitch=1;voice.PlayOneShot(cues[(int)cue]);}}
        public void GroundImpact(Vector3 position,float strength)
        {
            if(groundDust!=null){groundDust.transform.position=position+Vector3.up*.12f;groundDust.Emit(Mathf.RoundToInt(22*strength));}
            if(Time.time>=groundTimer){Cue(SoulEaterCue.Land);groundTimer=Time.time+.3f;}
        }
        public void StopAll(){StopBreath();if(voice!=null)voice.Stop();if(groundDust!=null)groundDust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);EyeIntensity(1);}
        void Update()
        {
            bool paused=Time.timeScale<=0;
            if(paused==audioPaused)return;audioPaused=paused;
            if(paused){voice?.Pause();fire?.Pause();}else{voice?.UnPause();fire?.UnPause();}
        }
        void OnDisable()=>StopAll();
    }
}
