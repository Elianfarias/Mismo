using UnityEngine;
public class AudioManager : MonoBehaviour
{
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource uiSource;
    private AudioSource alternateMusic;
    private AudioSource activeMusic;
    private const float FadeSeconds = 1.5f;
    public void Setup(AudioSource music, AudioSource sfx, AudioSource ui)
    {
        musicSource = music;
        alternateMusic = gameObject.AddComponent<AudioSource>();
        alternateMusic.playOnAwake = false;
        alternateMusic.loop = true;
        alternateMusic.outputAudioMixerGroup = music.outputAudioMixerGroup;
        alternateMusic.volume = music.volume = 0f;
        sfxSource = sfx;
        uiSource = ui;
    }
    private void OnEnable()
    {
        AudioEvents.OnPlayMusic += PlayMusic;
        AudioEvents.OnStopMusic += StopMusic;
        AudioEvents.OnPlaySFX += PlaySFX;
        AudioEvents.OnPlayAbilitySFX += PlayAbilitySFX;
        AudioEvents.OnPlayLoopedSFX += PlayLoopedSFX;
        AudioEvents.OnStopLoopedSFX += StopLoopedSFX;
        AudioEvents.OnPlayUI += PlayUI;
        AudioEvents.OnPlayCue += PlayCue;
        AudioEvents.OnStopAll += StopAll;
    }
    private void OnDisable()
    {
        AudioEvents.OnPlayMusic -= PlayMusic;
        AudioEvents.OnStopMusic -= StopMusic;
        AudioEvents.OnPlaySFX -= PlaySFX;
        AudioEvents.OnPlayAbilitySFX -= PlayAbilitySFX;
        AudioEvents.OnPlayLoopedSFX -= PlayLoopedSFX;
        AudioEvents.OnStopLoopedSFX -= StopLoopedSFX;
        AudioEvents.OnPlayUI -= PlayUI;
        AudioEvents.OnPlayCue -= PlayCue;
        AudioEvents.OnStopAll -= StopAll;
    }
    private void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        if (activeMusic != null && activeMusic.clip == clip && activeMusic.isPlaying) return;
        var next = activeMusic == musicSource ? alternateMusic : musicSource;
        if (next.clip != clip || !next.isPlaying)
        {
            next.clip = clip;
            next.Play();
        }
        activeMusic = next;
    }
    private void Update()
    {
        Fade(musicSource);
        Fade(alternateMusic);
    }
    private void Fade(AudioSource source)
    {
        if (source == null) return;
        source.volume = Mathf.MoveTowards(source.volume, source == activeMusic ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
        if (source != activeMusic && source.volume == 0f && source.isPlaying) source.Stop();
    }
    private void StopMusic()
    {
        if (musicSource == null) return;
        activeMusic = null;
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
    private void PlayAbilitySFX(AudioClip clip, float volume)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, volume);
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
    private void PlayCue(AudioClip clip, float volume)
    {
        if (uiSource != null && clip != null) uiSource.PlayOneShot(clip, volume);
    }
    private void StopAll()
    {
        StopMusic();
        musicSource?.Stop();
        alternateMusic?.Stop();
        if (musicSource != null) musicSource.volume = 0;
        if (alternateMusic != null) alternateMusic.volume = 0;
        StopLoopedSFX();
        uiSource?.Stop();
    }
}
