using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>
    /// Animación procedural temporal del arma. Lee eventos de las habilidades de espada y
    /// aplica poses aditivas al visual, dejando libre el Animator definitivo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwordAnimationFeedback : MonoBehaviour
    {
        private enum Motion
        {
            None,
            Slash,
            HeavySlash,
            Parry,
            Spin,
            Lunge
        }

        [Header("Sword visual")]
        [SerializeField] private Transform swordVisual;
        [SerializeField, Min(0.01f)] private float slashDuration = 0.38f;
        [SerializeField, Min(0.01f)] private float heavySlashDuration = 0.52f;
        [SerializeField, Min(0.01f)] private float spinDuration = 0.55f;
        [SerializeField, Min(0.01f)] private float lungeDuration = 0.24f;

        private BasicSwordCombo combo;
        private SwordParry parry;
        private SwordSpinAttack spin;
        private SwordLunge lunge;
        private PlayerMotor motor;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Motion motion;
        private float elapsed;
        private float duration;
        private int priority;
        private int comboStep;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            Transform weaponAnchor = motor != null ? motor.Visual : FindChild(transform, "Visual");
            if (swordVisual == null) swordVisual = FindChild(transform, "BasicSword");
            if (swordVisual == null) swordVisual = FindChild(transform, "Blade");

            // PlayerMotor rota Visual, no el root físico. Reparentar aquí corrige escenas
            // creadas por la herramienta anterior sin perder la pose local del arma.
            if (swordVisual != null && weaponAnchor != null && swordVisual != weaponAnchor &&
                !swordVisual.IsChildOf(weaponAnchor))
                swordVisual.SetParent(weaponAnchor, false);

            // Si el arma visual aún no fue adjuntada, no animar el cuerpo completo.
            // El componente queda listo y se resolverá al instanciar el prefab del arma.
            if (swordVisual != null)
            {
                basePosition = swordVisual.localPosition;
                baseRotation = swordVisual.localRotation;
            }
            combo = GetComponentInChildren<BasicSwordCombo>();
            parry = GetComponentInChildren<SwordParry>();
            spin = GetComponentInChildren<SwordSpinAttack>();
            lunge = GetComponentInChildren<SwordLunge>();
        }

        private void OnEnable()
        {
            if (combo != null)
            {
                combo.AttackStarted += OnComboStarted;
                combo.AttackFinished += OnComboFinished;
            }
            if (parry != null)
            {
                parry.WindowStarted += OnParryStarted;
                parry.WindowClosed += OnParryClosed;
            }
            if (spin != null)
            {
                spin.AttackStarted += OnSpinStarted;
                spin.AttackFinished += OnSpinFinished;
            }
            if (lunge != null)
            {
                lunge.AttackStarted += OnLungeStarted;
                lunge.AttackFinished += OnLungeFinished;
            }
        }

        private void OnDisable()
        {
            if (combo != null)
            {
                combo.AttackStarted -= OnComboStarted;
                combo.AttackFinished -= OnComboFinished;
            }
            if (parry != null)
            {
                parry.WindowStarted -= OnParryStarted;
                parry.WindowClosed -= OnParryClosed;
            }
            if (spin != null)
            {
                spin.AttackStarted -= OnSpinStarted;
                spin.AttackFinished -= OnSpinFinished;
            }
            if (lunge != null)
            {
                lunge.AttackStarted -= OnLungeStarted;
                lunge.AttackFinished -= OnLungeFinished;
            }
            ResetPose();
        }

        private void Update()
        {
            if (motion == Motion.None || swordVisual == null) return;
            elapsed += Time.deltaTime;
            float normalized = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            ApplyPose(normalized);
            if (elapsed >= duration) ResetPose();
        }

        private void OnComboStarted(int step, string id)
        {
            comboStep = step;
            Begin(step >= 2 ? Motion.HeavySlash : Motion.Slash,
                step >= 2 ? heavySlashDuration : slashDuration, nextPriority: 10);
        }

        private void OnComboFinished(int step, string id)
        {
            if (priority <= 10) ResetPose();
        }

        private void OnParryStarted(float window) => Begin(Motion.Parry, Mathf.Max(0.12f, window), nextPriority: 30);
        private void OnParryClosed() { if (priority <= 30) ResetPose(); }
        private void OnSpinStarted() => Begin(Motion.Spin, spinDuration, nextPriority: 20);
        private void OnSpinFinished() { if (priority <= 20) ResetPose(); }
        private void OnLungeStarted() => Begin(Motion.Lunge, lungeDuration, nextPriority: 20);
        private void OnLungeFinished() { if (priority <= 20) ResetPose(); }

        private void Begin(Motion next, float nextDuration, int nextPriority)
        {
            if (nextPriority < priority && motion != Motion.None) return;
            motion = next;
            duration = Mathf.Max(0.01f, nextDuration);
            elapsed = 0f;
            priority = nextPriority;
        }

        private void ApplyPose(float normalized)
        {
            float smooth = Mathf.SmoothStep(0f, 1f, normalized);
            Vector3 offset = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            switch (motion)
            {
                case Motion.Slash:
                    float slashYaw = Mathf.Lerp(comboStep == 0 ? -58f : 58f, comboStep == 0 ? 58f : -58f, smooth);
                    float slashPitch = Mathf.Sin(smooth * Mathf.PI) * -14f;
                    rotation = Quaternion.Euler(slashPitch, slashYaw, 0f);
                    offset = Vector3.Lerp(new Vector3(comboStep == 0 ? -0.12f : 0.12f, 0f, -0.03f),
                        new Vector3(comboStep == 0 ? 0.12f : -0.12f, 0.02f, 0.08f), smooth);
                    break;
                case Motion.HeavySlash:
                    float heavyYaw = Mathf.Lerp(-82f, 92f, smooth);
                    float heavyPitch = Mathf.Lerp(22f, -24f, smooth);
                    rotation = Quaternion.Euler(heavyPitch, heavyYaw, 0f);
                    offset = Vector3.Lerp(new Vector3(-0.16f, -0.08f, -0.16f), new Vector3(0.16f, 0.06f, 0.16f), smooth);
                    break;
                case Motion.Parry:
                    float parryPulse = Mathf.Sin(smooth * Mathf.PI);
                    rotation = Quaternion.Euler(-28f - parryPulse * 16f, -22f, 0f);
                    offset = new Vector3(0f, 0.08f + parryPulse * 0.04f, 0.06f);
                    break;
                case Motion.Spin:
                    rotation = Quaternion.Euler(0f, Mathf.Lerp(-35f, 325f, smooth), 0f);
                    offset = new Vector3(0f, 0.03f, 0.12f);
                    break;
                case Motion.Lunge:
                    rotation = Quaternion.Euler(-10f, 8f, 0f);
                    offset = new Vector3(0f, 0f, Mathf.Sin(smooth * Mathf.PI) * 0.34f);
                    break;
            }

            swordVisual.localPosition = basePosition + offset;
            swordVisual.localRotation = baseRotation * rotation;
        }

        private void ResetPose()
        {
            motion = Motion.None;
            elapsed = 0f;
            duration = 0f;
            priority = 0;
            if (swordVisual != null)
            {
                swordVisual.localPosition = basePosition;
                swordVisual.localRotation = baseRotation;
            }
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
        }
    }
}
