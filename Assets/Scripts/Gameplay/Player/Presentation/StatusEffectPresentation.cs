using System;
using Mismo.Core;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    [CreateAssetMenu(menuName = "Mismo/Combat/Status effect presentation")]
    public sealed class StatusEffectPresentation : ScriptableObject
    {
        public const string CatalogKey = "Combat/StatusEffects";
        [Serializable] public sealed class DamageColor
        {
            public StatusEffectType status;
            public Color color = Color.white;
        }
        public DamageColor[] damageColors = {
            new DamageColor { status = StatusEffectType.Poison, color = new Color(.48f, 1f, .12f) },
            new DamageColor { status = StatusEffectType.Bleed, color = new Color(1f, .18f, .27f) },
            new DamageColor { status = StatusEffectType.Burn, color = new Color(1f, .48f, .08f) }
        };
        public Material poisonOverlay;
        public GameObject poisonParticles;
        [Range(0, 1)] public float overlayIntensity = .6f;
        public static StatusEffectPresentation Current => ProjectAssets.Load<StatusEffectPresentation>(CatalogKey);
        public Color ColorFor(StatusEffectType type)
        {
            if (damageColors != null) foreach (var entry in damageColors)
                if (entry != null && entry.status == type) return entry.color;
            return DefaultColor(type);
        }
        public static Color DefaultColor(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison: return new Color(.48f, 1f, .12f);
                case StatusEffectType.Bleed: return new Color(1f, .18f, .27f);
                case StatusEffectType.Burn: return new Color(1f, .48f, .08f);
                default: return Color.white;
            }
        }
    }
}
