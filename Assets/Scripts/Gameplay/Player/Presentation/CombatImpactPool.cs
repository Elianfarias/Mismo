using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Scene-local bounded pool. Each contact owns its particles and audio until both finish.</summary>
    public sealed class CombatImpactPool : MonoBehaviour
    {
        const int Capacity = 32;
        static CombatImpactPool instance;
        sealed class Voice
        {
            public GameObject prefab, root;
            public ParticleSystem[] particles;
            public ParticleSystemRenderer[] renderers;
            public readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            public AudioSource audio;
            public float age, limit;
            public bool active;
        }
        readonly List<Voice> voices = new List<Voice>();
        public int ActiveCount { get { int count = 0; foreach (var voice in voices) if (voice.active) count++; return count; } }
        public static CombatImpactPool Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("Confirmed combat impacts").AddComponent<CombatImpactPool>();
                return instance;
            }
        }

        public static void Confirm(DamageReceiver receiver, DamageInfo damage, HitResult result)
        {
            var defense = result.Outcome == HitOutcome.Block || result.Outcome == HitOutcome.Parry || result.Outcome == HitOutcome.PerfectParry;
            var profile = defense ? receiver.GetComponent<Equipment.EquipmentLoadout>()?.ActiveDefinition?.FeedbackProfile : damage.FeedbackProfile;
            if (profile == null) profile = damage.FeedbackProfile;
            if (profile == null) profile = receiver.GetComponent<Equipment.EquipmentLoadout>()?.ActiveDefinition?.FeedbackProfile;
            if (profile == null) return;
            CombatCue kind = profile.Select(damage, result);
            if (kind == CombatCue.None) return;
            var cue = profile.Get(kind);
            Instance.Play(cue, damage.HitPoint, defense ? -damage.Direction : damage.Direction);
            if (Application.isPlaying && cue != null && cue.feel != null)
            {
                var presentation = Instance.GetComponent<CombatFeelPlayer>();
                if (presentation == null) presentation = Instance.gameObject.AddComponent<CombatFeelPlayer>();
                presentation.Play(cue.feel, damage.HitPoint);
            }
            if (cue != null && cue.hitStop > 0) CombatTimeFeedback.HitStop(cue.hitStop);
        }

        public bool Play(CombatFeedbackCue cue, Vector3 point, Vector3 direction)
        {
            if (cue == null || cue.prefab == null && cue.sound == null) return false;
            Voice voice = voices.Find(v => !v.active && v.prefab == cue.prefab && v.root != null);
            if (voice == null)
            {
                // Never cut a simultaneous impact to make room for a new one.
                if (voices.Count >= Capacity)
                {
                    voice = voices.Find(v => !v.active);
                    if (voice == null) return false;
                    Destroy(voice.root); voices.Remove(voice);
                }
                var root = new GameObject("Impact voice"); root.transform.SetParent(transform);
                if (cue.prefab != null)
                {
                    var effect=Instantiate(cue.prefab,root.transform);
                    effect.transform.localPosition=Vector3.zero; // Anchor the prefab at the confirmed contact, ignoring saved scene placement.
                }
                voice = new Voice { root = root, prefab = cue.prefab, particles = root.GetComponentsInChildren<ParticleSystem>(true), renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true), audio = root.AddComponent<AudioSource>() };
                voice.audio.playOnAwake = false; voice.audio.spatialBlend = 1; voice.audio.minDistance = 3; voice.audio.maxDistance = 24;
                voice.audio.outputAudioMixerGroup = AudioRuntime.SfxGroup;
                foreach (var particle in voice.particles)
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particle.main; main.loop = false; main.playOnAwake = false;
                    main.stopAction = ParticleSystemStopAction.None; main.useUnscaledTime = false;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                voices.Add(voice);
            }
            voice.root.SetActive(true);
            voice.root.transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction : Vector3.forward) * Quaternion.Euler(cue.rotation));
            voice.root.transform.localScale = Vector3.one * Mathf.Max(.01f, cue.scale);
            voice.age = 0; voice.limit = Mathf.Clamp(cue.maximumLifetime, .1f, 10); voice.active = true;
            foreach (var renderer in voice.renderers)
            {
                renderer.GetPropertyBlock(voice.properties); voice.properties.SetColor("_Color", cue.tint); renderer.SetPropertyBlock(voice.properties);
            }
            foreach (var particle in voice.particles) { particle.Clear(false); particle.Play(false); }
            voice.audio.clip = cue.sound; voice.audio.volume = cue.volume;
            if (cue.sound != null) voice.audio.Play();
            return true;
        }

        void Update() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (GameplayPause.IsPaused || dt <= 0) return;
            foreach (var voice in voices)
            {
                if (!voice.active) continue;
                voice.age += dt;
                bool alive = voice.audio.isPlaying;
                foreach (var particle in voice.particles) if (particle != null && particle.IsAlive(false)) alive = true;
                if (voice.age < voice.limit && (alive || voice.age < .05f)) continue;
                voice.audio.Stop();
                foreach (var particle in voice.particles) if (particle != null) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                voice.root.SetActive(false); voice.active = false;
            }
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
