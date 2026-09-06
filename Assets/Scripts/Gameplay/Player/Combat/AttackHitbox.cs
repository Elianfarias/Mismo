using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>Ventana de impacto configurable para un ataque concreto.</summary>
    [Serializable]
    public sealed class AttackWindow
    {
        [SerializeField] private string id = "attack";
        [SerializeField, Min(0f)] private float activeStart;
        [SerializeField, Min(0.001f)] private float activeDuration = 0.18f;
        [SerializeField, Min(0f)] private float damage = 10f;

        public string Id => id;
        public float ActiveStart => activeStart;
        public float ActiveDuration => activeDuration;
        public float Damage => damage;
    }

    /// <summary>Activa un collider únicamente durante la ventana del ataque y registra cada objetivo una vez.</summary>
    [RequireComponent(typeof(Collider), typeof(DamageDealer))]
    public sealed class AttackHitbox : MonoBehaviour
    {
        [SerializeField] private AttackWindow[] windows = { new AttackWindow() };
        private readonly HashSet<int> hitTargets = new HashSet<int>();
        private Collider hitbox;
        private DamageDealer damageDealer;
        private int currentIndex = -1;
        private float elapsed;
        private bool windowOpen;

        public bool IsAttacking => currentIndex >= 0;
        public bool IsWindowOpen => windowOpen;
        public int CurrentAttackIndex => currentIndex;
        public int WindowCount => windows != null ? windows.Length : 0;
        public string CurrentAttackId => IsAttacking ? windows[currentIndex].Id : string.Empty;

        private void Awake()
        {
            hitbox = GetComponent<Collider>();
            damageDealer = GetComponent<DamageDealer>();
            hitbox.isTrigger = true;
            hitbox.enabled = false;
        }

        private void Update()
        {
            if (!IsAttacking) return;
            elapsed += Time.deltaTime;
            AttackWindow attack = windows[currentIndex];
            if (attack == null)
            {
                currentIndex = -1;
                hitbox.enabled = false;
                return;
            }
            float end = attack.ActiveStart + attack.ActiveDuration;
            bool shouldBeOpen = elapsed >= attack.ActiveStart && elapsed < end;
            if (shouldBeOpen != windowOpen) SetWindowOpen(shouldBeOpen, attack);
            if (windowOpen) CheckOverlaps();
            if (elapsed >= end)
            {
                SetWindowOpen(false, attack);
                currentIndex = -1;
            }
        }

        /// <summary>Inicia una ventana desde cero; los impactos de la ejecución anterior no se reutilizan.</summary>
        public bool BeginAttack(int attackIndex)
        {
            if (IsAttacking || windows == null || attackIndex < 0 || attackIndex >= windows.Length || windows[attackIndex] == null) return false;
            currentIndex = attackIndex;
            elapsed = 0f;
            hitTargets.Clear();
            windowOpen = false;
            hitbox.enabled = false;
            return true;
        }

        public void CancelAttack()
        {
            if (!IsAttacking) return;
            SetWindowOpen(false, windows[currentIndex]);
            currentIndex = -1;
        }

        private void SetWindowOpen(bool open, AttackWindow attack)
        {
            windowOpen = open;
            if (open)
            {
                damageDealer.Configure(attack.Damage);
                hitTargets.Clear();
            }
            else hitTargets.Clear();
            hitbox.enabled = open;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!windowOpen || other == null) return;
            ApplyHit(other);
        }

        /// <summary>
        /// Consulta física explícita para que el hitbox funcione también con colliders
        /// estáticos o CharacterController, sin depender del orden de OnTriggerEnter.
        /// </summary>
        private void CheckOverlaps()
        {
            if (hitbox == null || damageDealer == null) return;
            Bounds bounds = hitbox.bounds;
            Collider[] overlaps = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider other in overlaps) ApplyHit(other);
        }

        private void ApplyHit(Collider other)
        {
            if (!windowOpen || other == null || other.transform.root == transform.root) return;
            int targetId = other.transform.root.GetInstanceID();
            if (!hitTargets.Add(targetId)) return;
            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 direction = other.transform.position - transform.position;
            if (!damageDealer.ApplyTo(other.gameObject, point, direction)) hitTargets.Remove(targetId);
        }
    }
}
