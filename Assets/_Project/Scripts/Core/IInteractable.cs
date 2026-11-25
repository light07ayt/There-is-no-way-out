using UnityEngine;

namespace Ogni.Core
{
    /// <summary>
    /// Всё, с чем игрок может взаимодействовать по кнопке: двери, ящики, верстак, костёр и т.д.
    /// Интерфейс живёт в Core, чтобы любой модуль мог его реализовать,
    /// не завися от кода игрока.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Подсказка для игрока, например "Открыть ящик".</summary>
        string Prompt { get; }

        /// <summary>Можно ли сейчас взаимодействовать (например, ящик не сломан).</summary>
        bool CanInteract(GameObject interactor);

        /// <summary>Выполнить действие.</summary>
        void Interact(GameObject interactor);
    }
}
