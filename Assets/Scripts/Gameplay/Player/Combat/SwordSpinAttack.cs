using System;
using System.Collections.Generic;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>
    /// Habilidad R de la espada: ataque circular con duración, coste y cooldown propios.
    /// Se equipa de forma opcional; otras armas pueden tener habilidades diferentes.
    /// </summary>
    [RequireComponent(typeof(SphereCollider), typeof(DamageDealer))]
    [DisallowMultipleComponent]
    public sealed class SwordSpinAttack : MonoBehaviour
    {
        [Header("Spin attack")]
        [SerializeField, Min(0.01f)] private float duration = 0.55f;
        [SerializeField, Min(0f)] private float cooldown = 1.1f;
        [SerializeField, Min(0f)] private float staminaCost = 20f;
        [SerializeField, Min(0f)] private float radius = 1.6f;
        [SerializeField] private Vector3 center = new Vector3(0f, 1f, 0f);
        [SerializeField, Min(0f)] private float damage = 24f;

        private readonly HashSet<EntityId> hitTargets = new HashSet<EntityId>();
        private SphereCollider area;
        private DamageDealer damageDealer;
        private Stamina stamina;
        private float activeRemaining;
        private float cooldownRemaining;

        public bool IsActive => activeRemaining > 0f;
        public bool IsOnCooldown => cooldownRemaining > 0f;
        public bool IsReady => !IsActive && !IsOnCooldown;
        public float Duration => Mathf.Max(0.01f, duration);
        public float ActiveRemaining => Mathf.Max(0f, activeRemaining);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public float CooldownRemaining => Mathf.Max(0f, cooldownRemaining);
        public float CooldownNormalized => Cooldown > 0f ? CooldownRemaining / Cooldown : 0f;
        public float StaminaCost => Mathf.Max(0f, staminaCost);
        public float Radius => Mathf.Max(0f, radius);

        public event Action AttackStarted;
        public event Action AttackFinished;
        public event Action AttackRejected;
        public event Action<float> CooldownStarted;
        public event Action CooldownReady;

        private void Awake()
        {
            area = GetComponent<SphereCollider>();
            damageDealer = GetComponent<DamageDealer>();
            stamina = GetComponentInParent<Stamina>();
            area.isTrigger = true;
            area.enabled = false;
            ApplyColliderSettings();
        }

        /// <summary>Inicia el giro si el arma está lista y hay stamina suficiente.</summary>
        public bool RequestSpin()
        {
            if (!IsReady || (stamina != null && !stamina.TrySpend(StaminaCost)))
            {
                AttackRejected?.Invoke();
                return false;
            }

            activeRemaining = Duration;
            hitTargets.Clear();
            damageDealer.Configure(Mathf.Max(0f, damage));
            area.enabled = true;
            AttackStarted?.Invoke();
            return true;
        }

        /// <summary>Avanza el ataque y su cooldown desde el coordinador del jugador.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (activeRemaining > 0f)
            {
                activeRemaining = Mathf.Max(0f, activeRemaining - deltaTime);
                CheckOverlaps();
                if (activeRemaining <= 0f)
                {
                    area.enabled = false;
                    hitTargets.Clear();
                    cooldownRemaining = Cooldown;
                    AttackFinished?.Invoke();
                    if (cooldownRemaining > 0f) CooldownStarted?.Invoke(cooldownRemaining);
                    else CooldownReady?.Invoke();
                }
            }

            if (cooldownRemaining <= 0f) return;
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
            if (cooldownRemaining <= 0f) CooldownReady?.Invoke();
        }

        /// <summary>Interrumpe el ataque sin borrar el cooldown ya iniciado.</summary>
        public void Cancel()
        {
            if (!IsActive) return;
            activeRemaining = 0f;
            area.enabled = false;
            hitTargets.Clear();
            AttackFinished?.Invoke();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsActive) ApplyHit(other);
        }

        private void CheckOverlaps()
        {
            if (!IsActive || damageDealer == null) return;
            Vector3 origin = transform.TransformPoint(center);
            Collider[] overlaps = Physics.OverlapSphere(origin, Radius, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider other in overlaps) ApplyHit(other);
        }

        private void ApplyHit(Collider other)
        {
            if (!IsActive || other == null || other.transform.root == transform.root) return;
            EntityId targetId = other.transform.root.GetEntityId();
            if (!hitTargets.Add(targetId)) return;
            Vector3 origin = transform.TransformPoint(center);
            Vector3 point = other.ClosestPoint(origin);
            if (!damageDealer.ApplyTo(other.gameObject, point, other.transform.position - origin))
                hitTargets.Remove(targetId);
        }

        private void ApplyColliderSettings()
        {
            if (area == null) return;
            area.radius = Radius;
            area.center = center;
        }
    }
}
