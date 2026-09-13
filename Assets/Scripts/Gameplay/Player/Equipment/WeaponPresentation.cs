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
        private Transform torso;
        private Vector3 torsoRestRootPosition;
        private Quaternion torsoRestRootRotation;
        private GameObject activeVisual, backVisual;
        private GameObject activeSecondVisual, backSecondVisual;
        public Transform ActiveSecondVisual => activeSecondVisual != null ? activeSecondVisual.transform : null;
        public Transform HolsteredSecondVisual => backSecondVisual != null ? backSecondVisual.transform : null;
        private Renderer[] embeddedSword;
        private Animator animator;
        private Renderer[] rigRenderers;
        private bool[] initialVisibility;
        public Transform ActiveHandAnchor => loadout != null && loadout.ActiveDefinition != null && loadout.ActiveDefinition.poseProfile != null
            ? loadout.ActiveDefinition.poseProfile.equipped.Resolve(transform, animator)
            : loadout != null && loadout.ActiveDefinition != null && loadout.ActiveDefinition.isBow ? leftHand : hand;
        public Transform ActiveVisual => activeVisual != null ? activeVisual.transform : null;
        public Transform HolsteredVisual => backVisual != null ? backVisual.transform : null;
        private void Start()
        {
            loadout = GetComponent<EquipmentLoadout>(); motor = GetComponent<PlayerMotor>();
            // Scenes may retain an invisible previous avatar with identically named bones.
            // Resolve only inside the rig actually driven by the character animator.
            var driver = GetComponent<Presentation.PlayerAnimationDriver>();
            animator = driver != null ? driver.Animator : GetComponentInChildren<Animator>();
            Transform rig = driver != null && driver.Animator != null ? driver.Animator.transform : motor.Visual;
            if (rig == null) rig = transform;
            var bones = rig.GetComponentsInChildren<Transform>(true);
            rigRenderers=rig.GetComponentsInChildren<Renderer>(true);
            initialVisibility=rigRenderers.Select(r=>r.enabled).ToArray();
            hand = bones.FirstOrDefault(t => t.name == "Hand.R"); leftHand = bones.FirstOrDefault(t => t.name == "Hand.L");
            // Resolve the actual Chest bone (detail meshes can also be named Chest).
            torso = bones.FirstOrDefault(t => t.name == "Chest" && hand != null && hand.IsChildOf(t))
                ?? bones.FirstOrDefault(t => t.name == "Chest");
            if(torso != null && animator != null)
            {
                torsoRestRootPosition=animator.transform.InverseTransformPoint(torso.position);
                torsoRestRootRotation=Quaternion.Inverse(animator.transform.rotation)*torso.rotation;
            }
            embeddedSword = rig.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Sword_E_RightHand" || r.name == "BasicSword").ToArray();
            loadout.Changed += Rebuild; Rebuild();
        }
        private void Rebuild()
        {
            for(int i=0;i<rigRenderers.Length;i++)if(rigRenderers[i]!=null)rigRenderers[i].enabled=initialVisibility[i];
            if (activeVisual != null) { activeVisual.SetActive(false); Destroy(activeVisual); }
            if (backVisual != null) { backVisual.SetActive(false); Destroy(backVisual); }
            if (activeSecondVisual != null) { activeSecondVisual.SetActive(false); Destroy(activeSecondVisual); }
            if (backSecondVisual != null) { backSecondVisual.SetActive(false); Destroy(backSecondVisual); }
            var weapon = loadout.ActiveDefinition;
            bool hasEmbedded = embeddedSword.Any(r => r is SkinnedMeshRenderer);
            foreach (var renderer in embeddedSword) renderer.enabled = weapon != null && !weapon.dualWield && weapon.poseProfile == null && !weapon.isBow && (renderer is SkinnedMeshRenderer || !hasEmbedded);
            if(weapon!=null && weapon.poseProfile!=null)weapon.poseProfile.HideEmbeddedVisuals(animator);
            if (weapon != null && weapon.visualPrefab != null && (weapon.dualWield || weapon.poseProfile != null || weapon.isBow || !hasEmbedded)) activeVisual = Instantiate(weapon.visualPrefab, transform);
            if (loadout.SecondaryDefinition != null && loadout.SecondaryDefinition.visualPrefab != null) backVisual = Instantiate(loadout.SecondaryDefinition.visualPrefab, transform);
            if (weapon != null && weapon.SecondaryVisualPrefab != null) activeSecondVisual = Instantiate(weapon.SecondaryVisualPrefab, transform);
            if (loadout.SecondaryDefinition != null && loadout.SecondaryDefinition.SecondaryVisualPrefab != null)
                backSecondVisual = Instantiate(loadout.SecondaryDefinition.SecondaryVisualPrefab, transform);
        }
        private void LateUpdate()
        {
            if (loadout == null) return;
            var weapon = loadout.ActiveDefinition;
            Quaternion facing = Quaternion.LookRotation(motor.Facing);
            if (activeSecondVisual != null && weapon != null)
            {
                activeSecondVisual.SetActive(GetComponent<WeaponSkillEffects>()?.BucklerAbsent!=true);
                ApplyProfile(activeSecondVisual, weapon.secondaryEquipped);
            }
            if (backSecondVisual != null && loadout.SecondaryDefinition != null)
                ApplyHolsteredProfile(backSecondVisual, loadout.SecondaryDefinition.secondaryHolstered);
            if (weapon != null && weapon.poseProfile == null && weapon.isBow)
            {
                var cast = loadout.Runner.Current;
                float draw = cast != null && cast.Definition.pose == AbilityPose.Bow
                    ? (!cast.Began ? Mathf.Clamp01(cast.Elapsed / Mathf.Max(.01f, cast.Definition.preparation)) : Mathf.Clamp01(1 - (cast.Elapsed - (cast.ReleasedAt >= 0 ? cast.ReleasedAt : cast.Definition.preparation)) / .12f)) : 0;
                AimArm(leftHand, transform.position + facing * new Vector3(-.28f, 1.25f, .64f), transform.position + facing * new Vector3(-1, .8f, .15f));
                AimArm(hand, transform.position + facing * new Vector3(.08f + draw * .14f, 1.27f, .48f - draw * .32f), transform.position + facing * new Vector3(1, 1.15f, -.2f));
            }
            if (activeVisual != null)
            {
                if (weapon.poseProfile != null) ApplyProfile(activeVisual, weapon.poseProfile.equipped);
                else
                {
                var anchor = weapon.isBow ? leftHand : hand;
                Vector3 position = anchor != null ? anchor.position : transform.position + facing * new Vector3(.4f, 1, .1f);
                activeVisual.transform.SetPositionAndRotation(position + facing * weapon.handOffset, facing * Quaternion.Euler(weapon.handRotation));
                }
            }
            if (backVisual != null)
            {
                var secondary = loadout.SecondaryDefinition;
                if (secondary.poseProfile != null) ApplyHolsteredProfile(backVisual, secondary.poseProfile.holstered);
                else
                {
                    // Use the driven visual root, not the physics root. PlayerLocomotionLean
                    // applies the authored body tilt there; a weapon on the back follows it.
                    ApplyTorsoFollow(backVisual.transform, secondary.backOffset, Quaternion.Euler(secondary.backRotation), facing);
                }
            }
        }
        private void ApplyHolsteredProfile(GameObject visual, WeaponAttachmentPose pose)
        {
            var anchor=pose.Resolve(transform,animator);
            visual.SetActive(anchor!=null);
            if(anchor==null)return;
            pose.Apply(visual.transform,anchor,animator);
            if(pose.anchor==WeaponAnchor.Character)
                ApplyTorsoFollow(visual.transform,pose.offset,Quaternion.Euler(pose.rotation),
                    motor!=null && motor.Visual!=null?motor.Visual.rotation:motor!=null?Quaternion.LookRotation(motor.Facing):transform.rotation);
        }
        private void ApplyTorsoFollow(Transform visual, Vector3 offset, Quaternion rotation, Quaternion fallbackBasis)
        {
            if(torso==null || animator==null || motor==null)
            {
                Quaternion basisFallback=motor!=null && motor.Visual!=null?motor.Visual.rotation:fallbackBasis;
                Vector3 originFallback=motor!=null && motor.Visual!=null?motor.Visual.position:transform.position;
                visual.SetPositionAndRotation(originFallback+basisFallback*offset,basisFallback*rotation);
                return;
            }
            Quaternion rootRotation=animator.transform.rotation;
            Quaternion currentRootTorsoRotation=Quaternion.Inverse(rootRotation)*torso.rotation;
            Quaternion torsoDelta=currentRootTorsoRotation*Quaternion.Inverse(torsoRestRootRotation);
            Vector3 currentRootTorsoPosition=animator.transform.InverseTransformPoint(torso.position);
            Vector3 localPosition=currentRootTorsoPosition+torsoDelta*(offset-torsoRestRootPosition);
            Quaternion basis=motor.Visual!=null?motor.Visual.rotation:rootRotation;
            Vector3 origin=motor.Visual!=null?motor.Visual.position:transform.position;
            visual.SetPositionAndRotation(origin+basis*localPosition,basis*torsoDelta*rotation);
        }
        private void ApplyProfile(GameObject visual, WeaponAttachmentPose pose)
        {
            var anchor = pose.Resolve(transform, animator);
            visual.SetActive(anchor != null);
            if (anchor != null) pose.Apply(visual.transform, anchor, animator);
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
