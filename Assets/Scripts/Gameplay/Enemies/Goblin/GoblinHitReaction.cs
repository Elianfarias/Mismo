using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Additive visual recoil; never moves the navigation root or opens a damage window.</summary>
    [DefaultExecutionOrder(200), DisallowMultipleComponent]
    public sealed class GoblinHitReaction : MonoBehaviour
    {
        Transform visual;
        DamageReceiver receiver;
        CombatState combat;
        Health health;
        Vector3 localDirection, basePosition;
        Quaternion baseRotation;
        float remaining, duration, amplitude;
        bool applied;
        void Awake()
        {
            visual = transform.Find("Visual");
            receiver = GetComponent<DamageReceiver>(); combat = GetComponent<CombatState>(); health = GetComponent<Health>();
        }
        void OnEnable() { if (receiver != null) receiver.Resolved += OnResolved; if (combat != null) combat.PostureBroken += OnBroken; }
        void OnDisable()
        {
            if (receiver != null) receiver.Resolved -= OnResolved;
            if (combat != null) combat.PostureBroken -= OnBroken;
            Restore(); remaining = 0;
        }
        void OnResolved(DamageInfo damage, HitResult result)
        {
            if (result.Outcome != HitOutcome.Hit || result.HealthDamage <= 0 || health.IsDead) return;
            localDirection = transform.InverseTransformDirection(Vector3.ProjectOnPlane(damage.Direction, Vector3.up)).normalized;
            if (localDirection.sqrMagnitude < .001f) localDirection = Vector3.back;
            if (combat.Broken) return;
            bool heavy = damage.FeedbackProfile != null && damage.FeedbackProfile.IsHeavy(damage);
            duration = remaining = heavy ? .24f : .16f; amplitude = heavy ? 11 : 5;
        }
        void OnBroken(float seconds) { duration = remaining = seconds; amplitude = 22; localDirection = Vector3.back; }
        void Restore()
        {
            if (!applied || visual == null) return;
            visual.localPosition = basePosition; visual.localRotation = baseRotation; applied = false;
        }
        void Update() { Restore(); remaining = Mathf.Max(0, remaining - Time.deltaTime); }
        void LateUpdate()
        {
            if(EnemyActionPlayback.IsPerformingAttack(this)){remaining=0;return;}
            if (visual == null || remaining <= 0 || health == null || health.IsDead) return;
            basePosition = visual.localPosition; baseRotation = visual.localRotation;
            float weight = remaining / Mathf.Max(.01f, duration);
            if (combat.Broken) weight = Mathf.Max(.55f, weight);
            visual.localRotation = baseRotation * Quaternion.Euler(localDirection.z * amplitude * weight, 0, -localDirection.x * amplitude * weight);
            visual.localPosition += localDirection * (.035f * weight) + Vector3.down * (combat.Broken ? .07f * weight : 0);
            applied = true;
        }
    }
}
