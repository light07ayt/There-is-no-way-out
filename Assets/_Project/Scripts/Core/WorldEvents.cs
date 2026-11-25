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
}
