using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Bounded cosmetic effects shared by enemies and the player.</summary>
    public sealed class ActorCombatVisuals : MonoBehaviour
    {
        Health health;
        float deathEffectDelay;
        Coroutine pendingDeath;
        public void DelayDeathEffect(float seconds) => deathEffectDelay = Mathf.Max(0, seconds);
        readonly List<Renderer> hidden = new List<Renderer>();
        void Awake() => health = GetComponent<Health>();
        void OnEnable() { health.Damaged += Hit; health.Died += Die; health.Changed += Changed; }
        void OnDisable() { health.Damaged -= Hit; health.Died -= Die; health.Changed -= Changed; CancelPendingDeath(); }
        void Hit(DamageInfo damage) => DamageNumbers.Show(transform.position + Vector3.up * 1.8f,
            health.LastDamageApplied, GetComponent<PlayerController>() != null);
        void Changed(float value, float maximum)
        {
            if (value <= 0) return;
            CancelPendingDeath();
            foreach (var renderer in hidden) if (renderer != null) renderer.enabled = true;
            hidden.Clear();
        }
        void Die(DamageInfo damage)
        {
            if (deathEffectDelay <= 0) { SpawnDeathCubes(damage); return; }
            CancelPendingDeath(); pendingDeath = StartCoroutine(DelayedDeath(damage));
        }
        System.Collections.IEnumerator DelayedDeath(DamageInfo damage)
        {
            yield return new WaitForSeconds(deathEffectDelay);
            pendingDeath = null;
            if (health.IsDead) SpawnDeathCubes(damage);
        }
        void CancelPendingDeath()
        { if (pendingDeath != null) StopCoroutine(pendingDeath); pendingDeath = null; }
        void SpawnDeathCubes(DamageInfo damage)
        {
            if (hidden.Count != 0) return;
            var go = new GameObject("Death cubes");
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = 240;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = .8f;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);
            var material = RuntimeParticleMaterial.Create("Death cubes", Color.white);
            renderer.sharedMaterial = material;
            go.AddComponent<DeathMaterialCleanup>().Material = material;
            var sources = GetComponentsInChildren<Renderer>();
            int meshCount = 0;
            foreach (var source in sources)
                if (source.enabled && (source is SkinnedMeshRenderer || source.GetComponent<MeshFilter>() != null)) meshCount++;
            int budget = Mathf.Max(1, 240 / Mathf.Max(1, meshCount));
            foreach (var source in sources)
            {
                if (!source.enabled) continue;
                Mesh mesh = null; bool baked = false;
                if (source is SkinnedMeshRenderer skin) { mesh = new Mesh(); skin.BakeMesh(mesh); baked = true; }
                else if (source.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;
                // Non-readable imported meshes still receive a bounded burst inside their bounds.
                Vector3[] vertices = mesh.isReadable ? mesh.vertices : null;
                Color color = GetComponent<PlayerController>() != null ? new Color(.28f,.39f,.48f) : new Color(.48f,.57f,.24f);
                var shared = source.sharedMaterial;
                if (shared != null && shared.HasProperty("_Color") && shared.color != Color.white) color = shared.color;
                for (int i = 0; i < budget; i++)
                {
                    Vector3 point = vertices != null && vertices.Length > 0
                        ? source.transform.TransformPoint(vertices[i * vertices.Length / budget])
                        : source.bounds.center + Vector3.Scale(Random.insideUnitSphere, source.bounds.extents);
                    // Imported rigs can bake scale into vertices. Keep every fragment inside
                    // the current rendered body rather than applying that scale twice.
                    if (!source.bounds.Contains(point))
                        point = source.bounds.center + Vector3.Scale(Random.insideUnitSphere, source.bounds.extents);
                    particles.Emit(new ParticleSystem.EmitParams {
                        position = point, velocity = (point-transform.position-Vector3.up).normalized * Random.Range(.8f,2.3f) + Vector3.up * 2,
                        startLifetime = Random.Range(1.1f,1.7f), startSize = Random.Range(.055f,.11f),
                        startColor = color * Random.Range(.75f,1.2f), rotation3D = Random.insideUnitSphere * 180
                    }, 1);
                }
                hidden.Add(source); source.enabled = false;
                if (baked) Destroy(mesh);
            }
            particles.Play(); Destroy(go, 2);
        }
    }
    public sealed class DeathMaterialCleanup : MonoBehaviour
    {
        public Material Material;
        void OnDestroy() { if (Material != null) Destroy(Material); }
    }
}
