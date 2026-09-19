using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Optional cube-particle breath; damage remains owned by DragonBossController.</summary>
    public sealed class DragonBreathVfx : MonoBehaviour
    {
        [SerializeField] ParticleSystem particles;
        public void Configure(ParticleSystem value) => particles = value;
        public void SetEmitting(bool active)
        {
            if (particles == null) return;
            var emission = particles.emission; emission.enabled = active;
            if (active && !particles.isPlaying) particles.Play();
            if (!active && particles.isPlaying) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        public void Aim(Vector3 direction, float range, Color color)
        {
            if (particles == null || direction.sqrMagnitude < .001f) return;
            particles.transform.rotation = Quaternion.LookRotation(direction);
            var main = particles.main; main.startColor = color; main.startSpeed = range / .8f;
        }
        void OnDisable() { if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    }
}
