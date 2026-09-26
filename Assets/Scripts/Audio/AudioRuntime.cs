using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-500)]
public sealed class AudioRuntime : MonoBehaviour
{
    public static AudioRuntime Instance {get;private set;}
    public static AudioMixer Mixer => Mismo.Core.ProjectAssets.Load<AudioMixer>("Audio/AudioMixer");
    public static AudioMixerGroup SfxGroup => Mixer != null ? Mixer.FindMatchingGroups("SFX").FirstOrDefault(g=>g.name=="SFX") : null;

    public static void PlayWorldSFX(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null || volume <= 0f) return;
        var go = new GameObject("World SFX");
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.volume = Mathf.Clamp01(volume);
        source.outputAudioMixerGroup = SfxGroup;
        source.PlayOneShot(clip);
        UnityEngine.Object.Destroy(go,Mathf.Max(0.05f,clip.length + 0.05f));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if(Instance==null)new GameObject("Audio service").AddComponent<AudioRuntime>();
    }
    void Awake()
    {
        if(Instance!=null && Instance!=this){Destroy(gameObject);return;}
        Instance=this; DontDestroyOnLoad(gameObject);
        var manager=gameObject.AddComponent<AudioManager>();
        manager.Setup(Source("Music",true),Source("SFX",false),Source("UI",false));
    }
    AudioSource Source(string group, bool loop)
    {
        var source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=loop;
        source.outputAudioMixerGroup=Mixer != null ? Mixer.FindMatchingGroups(group).FirstOrDefault(g=>g.name==group) : null;
        return source;
    }
    void Start()=>VolumeSettings.ApplySaved(Mixer);
    void OnApplicationPause(bool paused){if(paused)PlayerPrefs.Save();}
    void OnApplicationQuit()=>PlayerPrefs.Save();
    void OnDestroy(){if(Instance==this)Instance=null;}
}
