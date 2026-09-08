using System.Linq;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    [DefaultExecutionOrder(250)]
    public sealed class WeaponPresentation : MonoBehaviour
    {
        private EquipmentLoadout loadout;
        private PlayerMotor motor;
        private Transform hand, leftHand;
        private GameObject activeVisual, backVisual;
        private Renderer[] embeddedSword;
        public Transform ActiveHandAnchor => loadout != null && loadout.ActiveDefinition != null && loadout.ActiveDefinition.isBow ? leftHand : hand;
        public Transform ActiveVisual => activeVisual != null ? activeVisual.transform : null;
        private void Start()
        {
            loadout = GetComponent<EquipmentLoadout>(); motor = GetComponent<PlayerMotor>();
            // Scenes may retain an invisible previous avatar with identically named bones.
            // Resolve only inside the rig actually driven by the character animator.
            var driver = GetComponent<Presentation.PlayerAnimationDriver>();
            Transform rig = driver != null && driver.Animator != null ? driver.Animator.transform : motor.Visual;
            if (rig == null) rig = transform;
            var bones = rig.GetComponentsInChildren<Transform>(true);
            hand = bones.FirstOrDefault(t => t.name == "Hand.R"); leftHand = bones.FirstOrDefault(t => t.name == "Hand.L");
            embeddedSword = rig.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Sword_E_RightHand" || r.name == "BasicSword").ToArray();
            loadout.Changed += Rebuild; Rebuild();
        }
        private void Rebuild()
        {
            if (activeVisual != null) { activeVisual.SetActive(false); Destroy(activeVisual); }
            if (backVisual != null) { backVisual.SetActive(false); Destroy(backVisual); }
            var weapon = loadout.ActiveDefinition;
            bool hasEmbedded = embeddedSword.Any(r => r is SkinnedMeshRenderer);
            foreach (var renderer in embeddedSword) renderer.enabled = weapon != null && !weapon.isBow && (renderer is SkinnedMeshRenderer || !hasEmbedded);
            if (weapon != null && weapon.visualPrefab != null && (weapon.isBow || !hasEmbedded)) activeVisual = Instantiate(weapon.visualPrefab, transform);
            if (loadout.SecondaryDefinition != null && loadout.SecondaryDefinition.visualPrefab != null) backVisual = Instantiate(loadout.SecondaryDefinition.visualPrefab, transform);
        }
        private void LateUpdate()
        {
            if (loadout == null) return;
            var weapon = loadout.ActiveDefinition;
            Quaternion facing = Quaternion.LookRotation(motor.Facing);
            if (weapon != null && weapon.isBow)
            {
                var cast = loadout.Runner.Current;
                float draw = cast != null && cast.Definition.pose == AbilityPose.Bow
                    ? (!cast.Began ? Mathf.Clamp01(cast.Elapsed / Mathf.Max(.01f, cast.Definition.preparation)) : Mathf.Clamp01(1 - (cast.Elapsed - (cast.ReleasedAt >= 0 ? cast.ReleasedAt : cast.Definition.preparation)) / .12f)) : 0;
                AimArm(leftHand, transform.position + facing * new Vector3(-.28f, 1.25f, .64f), transform.position + facing * new Vector3(-1, .8f, .15f));
                AimArm(hand, transform.position + facing * new Vector3(.08f + draw * .14f, 1.27f, .48f - draw * .32f), transform.position + facing * new Vector3(1, 1.15f, -.2f));
            }
            if (activeVisual != null)
            {
                var anchor = weapon.isBow ? leftHand : hand;
                Vector3 position = anchor != null ? anchor.position : transform.position + facing * new Vector3(.4f, 1, .1f);
                activeVisual.transform.SetPositionAndRotation(position + facing * weapon.handOffset, facing * Quaternion.Euler(weapon.handRotation));
            }
            if (backVisual != null)
            {
                var secondary = loadout.SecondaryDefinition;
                backVisual.transform.SetPositionAndRotation(transform.position + facing * secondary.backOffset, facing * Quaternion.Euler(secondary.backRotation));
            }
        }
        private static void AimArm(Transform wrist, Vector3 target, Vector3 pole)
        {
            if (wrist == null || wrist.parent == null || wrist.parent.parent == null) return;
            var upper = wrist.parent.parent;
            var lower = wrist.parent;
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, wrist.position);
            if (a < .001f || b < .001f) return;
            Vector3 direction = (target - upper.position).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(upper.position, target), Mathf.Abs(a - b) + .001f, a + b - .001f);
            target = upper.position + direction * distance;
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            Vector3 bend = Vector3.ProjectOnPlane(pole - upper.position, direction).normalized;
            Vector3 elbow = upper.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(wrist.position - lower.position, target - lower.position) * lower.rotation;
        }
        private void OnDestroy() { if (loadout != null) loadout.Changed -= Rebuild; }
    }
}
