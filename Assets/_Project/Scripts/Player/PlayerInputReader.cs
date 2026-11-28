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

        public Vector2 Move
        {
            get
            {
                Vector2 v = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
                if (v.sqrMagnitude > 0.001f) return v;
                Keyboard kb = Keyboard.current;
                if (kb == null) return Vector2.zero;
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                        - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                        - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                Vector2 fb = new Vector2(x, y);
                return fb.sqrMagnitude > 1f ? fb.normalized : fb;
            }
        }

        public Vector2 Look
        {
            get
            {
                Vector2 v = _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;
                if (v.sqrMagnitude > 0.0001f) return v;
                return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            }
        }

        public bool SprintHeld => (_sprint != null && _sprint.IsPressed()) || (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);
        public bool CrouchHeld => (_crouch != null && _crouch.IsPressed()) || (Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed);
        public bool InteractPressed => (_interact != null && _interact.WasPressedThisFrame()) || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);
        public bool JumpPressed => (_jump != null && _jump.WasPressedThisFrame()) || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
        public bool GatherPressed => (_gather != null && _gather.WasPressedThisFrame()) || (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame);
        public bool FlashlightPressed => (_flashlight != null && _flashlight.WasPressedThisFrame()) || (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame);
        public bool FirePressed => (_fire != null && _fire.WasPressedThisFrame()) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        public bool FireHeld => (_fire != null && _fire.IsPressed()) || (Mouse.current != null && Mouse.current.leftButton.isPressed);
        public bool FireReleased => (_fire != null && _fire.WasReleasedThisFrame()) || (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame);
        public bool ReloadPressed => (_reload != null && _reload.WasPressedThisFrame()) || (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);
        public bool CraftMenuPressed => (_craftMenu != null && _craftMenu.WasPressedThisFrame()) || (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame);
        public bool BuildPressed => (_build != null && _build.WasPressedThisFrame()) || (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame);

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
            try
            {
                BuildActions();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[PlayerInputReader] Сброс повреждённых привязок клавиш: " + ex.Message);
                GameSettings.ResetBindingsToDefault();
                BuildActions();
            }
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
