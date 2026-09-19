using System.Collections.Generic;
using UnityEngine;

/// <summary>Named one-shot cues. Missing assignments are intentionally silent.</summary>
public static class GameAudio
{
    static GameSoundCatalog catalog;
    static readonly Dictionary<GameSound, float> readyAt = new Dictionary<GameSound, float>();
    static readonly Dictionary<int, (bool inside, int frame)> hovered = new Dictionary<int, (bool, int)>();
    static int lastHoverFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        catalog = null; readyAt.Clear(); hovered.Clear(); lastHoverFrame = -1;
    }

    public static void Play(GameSound sound)
    {
        if (!Application.isPlaying || AudioRuntime.Instance == null) return;
        if (catalog == null) catalog = Mismo.Core.ProjectAssets.Load<GameSoundCatalog>(GameSoundCatalog.ResourcePath);
        var entry = catalog != null ? catalog.Find(sound) : null;
        if (entry == null || entry.clip == null || entry.volume <= 0) return;
        if (readyAt.TryGetValue(sound, out float time) && Time.unscaledTime < time) return;
        readyAt[sound] = Time.unscaledTime + Mathf.Max(0, entry.cooldown);
        if(sound >= GameSound.FootstepWalk && sound <= GameSound.Land)
            AudioEvents.RaisePlayAbilitySFX(entry.clip, Mathf.Clamp01(entry.volume));
        else AudioEvents.RaisePlayCue(entry.clip, Mathf.Clamp01(entry.volume));
    }

    // Call immediately BEFORE GUI.Button so its control ID remains stable across GUI events.
    public static void Hover(Rect rect)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive, rect);
        if (Event.current.type != EventType.Repaint) return;
        if (Time.frameCount - lastHoverFrame > 1 || hovered.Count > 2048) hovered.Clear();
        lastHoverFrame = Time.frameCount;
        bool inside = GUI.enabled && rect.Contains(Event.current.mousePosition);
        if (inside && (!hovered.TryGetValue(id, out var previous) || !previous.inside || Time.frameCount - previous.frame > 1)) Play(GameSound.Hover);
        hovered[id] = (inside, Time.frameCount);
    }

    public static bool Button(Rect rect, string text, GUIStyle style)
    {
        Hover(rect);
        bool clicked = GUI.Button(rect, text, style);
        if (clicked) Play(GameSound.Click);
        return clicked;
    }
}
