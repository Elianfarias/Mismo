using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>
    /// Modifier visual provisional para el tier Elite. Reutiliza toda la criatura base:
    /// sólo cambia escala, tinte y feedback de lectura durante el playtest.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoblinEliteVisual : MonoBehaviour
    {
        [Header("Elite presentation")]
        [SerializeField, Min(1f)] private float scaleMultiplier = 1.28f;
        [SerializeField] private Color eliteTint = new Color(0.74f, 0.32f, 1f, 1f);
        [SerializeField] private Color auraColor = new Color(0.88f, 0.28f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float tintStrength = 0.78f;
        [SerializeField, Min(0.05f)] private float auraRadius = 0.92f;
        [SerializeField, Min(0f)] private float auraPulseSpeed = 2.8f;
        [SerializeField, Min(0f)] private float hitFeedbackDuration = 0.3f;

        private GoblinVisualStyle style;
        private Health health;
        private LineRenderer aura;
        private ParticleSystem burst;
        private Material auraMaterial;
        private Vector3 originalScale;
        private float hitRemaining;

        public float ScaleMultiplier => Mathf.Max(1f, scaleMultiplier);
        public Color EliteTint => eliteTint;
        public Color AuraColor => auraColor;
        public bool IsHitFlashing => hitRemaining > 0f;

        public void Configure(float scale, Color tint, Color auraTint, float strength)
        {
            scaleMultiplier = Mathf.Max(1f, scale);
            eliteTint = tint;
            auraColor = auraTint;
            tintStrength = Mathf.Clamp01(strength);
        }

        private void Awake()
        {
            originalScale = transform.localScale;
            transform.localScale = originalScale * ScaleMultiplier;
            style = GetComponent<GoblinVisualStyle>();
            if (style == null) style = gameObject.AddComponent<GoblinVisualStyle>();
            style.Configure("ELITE", eliteTint, tintStrength);
            health = GetComponent<Health>();
            BuildAura();
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<Health>();
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Changed += OnHealthChanged;
            }
            SetAuraVisible(health == null || !health.IsDead);
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Changed -= OnHealthChanged;
            }
            SetAuraVisible(false);
        }

        private void OnDestroy()
        {
            if (auraMaterial != null) Destroy(auraMaterial);
        }

        private void OnDamaged(DamageInfo _) => hitRemaining = health != null && health.IsDead ? 0 : Mathf.Max(0f, hitFeedbackDuration);

        private void OnHealthChanged(float current, float maximum) => SetAuraVisible(current > 0);

        private void SetAuraVisible(bool visible)
        {
            if (aura != null) aura.enabled = visible;
            if (!visible) hitRemaining = 0;
            if (burst == null) return;
            if (!visible) burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            else if (!burst.isPlaying) burst.Play(true);
        }

        private void LateUpdate()
        {
            if (health != null && health.IsDead) { SetAuraVisible(false); return; }
            hitRemaining = Mathf.Max(0f, hitRemaining - Time.deltaTime);
            if (aura == null) return;
            float pulse = 1f + Mathf.Sin(Time.time * auraPulseSpeed * Mathf.PI * 2f) * 0.09f;
            float intensity = hitRemaining > 0f ? 1.8f : 0.95f + pulse * 0.25f;
            aura.widthMultiplier = hitRemaining > 0f ? 0.095f : 0.045f;
            aura.startColor = aura.endColor = WithAlpha(auraColor, Mathf.Clamp01(intensity));
            aura.transform.localScale = new Vector3(pulse, 1f, pulse);
            if (burst != null)
            {
                var emission = burst.emission;
                emission.rateOverTime = 4f + (hitRemaining > 0f ? 24f : 0f);
            }
        }

        private void BuildAura()
        {
            GameObject auraObject = new GameObject("Elite Aura");
            auraObject.transform.SetParent(transform, false);
            auraObject.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            aura = auraObject.AddComponent<LineRenderer>();
            aura.useWorldSpace = false;
            aura.loop = true;
            aura.positionCount = 40;
            aura.widthMultiplier = 0.045f;
            aura.sharedMaterial = CreateMaterial("Elite Aura Material", auraColor);
            auraMaterial = aura.sharedMaterial;
            for (int i = 0; i < aura.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / aura.positionCount;
                aura.SetPosition(i, new Vector3(Mathf.Cos(angle) * auraRadius, 0.015f, Mathf.Sin(angle) * auraRadius));
            }

            GameObject burstObject = new GameObject("Elite Feedback");
            burstObject.transform.SetParent(transform, false);
            burstObject.transform.localPosition = Vector3.up * 0.8f;
            burst = burstObject.AddComponent<ParticleSystem>();
            var main = burst.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.65f;
            main.startSpeed = 0.25f;
            main.startSize = 0.045f;
            main.startColor = auraColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = burst.emission;
            emission.rateOverTime = 4f;
            var shape = burst.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = auraRadius * 0.76f;
            var renderer = burst.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = auraMaterial;
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            return Mismo.Gameplay.Player.Presentation.RuntimeParticleMaterial.Create(materialName, color);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
