using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public enum TutorialAnchor { None, Health, Stamina, Weapons, Skills, Basic, Q, E, R, Dash, Consumables }

    [Serializable]
    public sealed class TutorialPage
    {
        public string title;
        [TextArea(3, 8)] public string body;
        public TutorialAnchor highlight;
    }

    [CreateAssetMenu(menuName = "Mismo/Tutorial/Sequence")]
    public sealed class TutorialSequence : ScriptableObject
    {
        public string id;
        public string completionLabel="Jugar · Enter";
        [Tooltip("Marcadores disponibles: {q}, {e}, {r}, {focusCost}, {basicGain}.")]
        public TutorialPage[] pages = Array.Empty<TutorialPage>();
        public bool IsValid => !string.IsNullOrWhiteSpace(id) && pages != null && pages.Length > 0 &&
            Array.TrueForAll(pages, p => p != null && !string.IsNullOrWhiteSpace(p.title) && !string.IsNullOrWhiteSpace(p.body));
    }
}
