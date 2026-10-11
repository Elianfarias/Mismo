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
        // A timed mode (Berserker): it is shown on its ability icon only (frame and timer), never as a card next to the vitals.
        public bool mode;
        // While ready, its ability icon also shows the amber bar of seconds left (Tercer impacto, Tajo sangrante).
        public bool iconTimer;
        public int progress, goal;
        public float remaining, duration;
        public BuffView(BuffKind kind, string label, string detail, bool ready, bool worldVisible,
            float remaining = -1, float duration = 0, AbilityDefinition ability = null, int progress = 0, int goal = 0, bool mode = false, bool iconTimer = false)
        {
            this.kind=kind; this.label=label; this.detail=detail; this.ready=ready; this.worldVisible=worldVisible;
            this.remaining=remaining; this.duration=duration; this.ability=ability; this.progress=progress; this.goal=goal; this.mode=mode; this.iconTimer=iconTimer;
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
        [Header("Marca de sangrado (Tajo sangrante)")]
        [Tooltip("Símbolo de las gotas que orbitan mientras el próximo básico va a hacer sangrar.")]
        public Mesh bleedMarkSymbol;
        public Color bleedMarkColor=new Color(.77f,.12f,.18f,1);
        [Range(.1f,.6f)] public float bleedMarkSize=.22f;
        [Tooltip("Material aditivo (Mismo/Sword Blade Glow) que ilumina la cabeza del hacha.")]
        public Material bladeGlowMaterial;
        [Tooltip("Desde qué fracción del mango (trailBase → trailTip del perfil de pose) se ilumina el arma.")]
        [Range(0,1)] public float bladeGlowFrom=.3f;
        [Range(0,1)] public float bladeGlowIntensity=.8f;
        [Header("Modo Berserker")]
        [Tooltip("Brasas que sueltan las cabezas de las hachas; se instancia una por hacha.")]
        public GameObject furyEmbers;
        [Tooltip("Anillo plano de las ondas en el piso.")]
        public Mesh furyRing;
        [Tooltip("Gradiente del borde rojo de pantalla.")]
        public Texture2D furyVignette;
        public Color furyColor=new Color(1,.24f,.16f,1);
        public Color axeGlowColor=new Color(1,.29f,.16f,1);
        [Range(0,1)] public float axeGlowIntensity=.8f;
        [Tooltip("Brasas por segundo, sumando las dos hachas, al empezar (x) y con la furia al máximo (y).")]
        public Vector2 emberRate=new Vector2(10,56);
        [Tooltip("Segundos entre ondas al empezar (x) y con la furia al máximo (y).")]
        public Vector2 ringInterval=new Vector2(.9f,.45f);
        [Tooltip("Opacidad máxima del borde rojo de pantalla. 0 lo apaga.")]
        [Range(0,1)] public float screenEdgeMax=.4f;
        public static BuffPresentation Current=>ProjectAssets.Load<BuffPresentation>(CatalogKey);
        public Style For(BuffKind kind)
        { foreach(var style in styles)if(style!=null&&style.kind==kind)return style;return null; }
    }
}
