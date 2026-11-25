using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Сетевая входная дверь: открывается и закрывается по кнопке [E]. Всегда открывается ОТ игрока,
    /// плавно разгоняется и тормозит, а пока качается - без коллизии (не зажмёт игрока в проёме).
    /// </summary>
    public class Door : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Transform doorPivot;
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float creakNoiseRadius = 4f;

        // 0 - закрыта, +1/-1 - открыта в одну из сторон.
        private readonly NetworkVariable<int> _state = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private DoorSwing _swing;
        private int _appliedState;

        public string Prompt => _state.Value != 0 ? "Закрыть дверь" : "Открыть дверь";

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

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            Vector3 from = interactor != null ? interactor.transform.position : transform.position;
            ToggleServerRpc(_swing.SwingSignAwayFrom(from) >= 0 ? 1 : -1);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ToggleServerRpc(int swingSign, ServerRpcParams rpc = default)
        {
            if (NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out NetworkClient c) &&
                c.PlayerObject != null &&
                (c.PlayerObject.transform.position - transform.position).sqrMagnitude > 36f)
            {
                return;
            }

            _state.Value = _state.Value != 0 ? 0 : (swingSign >= 0 ? 1 : -1);
            NoiseBus.Emit(transform.position, creakNoiseRadius, gameObject);
        }
    }
}
