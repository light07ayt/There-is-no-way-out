using System;
using UnityEngine;

namespace Ogni.Core
{
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly GameObject Source;

        public NoiseEvent(Vector3 position, float radius, GameObject source)
        {
            Position = position;
            Radius = radius;
            Source = source;
        }
    }

    /// <summary>
    /// Шина звуковых событий для ИИ-сущностей (Слушатель, ИИ-напарник).
    /// Позволяет издавать шум без жёсткой привязки к конкретным врагам.
    /// </summary>
    public static class NoiseBus
    {
        public static event Action<NoiseEvent> OnNoiseEmitted;

        public static void Emit(Vector3 position, float radius, GameObject source = null)
        {
            if (radius <= 0f)
            {
                return;
            }

            OnNoiseEmitted?.Invoke(new NoiseEvent(position, radius, source));
        }
    }
}
