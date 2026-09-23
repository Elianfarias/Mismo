using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    [Serializable]
    public sealed class VegetationMotionSettings
    {
        public bool enabled = true;
        [Tooltip("Referencia directa para incluir el shader en la build.")]
        public Shader shader;
        [Range(0, 2)] public float windStrength = 1;
        [Range(0, 2)] public float gustStrength = .65f;
        [Range(0, 360)] public float directionDegrees = 35;
        [Range(.1f, 3)] public float windSpeed = .8f;
        public bool playerInteraction = true;
        [Range(.3f, 2.5f)] public float interactionRadius = 1.15f;
        [Range(0, .6f)] public float interactionStrength = .3f;
        [Range(.15f, 1.5f)] public float recoverySeconds = .7f;
    }

    // One update for the loaded world; individual plants have no per-frame CPU work.
    [DefaultExecutionOrder(200)]
    public sealed class VegetationMotionWorld : MonoBehaviour
    {
        public static VegetationMotionWorld Current { get; private set; }
        public VegetationMotionSettings Settings { get; private set; }
        public int MaterialCount => materials.Count;
        readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        readonly Vector4[] trail = new Vector4[8];
        Transform player;
        float clock, nextSample;
        Vector3 previousPosition;
        bool hasPosition;
        static readonly int Wind = Shader.PropertyToID("_MismoWind");
        static readonly int Motion = Shader.PropertyToID("_MismoMotion");
        static readonly int Interaction = Shader.PropertyToID("_MismoInteraction");
        static readonly int Trail = Shader.PropertyToID("_MismoPlayerTrail");
        static readonly Vector4[] EmptyTrail = new Vector4[8];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGlobals() { Current = null; ClearGlobals(); }
        static void ClearGlobals()
        {
            Shader.SetGlobalVector(Wind, Vector4.zero);
            Shader.SetGlobalVector(Interaction, Vector4.zero);
            Shader.SetGlobalVectorArray(Trail, EmptyTrail);
        }
        public void Initialize(WorldContentCatalog catalog, Transform target)
        {
            Current = this; player = target;
            Settings = catalog != null ? catalog.vegetationMotion : null;
            Array.Clear(trail, 0, trail.Length); hasPosition = false;
            Tick(0);
        }
        void LateUpdate() => Tick(Time.deltaTime);
        public void Tick(float deltaTime)
        {
            if (Current != this) return;
            if (Settings == null || !Settings.enabled) { Array.Clear(trail, 0, trail.Length); hasPosition = false; ClearGlobals(); return; }
            float dt = Mathf.Max(0, deltaTime);
            clock += dt;
            float angle = Settings.directionDegrees * Mathf.Deg2Rad;
            Shader.SetGlobalVector(Wind, new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), Mathf.Clamp(Settings.windStrength, 0, 2), Mathf.Clamp(Settings.gustStrength, 0, 2)));
            Shader.SetGlobalVector(Motion, new Vector4(clock, Mathf.Clamp(Settings.windSpeed, .1f, 3), 0, 0));
            float recovery = Mathf.Clamp(Settings.recoverySeconds, .15f, 1.5f);
            for (int i = 0; i < trail.Length; i++) trail[i].w = Mathf.Max(0, trail[i].w - dt / recovery);
            if (player != null && player.gameObject.activeInHierarchy && Settings.playerInteraction)
            {
                Vector3 p = player.position;
                // Teleports/respawns must not bend vegetation along an old position.
                if (hasPosition && (p - previousPosition).sqrMagnitude > 64) Array.Clear(trail, 0, trail.Length);
                if (clock >= nextSample || !hasPosition)
                {
                    for (int i = trail.Length - 1; i > 0; i--) trail[i] = trail[i - 1];
                    nextSample = clock + recovery / (trail.Length - 1);
                }
                trail[0] = new Vector4(p.x, p.y, p.z, 1);
                previousPosition = p; hasPosition = true;
            }
            else { Array.Clear(trail, 0, trail.Length); hasPosition = false; }
            Shader.SetGlobalVectorArray(Trail, trail);
            Shader.SetGlobalVector(Interaction, new Vector4(Mathf.Clamp(Settings.interactionRadius, .3f, 2.5f), Mathf.Clamp(Settings.interactionStrength, 0, .6f), 0, 0));
        }
        public Material MaterialFor(Material source)
        {
            if (source == null || Settings?.shader == null) return null;
            if (materials.TryGetValue(source, out var cached)) return cached;
            string name = source.shader.name;
            // Do not silently replace custom effects, transparency, or animated materials.
            bool vertexColor = name == "Mismo/Voxel Landscape";
            if (!vertexColor && name != "Universal Render Pipeline/Lit" && name != "Universal Render Pipeline/Simple Lit" && name != "Standard") return null;
            if (source.renderQueue >= 3000) return null;
            var result = new Material(Settings.shader) { name = source.name + " (vegetation runtime)", enableInstancing = true };
            string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            if (source.HasProperty(map))
            {
                result.SetTexture("_BaseMap", source.GetTexture(map));
                result.SetTextureScale("_BaseMap", source.GetTextureScale(map));
                result.SetTextureOffset("_BaseMap", source.GetTextureOffset(map));
            }
            string tint = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (source.HasProperty(tint)) result.SetColor("_BaseColor", source.GetColor(tint));
            result.SetFloat("_VertexColor", vertexColor ? 1 : 0);
            foreach (string property in new[] { "_Cutoff", "_Cull", "_Smoothness" })
                if (source.HasProperty(property)) result.SetFloat(property, source.GetFloat(property));
            result.SetFloat("_AlphaClip", source.IsKeywordEnabled("_ALPHATEST_ON") ? 1 : 0);
            materials.Add(source, result);
            return result;
        }
        void OnDisable() { if (Current == this) ClearGlobals(); }
        void OnDestroy()
        {
            if (Current == this) { Current = null; ClearGlobals(); }
            foreach (var material in materials.Values)
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            materials.Clear();
        }
    }
}
