using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>Adaptador que compone salud e invulnerabilidad para recibir impactos.</summary>
    [DisallowMultipleComponent]
    public sealed class DamageReceiver : MonoBehaviour, IDamageReceiver
    {
        [SerializeField] private Health health;
        [SerializeField] private Invulnerability invulnerability;
        [SerializeField] private SwordParry swordParry;
        [SerializeField, Min(0f)] private float invulnerabilityAfterHit = 0.18f;

        public Health Health => health;
        public Invulnerability Invulnerability => invulnerability;
        public SwordParry SwordParry => swordParry;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            if (invulnerability == null) invulnerability = GetComponent<Invulnerability>();
            if (swordParry == null) swordParry = GetComponentInParent<SwordParry>();
            if (swordParry == null) swordParry = GetComponentInChildren<SwordParry>();
        }

        public bool ReceiveDamage(DamageInfo damage)
        {
            if (swordParry == null) swordParry = GetComponentInParent<SwordParry>();
            if (swordParry == null) swordParry = GetComponentInChildren<SwordParry>();
            if (swordParry != null && swordParry.TryParry(damage)) return false;
            if (health == null || health.IsDead || (invulnerability != null && invulnerability.IsInvulnerable)) return false;
            bool applied = health.ApplyDamage(damage);
            if (applied && invulnerability != null) invulnerability.StartWindow(invulnerabilityAfterHit);
            return applied;
        }
    }
}
