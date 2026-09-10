using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    public enum WeaponAnchor { Character, RightHand, LeftHand, BonePath }

    [System.Serializable]
    public sealed class WeaponAttachmentPose
    {
        public WeaponAnchor anchor = WeaponAnchor.RightHand;
        [Tooltip("Ruta relativa al objeto del Animator. Para rigs Generic.")]
        public string bonePath;
        public Vector3 offset;
        public Vector3 rotation;
        [Min(.001f)] public float scale = 1;

        public Transform Resolve(Transform character, Animator animator)
        {
            if (anchor == WeaponAnchor.Character) return character;
            if (animator == null) return null;
            if (anchor == WeaponAnchor.BonePath)
                return string.IsNullOrEmpty(bonePath) ? null : animator.transform.Find(bonePath);
            if (!animator.isHuman) return null;
            return animator.GetBoneTransform(anchor == WeaponAnchor.RightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        }

        // Offsets are metres in the anchor's orientation, independent of imported rig scale.
        public Quaternion Orientation(Transform anchorTransform, Animator animator = null)
        {
            if(anchor != WeaponAnchor.Character || animator == null)return anchorTransform.rotation;
            // The rendered character can turn and lean independently of the physics root.
            // Holstered weapons live on the character's back, so they must inherit both
            // parts of that visual rotation instead of using the flat root yaw only.
            var motor=anchorTransform.GetComponent<Movement.PlayerMotor>();
            if(motor!=null && motor.Visual!=null)return motor.Visual.rotation;
            Vector3 forward=Vector3.ProjectOnPlane(motor!=null && motor.Visual!=null?motor.Facing:animator.transform.forward,Vector3.up);
            return forward.sqrMagnitude>.0001f?Quaternion.LookRotation(forward):anchorTransform.rotation;
        }
        public void Apply(Transform visual, Transform anchorTransform, Animator animator = null)
        {
            Quaternion basis=Orientation(anchorTransform,animator);
            visual.SetPositionAndRotation(anchorTransform.position + basis * offset,
                basis * Quaternion.Euler(rotation));
            Vector3 parentScale = visual.parent != null ? visual.parent.lossyScale : Vector3.one;
            visual.localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, scale / parentScale.z);
        }
    }

    [CreateAssetMenu(menuName = "Mismo/Player/Weapon Pose Profile")]
    public sealed class WeaponPoseProfile : ScriptableObject
    {
        [Tooltip("Rutas de renderers relativas al Animator que deben ocultarse, por ejemplo un arma integrada en el modelo.")]
        public string[] hiddenRendererPaths = new string[0];
        public WeaponAttachmentPose equipped = new WeaponAttachmentPose();
        public WeaponAttachmentPose holstered = new WeaponAttachmentPose { anchor = WeaponAnchor.Character, offset = new Vector3(0,1.2f,-.25f) };
        [Tooltip("Opcional. Mismo contrato de parámetros Motion, ActionTime y PlaybackRate del controlador base.")]
        public AnimatorOverrideController animations;
        public bool meleeTrail;
        [Tooltip("Punta de la estela, en coordenadas locales del prefab visual del arma.")]
        public Vector3 trailTip = Vector3.forward;
        public void HideEmbeddedVisuals(Animator animator)
        {
            if(animator==null || hiddenRendererPaths==null)return;
            foreach(string path in hiddenRendererPaths)
            {
                var target=string.IsNullOrEmpty(path)?animator.transform:animator.transform.Find(path);
                if(target!=null)foreach(var renderer in target.GetComponents<Renderer>())renderer.enabled=false;
            }
        }
    }
}
