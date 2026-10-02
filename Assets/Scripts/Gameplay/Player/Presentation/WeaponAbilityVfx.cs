using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    // Owns only visuals. AbilityRunner remains the owner of charging, release and cancellation.
    [DefaultExecutionOrder(310), DisallowMultipleComponent]
    public sealed class WeaponAbilityVfx : MonoBehaviour
    {
        sealed class Effect
        {
            public GameObject root;
            public WeaponVfxDefinition definition;
            public ParticleSystem[] particles;
            public bool[] resumeParticles;
            public Vector3 scale;
            public float remaining;
        }

        readonly List<Effect> effects = new List<Effect>();
        EquipmentLoadout loadout;
        WeaponPresentation presentation;
        Health health;
        WeaponVfxDefinition[] abilityEffects, modifierEffects;
        bool paused;

        void Awake()
        {
            loadout = GetComponent<EquipmentLoadout>();
            health = GetComponent<Health>();
            if (loadout != null) loadout.Changed += Clear;
            if (health != null) health.Died += OnDeath;
        }

        public void Begin(AbilityExecution cast)
        {
            Clear();
            if (cast == null || !isActiveAndEnabled) return;
            presentation = GetComponent<WeaponPresentation>();
            abilityEffects = cast.Definition.weaponVfx;
            var inventory = GetComponent<PlayerInventory>();
            if (inventory != null)
                modifierEffects = inventory.AbilityVisualModifier(cast.Weapon, cast.Definition)?.weaponVfx;
            if (cast.Began) Release();
            else if (cast.Chargeable || cast.Definition.preparation > 0) Play(WeaponVfxPhase.Preparation);
        }

        public void Release()
        {
            StopPhase(WeaponVfxPhase.Preparation);
            Play(WeaponVfxPhase.Execution);
            Play(WeaponVfxPhase.Active);
        }

        public void EndActive() => StopPhase(WeaponVfxPhase.Active);
        public void Complete()
        {
            StopPhase(WeaponVfxPhase.Preparation);
            EndActive();
            abilityEffects = modifierEffects = null;
        }

        public void SetCharge(float normalized)
        {
            foreach (var effect in effects)
                if (effect.root != null && effect.definition.phase == WeaponVfxPhase.Preparation)
                    effect.root.transform.localScale = effect.scale * Mathf.Lerp(1, Mathf.Max(.01f, effect.definition.fullChargeScale), Mathf.Clamp01(normalized));
        }

        void Play(WeaponVfxPhase phase)
        {
            if (!isActiveAndEnabled) return;
            Play(abilityEffects, phase);
            Play(modifierEffects, phase);
        }

        void Play(WeaponVfxDefinition[] definitions, WeaponVfxPhase phase)
        {
            if (definitions == null) return;
            foreach (var definition in definitions)
            {
                if (definition == null || definition.prefab == null || definition.phase != phase) continue;
                if(definition.anchor!=WeaponVfxAnchor.Weapon){Spawn(definition,transform);continue;}
                if(presentation==null)continue;
                if (definition.hand != WeaponVfxHand.Offhand) Spawn(definition, presentation.ActiveVisual);
                if (definition.hand != WeaponVfxHand.Main) Spawn(definition, presentation.ActiveSecondVisual);
            }
        }

        void Spawn(WeaponVfxDefinition definition, Transform visual)
        {
            var socket = definition.anchor==WeaponVfxAnchor.Weapon?WeaponVfxSocket.Resolve(visual, definition.socketId):transform;
            if (socket == null) return;
            var root = Instantiate(definition.prefab, socket, false);
            root.name = "Weapon VFX · " + definition.prefab.name;
            root.transform.localPosition = definition.localOffset;
            if(definition.anchor==WeaponVfxAnchor.AboveHead)
            {
                var body=GetComponent<CharacterController>();
                float top=body!=null?body.bounds.max.y:transform.position.y+2;
                root.transform.localPosition+=transform.InverseTransformPoint(new Vector3(transform.position.x,top+.65f,transform.position.z));
            }
            root.transform.localRotation = Quaternion.Euler(definition.localRotation);
            root.transform.localScale = Vector3.Scale(root.transform.localScale, definition.localScale);
            FaceCamera(root.transform,definition);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
            {
                var main = particle.main;
                main.useUnscaledTime = false;
                main.stopAction = ParticleSystemStopAction.None;
            }
            root.SetActive(true);
            foreach (var particle in particles)
                if (particle.gameObject.activeInHierarchy) particle.Play(false);
            effects.Add(new Effect { root = root, definition = definition, particles = particles,
                resumeParticles = new bool[particles.Length], scale = root.transform.localScale, remaining = Mathf.Max(.01f, definition.lifetime) });
        }

        void LateUpdate()
        {
            if (health != null && health.IsDead) { Clear(); return; }
            SetPaused(GameplayPause.IsPaused);
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                if (effect.root == null || !effect.root.activeInHierarchy) { Remove(i); continue; }
                FaceCamera(effect.root.transform,effect.definition);
                if (paused || effect.definition.phase != WeaponVfxPhase.Execution) continue;
                effect.remaining -= Time.deltaTime;
                if (effect.remaining <= 0) Remove(i);
            }
        }

        static void FaceCamera(Transform effect,WeaponVfxDefinition definition)
        {
            if(!definition.faceCamera)return;
            var camera=UnityEngine.Camera.main;
            if(camera!=null)effect.rotation=camera.transform.rotation*Quaternion.Euler(definition.localRotation);
        }

        void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            foreach (var effect in effects)
                for (int i = 0; i < effect.particles.Length; i++)
                {
                    var particle = effect.particles[i]; if (particle == null) continue;
                    if (value) { effect.resumeParticles[i] = particle.isPlaying; if (particle.isPlaying) particle.Pause(false); }
                    else if (effect.resumeParticles[i]) particle.Play(false);
                }
        }

        void StopPhase(WeaponVfxPhase phase)
        {
            for (int i = effects.Count - 1; i >= 0; i--) if (effects[i].definition.phase == phase) Remove(i);
        }
        void Remove(int index)
        {
            var root = effects[index].root; effects.RemoveAt(index);
            if (root == null) return;
            root.SetActive(false);
            if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
        }
        public void Clear()
        {
            for (int i = effects.Count - 1; i >= 0; i--) Remove(i);
            abilityEffects = modifierEffects = null; paused = false;
        }
        void OnDeath(DamageInfo _) => Clear();
        void OnDisable() => Clear();
        void OnDestroy()
        {
            Clear();
            if (loadout != null) loadout.Changed -= Clear;
            if (health != null) health.Died -= OnDeath;
        }
    }
}
