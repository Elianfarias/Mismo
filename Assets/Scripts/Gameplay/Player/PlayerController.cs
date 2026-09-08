using Mismo.Gameplay.Player.Input;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Equipment;
using UnityEngine;

namespace Mismo.Gameplay.Player
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor), typeof(Stamina))]
    [RequireComponent(typeof(EquipmentLoadout))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform cameraBasis;
        private PlayerInputReader input;
        private PlayerMotor motor;
        private Stamina stamina;
        private EquipmentLoadout loadout;
        public bool IsSprinting { get; private set; }
        public void Configure(Transform basis) => cameraBasis = basis;
        private void Awake()
        {
            input = GetComponent<PlayerInputReader>(); motor = GetComponent<PlayerMotor>(); stamina = GetComponent<Stamina>();
            loadout = GetComponent<EquipmentLoadout>(); loadout.Initialize();
            if (GetComponent<Presentation.MovementFeedback>() == null) gameObject.AddComponent<Presentation.MovementFeedback>();
            if (GetComponent<Mismo.Gameplay.Combat.Health>() != null && GetComponent<Presentation.PlayerHUD>() == null) gameObject.AddComponent<Presentation.PlayerHUD>();
        }
        private void Update()
        {
            float dt = Time.deltaTime;
            var runner = loadout.Runner; var belt = loadout.Belt;
            if (cameraBasis == null && UnityEngine.Camera.main != null) cameraBasis = UnityEngine.Camera.main.transform;
            if (cameraBasis == null || !input.isActiveAndEnabled) { runner.Tick(dt); return; }
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1);
            Vector3 forward = Vector3.ProjectOnPlane(cameraBasis.forward, Vector3.up).normalized;
            Vector3 direction = forward * move.y + Vector3.Cross(Vector3.up, forward) * move.x;
            belt?.TickCooldown(dt);
            if (input.WasSwapWeaponPressedThisFrame()) loadout.TrySwap();
            if (input.WasDashPressedThisFrame()) runner.TryDash(direction.sqrMagnitude > 0 ? direction : motor.Facing);
            if (input.WasAttackPressedThisFrame()) Request(AbilitySlot.Basic, direction);
            if (input.WasLungePressedThisFrame()) Request(AbilitySlot.Q, direction);
            if (input.WasParryPressedThisFrame()) Request(AbilitySlot.E, direction);
            if (input.WasSpinAttackPressedThisFrame()) Request(AbilitySlot.R, direction);
            runner.SetHeld(input.AttackHeld);
            if (runner.Current != null && !runner.Current.Began && runner.Current.Definition.aimFromCamera && !runner.Current.Definition.targetsGround) RefreshAim(runner.Current);
            bool weaponMovement = runner.IsMoving;
            runner.Tick(dt);
            bool special = belt != null && belt.IsActive;
            if (special) { Vector3 displacement = belt.Step(dt); motor.RequestControlledDisplacement(displacement, displacement, 0); }
            IsSprinting = stamina.Tick(input.SprintHeld && move.sqrMagnitude > .01f && motor.Speed > .05f && motor.IsGrounded && !special && !runner.IsBusy, dt);
            var collisions = motor.Tick(direction * runner.Mobility, IsSprinting, input.WasJumpPressedThisFrame(), input.JumpHeld, dt);
            if ((collisions & CollisionFlags.Sides) != 0) { if (special) belt.Cancel(); if (weaponMovement || runner.IsMoving) runner.Cancel(); }
            if (runner.IsBusy && runner.Current.Definition.aimFromCamera) motor.Face(runner.Current.Direction);
        }
        private void RefreshAim(AbilityExecution cast)
        {
            Ray ray=WeaponAim.RayFrom(cameraBasis);
            float distance=cast.Definition.range+Vector3.Distance(cameraBasis.position,transform.position);
            Vector3 point=ray.GetPoint(distance);
            var hits=Physics.RaycastAll(ray,distance,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits) { if(hit.transform.root==transform.root)continue;point=hit.point;break; }
            cast.AimPoint=point;cast.Direction=(point-WeaponAim.Muzzle(gameObject)).normalized;
        }
        private void Request(AbilitySlot slot, Vector3 move)
        {
            var weapon = loadout.ActiveDefinition; var ability = weapon != null ? weapon.GetAbility(slot) : null;
            if (ability == null) return;
            Vector3 direction = move.sqrMagnitude > .001f ? move.normalized : motor.Facing;
            Vector3 point = transform.position;
            Vector3? aimPoint = null;
            if (ability.aimFromCamera || ability.targetsGround)
            {
                Ray aimRay = WeaponAim.RayFrom(cameraBasis);
                float aimDistance = ability.range + Vector3.Distance(cameraBasis.position, transform.position);
                Vector3 aim = aimRay.GetPoint(aimDistance);
                var hits = Physics.RaycastAll(aimRay, aimDistance, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits) { if (hit.transform.root == transform.root) continue; aim = hit.point; break; }
                Vector3 origin = WeaponAim.Muzzle(gameObject);
                direction = (aim - origin).normalized;
                aimPoint = aim;
                if (ability.targetsGround)
                {
                    Vector3 offset = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(aim - transform.position, Vector3.up), ability.range);
                    var groundHits = Physics.RaycastAll(transform.position + offset + Vector3.up * 15, Vector3.down, 35, ~0, QueryTriggerInteraction.Ignore);
                    System.Array.Sort(groundHits, (a, b) => a.distance.CompareTo(b.distance));
                    bool found = false;
                    foreach (var ground in groundHits)
                    { if (ground.transform.root == transform.root || ground.collider.GetComponentInParent<Mismo.Gameplay.Combat.IDamageReceiver>() != null || ground.normal.y < .65f) continue; point = ground.point; found = true; break; }
                    if (!found || Vector3.Distance(transform.position, point) > ability.range) return;
                }
            }
            loadout.Runner.TryUse(slot, direction, point, aimPoint, slot == AbilitySlot.Basic && input.AttackHeld);
        }
    }
}
