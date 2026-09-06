using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Cooldowns
{
    /// <summary>Contrato mínimo para cualquier consumidor de cooldowns.</summary>
    public interface ICooldownService
    {
        bool IsReady(string key);
        float Remaining(string key);
        bool TryStart(string key, float duration);
        void Tick(float deltaTime);
        void Reset(string key);
    }

    /// <summary>Reloj reutilizable para habilidades, armas y cinturones sin conocer sus implementaciones.</summary>
    public sealed class CooldownBook : ICooldownService
    {
        private readonly Dictionary<string, float> remaining = new Dictionary<string, float>();

        public bool IsReady(string key) => Remaining(key) <= 0f;

        public float Remaining(string key)
        {
            if (string.IsNullOrEmpty(key) || !remaining.TryGetValue(key, out float value)) return 0f;
            return Mathf.Max(0f, value);
        }

        public bool TryStart(string key, float duration)
        {
            if (string.IsNullOrEmpty(key) || !IsReady(key)) return false;
            remaining[key] = Mathf.Max(0f, duration);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (remaining.Count == 0) return;
            var keys = new List<string>(remaining.Keys);
            foreach (string key in keys) remaining[key] = Mathf.Max(0f, remaining[key] - deltaTime);
        }

        public void Reset(string key)
        {
            if (!string.IsNullOrEmpty(key)) remaining.Remove(key);
        }
    }
}
