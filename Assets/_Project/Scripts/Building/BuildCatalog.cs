using Unity.Netcode;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Сценовый каталог префабов построек (индекс = индекс в BuildPieces.All).
    /// Регистрирует сетевые префабы в Awake, до старта сети - как RuntimeWorldGenerator.
    /// </summary>
    public class BuildCatalog : MonoBehaviour
    {
        public static BuildCatalog Instance { get; private set; }

        [SerializeField] private GameObject[] prefabs;

        public GameObject Get(int index) => prefabs != null && index >= 0 && index < prefabs.Length ? prefabs[index] : null;

        private void Awake()
        {
            Instance = this;
            if (prefabs == null || NetworkManager.Singleton == null) return;
            foreach (GameObject p in prefabs)
            {
                if (p != null && !NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(p)) NetworkManager.Singleton.AddNetworkPrefab(p);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
