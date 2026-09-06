using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>
    /// Objetivo de práctica reutilizable: recibe daño, muestra vida/flash y revive solo
    /// después de caer para poder probar una habilidad tras otra.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(DamageReceiver), typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class TrainingDummy : MonoBehaviour
    {
        [Header("Training target")]
        [SerializeField, Min(0f)] private float respawnDelay = 1.2f;
        [SerializeField, Min(0f)] private float hitFlashDuration = 0.12f;
        [SerializeField] private Color idleColor = new Color(0.15f, 0.65f, 0.82f, 1f);
        [SerializeField] private Color hitColor = new Color(1f, 0.28f, 0.12f, 1f);
        [SerializeField] private Color deadColor = new Color(0.12f, 0.12f, 0.16f, 1f);

        private Health health;
        private Renderer[] renderers;
        private Material[] materials;
        private Transform healthFill;
        private float hitFlashRemaining;
        private float respawnRemaining;

        public Health Health => health;

        private void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>(true);
            materials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                materials[i] = renderers[i].material;
                SetMaterialColor(materials[i], idleColor);
            }
            BuildHealthBar();
            RefreshHealthBar();
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<Health>();
            if (health != null)
            {
                health.Changed += OnHealthChanged;
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
            respawnRemaining = 0f;
            SetBodyColor(idleColor);
            RefreshHealthBar();
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Changed -= OnHealthChanged;
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
        }

        private void OnDestroy()
        {
            if (materials == null) return;
            foreach (Material material in materials)
                if (material != null) Destroy(material);
        }

        private void Update()
        {
            if (hitFlashRemaining > 0f)
            {
                hitFlashRemaining = Mathf.Max(0f, hitFlashRemaining - Time.deltaTime);
                if (hitFlashRemaining <= 0f && health != null && !health.IsDead) SetBodyColor(idleColor);
            }

            if (health == null || !health.IsDead || respawnRemaining <= 0f) return;
            respawnRemaining = Mathf.Max(0f, respawnRemaining - Time.deltaTime);
            if (respawnRemaining <= 0f)
            {
                health.Revive();
                SetBodyColor(idleColor);
            }
        }

        private void OnDamaged(DamageInfo damage)
        {
            hitFlashRemaining = Mathf.Max(0f, hitFlashDuration);
            SetBodyColor(hitColor);
        }

        private void OnDied(DamageInfo damage)
        {
            respawnRemaining = Mathf.Max(0f, respawnDelay);
            SetBodyColor(deadColor);
        }

        private void OnHealthChanged(float current, float maximum) => RefreshHealthBar();

        private void SetBodyColor(Color color)
        {
            if (materials == null) return;
            foreach (Material material in materials) SetMaterialColor(material, color);
        }

        private void RefreshHealthBar()
        {
            if (healthFill == null || health == null) return;
            float normalized = Mathf.Clamp01(health.Normalized);
            Vector3 scale = healthFill.localScale;
            scale.x = normalized;
            healthFill.localScale = scale;
            Vector3 position = healthFill.localPosition;
            position.x = -0.65f * (1f - normalized);
            healthFill.localPosition = position;
        }

        private void BuildHealthBar()
        {
            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Health Bar Background";
            background.transform.SetParent(transform, false);
            background.transform.localPosition = new Vector3(0f, 2.95f, 0f);
            background.transform.localScale = new Vector3(1.5f, 0.12f, 0.06f);
            Destroy(background.GetComponent<Collider>());
            Material backgroundMaterial = CreateMaterial(new Color(0.025f, 0.03f, 0.04f, 1f));
            background.GetComponent<Renderer>().material = backgroundMaterial;

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Health Bar Fill";
            fill.transform.SetParent(transform, false);
            fill.transform.localPosition = new Vector3(0f, 2.95f, -0.04f);
            fill.transform.localScale = new Vector3(1.3f, 0.07f, 0.07f);
            Destroy(fill.GetComponent<Collider>());
            Material fillMaterial = CreateMaterial(new Color(0.2f, 0.95f, 0.35f, 1f));
            fill.GetComponent<Renderer>().material = fillMaterial;
            healthFill = fill.transform;
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Unlit/Color"));
            SetMaterialColor(material, color);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
    }
}
