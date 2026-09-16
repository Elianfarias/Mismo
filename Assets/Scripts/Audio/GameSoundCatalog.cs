using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameSound
{
    MenuOpen, MenuClose, Hover, Click, TabChanged, CraftSuccess, CraftFailed,
    ExperienceGained, LevelUp, WeaponDrop, LootCollected, ItemEquipped,
    ConsumableUsed, ResourceCollected, RegionDiscovered, SpeciesDiscovered, SaveFailed,
    FootstepWalk, FootstepRun, Jump, Land
}

[CreateAssetMenu(menuName = "Mismo/Audio/Catalogo de sonidos")]
public sealed class GameSoundCatalog : ScriptableObject
{
    public const string ResourcePath = "Audio/GameSounds";
    [Header("Pasos (distancia recorrida entre sonidos)")]
    [Min(.1f)] public float walkStepDistance = 2f;
    [Min(.1f)] public float runStepDistance = 2.5f;
    [Header("Musica")]
    public AudioClip explorationMusic;
    public AudioClip combatMusic;
    public AudioClip menuMusic;
    public AudioClip meadowMusic;
    public AudioClip forestMusic;
    public AudioClip highlandsMusic;
    public AudioClip villageMusic;
    [Range(0, 128)] public float villageMusicMargin = 25f;
    [Range(0, 10)] public float musicZoneDelay = 2f;
    [Serializable]
    public sealed class Entry
    {
        public GameSound sound;
        public AudioClip clip;
        [Range(0, 1)] public float volume = .7f;
        [Min(0)] public float cooldown = .08f;
    }
    public List<Entry> entries = new List<Entry>();
    public Entry Find(GameSound sound) => entries.Find(e => e != null && e.sound == sound);
    public void EnsureEvents()
    {
        foreach (GameSound sound in Enum.GetValues(typeof(GameSound)))
            if (Find(sound) == null) entries.Add(new Entry { sound = sound });
    }
}
