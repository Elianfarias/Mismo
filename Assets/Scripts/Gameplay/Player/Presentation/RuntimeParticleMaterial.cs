using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public static class RuntimeParticleMaterial
    {
        public static Material Create(string name, Color color)
        {
            // Resources is an explicit build dependency; Shader.Find alone is stripped in players.
            var shader = Resources.Load<Shader>("CombatParticles");
            if (shader == null) { Debug.LogError("Missing combat particle shader."); return null; }
            return new Material(shader) { name = name, color = color };
        }
    }
}
