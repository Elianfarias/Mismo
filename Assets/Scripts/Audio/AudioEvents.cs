using System;
using UnityEngine;
public static class AudioEvents
{
    // ── Music ────────────────────────────────────────────────
    public static event Action<AudioClip> OnPlayMusic;
    public static event Action OnStopMusic;
    // ── SFX ──────────────────────────────────────────────────
    public static event Action<AudioClip> OnPlaySFX;
    public static event Action<AudioClip> OnPlayLoopedSFX;
    public static event Action OnStopLoopedSFX;
    // ── UI ───────────────────────────────────────────────────
    public static event Action<AudioClip> OnPlayUI;

    // ── Global ───────────────────────────────────────────────
    public static event Action OnStopAll;

    // ── helpers ─────────────────────────────────────────
    public static void RaisePlayMusic(AudioClip clip) => OnPlayMusic?.Invoke(clip);
    public static void RaiseStopMusic() => OnStopMusic?.Invoke();
    public static void RaisePlaySFX(AudioClip clip) => OnPlaySFX?.Invoke(clip);
    public static void RaisePlayLoopedSFX(AudioClip clip) => OnPlayLoopedSFX?.Invoke(clip);
    public static void RaiseStopLoopedSFX() => OnStopLoopedSFX?.Invoke();
    public static void RaisePlayUI(AudioClip clip) => OnPlayUI?.Invoke(clip);
    public static void RaiseStopAll() => OnStopAll?.Invoke();
}