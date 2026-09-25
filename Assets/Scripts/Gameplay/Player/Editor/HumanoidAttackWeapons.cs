using System;
using Mismo.Gameplay.Player.Equipment;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Visuals only: use the workshop's attachment contract without running weapon gameplay.
    public sealed class HumanoidAttackWeapons : IDisposable
    {
        readonly HumanoidAttackRig rig;
        readonly GameObject root;
        readonly Renderer[] bodyRenderers;
        readonly bool[] bodyVisibility;
        GameObject mainPrefab, offhandPrefab;
        public GameObject Main { get; private set; }
        public GameObject Offhand { get; private set; }
        public string Warning { get; private set; }

        public HumanoidAttackWeapons(HumanoidAttackRig rig)
        {
            this.rig = rig;
            bodyRenderers = rig.Container.GetComponentsInChildren<Renderer>(true);
            bodyVisibility = Array.ConvertAll(bodyRenderers, r => r.enabled);
            root = new GameObject("Armas del ataque (vista)");
            root.transform.SetParent(rig.Container.transform, false);
        }

        public bool Refresh(HumanoidAttackRecipe recipe)
        {
            var main = recipe.previewWeapon;
            var second = recipe.previewPair && main != null && !main.isTwoHanded
                ? (recipe.previewOffhand != null ? recipe.previewOffhand : main) : null;
            string previousWarning = Warning;
            Warning = null;
            bool changed = Replace(main != null ? main.visualPrefab : null, ref mainPrefab, Main, out var primary);
            Main = primary;
            changed |= Replace(second != null ? second.visualPrefab : null, ref offhandPrefab, Offhand, out var secondary);
            Offhand = secondary;
            bool visible = recipe.showPreviewWeapons;
            changed |= root.activeSelf != visible;
            root.SetActive(visible);
            RestoreBodyVisibility();
            if (visible)
            {
                main?.poseProfile?.HideEmbeddedVisuals(rig.Animator);
                second?.poseProfile?.HideEmbeddedVisuals(rig.Animator);
                if (main != null)
                {
                    if (Main == null) AddWarning("El arma principal no tiene Visual Prefab en el taller de armas.");
                    else
                    {
                        var pose = main.poseProfile != null ? main.poseProfile.equipped : new WeaponAttachmentPose
                        {
                            anchor = WeaponAnchor.RightHand, offset = main.handOffset, rotation = main.handRotation
                        };
                        changed |= Apply(Main, pose, "principal");
                    }
                }
                if (second != null)
                {
                    if (Offhand == null) AddWarning("El arma secundaria no tiene Visual Prefab en el taller de armas.");
                    else changed |= Apply(Offhand, second.secondaryEquipped, "secundaria");
                }
            }
            return changed || previousWarning != Warning;
        }

        bool Replace(GameObject prefab, ref GameObject previous, GameObject instance, out GameObject result)
        {
            result = instance;
            if (prefab == previous && (prefab == null || instance != null)) return false;
            if (instance != null) Object.DestroyImmediate(instance);
            previous = prefab;
            result = null;
            if (prefab == null) return true;
            // Configure the copy while inactive, before showing its renderers.
            bool active = root.activeSelf;
            root.SetActive(false);
            try
            {
                result = Object.Instantiate(prefab, root.transform);
                result.name = prefab.name + " (vista)";
                foreach (var behaviour in result.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                foreach (var body in result.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
                foreach (var collider in result.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var particles in result.GetComponentsInChildren<ParticleSystem>(true)) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var trail in result.GetComponentsInChildren<TrailRenderer>(true)) { trail.emitting = false; trail.Clear(); trail.enabled = false; }
            }
            finally { root.SetActive(active); }
            return true;
        }

        bool Apply(GameObject visual, WeaponAttachmentPose pose, string label)
        {
            var anchor = pose?.Resolve(rig.Animator.transform, rig.Animator);
            bool changed = visual.activeSelf != (anchor != null);
            visual.SetActive(anchor != null);
            if (anchor == null)
            {
                AddWarning("No se encontró el anclaje del arma " + label + ". Revisá su agarre en el taller de armas para este personaje.");
                return changed;
            }
            var transform = visual.transform;
            Vector3 position = transform.position, scale = transform.localScale;
            Quaternion rotation = transform.rotation;
            pose.Apply(transform, anchor, rig.Animator);
            return changed || position != transform.position || rotation != transform.rotation || scale != transform.localScale;
        }

        void AddWarning(string message) => Warning = string.IsNullOrEmpty(Warning) ? message : Warning + "\n" + message;
        void RestoreBodyVisibility()
        {
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null && bodyRenderers[i].enabled != bodyVisibility[i]) bodyRenderers[i].enabled = bodyVisibility[i];
        }
        public void Dispose()
        {
            RestoreBodyVisibility();
            if (root != null) Object.DestroyImmediate(root);
            Main = Offhand = null;
        }
    }
}
