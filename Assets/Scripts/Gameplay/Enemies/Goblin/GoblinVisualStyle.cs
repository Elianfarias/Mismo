using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Datos de presentación que una criatura puede cambiar por tier sin tocar su IA.</summary>
    [DisallowMultipleComponent]
    public sealed class GoblinVisualStyle : MonoBehaviour
    {
        [SerializeField] private string labelPrefix = "";
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Range(0f, 1f)] private float tintStrength = 0f;

        public string LabelPrefix => labelPrefix;
        public Color Tint => tint;
        public float TintStrength => Mathf.Clamp01(tintStrength);

        public void Configure(string prefix, Color color, float strength)
        {
            labelPrefix = prefix ?? string.Empty;
            tint = color;
            tintStrength = Mathf.Clamp01(strength);
        }

        public Color Apply(Color stateColor)
        {
            return Color.Lerp(stateColor, tint, TintStrength);
        }
    }
}
