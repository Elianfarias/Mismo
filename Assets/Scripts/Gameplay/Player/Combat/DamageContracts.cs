using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>Datos inmutables de un impacto, sin acoplar al receptor a un arma concreta.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly float FocusGainOnHit;
        public readonly float PostureDamage;
        public readonly long AttackId;
        public readonly bool Ranged, Area, Parryable;
        public readonly Vector3 Origin;
        public readonly GameObject Source;
        public readonly Vector3 HitPoint;
        public readonly Vector3 Direction;
        public readonly string WeaponFamilyId;
        public readonly Mismo.Gameplay.Player.Presentation.CombatFeedbackProfile FeedbackProfile;

        public DamageInfo(float amount, GameObject source, Vector3 hitPoint, Vector3 direction, long attackId = 0, float postureDamage = -1, bool ranged = false, bool area = false, Vector3? origin = null, bool parryable = true, string weaponFamilyId = null, float focusGainOnHit = 0, Mismo.Gameplay.Player.Presentation.CombatFeedbackProfile feedbackProfile = null)
        {
            Amount = Mathf.Max(0f, amount);
            FocusGainOnHit = Mathf.Max(0f, focusGainOnHit);
            WeaponFamilyId=weaponFamilyId;
            FeedbackProfile = feedbackProfile != null ? feedbackProfile : source != null ? source.GetComponentInParent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>()?.ActiveDefinition?.FeedbackProfile : null;
            PostureDamage = postureDamage < 0 ? amount * .65f : postureDamage;
            AttackId = attackId; Ranged = ranged; Area = area; Parryable = parryable;
            Origin = origin ?? (source != null ? source.transform.position : hitPoint);
            Source = source;
            HitPoint = hitPoint;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }
    }

    public enum HitOutcome { Ignored, Hit, Invulnerable, Parry, PerfectParry, Dodge, PerfectDodge, Block }
    public readonly struct HitResult
    {
        public readonly HitOutcome Outcome;
        public readonly float HealthDamage, PostureDamage;
        public readonly bool BackHit;
        public readonly bool PostureBroken;
        public HitResult(HitOutcome outcome, float health = 0, float posture = 0, bool back = false, bool postureBroken = false)
        { Outcome=outcome; HealthDamage=health; PostureDamage=posture; BackHit=back; PostureBroken=postureBroken; }
    }
    public static class AttackIdentity
    {
        static long next;
        public static long Next() => ++next;
    }

    /// <summary>Contrato mínimo que cualquier receptor de daño debe exponer.</summary>
    public interface IDamageReceiver
    {
        bool ReceiveDamage(DamageInfo damage);
    }

    /// <summary>Reacción opcional de la fuente cuando su ataque es rechazado.</summary>
    public interface IParryResponder
    {
        void OnAttackParried(DamageInfo damage);
    }
    public interface ICombatContribution
    {
        void RecordDefense(Mismo.Gameplay.Player.Equipment.Inventory.PlayerInventory player,string family);
    }
}
