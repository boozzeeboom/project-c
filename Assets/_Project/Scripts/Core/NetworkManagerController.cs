using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Менеджер сетевого соединения для Project C
    /// Управляет подключениями клиентов и хостом
    /// </summary>
    public class NetworkManagerController : MonoBehaviour
    {
        [Header("Настройки сервера")]
        [SerializeField] private string serverIp = "127.0.0.1";
        [SerializeField] private ushort serverPort = 7777;

        private Unity.Netcode.NetworkManager networkManager;

        private void Awake()
        {
            // Получаем или добавляем Network Manager
            networkManager = GetComponent<Unity.Netcode.NetworkManager>();
            
            if (networkManager == null)
            {
                networkManager = gameObject.AddComponent<Unity.Netcode.NetworkManager>();
            }

            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Запустить сервер (хост)
        /// </summary>
        public void StartHost()
        {
            networkManager.StartHost();
            Debug.Log($"[Server] Запущен хост на порту {serverPort}");
        }

        /// <summary>
        /// Запустить сервер (dedicated)
        /// </summary>
        public void StartServer()
        {
            networkManager.StartServer();
            Debug.Log($"[Server] Запущен сервер на порту {serverPort}");
        }

        /// <summary>
        /// Подключиться к серверу как клиент
        /// </summary>
        public void ConnectToServer(string ipAddress = null, ushort port = 0)
        {
            string targetIp = string.IsNullOrEmpty(ipAddress) ? serverIp : ipAddress;
            ushort targetPort = port == 0 ? serverPort : port;

            // Устанавливаем адрес подключения через NetworkManager
            var transport = networkManager.NetworkConfig.NetworkTransport;
            if (transport is Unity.Netcode.Transports.UTP.UnityTransport unityTransport)
            {
                unityTransport.SetConnectionData(targetIp, targetPort);
            }

            Debug.Log($"[Client] Подключение к {targetIp}:{targetPort}");

            networkManager.StartClient();
        }

        /// <summary>
        /// Отключиться от сервера
        /// </summary>
        public void Disconnect()
        {
            if (networkManager.IsConnectedClient || networkManager.IsListening)
            {
                networkManager.Shutdown();
                Debug.Log("[Network] Отключено от сервера");
            }
        }

        /// <summary>
        /// Проверка: мы сервер?
        /// </summary>
        public bool IsServer => networkManager.IsServer;

        /// <summary>
        /// Проверка: мы клиент?
        /// </summary>
        public bool IsClient => networkManager.IsClient;

        /// <summary>
        /// Проверка: мы хост?
        /// </summary>
        public bool IsHost => networkManager.IsHost;
    }
}
