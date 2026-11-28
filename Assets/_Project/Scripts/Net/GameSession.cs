using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Ogni.Net
{
    public enum SessionMode
    {
        None,
        Solo,
        Host,
        Client
    }

    /// <summary>
    /// Единая точка запуска и остановки игровой сессии.
    /// Остальной код игры не должен вызывать NetworkManager напрямую:
    /// так сетевой транспорт (прямое подключение, Relay, Steam) можно менять
    /// в одном месте.
    ///
    /// Соло - это хост, который слушает только локальный адрес. Вся игровая логика
    /// работает одинаково и в одиночной игре, и в кооперативе.
    ///
    /// Этап 2а: только прямое подключение по IP. Relay/Lobby - следующий шаг.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public const ushort DefaultPort = 7777;
        private const string LoopbackAddress = "127.0.0.1";
        private const string AnyAddress = "0.0.0.0";

        public SessionMode Mode { get; private set; } = SessionMode.None;

        public bool IsRunning =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>Одиночная игра: хост, доступный только с этого компьютера.</summary>
        public bool StartSolo()
        {
            if (!TryGetTransport(out UnityTransport transport))
            {
                return false;
            }

            transport.SetConnectionData(LoopbackAddress, DefaultPort, LoopbackAddress);
            return Finish(SessionMode.Solo, NetworkManager.Singleton.StartHost());
        }

        /// <summary>Хост кооперативной игры: принимает подключения по сети.</summary>
        public bool StartHost(ushort port = DefaultPort)
        {
            if (!TryGetTransport(out UnityTransport transport))
            {
                return false;
            }

            transport.SetConnectionData(LoopbackAddress, port, AnyAddress);
            return Finish(SessionMode.Host, NetworkManager.Singleton.StartHost());
        }

        /// <summary>Подключение к хосту по IP-адресу.</summary>
        public bool StartClient(string address, ushort port = DefaultPort)
        {
            if (!TryGetTransport(out UnityTransport transport))
            {
                return false;
            }

            transport.SetConnectionData(address, port);
            return Finish(SessionMode.Client, NetworkManager.Singleton.StartClient());
        }

        public void Shutdown()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SoloCompanionSpawner.DespawnAll();
            Mode = SessionMode.None;
        }

        private bool TryGetTransport(out UnityTransport transport)
        {
            transport = null;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("[GameSession] В сцене нет NetworkManager.");
                return false;
            }

            if (manager.IsListening)
            {
                Debug.LogWarning("[GameSession] Сессия уже запущена.");
                return false;
            }

            transport = manager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[GameSession] На NetworkManager нет компонента UnityTransport.");
                return false;
            }

            return true;
        }

        private bool Finish(SessionMode mode, bool started)
        {
            Mode = started ? mode : SessionMode.None;
            if (!started)
            {
                Debug.LogError($"[GameSession] Не удалось запустить режим {mode}.");
            }
            else
            {
                SoloCompanionSpawner.OnSessionStarted(mode);
            }

            return started;
        }
    }
}
