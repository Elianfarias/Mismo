using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>One-shot Ellen-style arrival on the active skin; shared assets are never mutated.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerArrivalVfx : MonoBehaviour
    {
        [SerializeField] Material revealMaterial;
        [SerializeField] GameObject particlesPrefab;
        [SerializeField, Min(.1f)] float duration = 3f;
        sealed class Binding
        {
            public Renderer renderer;
            public Material[] original, temporary;
            public MaterialPropertyBlock[] originalBlocks, blocks;
        }
        readonly List<Binding> bindings = new List<Binding>();
        readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        GameObject particles;
        float elapsed, particleLifetime, height, bottomOffset;
        public bool IsPlaying => bindings.Count > 0;
        public float Progress => Mathf.Clamp01(elapsed / duration);
        public int PlayCount { get; private set; }
        static readonly int ProgressId = Shader.PropertyToID("_ArrivalProgress");
        static readonly int OriginId = Shader.PropertyToID("_ArrivalOrigin");
        static readonly int HeightId = Shader.PropertyToID("_ArrivalHeight");

        public void Configure(Material reveal, GameObject particleTemplate, float seconds = 3f)
        { revealMaterial = reveal; particlesPrefab = particleTemplate; duration = Mathf.Max(.1f, seconds); }

        public bool Play()
        {
            if (!isActiveAndEnabled || revealMaterial == null || particlesPrefab == null) return false;
            Clear();
            var animator = GetComponent<PlayerAnimationDriver>()?.Animator ?? GetComponentInChildren<Animator>();
            var visual = animator != null ? animator.transform : transform;
            bool hasBounds = false; Bounds bounds = default;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                var originals = renderer.sharedMaterials;
                if (originals.Length == 0) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
                var binding = new Binding { renderer = renderer, original = originals, temporary = new Material[originals.Length],
                    originalBlocks = new MaterialPropertyBlock[originals.Length], blocks = new MaterialPropertyBlock[originals.Length] };
                for (int i = 0; i < originals.Length; i++)
                {
                    var original = originals[i];
                    if (original == null) continue;
                    if (!materials.TryGetValue(original, out var material))
                    {
                        material = new Material(revealMaterial) { name = original.name + " (arrival)", hideFlags = HideFlags.DontSave };
                        var map = original.HasProperty("_BaseMap") ? "_BaseMap" : original.HasProperty("_MainTex") ? "_MainTex" : null;
                        if (map != null)
                        {
                            material.SetTexture("_BaseMap", original.GetTexture(map));
                            material.SetTextureScale("_BaseMap", original.GetTextureScale(map));
                            material.SetTextureOffset("_BaseMap", original.GetTextureOffset(map));
                        }
                        material.SetColor("_BaseColor", original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white);
                        materials.Add(original, material);
                    }
                    binding.temporary[i] = material;
                    binding.originalBlocks[i] = new MaterialPropertyBlock();
                    binding.blocks[i] = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(binding.originalBlocks[i], i);
                    renderer.GetPropertyBlock(binding.blocks[i], i);
                }
                renderer.sharedMaterials = binding.temporary;
                bindings.Add(binding);
            }
            if (bindings.Count == 0) { Clear(); return false; }
            height = Mathf.Max(.5f, bounds.size.y);
            bottomOffset = bounds.min.y - transform.position.y;
            elapsed = 0;
            Apply();
            particles = Instantiate(particlesPrefab, transform);
            particles.transform.SetParent(null, true);
            particles.transform.SetPositionAndRotation(transform.position + Vector3.up * bottomOffset, Quaternion.identity);
            particles.transform.localScale = Vector3.one * Mathf.Clamp(height / 1.8f, .8f, 1.35f);
            particles.name = "Player arrival particles";
            particleLifetime = duration;
            foreach (var system in particles.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                particleLifetime = Mathf.Max(particleLifetime, main.startDelay.constantMax + main.duration + Mathf.Max(main.startLifetime.constantMax, main.startLifetime.curveMultiplier));
            }
            foreach (var system in particles.GetComponentsInChildren<ParticleSystem>(true)) system.Play(false);
            PlayCount++;
            return true;
        }
        void Update() => Tick(Time.deltaTime);
        void Tick(float deltaTime)
        {
            if (deltaTime <= 0 || !IsPlaying && particles == null) return;
            elapsed += deltaTime;
            if (IsPlaying)
            {
                if (elapsed >= duration) RestoreMaterials();
                else Apply();
            }
            if (particles != null && elapsed >= particleLifetime) { Release(particles); particles = null; }
        }
        void Apply()
        {
            var origin = transform.position + Vector3.up * bottomOffset;
            foreach (var binding in bindings)
            {
                if (binding.renderer == null) continue;
                for (int i = 0; i < binding.blocks.Length; i++)
                {
                    var block = binding.blocks[i]; if (block == null) continue;
                    block.SetFloat(ProgressId, Progress); block.SetVector(OriginId, origin); block.SetFloat(HeightId, height);
                    binding.renderer.SetPropertyBlock(block, i);
                }
            }
        }
        void RestoreMaterials()
        {
            foreach (var binding in bindings)
            {
                if (binding.renderer == null) continue;
                binding.renderer.sharedMaterials = binding.original;
                for (int i = 0; i < binding.originalBlocks.Length; i++) binding.renderer.SetPropertyBlock(binding.originalBlocks[i], i);
            }
            bindings.Clear();
            foreach (var material in materials.Values) Release(material);
            materials.Clear();
        }
        void Clear()
        {
            RestoreMaterials();
            if (particles != null) { particles.SetActive(false); Release(particles); particles = null; }
        }
        static void Release(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        void OnDisable() => Clear();
        void OnDestroy() => Clear();
    }
}
