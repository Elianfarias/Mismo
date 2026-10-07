using System;
using Mismo.Core;
using Mismo.Gameplay.Player.Equipment;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public enum BuffKind { Shield, Damage, Defense, Speed }

    /// <summary>A read-only view of a gameplay-owned effect, including conditional next hits.</summary>
    public struct BuffView
    {
        public BuffKind kind;
        public string label, detail;
        public AbilityDefinition ability;
        public bool ready, worldVisible;
        public int progress, goal;
        public float remaining, duration;
        public BuffView(BuffKind kind, string label, string detail, bool ready, bool worldVisible,
            float remaining = -1, float duration = 0, AbilityDefinition ability = null, int progress = 0, int goal = 0)
        {
            this.kind=kind; this.label=label; this.detail=detail; this.ready=ready; this.worldVisible=worldVisible;
            this.remaining=remaining; this.duration=duration; this.ability=ability; this.progress=progress; this.goal=goal;
        }
    }

    [CreateAssetMenu(menuName="Mismo/Combat/Buff presentation")]
    public sealed class BuffPresentation : ScriptableObject
    {
        public const string CatalogKey="Combat/Buffs";
        [Serializable] public sealed class Style
        {
            public BuffKind kind;
            public Color color=Color.white;
            public Mesh symbol;
            public Texture2D icon;
            [Range(.1f,.6f)] public float size=.25f;
        }
        public Style[] styles=Array.Empty<Style>();
        public Material material;
        public Mesh footArc;
        [Range(0,1)] public float symbolOpacity=.48f;
        [Range(0,.3f)] public float footOpacity=.085f;
        [Range(5,60)] public float orbitDegreesPerSecond=24;
        [Range(.05f,.5f)] public float fadeSeconds=.2f;
        public static BuffPresentation Current=>ProjectAssets.Load<BuffPresentation>(CatalogKey);
        public Style For(BuffKind kind)
        { foreach(var style in styles)if(style!=null&&style.kind==kind)return style;return null; }
    }
}
