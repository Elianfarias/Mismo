using System;
using System.Collections.Generic;
using UnityEngine;
using Mismo.Gameplay.Player.Movement;

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
    [DefaultExecutionOrder(150)]
    public sealed class AttackHitbox : MonoBehaviour
    {
        [SerializeField] private AttackWindow[] windows = { new AttackWindow() };
        private readonly HashSet<int> hitTargets = new HashSet<int>();
        private Collider hitbox;
        private DamageDealer damageDealer;
        private int currentIndex = -1;
        private float elapsed;
        private bool windowOpen;
        private PlayerMotor motor;
        private Vector3 facingOffset;
        private Quaternion facingRotation;
        private float attackSpeed=1,damageMultiplier=1;
        private string family; private float focusGain;

        public bool IsAttacking => currentIndex >= 0;
        public bool IsWindowOpen => windowOpen;
        public int CurrentAttackIndex => currentIndex;
        public int WindowCount => windows != null ? windows.Length : 0;
        public AttackWindow GetWindow(int index) => windows != null && index >= 0 && index < windows.Length ? windows[index] : null;
        public string CurrentAttackId => IsAttacking ? windows[currentIndex].Id : string.Empty;

        private void Awake()
        {
            hitbox = GetComponent<Collider>();
            damageDealer = GetComponent<DamageDealer>();
            hitbox.isTrigger = true;
            hitbox.enabled = false;
            motor=GetComponentInParent<PlayerMotor>();
            if(motor!=null)
            {
                facingOffset=motor.transform.InverseTransformPoint(transform.position);
                facingRotation=Quaternion.Inverse(motor.transform.rotation)*transform.rotation;
            }
        }

        private void Update()
        {
            if(motor!=null)
            {
                Quaternion facing=Quaternion.LookRotation(motor.Facing,Vector3.up);
                transform.SetPositionAndRotation(motor.transform.position+facing*Vector3.Scale(facingOffset,motor.transform.lossyScale),facing*facingRotation);
            }
            if (!IsAttacking) return;
            elapsed += Time.deltaTime*attackSpeed;
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
            var inventory=GetComponentInParent<Mismo.Gameplay.Player.Equipment.Inventory.PlayerInventory>();
            var weapon=GetComponentInParent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>()?.ActiveDefinition;
            attackSpeed=inventory!=null?inventory.AttackSpeed(weapon):1;
            damageMultiplier=inventory!=null?inventory.DamageMultiplier(weapon):1;
            family=weapon!=null?weapon.MasteryId:null; focusGain=GetComponentInParent<Mismo.Gameplay.Player.Equipment.AbilityRunner>()?.Current?.Definition.focusGainOnHit ?? weapon?.GetAbility(Mismo.Gameplay.Player.Equipment.AbilitySlot.Basic)?.focusGainOnHit ?? 0;
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
                damageDealer.Configure(attack.Damage*damageMultiplier,family,focusGain);
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
            var box=hitbox as BoxCollider;
            Vector3 scale=transform.lossyScale;
            Vector3 extents=box!=null?Vector3.Scale(box.size*.5f,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z))):bounds.extents;
            Collider[] overlaps = Physics.OverlapBox(box!=null?transform.TransformPoint(box.center):bounds.center, extents, box!=null?transform.rotation:Quaternion.identity,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider other in overlaps) ApplyHit(other);
        }

        private void ApplyHit(Collider other)
        {
            if (!windowOpen || other == null || other.transform.root == transform.root) return;
            if(!(other.GetComponentInParent<IDamageReceiver>() is Component receiver))return;
            // Multiple streamed enemies share a chunk/world root; deduplicate per actor.
            int targetId = receiver.GetInstanceID();
            if (!hitTargets.Add(targetId)) return;
            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 direction = other.transform.position - transform.position;
            damageDealer.ApplyTo(other.gameObject, point, direction);
        }
    }
}

