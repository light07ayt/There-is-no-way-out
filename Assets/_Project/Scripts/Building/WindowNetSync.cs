using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Сетевая синхронизация разбиваемых окон (<see cref="BreakableWindow"/>). Вешается на сценовый
    /// NetworkObject WorldSystems рядом с DoorNetSync. Сервер хранит набор разбитых id, рассылает разбитие
    /// клиентам и отдаёт снимок поздно подключившимся. Без сети (офлайн-тест) окна бьются локально.
    /// </summary>
    public class WindowNetSync : NetworkBehaviour
    {
        private const float MaxBreakDistanceSqr = 120f * 120f;

        private static WindowNetSync _instance;
        private readonly HashSet<int> _broken = new();

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

        public static void RequestBreak(BreakableWindow window, Vector3 hitPoint)
        {
            if (window == null || window.IsBroken) return;

            if (_instance != null && _instance.IsSpawned)
            {
                if (_instance.IsServer) _instance.BreakOnServer(window.WindowId, hitPoint);
                else _instance.BreakServerRpc(window.WindowId, hitPoint);
                return;
            }

            window.Break(hitPoint);
            window.EmitNoise();
        }

        private void BreakOnServer(int id, Vector3 hitPoint)
        {
            if (!BreakableWindow.TryGet(id, out BreakableWindow window)) return;
            if (!_broken.Add(id)) return;

            if (!IsClient) window.Break(hitPoint); // выделенный сервер без клиентской части
            window.EmitNoise();
            BreakClientRpc(id, hitPoint);
        }

        [ServerRpc(RequireOwnership = false)]
        private void BreakServerRpc(int id, Vector3 hitPoint, ServerRpcParams rpc = default)
        {
            if (!BreakableWindow.TryGet(id, out BreakableWindow window)) return;

            if (NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out NetworkClient client) &&
                client.PlayerObject != null &&
                (client.PlayerObject.transform.position - window.transform.position).sqrMagnitude > MaxBreakDistanceSqr)
            {
                return;
            }

            BreakOnServer(id, hitPoint);
        }

        [ClientRpc]
        private void BreakClientRpc(int id, Vector3 hitPoint)
        {
            if (BreakableWindow.TryGet(id, out BreakableWindow window)) window.Break(hitPoint);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSnapshotServerRpc(ServerRpcParams rpc = default)
        {
            if (_broken.Count == 0) return;
            int[] ids = new int[_broken.Count];
            _broken.CopyTo(ids);

            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpc.Receive.SenderClientId } }
            };
            SnapshotClientRpc(ids, target);
        }

        [ClientRpc]
        private void SnapshotClientRpc(int[] ids, ClientRpcParams clientRpcParams = default)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (BreakableWindow.TryGet(ids[i], out BreakableWindow window)) window.Break(window.transform.position);
            }
        }
    }
}
