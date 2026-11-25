using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Дверь, которую игрок ставит в «Стену с проёмом». Не реализует IInteractable сама:
    /// открывать/закрывать её по [E] вызывает <see cref="BuildPiece"/> (чтобы у объекта был один интерактив:
    /// без молотка — открыть/закрыть, с молотком — чинить/разбирать). Открывается от игрока, плавно, без зажима.
    /// </summary>
    public class BuiltDoor : NetworkBehaviour
    {
        [SerializeField] private Transform doorPivot;
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float creakNoiseRadius = 5f;

        private readonly NetworkVariable<int> _state = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private DoorSwing _swing;
        private int _appliedState;

        public bool IsOpen => _state.Value != 0;

        private void Awake()
        {
            if (doorPivot == null) doorPivot = transform;
            _swing = new DoorSwing(doorPivot, openAngle, false, 1);
        }

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += OnStateChanged;
            if (_state.Value != 0)
            {
                _appliedState = _state.Value;
                _swing.SetState(_state.Value);
            }
        }

        public override void OnNetworkDespawn() => _state.OnValueChanged -= OnStateChanged;

        private void OnStateChanged(int previous, int current)
        {
            if (current == _appliedState) return;
            _appliedState = current;
            _swing.SetState(current);
        }

        private void Update() => _swing.Tick(Time.deltaTime);

        /// <summary>fromWorld - позиция того, кто открывает (дверь уйдёт от него).</summary>
        public void RequestToggle(Vector3 fromWorld) => ToggleServerRpc(_swing.SwingSignAwayFrom(fromWorld) >= 0 ? 1 : -1);

        [ServerRpc(RequireOwnership = false)]
        private void ToggleServerRpc(int swingSign, ServerRpcParams rpc = default)
        {
            if (NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out NetworkClient c) &&
                c.PlayerObject != null &&
                (c.PlayerObject.transform.position - transform.position).sqrMagnitude > 64f)
            {
                return;
            }

            _state.Value = _state.Value != 0 ? 0 : (swingSign >= 0 ? 1 : -1);
            NoiseBus.Emit(transform.position, creakNoiseRadius, gameObject);
        }
    }
}
