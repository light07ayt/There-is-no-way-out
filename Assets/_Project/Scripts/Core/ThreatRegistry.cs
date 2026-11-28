using System.Collections.Generic;
using UnityEngine;

namespace Ogni.Core
{
    /// <summary>
    /// Реестр живых монстров (Слушатель, Тьма, Подражатель, Кицунэ).
    /// Монстры сами добавляются в OnEnable и удаляются в OnDisable, поэтому ИИ напарников и звук
    /// сердцебиения больше не сканируют всю сцену через FindObjectsByType&lt;MonoBehaviour&gt; (это были
    /// периодические фризы: в сцене тысячи MonoBehaviour, а сравнение шло по строковому имени типа).
    /// Лежит в Ogni.Core, чтобы Player/World/Entities не зависели друг от друга.
    /// </summary>
    public static class ThreatRegistry
    {
        private static readonly List<MonoBehaviour> Threats = new List<MonoBehaviour>(16);

        /// <summary>Только для чтения. Перебирать циклом for (без foreach), чтобы не плодить мусор.</summary>
        public static IReadOnlyList<MonoBehaviour> All => Threats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Threats.Clear();

        public static void Register(MonoBehaviour monster)
        {
            if (monster != null && !Threats.Contains(monster)) Threats.Add(monster);
        }

        public static void Unregister(MonoBehaviour monster)
        {
            Threats.Remove(monster);
        }
    }
}
