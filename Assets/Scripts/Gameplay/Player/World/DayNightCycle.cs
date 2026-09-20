using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mismo.Gameplay.Player.World
{
    [DefaultExecutionOrder(100)]
    public sealed class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Current{get;private set;}
        public DayNightSettings settings;
        [Range(0,24)] public float hour=9;
        public string Phase{get;private set;}
        /// <summary>Brightness multiplier used by screen-space world previews such as the minimap.</summary>
        public float MinimapBrightness
        {
            get
            {
                float elevation=Mathf.Sin((hour-6)*Mathf.PI/12f);
                return Mathf.Lerp(.38f,1f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.18f,.22f,elevation)));
            }
        }
        Material sky;
        Light sun,moon;
        public void Initialize(DayNightSettings value)
        {
            settings=value;if(settings==null)return;Current=this;
            hour=WorldSession.Current!=null&&WorldSession.Current.hasTimeOfDay?WorldSession.Current.timeOfDay:settings.startingHour;
            sun=RenderSettings.sun;
            if(sun==null)sun=Object.FindObjectsByType<Light>().FirstOrDefault(l=>l.type==LightType.Directional&&l.gameObject.scene==gameObject.scene);
            if(sun==null){var go=new GameObject("Sun");go.transform.SetParent(transform,false);sun=go.AddComponent<Light>();sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;}
            var moonObject=new GameObject("Moon light");moonObject.transform.SetParent(transform,false);moon=moonObject.AddComponent<Light>();moon.type=LightType.Directional;moon.shadows=LightShadows.Soft;
            sky=new Material(Shader.Find("Mismo/Blended Skybox")){name="Day-night sky (runtime)"};RenderSettings.skybox=sky;RenderSettings.sun=sun;
            var camera=UnityEngine.Camera.main;if(camera!=null)camera.clearFlags=CameraClearFlags.Skybox;
            Apply();
        }
        void LateUpdate()=>Advance(Time.deltaTime);
        public void Advance(float seconds){if(settings==null||sky==null)return;if(settings.running)hour=Mathf.Repeat(hour+Mathf.Max(0,seconds)*24/(Mathf.Max(1,settings.cycleMinutes)*60),24);Apply();}
        public void SetHour(float value){hour=Mathf.Repeat(value,24);Apply();}
        public void Apply()
        {
            if(settings==null||sky==null||sun==null)return;
            settings.Sample(hour,out var a,out var b,out float blend);if(a==null||b==null)return;
            Phase=blend<.5f?a.name:b.name;
            sky.SetTexture("_SkyA",a.sky);sky.SetTexture("_SkyB",b.sky);sky.SetFloat("_Blend",blend);
            sky.SetFloat("_ExposureA",a.exposure);sky.SetFloat("_ExposureB",b.exposure);sky.SetFloat("_Rotation",settings.skyRotation);
            sun.color=Color.Lerp(a.sunColor,b.sunColor,blend);sun.intensity=Mathf.Lerp(a.sunIntensity,b.sunIntensity,blend);
            float elevation=Mathf.Sin((hour-6)*Mathf.PI/12);
            sun.intensity*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f,.18f,elevation));
            sun.transform.rotation=Quaternion.Euler(hour*15-90,settings.sunAzimuth,0);
            moon.transform.rotation=Quaternion.Euler(hour*15+90,settings.sunAzimuth,0);
            moon.color=settings.moonColor;moon.intensity=settings.moonIntensity*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,-.18f,elevation));
            var ambient=Color.Lerp(a.ambientColor,b.ambientColor,blend);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=ambient;RenderSettings.ambientEquatorColor=ambient*.75f;RenderSettings.ambientGroundColor=ambient*.4f;
            RenderSettings.fogColor=Color.Lerp(a.fogColor,b.fogColor,blend);
            RenderSettings.reflectionIntensity=Mathf.Lerp(.2f,1,Mathf.Clamp01(sun.intensity));
        }
        void OnDestroy(){if(Current==this)Current=null;if(sky!=null){if(RenderSettings.skybox==sky)RenderSettings.skybox=null;Destroy(sky);}}
    }
}
