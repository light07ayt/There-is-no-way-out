using UnityEngine;

namespace Ogni.Core
{
    /// <summary>
    /// Общий интерфейс для всего, что может получать урон (игроки, постройки, сущности).
    /// Живёт в Core, чтобы сущности не зависели напрямую от сборок Player и Building.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        Transform TargetTransform { get; }
        void ApplyDamage(float amount, GameObject source);
    }
}
