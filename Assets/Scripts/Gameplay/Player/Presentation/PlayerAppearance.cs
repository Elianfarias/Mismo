using System.Linq;
using Mismo.Core;
using Mismo.Gameplay.Player.World;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public static class PlayerAppearance
    {
        public const string Mage = "mage", Knight = "knight";
        public const int NameLimit = 24;
        public static string DisplayName => string.IsNullOrWhiteSpace(WorldSession.Current?.playerName)
            ? "Aventurero" : WorldSession.Current.playerName;
        public static bool ValidSkin(string id) => id == Mage || id == Knight;
        public static bool ValidName(string value) => !string.IsNullOrWhiteSpace(value) &&
            value.Trim().Length <= NameLimit && !value.Any(c => char.IsControl(c) || c == '<' || c == '>');
        public static GameObject Prefab(string id) => ValidSkin(id)
            ? ProjectAssets.Load<GameObject>("PlayerSkins/" + id) : null;

        // Called before animation and weapon presentation cache any rig references.
        // Existing saves without a skin retain their authored appearance.
        public static Animator Apply(GameObject player, Animator original)
        {
            if (original == null || !ValidSkin(WorldSession.Current?.playerSkin)) return original;
            var prefab = Prefab(WorldSession.Current.playerSkin);
            if (prefab == null) { Debug.LogError("Falta la apariencia del jugador en el catálogo."); return original; }
            var replacement = Object.Instantiate(prefab, original.transform.parent, false);
            replacement.name = "Player appearance";
            replacement.transform.localPosition = original.transform.localPosition;
            replacement.transform.localRotation = original.transform.localRotation;
            replacement.transform.localScale = original.transform.localScale;
            foreach (var t in replacement.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = player.layer;
            var animator = replacement.GetComponent<Animator>();
            animator.runtimeAnimatorController = original.runtimeAnimatorController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            player.GetComponent<Movement.PlayerMotor>()?.SetVisual(animator.transform);
            player.GetComponent<SwordAnimationFeedback>()?.ConfigureRig(animator.GetBoneTransform(HumanBodyBones.RightHand));
            replacement.GetComponent<MageGroundContact>()?.Initialize(animator, player.GetComponent<CharacterController>());
            original.gameObject.SetActive(false);
            if(Application.isPlaying) Object.Destroy(original.gameObject);
            else Object.DestroyImmediate(original.gameObject);
            return animator;
        }
    }
}
