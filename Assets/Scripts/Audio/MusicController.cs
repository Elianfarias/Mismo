using UnityEngine;
public class MusicController : MonoBehaviour
{
    [SerializeField] private AudioClip backgroundMusic;
    private void Start()
    {
        if (backgroundMusic != null)
            AudioEvents.RaisePlayMusic(backgroundMusic);
    }
    private void OnDestroy()
    {
        AudioEvents.RaiseStopMusic();
    }
}