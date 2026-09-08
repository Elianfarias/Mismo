using UnityEngine;
public class AudioManager : MonoBehaviour
{
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource uiSource;
    public void Setup(AudioSource music, AudioSource sfx, AudioSource ui)
    {
        musicSource = music;
        sfxSource = sfx;
        uiSource = ui;
    }
    private void OnEnable()
    {
        AudioEvents.OnPlayMusic += PlayMusic;
        AudioEvents.OnStopMusic += StopMusic;
        AudioEvents.OnPlaySFX += PlaySFX;
        AudioEvents.OnPlayLoopedSFX += PlayLoopedSFX;
        AudioEvents.OnStopLoopedSFX += StopLoopedSFX;
        AudioEvents.OnPlayUI += PlayUI;
        AudioEvents.OnStopAll += StopAll;
    }
    private void OnDisable()
    {
        AudioEvents.OnPlayMusic -= PlayMusic;
        AudioEvents.OnStopMusic -= StopMusic;
        AudioEvents.OnPlaySFX -= PlaySFX;
        AudioEvents.OnPlayLoopedSFX -= PlayLoopedSFX;
        AudioEvents.OnStopLoopedSFX -= StopLoopedSFX;
        AudioEvents.OnPlayUI -= PlayUI;
        AudioEvents.OnStopAll -= StopAll;
    }
    private void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        if (musicSource.clip == clip) return;
        musicSource.clip = clip;
        musicSource.Play();
    }
    private void StopMusic()
    {
        if (musicSource == null) return;
        musicSource.Stop();
        musicSource.clip = null;
    }
    private void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }
    private void PlayLoopedSFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.clip = clip;
        sfxSource.loop = true;
        sfxSource.Play();
    }
    private void StopLoopedSFX()
    {
        if (sfxSource == null) return;
        sfxSource.loop = false;
        sfxSource.Stop();
    }
    private void PlayUI(AudioClip clip)
    {
        if (uiSource == null || clip == null) return;
        uiSource.PlayOneShot(clip);
    }
    private void StopAll()
    {
        StopMusic();
        StopLoopedSFX();
        uiSource?.Stop();
    }
}