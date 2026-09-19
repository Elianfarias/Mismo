using UnityEngine;
public class MusicController : MonoBehaviour
{
    [SerializeField] private AudioClip backgroundMusic;
    private void Start()
    {
        var catalog = Mismo.Core.ProjectAssets.Load<GameSoundCatalog>(GameSoundCatalog.ResourcePath);
        if (catalog != null) backgroundMusic = catalog.menuMusic;
        if (backgroundMusic != null)
            AudioEvents.RaisePlayMusic(backgroundMusic);
    }
    private void OnDestroy()
    {
        AudioEvents.RaiseStopMusic();
    }
}
