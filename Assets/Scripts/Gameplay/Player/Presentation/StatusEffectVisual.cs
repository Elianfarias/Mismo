using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Cosmetic overlay follows original meshes/bones, without changing their materials.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(500)]
    public sealed class StatusEffectVisual : MonoBehaviour
    {
        sealed class Binding { public Renderer source, overlay; }
        readonly List<Binding> bindings = new List<Binding>();
        MaterialPropertyBlock properties;
        StatusEffectPresentation profile;
        GameObject root, aura;
        ParticleSystemRenderer[] particleRenderers;
        float elapsed;
        static readonly int TimeId = Shader.PropertyToID("_EffectTime");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        public bool IsPlaying => root != null;
        public int OverlayCount => bindings.Count;

        public bool PlayPoison()
        {
            if (IsPlaying) return true;
            if (!isActiveAndEnabled) return false;
            profile = StatusEffectPresentation.Current;
            if (profile == null || profile.poisonOverlay == null || profile.poisonParticles == null) return false;
            if (properties == null) properties = new MaterialPropertyBlock();
            root = new GameObject("Poison status VFX") { hideFlags = HideFlags.DontSave };
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            // Separate hierarchy keeps cosmetic meshes out of hitboxes, death fragments and bounds scans.
            foreach (var source in GetComponentsInChildren<Renderer>())
            {
                if (!(source is MeshRenderer || source is SkinnedMeshRenderer) || !source.enabled) continue;
                Mesh mesh = source is SkinnedMeshRenderer skin ? skin.sharedMesh : source.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || mesh.subMeshCount == 0) continue;
                var go = new GameObject(source.name + " poison overlay");
                go.transform.SetParent(root.transform, false); go.layer = source.gameObject.layer;
                Renderer overlay;
                if (source is SkinnedMeshRenderer original)
                {
                    var copy = go.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = mesh; copy.bones = original.bones; copy.rootBone = original.rootBone;
                    copy.localBounds = original.localBounds; copy.quality = original.quality;
                    copy.updateWhenOffscreen = original.updateWhenOffscreen;
                    overlay = copy;
                }
                else
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    overlay = go.AddComponent<MeshRenderer>();
                }
                var materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = profile.poisonOverlay;
                overlay.sharedMaterials = materials;
                overlay.shadowCastingMode = ShadowCastingMode.Off; overlay.receiveShadows = false;
                overlay.lightProbeUsage = LightProbeUsage.Off; overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
                overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                overlay.renderingLayerMask = source.renderingLayerMask;
                bindings.Add(new Binding { source = source, overlay = overlay });
            }
            aura = Instantiate(profile.poisonParticles, root.transform);
            particleRenderers = aura.GetComponentsInChildren<ParticleSystemRenderer>();
            elapsed = 0; UpdateVisual(0);
            foreach (var particles in aura.GetComponentsInChildren<ParticleSystem>()) particles.Play(false);
            return true;
        }
        void LateUpdate() => UpdateVisual(Time.deltaTime);
        void UpdateVisual(float dt)
        {
            if (root == null) return;
            elapsed += dt;
            properties.SetFloat(TimeId, elapsed);
            properties.SetFloat(IntensityId, profile.overlayIntensity * Mathf.SmoothStep(0, 1, elapsed / .2f));
            bool found = false; Bounds bounds = default;
            foreach (var binding in bindings)
            {
                var source = binding.source; var overlay = binding.overlay;
                if (overlay == null) continue;
                overlay.enabled = source != null && source.enabled && source.gameObject.activeInHierarchy && !source.forceRenderingOff;
                if (!overlay.enabled) continue;
                overlay.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                overlay.transform.localScale = source.transform.lossyScale;
                overlay.SetPropertyBlock(properties);
                if (source is SkinnedMeshRenderer skin && overlay is SkinnedMeshRenderer copy)
                {
                    copy.localBounds = skin.localBounds;
                    for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) copy.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                }
                if (!found) { bounds = source.bounds; found = true; } else bounds.Encapsulate(source.bounds);
            }
            if (!found) bounds = new Bounds(transform.position + Vector3.up * .9f, new Vector3(.65f, 1.8f, .65f));
            // Body collider excludes long weapons and wide ears from the cloud's footprint.
            var body = GetComponent<Collider>();
            if (body != null && body.enabled) bounds = body.bounds;
            float height = Mathf.Max(.25f, bounds.size.y);
            float width = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z), height * .28f, height * .8f);
            if (aura != null)
            {
                aura.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
                aura.transform.localScale = new Vector3(width, height / 1.8f, width);
                foreach (var renderer in particleRenderers) renderer.SetPropertyBlock(properties);
            }
        }
        public void Stop()
        {
            if (root != null)
            {
                root.SetActive(false);
                if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
            }
            root = aura = null; particleRenderers = null; bindings.Clear(); elapsed = 0;
        }
        void OnDisable() => Stop();
        void OnDestroy() => Stop();
    }
}
