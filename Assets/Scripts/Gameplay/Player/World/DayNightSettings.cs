using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    [Serializable] public sealed class SkyPhase
    {
        public string name;
        [Range(0,24)] public float hour;
        public Cubemap sky;
        [Min(0)] public float exposure=1,sunIntensity=1;
        public Color sunColor=Color.white,ambientColor=new Color(.55f,.65f,.75f),fogColor=new Color(.62f,.72f,.78f);
    }
    [CreateAssetMenu(menuName="Mismo/World/Day and night")]
    public sealed class DayNightSettings : ScriptableObject
    {
        public bool running=true;
        [Min(1)] public float cycleMinutes=30;
        [Range(0,24)] public float startingHour=9;
        [Range(0,360)] public float skyRotation=0,sunAzimuth=135;
        [Min(0)] public float moonIntensity=.3f;
        public Color moonColor=new Color(.65f,.75f,1);
        public SkyPhase[] phases=Array.Empty<SkyPhase>();
        public void Sample(float hour,out SkyPhase from,out SkyPhase to,out float blend)
        {
            from=null;to=null;blend=0;float previousDistance=25,nextDistance=25;hour=Mathf.Repeat(hour,24);
            foreach(var phase in phases)
            {
                if(phase==null||phase.sky==null)continue;
                float before=Mathf.Repeat(hour-phase.hour,24),after=Mathf.Repeat(phase.hour-hour,24);
                if(before<previousDistance){previousDistance=before;from=phase;}
                if(after<nextDistance){nextDistance=after;to=phase;}
            }
            if(from==null||to==null)return;
            blend=previousDistance+nextDistance<.0001f?0:Mathf.SmoothStep(0,1,previousDistance/(previousDistance+nextDistance));
        }
    }
}
