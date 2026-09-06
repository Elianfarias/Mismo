using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Feedback funcional provisional para que cada acción de movimiento sea legible al jugar.</summary>
    [DisallowMultipleComponent]
    public sealed class MovementFeedback : MonoBehaviour
    {
        [Header("Provisional feedback")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.7f;
        [SerializeField] private Color sprintColor = new Color(0.65f, 0.9f, 1f, 0.65f);
        [SerializeField] private Color dashColor = new Color(1f, 0.62f, 0.15f, 0.9f);
        [SerializeField] private Color landingColor = new Color(0.8f, 0.9f, 1f, 0.75f);

        private PlayerMotor motor;
        private PlayerController controller;
        private BeltDash dash;
        private Transform visual;
        private ParticleSystem movementParticles;
        private Material particleMaterial;
        private AudioSource audioSource;
        private Vector3 restScale;
        private bool wasDashing;
        private float squashRemaining;
        private float sprintPulseRemaining;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            controller = GetComponent<PlayerController>();
            dash = GetComponent<BeltDash>();
            visual = motor != null ? motor.Visual : null;
            if (visual != null) restScale = visual.localScale;
            CreatePresentationObjects();
        }

        private void OnEnable()
        {
            if (motor == null) return;
            motor.Jumped += OnJumped;
            motor.Landed += OnLanded;
        }

        private void OnDisable()
        {
            if (motor == null) return;
            motor.Jumped -= OnJumped;
            motor.Landed -= OnLanded;
        }

        private void OnDestroy()
        {
            if (particleMaterial != null) Destroy(particleMaterial);
        }

        private void Update()
        {
            if (motor == null || dash == null || visual == null) return;
            bool sprinting = controller != null && controller.IsSprinting;
            bool dashing = dash.IsActive;
            if (sprinting)
            {
                sprintPulseRemaining -= Time.deltaTime;
                if (sprintPulseRemaining <= 0f)
                {
                    Emit(ResolveGroundColor(), 3, 0.45f, 0.12f);
                    PlayTone(95f, 0.055f, 0.16f);
                    sprintPulseRemaining = 0.18f;
                }
            }
            else sprintPulseRemaining = 0f;
            if (dashing && !wasDashing)
            {
                Emit(dashColor, 16, 1.2f, 0.28f);
                PlayTone(155f, 0.13f, 0.5f);
                Squash(0.10f);
            }
            wasDashing = dashing;
            Vector3 targetScale = restScale;
            if (squashRemaining > 0f)
            {
                squashRemaining -= Time.deltaTime;
                float t = Mathf.Clamp01(squashRemaining / 0.12f);
                targetScale = Vector3.Lerp(restScale, Vector3.Scale(restScale, new Vector3(1.12f, 0.78f, 1.12f)), t);
            }
            else if (dashing) targetScale = Vector3.Scale(restScale, new Vector3(0.9f, 0.9f, 1.18f));
            else if (!motor.IsGrounded) targetScale = Vector3.Scale(restScale, new Vector3(0.94f, 1.08f, 0.94f));
            else if (sprinting) targetScale = Vector3.Scale(restScale, new Vector3(0.97f, 1.02f, 1.08f));
            visual.localScale = Vector3.Lerp(visual.localScale, targetScale, Time.deltaTime * 15f);
        }

        private void OnJumped()
        {
            Emit(sprintColor, 8, 0.75f, 0.22f);
            PlayTone(360f, 0.1f, 0.42f);
            Squash(0.08f);
        }

        private void OnLanded()
        {
            Emit(landingColor, 12, 0.9f, 0.25f);
            PlayTone(105f, 0.14f, 0.55f);
            Squash(0.12f);
        }

        private void Squash(float duration) => squashRemaining = Mathf.Max(squashRemaining, duration);

        private void CreatePresentationObjects()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            GameObject particleObject = new GameObject("Movement Feedback Particles");
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            movementParticles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader == null) particleShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (particleShader != null)
            {
                particleMaterial = new Material(particleShader) { name = "Movement Feedback Material (Runtime)" };
                if (particleMaterial.HasProperty("_BaseColor")) particleMaterial.SetColor("_BaseColor", Color.white);
                if (particleMaterial.HasProperty("_Color")) particleMaterial.SetColor("_Color", Color.white);
                particleRenderer.sharedMaterial = particleMaterial;
            }
            var main = movementParticles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 2.3f;
            main.startSize = 0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = movementParticles.emission;
            emission.enabled = false;
            var shape = movementParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.28f;
        }

        private void Emit(Color color, int count, float speed, float size)
        {
            if (movementParticles == null) return;
            var main = movementParticles.main;
            main.startSpeed = speed;
            var parameters = new ParticleSystem.EmitParams { startColor = color, startSize = size };
            movementParticles.Emit(parameters, count);
        }

        /// <summary>Obtiene el color visible del material inmediatamente bajo los pies.</summary>
        private Color ResolveGroundColor()
        {
            Vector3 origin = transform.position + Vector3.up * 0.25f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.5f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                return sprintColor;

            Renderer groundRenderer = hit.collider.GetComponent<Renderer>();
            Material material = groundRenderer != null ? groundRenderer.sharedMaterial : null;
            if (material == null) return sprintColor;

            Color sampled;
            if (material.HasProperty("_BaseColor")) sampled = material.GetColor("_BaseColor");
            else if (material.HasProperty("_Color")) sampled = material.GetColor("_Color");
            else return sprintColor;

            sampled.a = sprintColor.a;
            return sampled;
        }

        private void PlayTone(float frequency, float duration, float volume)
        {
            if (audioSource == null || masterVolume <= 0f) return;
            int sampleRate = 22050;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("MovementFeedback", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float progress = i / (float)samples;
                float attack = Mathf.Clamp01(progress / 0.08f);
                float release = 1f - progress;
                float fundamental = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate);
                float harmonic = Mathf.Sin(2f * Mathf.PI * frequency * 2f * i / sampleRate) * 0.25f;
                data[i] = (fundamental + harmonic) * attack * release * 0.8f;
            }
            clip.SetData(data, 0);
            audioSource.PlayOneShot(clip, volume * masterVolume);
            Destroy(clip, duration + 0.05f);
        }
    }
}
