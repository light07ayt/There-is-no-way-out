using UnityEngine;
using System;

namespace Ogni.Core
{
    /// <summary>
    /// Предмет мира, которому генератор присваивает стабильный индекс (одинаковый на всех клиентах
    /// при одинаковом Seed). Нужен, чтобы сервер и клиенты ссылались на одно и то же дерево.
    /// </summary>
    public interface IWorldIndexed
    {
        void SetWorldIndex(int index);
    }

    /// <summary>Статические события мира, не требующие ссылок между сборками.</summary>
    public static class WorldEvents
    {
        /// <summary>Генератор мира сейчас пересоздаёт локальные объекты (смена Seed / старт сессии).</summary>
        public static event Action Regenerating;

        public static void RaiseRegenerating() => Regenerating?.Invoke();
    }

    /// <summary>
    /// Общие параметры озера (уровень воды и границы) в сборке Ogni.Core,
    /// чтобы Ogni.Player мог определять плавание без зависимости от сборки Ogni.World.
    /// </summary>
    public static class WaterZoneState
    {
        public static Vector2 LakeCenter = new Vector2(-120f, -125f);
        public static float LakeRadiusX = 36f;
        public static float LakeRadiusZ = 28f;
        public static float WaterLevelY = -0.35f;

        public static float LakeNormDist(float wx, float wz)
        {
            float dx = (wx - LakeCenter.x) / Mathf.Max(1f, LakeRadiusX);
            float dz = (wz - LakeCenter.y) / Mathf.Max(1f, LakeRadiusZ);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
