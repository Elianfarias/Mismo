using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeSettings : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider musicSlider, sfxSlider, uiSlider;
    public void Configure(AudioMixer mixer, Slider music, Slider sfx, Slider ui)
    { audioMixer=mixer; musicSlider=music; sfxSlider=sfx; uiSlider=ui; }
    private void Start()
    {
        musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Mismo.Music", .75f));
        sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Mismo.SFX", .75f));
        uiSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Mismo.UI", .75f));
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        uiSlider.onValueChanged.AddListener(SetUIVolume);
        ApplySaved(audioMixer);
    }
    public void SetMusicVolume(float value) => Apply("VolumeMusic", "Mismo.Music", value);
    public void SetSFXVolume(float value) => Apply("VolumeSFX", "Mismo.SFX", value);
    public void SetUIVolume(float value) => Apply("VolumeUI", "Mismo.UI", value);
    private void Apply(string parameter, string key, float value)
    {
        value=Mathf.Clamp01(value);
        if(audioMixer!=null) audioMixer.SetFloat(parameter, Decibels(value));
        PlayerPrefs.SetFloat(key,value);
    }
    public static float Decibels(float value) => value<=0 ? -80f : Mathf.Log10(Mathf.Clamp(value,.000001f,1))*10;
    public static void ApplySaved(AudioMixer mixer)
    {
        if(mixer==null)return;
        mixer.SetFloat("VolumeMusic",Decibels(PlayerPrefs.GetFloat("Mismo.Music",.75f)));
        mixer.SetFloat("VolumeSFX",Decibels(PlayerPrefs.GetFloat("Mismo.SFX",.75f)));
        mixer.SetFloat("VolumeUI",Decibels(PlayerPrefs.GetFloat("Mismo.UI",.75f)));
        mixer.SetFloat("VolumeSFXEnemies",0); // Remove the previous project's +20 dB boost.
    }
    private void OnDisable() => PlayerPrefs.Save();
    private void OnDestroy()
    {
        if(musicSlider!=null)musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        if(sfxSlider!=null)sfxSlider.onValueChanged.RemoveListener(SetSFXVolume);
        if(uiSlider!=null)uiSlider.onValueChanged.RemoveListener(SetUIVolume);
    }
}
