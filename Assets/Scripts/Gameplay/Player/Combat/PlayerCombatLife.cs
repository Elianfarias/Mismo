using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Dash;
using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>Cierra las acciones pendientes al morir en el prototipo de combate.</summary>
    [RequireComponent(typeof(Health), typeof(DamageReceiver), typeof(Invulnerability))]
    public sealed class PlayerCombatLife : MonoBehaviour
    {
        private Health health;
        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;
        private void OnDied(DamageInfo _)
        {
            GetComponent<BeltDash>()?.Cancel();
            GetComponent<Mismo.Gameplay.Player.Equipment.AbilityRunner>()?.Cancel();
            foreach (SwordParry parry in GetComponentsInChildren<SwordParry>(true)) parry.Cancel();
            foreach (BasicSwordCombo combo in GetComponentsInChildren<BasicSwordCombo>()) combo.Cancel();
            foreach (SwordLunge lunge in GetComponentsInChildren<SwordLunge>()) lunge.Cancel();
            foreach (SwordSpinAttack spin in GetComponentsInChildren<SwordSpinAttack>()) spin.Cancel();
            foreach (AttackHitbox hitbox in GetComponentsInChildren<AttackHitbox>()) hitbox.CancelAttack();
            var controller = GetComponent<PlayerController>();
            if (controller != null) controller.enabled = false;
        }
    }
}
