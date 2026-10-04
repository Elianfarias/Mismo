using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Eligibility and clocks shared by species; selecting a policy belongs to the controller.</summary>
    public sealed class EnemyAttackSelection
    {
        readonly Dictionary<GoblinAttack, float> ready = new Dictionary<GoblinAttack, float>();
        float clock;
        float chargeCooldown;

        public bool HasActions(EnemySettings settings) => settings.attacks != null && settings.attacks.Length > 0;

        public void Tick(float dt)
        {
            clock += dt;
            chargeCooldown = Mathf.Max(0f, chargeCooldown - dt);
        }

        public void Reset()
        {
            ready.Clear();
            clock = chargeCooldown = 0f;
        }

        public void Commit(GoblinAttack action, EnemySettings settings)
        {
            ready[action] = clock + Mathf.Max(0, action.cooldown) + action.windup + action.active + action.recovery;
            if (action == settings.charge) chargeCooldown = settings.chargeCooldown;
        }

        public GoblinAttack SelectLegacyMelee(EnemySettings settings, float distance, bool pressure)
        {
            bool canCharge = distance >= 2.5f && distance <= settings.charge.range && chargeCooldown <= 0f;
            return pressure && canCharge ? settings.charge
                : distance <= settings.slash.range ? settings.slash
                : canCharge ? settings.charge : null;
        }

        public GoblinAttack SelectWeighted(EnemySettings settings, float distance, System.Predicate<GoblinAttack> filter = null)
        {
            float total = 0f;
            foreach (var action in settings.attacks)
                if (Eligible(action, distance) && (filter == null || filter(action))) total += action.weight;
            if (total <= 0f) return null;
            float roll = Random.value * total;
            foreach (var action in settings.attacks)
            {
                if (!Eligible(action, distance) || (filter != null && !filter(action))) continue;
                roll -= action.weight;
                if (roll < 0f) return action;
            }
            return null;
        }

        public bool Eligible(GoblinAttack action, float distance) =>
            action != null && action.enabled && action.weight > 0 &&
            distance >= action.minimumRange && distance <= action.range &&
            (!ready.TryGetValue(action, out float time) || clock >= time) &&
            (action.kind != CreatureAttackKind.Projectile || action.projectileVisual != null);
    }
}
