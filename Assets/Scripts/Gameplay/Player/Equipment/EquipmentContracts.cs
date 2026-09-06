using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    /// <summary>Información mínima que el combate necesita del arma equipada.</summary>
    public interface IWeapon
    {
        string Id { get; }
        string DisplayName { get; }
        float BasicAttackCooldown { get; }
    }

    /// <summary>Acciones mínimas que el gameplay necesita del cinturón equipado.</summary>
    public interface IBelt
    {
        event System.Action<float> CooldownStarted;
        event System.Action CooldownReady;
        bool IsActive { get; }
        float CooldownRemaining { get; }
        void TickCooldown(float dt);
        bool TryStart(Vector3 direction, bool grounded);
        Vector3 Step(float dt);
        void Cancel();
    }
}
