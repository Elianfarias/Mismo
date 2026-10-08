using System;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    [CreateAssetMenu(menuName = "Mismo/Audio/Soul Eater", fileName = "SoulEaterAudio")]
    public sealed class SoulEaterAudioProfile : ScriptableObject
    {
        [Serializable]
        public struct Sound
        {
            public SoulEaterCue cue;
            public AudioClip clip;
            [Range(0, 1)] public float volume;
        }

        [Tooltip("Cambiar cada clip o volumen sin modificar las habilidades del boss.")]
        public Sound[] sounds = Array.Empty<Sound>();
        [Header("Aliento de fuego")]
        public AudioClip fireStart;
        public AudioClip fireLoop;
        [Range(0, 1)] public float fireVolume = .65f;
        [Range(.02f, .3f)] public float fireFadeOut = .08f;
        [Header("Ritmo y mezcla")]
        [Min(.1f)] public float hurtCooldown = .65f;
        [Range(0, 1)] public float wingBeatPhase = .25f;
        [Range(0, 1)] public float firstFootstepPhase = .2f;
        [Range(0, 1)] public float secondFootstepPhase = .7f;

        public bool TryGet(SoulEaterCue cue, out Sound sound)
        {
            if (sounds != null)
                foreach (var entry in sounds)
                    if (entry.cue == cue) { sound = entry; return entry.clip != null; }
            sound = default;
            return false;
        }
    }
}
