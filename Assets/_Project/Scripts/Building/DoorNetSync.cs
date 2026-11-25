using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Общая сетевая синхронизация межкомнатных дверей (<see cref="InteriorDoor"/>).
    /// Вешается на сценовый NetworkObject WorldSystems. Хранит только отличия от «запечённого» состояния,
    /// поэтому поздно подключившийся клиент получает компактный снимок.
    /// Состояние двери: 0 - закрыта, +1/-1 - открыта в сторону, противоположную тому, кто её открыл
    /// (клиент присылает свой знак поворота, сервер проверяет дальность и применяет).
    /// Без сети (офлайн-тест) двери просто переключаются локально.
    /// </summary>
    public class DoorNetSync : NetworkBehaviour
    {
        private const float MaxUseDistanceSqr = 36f;

        private static DoorNetSync _instance;
        private readonly Dictionary<int, int> _changed = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public override void OnNetworkSpawn()
        {
            _instance = this;
            if (!IsServer) RequestSnapshotServerRpc();
        }

        public override void OnNetworkDespawn()
        {
            if (_instance == this) _instance = null;
        }

        public static void RequestToggle(InteriorDoor door, int swingSign, Vector3 fromPosition)
        {
            if (door == null) return;
            if (_instance != null && _instance.IsSpawned)
            {
                _instance.ToggleServerRpc(door.DoorId, swingSign >= 0 ? 1 : -1);
                return;
            }

            door.SetState(door.IsOpen ? 0 : (swingSign >= 0 ? 1 : -1));
            door.EmitNoise();
        }

        private int CurrentState(int doorId, InteriorDoor door)
            => _changed.TryGetValue(doorId, out int s) ? s : door.StartState;

        [ServerRpc(RequireOwnership = false)]
        private void ToggleServerRpc(int doorId, int swingSign, ServerRpcParams rpc = default)
        {
            if (!InteriorDoor.TryGet(doorId, out InteriorDoor door)) return;

            if (NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out NetworkClient client) &&
                client.PlayerObject != null &&
                (client.PlayerObject.transform.position - door.transform.position).sqrMagnitude > MaxUseDistanceSqr)
            {
                return;
            }

            int next = CurrentState(doorId, door) != 0 ? 0 : (swingSign >= 0 ? 1 : -1);
            _changed[doorId] = next;
            if (!IsClient) door.SetState(next); // выделенный сервер без клиентской части
            door.EmitNoise();
            ApplyClientRpc(doorId, next);
        }

        [ClientRpc]
        private void ApplyClientRpc(int doorId, int state)
        {
            if (InteriorDoor.TryGet(doorId, out InteriorDoor door)) door.SetState(state);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSnapshotServerRpc(ServerRpcParams rpc = default)
        {
            if (_changed.Count == 0) return;
            int[] ids = new int[_changed.Count];
            int[] states = new int[_changed.Count];
            int i = 0;
            foreach (KeyValuePair<int, int> kv in _changed)
            {
                ids[i] = kv.Key;
                states[i] = kv.Value;
                i++;
            }

            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpc.Receive.SenderClientId } }
            };
            SnapshotClientRpc(ids, states, target);
        }

        [ClientRpc]
        private void SnapshotClientRpc(int[] ids, int[] states, ClientRpcParams clientRpcParams = default)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (InteriorDoor.TryGet(ids[i], out InteriorDoor door)) door.SetState(states[i]);
            }
        }
    }
}
