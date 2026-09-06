using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    /// <summary>Datos inmutables de un impacto, sin acoplar al receptor a un arma concreta.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Source;
        public readonly Vector3 HitPoint;
        public readonly Vector3 Direction;

        public DamageInfo(float amount, GameObject source, Vector3 hitPoint, Vector3 direction)
        {
            Amount = Mathf.Max(0f, amount);
            Source = source;
            HitPoint = hitPoint;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }
    }

    /// <summary>Contrato mínimo que cualquier receptor de daño debe exponer.</summary>
    public interface IDamageReceiver
    {
        bool ReceiveDamage(DamageInfo damage);
    }
}
