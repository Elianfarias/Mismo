using System;
using System.Collections.Generic;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>
    /// Habilidad Q de la espada: avance ofensivo con desplazamiento controlado y hitbox temporal.
    /// </summary>
    [RequireComponent(typeof(DamageDealer))]
    [DisallowMultipleComponent]
    public sealed class SwordLunge : MonoBehaviour
    {
        [Header("Lunge")]
        [SerializeField, Min(0.01f)] private float duration = 0.24f;
        [SerializeField, Min(0f)] private float distance = 3.2f;
        [SerializeField, Min(0f)] private float cooldown = 0.8f;
        [SerializeField, Min(0f)] private float staminaCost = 12f;
        [SerializeField, Min(0f)] private float damage = 18f;
        [SerializeField, Min(0f)] private float hitboxRadius = 0.6f;
        [SerializeField] private Vector3 hitboxCenter = new Vector3(0f, 1f, 0.7f);
        [SerializeField, Min(0f)] private float hitboxStart = 0.02f;
        [SerializeField, Min(0f)] private float hitboxDuration = 0.18f;

        private readonly HashSet<EntityId> hitTargets = new HashSet<EntityId>();
        private DamageDealer damageDealer;
        private Stamina stamina;
        private PlayerMotor motor;
        private Vector3 direction;
        private float activeElapsed;
        private float cooldownRemaining;
        private float distanceTravelled;

        public bool IsActive => activeElapsed >= 0f && activeElapsed < Duration;
        public bool IsOnCooldown => cooldownRemaining > 0f;
        public bool IsReady => !IsActive && !IsOnCooldown;
        public float Duration => Mathf.Max(0.01f, duration);
        public float Distance => Mathf.Max(0f, distance);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public float CooldownRemaining => Mathf.Max(0f, cooldownRemaining);
        public float CooldownNormalized => Cooldown > 0f ? CooldownRemaining / Cooldown : 0f;
        public float StaminaCost => Mathf.Max(0f, staminaCost);
        public float ActiveNormalized => IsActive ? Mathf.Clamp01(activeElapsed / Duration) : 0f;
        public Vector3 Direction => direction;

        public event Action AttackStarted;
        public event Action AttackFinished;
        public event Action AttackRejected;
        public event Action<float> CooldownStarted;
        public event Action CooldownReady;
        public event Action<GameObject> Hit;

        private void Awake()
        {
            damageDealer = GetComponent<DamageDealer>();
            stamina = GetComponentInParent<Stamina>();
            motor = GetComponentInParent<PlayerMotor>();
            activeElapsed = -1f;
        }

        /// <summary>Inicia el avance si la espada está lista y hay stamina suficiente.</summary>
        public bool RequestLunge(Vector3 requestedDirection)
        {
            if (!IsReady || (stamina != null && !stamina.TrySpend(StaminaCost)))
            {
                AttackRejected?.Invoke();
                return false;
            }

            Vector3 candidate = Vector3.ProjectOnPlane(requestedDirection, Vector3.up);
            if (candidate.sqrMagnitude <= 0.0001f && motor != null)
                candidate = Vector3.ProjectOnPlane(motor.Facing, Vector3.up);
            direction = candidate.sqrMagnitude > 0.0001f ? candidate.normalized : transform.forward;
            activeElapsed = 0f;
            distanceTravelled = 0f;
            hitTargets.Clear();
            damageDealer.Configure(Mathf.Max(0f, damage));
            AttackStarted?.Invoke();
            return true;
        }

        /// <summary>Consume el avance de este frame para entregarlo a PlayerMotor.</summary>
        public Vector3 Step(float deltaTime)
        {
            if (deltaTime <= 0f || !IsActive) return Vector3.zero;
            float previousElapsed = activeElapsed;
            activeElapsed = Mathf.Min(Duration, activeElapsed + deltaTime);
            float remainingDistance = Mathf.Max(0f, Distance - distanceTravelled);
            float stepDistance = Mathf.Min(remainingDistance, Distance * (activeElapsed - previousElapsed) / Duration);
            distanceTravelled += stepDistance;
            if (IsHitboxOpen(activeElapsed)) CheckHitbox();
            if (activeElapsed >= Duration) Finish();
            return direction * stepDistance;
        }

        /// <summary>Avanza únicamente el cooldown cuando no se está ejecutando el movimiento.</summary>
        public void TickCooldown(float deltaTime)
        {
            if (deltaTime <= 0f || cooldownRemaining <= 0f) return;
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
            if (cooldownRemaining <= 0f) CooldownReady?.Invoke();
        }

        /// <summary>Interrumpe el avance, por ejemplo al chocar contra una pared.</summary>
        public void Cancel()
        {
            if (!IsActive) return;
            Finish();
        }

        private bool IsHitboxOpen(float elapsed) => elapsed >= Mathf.Max(0f, hitboxStart) &&
            elapsed < Mathf.Min(Duration, Mathf.Max(0f, hitboxStart) + Mathf.Max(0f, hitboxDuration));

        private void CheckHitbox()
        {
            Vector3 origin = transform.TransformPoint(hitboxCenter) + direction * (Distance * ActiveNormalized);
            Collider[] overlaps = Physics.OverlapSphere(origin, Mathf.Max(0f, hitboxRadius));
            foreach (Collider other in overlaps)
            {
                if (other == null || other.transform.root == transform.root) continue;
                EntityId targetId = other.transform.root.GetEntityId();
                if (!hitTargets.Add(targetId)) continue;
                Vector3 point = other.ClosestPoint(origin);
                if (damageDealer.ApplyTo(other.gameObject, point, direction)) Hit?.Invoke(other.gameObject);
            }
        }

        private void Finish()
        {
            activeElapsed = -1f;
            hitTargets.Clear();
            cooldownRemaining = Cooldown;
            AttackFinished?.Invoke();
            if (cooldownRemaining > 0f) CooldownStarted?.Invoke(cooldownRemaining);
            else CooldownReady?.Invoke();
        }
    }
}
