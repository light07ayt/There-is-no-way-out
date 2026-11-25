using System;
using Ogni.Core;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Ищет объект, на который смотрит игрок, и по кнопке взаимодействия
    /// вызывает у него IInteractable.Interact.
    /// Вешается на игрока. Луч пускается из камеры.
    /// Для чистоты рекомендуется поместить игрока на слой Player и убрать этот
    /// слой из interactionMask.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform viewOrigin;
        [SerializeField] private float range = 2.5f;

        [Tooltip("Радиус луча в метрах. Чем больше, тем легче навестись на мелкий предмет.")]
        [SerializeField] private float aimRadius = 0.15f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private PlayerInputReader _input;
        private IInteractable _current;

        /// <summary>Подсказка для интерфейса. Пустая строка, если взаимодействовать не с чем.</summary>
        public string CurrentPrompt { get; private set; } = string.Empty;

        /// <summary>Клавиша для текущей подсказки: F - сбор ресурсов, E - всё остальное.</summary>
        public string CurrentKeyLabel => _current is IGatherable ? "F" : "E";

        /// <summary>Вызывается при смене подсказки (в том числе на пустую).</summary>
        public event Action<string> PromptChanged;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            if (viewOrigin == null && Camera.main != null)
            {
                viewOrigin = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (viewOrigin == null)
            {
                return;
            }

            _current = FindInteractable();
            SetPrompt(_current != null ? _current.Prompt : string.Empty);

            bool pressed = _current is IGatherable ? _input.GatherPressed : _input.InteractPressed;
            if (_current != null && pressed)
            {
                _current.Interact(gameObject);
            }
        }

        private IInteractable FindInteractable()
        {
            // SphereCast вместо тонкого луча: допускает небольшую неточность прицела.
            // Коллайдеры, в которых начинается сфера (в том числе собственный
            // CharacterController игрока), в результат не попадают.
            if (!Physics.SphereCast(viewOrigin.position, aimRadius, viewOrigin.forward, out RaycastHit hit,
                    range, interactionMask, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(gameObject))
            {
                return null;
            }

            return interactable;
        }

        private void SetPrompt(string prompt)
        {
            if (prompt == CurrentPrompt)
            {
                return;
            }

            CurrentPrompt = prompt;
            PromptChanged?.Invoke(prompt);
        }
    }
}
