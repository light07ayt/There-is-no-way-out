using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.World
{
    /// <summary>
    /// Страховка для WorldSystems: если NGO не заспавнил in-scene NetworkObject при старте
    /// Соло/Хоста (сбитый GlobalObjectIdHash / InScenePlaced), спавним его вручную на сервере,
    /// чтобы RuntimeWorldGenerator и DayNightCycle гарантированно получили OnNetworkSpawn.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class WorldSpawnGuard : MonoBehaviour
    {
        private NetworkObject _no;

        private void Awake() => _no = GetComponent<NetworkObject>();

        private void OnEnable() => StartCoroutine(Watch());

        private IEnumerator Watch()
        {
            while (true)
            {
                NetworkManager nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening && nm.IsServer)
                {
                    yield return null;
                    yield return null; // даём NGO самому заспавнить сцену
                    if (_no != null && !_no.IsSpawned)
                    {
                        Debug.LogWarning("[WorldSpawnGuard] WorldSystems не был заспавнен NGO — спавню вручную. " +
                                         "Пересоберите сцену: Ogni -> Build World Scene.");
                        _no.Spawn(false);
                    }

                    while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) yield return null;
                }

                yield return null;
            }
        }
    }
}
