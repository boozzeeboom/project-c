using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System;

namespace ProjectC.Core
{
    /// <summary>
    /// Менеджер сетевого соединения для Project C
    /// Управляет подключениями, отключениями, обработкой обрывов
    /// </summary>
    public class NetworkManagerController : MonoBehaviour
    {
        [Header("Настройки сервера")]
        [SerializeField] private string serverIp = "127.0.0.1";
        [SerializeField] private ushort serverPort = 7777;

        private Unity.Netcode.NetworkManager networkManager;

        // События для UI
        public event Action<string> OnConnectionStatusChanged;
        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong> OnPlayerDisconnected;

        private void Awake()
        {
            networkManager = GetComponent<Unity.Netcode.NetworkManager>();
            if (networkManager == null)
                networkManager = gameObject.AddComponent<Unity.Netcode.NetworkManager>();

            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback += HandleClientConnected;
                networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
                networkManager.OnServerStarted += HandleServerStarted;
                networkManager.OnTransportFailure += HandleTransportFailure;
            }
        }

        private void OnDisable()
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= HandleClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                networkManager.OnServerStarted -= HandleServerStarted;
                networkManager.OnTransportFailure -= HandleTransportFailure;
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            OnPlayerConnected?.Invoke(clientId);

            if (IsHost)
                UpdateStatus($"Хост запущен. Игроков: {networkManager.ConnectedClients.Count}");
            else
                UpdateStatus("Подключено к серверу");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            OnPlayerDisconnected?.Invoke(clientId);

            if (IsHost)
                UpdateStatus($"Хост запущен. Игроков: {networkManager.ConnectedClients.Count}");
        }

        private void HandleServerStarted()
        {
            UpdateStatus($"Сервер запущен на порту {serverPort}");
        }

        private void HandleTransportFailure()
        {
            Debug.LogError("[Network] Ошибка транспорта!");
            UpdateStatus("Ошибка подключения");
        }

        public void StartHost()
        {
            networkManager.StartHost();
            UpdateStatus("Запуск хоста...");
        }

        /// <summary>
        /// Запустить сервер (dedicated, без клиента)
        /// </summary>
        public void StartServer()
        {
            networkManager.StartServer();
            UpdateStatus("Запуск сервера...");
        }

        /// <summary>
        /// Подключиться к серверу как клиент
        /// </summary>
        public void ConnectToServer(string ipAddress = null, ushort port = 0)
        {
            string targetIp = string.IsNullOrEmpty(ipAddress) ? serverIp : ipAddress;
            ushort targetPort = port == 0 ? serverPort : port;

            var transport = networkManager.NetworkConfig.NetworkTransport;
            if (transport is UnityTransport unityTransport)
            {
                unityTransport.SetConnectionData(targetIp, targetPort);
            }

            UpdateStatus($"Подключение к {targetIp}:{targetPort}...");

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
                UpdateStatus("Отключено");
            }
        }

        private void UpdateStatus(string status)
        {
            OnConnectionStatusChanged?.Invoke(status);
        }

        public bool IsServer => networkManager != null && networkManager.IsServer;
        public bool IsClient => networkManager != null && networkManager.IsClient;
        public bool IsHost => networkManager != null && networkManager.IsHost;
        public bool IsConnected => networkManager != null && networkManager.IsListening;
        public int ConnectedPlayers => networkManager != null ? networkManager.ConnectedClients.Count : 0;
    }
}
