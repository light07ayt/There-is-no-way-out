using Ogni.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ogni.Player
{
    /// <summary>
    /// Единственное место, которое знает про клавиши и мышь.
    /// Читает привязки клавиш из GameSettings и обновляет их на лету при смене в меню Settings.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        private InputAction _move;
        private InputAction _look;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _interact;
        private InputAction _jump;
        private InputAction _flashlight;
        private InputAction _gather;
        private InputAction _fire;
        private InputAction _reload;
        private InputAction _craftMenu;
        private InputAction _build;
        private readonly InputAction[] _slots = new InputAction[7];
        private bool _enabledState;

        public Vector2 Move => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;
        public bool SprintHeld => _sprint != null && _sprint.IsPressed();
        public bool CrouchHeld => _crouch != null && _crouch.IsPressed();
        public bool InteractPressed => _interact != null && _interact.WasPressedThisFrame();
        public bool JumpPressed => _jump != null && _jump.WasPressedThisFrame();
        public bool GatherPressed => _gather != null && _gather.WasPressedThisFrame();
        public bool FlashlightPressed => _flashlight != null && _flashlight.WasPressedThisFrame();
        public bool FirePressed => _fire != null && _fire.WasPressedThisFrame();
        public bool FireHeld => _fire != null && _fire.IsPressed();
        public bool FireReleased => _fire != null && _fire.WasReleasedThisFrame();
        public bool ReloadPressed => _reload != null && _reload.WasPressedThisFrame();
        public bool CraftMenuPressed => _craftMenu != null && _craftMenu.WasPressedThisFrame();
        public bool BuildPressed => _build != null && _build.WasPressedThisFrame();

        /// <summary>Нажатый слот инструмента 0..6 (клавиши 1-7) или -1.</summary>
        public int SlotPressed
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null && _slots[i].WasPressedThisFrame()) return i;
                }

                return -1;
            }
        }

        private void Awake()
        {
            GameSettings.EnsureLoaded();
            BuildActions();
            GameSettings.BindingsChanged += RebuildActions;
        }

        private void RebuildActions()
        {
            bool wasEnabled = _enabledState;
            DisposeActions();
            BuildActions();
            if (wasEnabled) EnableActions();
        }

        private void BuildActions()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", GameSettings.GetBindingPath(GameAction.MoveUp))
                .With("Down", GameSettings.GetBindingPath(GameAction.MoveDown))
                .With("Left", GameSettings.GetBindingPath(GameAction.MoveLeft))
                .With("Right", GameSettings.GetBindingPath(GameAction.MoveRight));

            _look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            _sprint = new InputAction("Sprint", InputActionType.Button, GameSettings.GetBindingPath(GameAction.Sprint));
            _crouch = new InputAction("Crouch", InputActionType.Button, GameSettings.GetBindingPath(GameAction.Crouch));
            _interact = new InputAction("Interact", InputActionType.Button, GameSettings.GetBindingPath(GameAction.Interact));
            _jump = new InputAction("Jump", InputActionType.Button, GameSettings.GetBindingPath(GameAction.Jump));
            _flashlight = new InputAction("Flashlight", InputActionType.Button, GameSettings.GetBindingPath(GameAction.Flashlight));
            _gather = new InputAction("Gather", InputActionType.Button, GameSettings.GetBindingPath(GameAction.Gather));
            _fire = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
            _reload = new InputAction("Reload", InputActionType.Button, GameSettings.GetBindingPath(GameAction.Reload));
            _craftMenu = new InputAction("CraftMenu", InputActionType.Button, GameSettings.GetBindingPath(GameAction.CraftMenu));
            _build = new InputAction("Build", InputActionType.Button, GameSettings.GetBindingPath(GameAction.Build));
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new InputAction("Slot" + (i + 1), InputActionType.Button, "<Keyboard>/" + (i + 1));
            }
        }

        private void OnEnable()
        {
            _enabledState = true;
            EnableActions();
        }

        private void OnDisable()
        {
            _enabledState = false;
            DisableActions();
        }

        private void OnDestroy()
        {
            GameSettings.BindingsChanged -= RebuildActions;
            DisposeActions();
        }

        private void EnableActions()
        {
            _move?.Enable();
            _look?.Enable();
            _sprint?.Enable();
            _crouch?.Enable();
            _interact?.Enable();
            _jump?.Enable();
            _flashlight?.Enable();
            _gather?.Enable();
            _fire?.Enable();
            _reload?.Enable();
            _craftMenu?.Enable();
            _build?.Enable();
            foreach (InputAction a in _slots) a?.Enable();
        }

        private void DisableActions()
        {
            _move?.Disable();
            _look?.Disable();
            _sprint?.Disable();
            _crouch?.Disable();
            _interact?.Disable();
            _jump?.Disable();
            _flashlight?.Disable();
            _gather?.Disable();
            _fire?.Disable();
            _reload?.Disable();
            _craftMenu?.Disable();
            _build?.Disable();
            foreach (InputAction a in _slots) a?.Disable();
        }

        private void DisposeActions()
        {
            DisableActions();
            _move?.Dispose();
            _look?.Dispose();
            _sprint?.Dispose();
            _crouch?.Dispose();
            _interact?.Dispose();
            _jump?.Dispose();
            _flashlight?.Dispose();
            _gather?.Dispose();
            _fire?.Dispose();
            _reload?.Dispose();
            _craftMenu?.Dispose();
            _build?.Dispose();
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i]?.Dispose();
                _slots[i] = null;
            }
        }
    }
}
