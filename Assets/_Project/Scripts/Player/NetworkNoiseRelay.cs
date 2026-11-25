using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Пересылает шум локального игрока (шаги, прыжки) на сервер,
    /// чтобы Слушатель на хосте слышал не только хоста, но и подключённых клиентов.
    /// </summary>
    public class NetworkNoiseRelay : NetworkBehaviour
    {
        public void EmitNoise(Vector3 position, float radius)
        {
            if (radius <= 0f)
            {
                return;
            }

            if (IsServer)
            {
                NoiseBus.Emit(position, radius, gameObject);
                return;
            }

            if (IsOwner)
            {
                EmitNoiseServerRpc(position, radius);
            }
        }

        [ServerRpc]
        private void EmitNoiseServerRpc(Vector3 position, float radius)
        {
            NoiseBus.Emit(position, radius, gameObject);
        }
    }
}
