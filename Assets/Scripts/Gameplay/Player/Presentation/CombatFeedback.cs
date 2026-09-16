using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Feedback provisional de combate: impacto, bloqueo, parry y cooldown.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatFeedback : MonoBehaviour
    {
        [Header("Provisional feedback")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.7f;
        [SerializeField] private Color impactColor = new Color(1f, 0.18f, 0.08f, 0.95f);
        [SerializeField] private Color blockedColor = new Color(0.2f, 0.65f, 1f, 0.95f);
        [SerializeField] private Color parriedColor = new Color(1f, 0.84f, 0.2f, 1f);
        [SerializeField] private Color comboColor = new Color(1f, 0.42f, 0.12f, 0.95f);
        [SerializeField] private Color spinColor = new Color(0.55f, 0.9f, 1f, 0.95f);
        [SerializeField] private Color cooldownColor = new Color(0.75f, 0.35f, 1f, 0.9f);

        private Health health;
        private PlayerMotor motor;
        private Vector3 Facing => motor!=null?motor.Facing:transform.forward;
        private BeltDash belt;
        private BasicSwordCombo swordCombo;
        private SwordParry swordParry;
        private SwordSpinAttack swordSpinAttack;
        private SwordLunge swordLunge;
        private ParticleSystem combatParticles;
        private Material particleMaterial;
        private AudioSource audioSource;
        private float cooldownRemaining;
        private float cooldownDuration;

        public float CooldownRemaining => Mathf.Max(0f, cooldownRemaining);
        public bool IsOnCooldown => CooldownRemaining > 0f;
        public float CooldownNormalized => cooldownDuration > 0f ? CooldownRemaining / cooldownDuration : 0f;

        private void Awake()
        {
            health = GetComponent<Health>();
            motor = GetComponent<PlayerMotor>();
            belt = GetComponent<BeltDash>();
            swordCombo = GetComponentInChildren<BasicSwordCombo>();
            swordParry = GetComponentInChildren<SwordParry>();
            swordSpinAttack = GetComponentInChildren<SwordSpinAttack>();
            swordLunge = GetComponentInChildren<SwordLunge>();
            CreatePresentationObjects();
        }

        private void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
            if (belt != null)
            {
                belt.CooldownStarted += OnCooldownStarted;
                belt.CooldownReady += OnCooldownReady;
            }
            if (swordParry != null) swordParry.Parried += OnParried;
            if (swordCombo != null) swordCombo.AttackStarted += OnComboStarted;
            if (swordSpinAttack != null) swordSpinAttack.AttackStarted += OnSpinStarted;
            if (swordLunge != null) swordLunge.AttackStarted += OnLungeStarted;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
            if (belt != null)
            {
                belt.CooldownStarted -= OnCooldownStarted;
                belt.CooldownReady -= OnCooldownReady;
            }
            if (swordParry != null) swordParry.Parried -= OnParried;
            if (swordCombo != null) swordCombo.AttackStarted -= OnComboStarted;
            if (swordSpinAttack != null) swordSpinAttack.AttackStarted -= OnSpinStarted;
            if (swordLunge != null) swordLunge.AttackStarted -= OnLungeStarted;
        }

        private void OnDestroy()
        {
            if (particleMaterial != null) Destroy(particleMaterial);
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
        }

        private void OnDamaged(DamageInfo damage) => NotifyImpact(damage.HitPoint, damage.Direction, damage.Amount);

        private void OnParried(DamageInfo damage) => NotifyParried(damage.HitPoint, -damage.Direction);

        private void OnComboStarted(int step, string id)
        {
            // Feedback corto por etapa para distinguir la cadena aun sin animación final.
            Emit(transform.position + Facing * 0.55f + Vector3.up, Facing,
                comboColor, 8 + step * 3, 1.4f + step * 0.2f, 0.08f);
        }

        private void OnSpinStarted()
        {
            Emit(transform.position + Vector3.up, Vector3.up, spinColor, 16, 2.4f, 0.13f);
        }

        private void OnLungeStarted()
        {
            Vector3 direction=swordLunge!=null?swordLunge.Direction:Facing;
            Emit(transform.position + direction*.55f + Vector3.up, direction, spinColor, 12, 2f, 0.11f);
        }

        /// <summary>Emite impacto rojo y un tono grave en el punto recibido.</summary>
        public void NotifyImpact(Vector3 point, Vector3 direction, float amount = 0f)
        {
            Emit(point, direction, impactColor, amount > 0f ? 14 : 10, 2.4f, 0.11f);
            PlayTone(125f, 0.1f, 0.48f);
        }

        /// <summary>Emite una señal azul para un ataque bloqueado.</summary>
        public void NotifyBlocked(Vector3 point, Vector3 normal)
        {
            Emit(point, normal, blockedColor, 14, 2.1f, 0.1f);
        }

        /// <summary>Emite una señal dorada para un parry confirmado.</summary>
        public void NotifyParried(Vector3 point, Vector3 direction)
        {
            Emit(point, direction, parriedColor, 20, 3.2f, 0.12f);
        }

        /// <summary>Marca visualmente el inicio de un cooldown.</summary>
        public void NotifyCooldown(float duration)
        {
            cooldownDuration = Mathf.Max(0f, duration);
            cooldownRemaining = cooldownDuration;
            Emit(transform.position + Vector3.up, Vector3.up, cooldownColor, 9, 1.3f, 0.09f);
        }

        private void OnCooldownStarted(float duration)
        {
            cooldownDuration = Mathf.Max(0f, duration);
            cooldownRemaining = cooldownDuration;
            Emit(transform.position + Vector3.up, Vector3.up, cooldownColor, 9, 1.3f, 0.09f);
        }

        private void OnCooldownReady()
        {
            cooldownRemaining = 0f;
            Emit(transform.position + Vector3.up, Vector3.up, cooldownColor, 5, 0.8f, 0.07f);
        }

        private void CreatePresentationObjects()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.outputAudioMixerGroup = AudioRuntime.SfxGroup;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            GameObject particleObject = new GameObject("Combat Feedback Particles");
            particleObject.transform.SetParent(transform, false);
            combatParticles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            particleMaterial = RuntimeParticleMaterial.Create("Combat Feedback Material (Runtime)", Color.white);
            renderer.sharedMaterial = particleMaterial;
            var main = combatParticles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 1.5f;
            main.startSize = 0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = combatParticles.emission;
            emission.enabled = false;
        }

        private void Emit(Vector3 point, Vector3 direction, Color color, int count, float speed, float size)
        {
            if (combatParticles == null) return;
            Vector3 velocity = direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector3.up * speed;
            var emit = new ParticleSystem.EmitParams
            {
                position = point,
                velocity = velocity,
                startColor = color,
                startSize = size
            };
            combatParticles.Emit(emit, count);
        }

        private void PlayTone(float frequency, float duration, float volume)
        {
            if (audioSource == null || masterVolume <= 0f) return;
            int sampleRate = 22050;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("CombatFeedback", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float progress = i / (float)samples;
                float attack = Mathf.Clamp01(progress / 0.04f);
                float release = 1f - progress;
                float fundamental = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate);
                float harmonic = Mathf.Sin(2f * Mathf.PI * frequency * 2f * i / sampleRate) * 0.2f;
                data[i] = (fundamental + harmonic) * attack * release * 0.75f;
            }
            clip.SetData(data, 0);
            audioSource.PlayOneShot(clip, volume * masterVolume);
            Destroy(clip, duration + 0.05f);
        }
    }
}
