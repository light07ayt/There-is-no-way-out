using Ogni.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Ogni.Player
{
    /// <summary>
    /// Тестовый объект для проверки системы взаимодействия на этапе 1.
    /// Не является игровой механикой; будет удалён, когда появятся настоящие
    /// интерактивные объекты.
    /// </summary>
    public class TestInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Взаимодействовать";
        [SerializeField] private UnityEvent onInteract;

        public string Prompt => prompt;

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            Debug.Log($"[TestInteractable] {name}: взаимодействие от {interactor.name}");
            onInteract?.Invoke();
        }
    }
}
