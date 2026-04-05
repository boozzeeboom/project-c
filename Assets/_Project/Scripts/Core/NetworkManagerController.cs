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

        [Header("Reconnect")]
        [SerializeField] private bool enableAutoReconnect = true;
        [SerializeField] private float reconnectDelay = 3f;
        [SerializeField] private int maxReconnectAttempts = 5;

        private Unity.Netcode.NetworkManager networkManager;

        // Состояние reconnect
        private string _lastServerIp = "127.0.0.1";
        private ushort _lastServerPort = 7777;
        private int _reconnectAttempts = 0;
        private bool _isReconnecting = false;

        // События для UI
        public event Action<string> OnConnectionStatusChanged;
        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong> OnPlayerDisconnected;
        public event Action OnReconnectRequested; // Для UI кнопки Reconnect

        private void Awake()
        {
            networkManager = GetComponent<Unity.Netcode.NetworkManager>();
            if (networkManager == null)
                networkManager = gameObject.AddComponent<Unity.Netcode.NetworkManager>();

            DontDestroyOnLoad(gameObject);

            // Автоматический запуск Dedicated Server если передан аргумент -server
            if (IsDedicatedServerMode())
            {
                Debug.Log("[Network] Запуск в режиме Dedicated Server");
                Invoke(nameof(StartServer), 0.5f);
            }
        }

        /// <summary>
        /// Проверка режима Dedicated Server (аргумент командной строки -server)
        /// </summary>
        private bool IsDedicatedServerMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.ToLower() == "-server" || arg.ToLower() == "-dedicatedserver")
                    return true;
            }
            return false;
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

            // Запускаем авто-реконнект
            if (enableAutoReconnect && !_isReconnecting)
            {
                StartAutoReconnect();
            }
        }

        /// <summary>
        /// Запустить автоматический реконнект
        /// </summary>
        private void StartAutoReconnect()
        {
            _isReconnecting = true;
            _reconnectAttempts = 0;
            UpdateStatus($"Попытка восстановления соединения...");
            Invoke(nameof(TryReconnect), reconnectDelay);
        }

        /// <summary>
        /// Попытка реконнекта
        /// </summary>
        private void TryReconnect()
        {
            _reconnectAttempts++;

            if (_reconnectAttempts > maxReconnectAttempts)
            {
                _isReconnecting = false;
                UpdateStatus("Не удалось подключиться. Используйте кнопку Reconnect.");
                OnReconnectRequested?.Invoke();
                return;
            }

            Debug.Log($"[Network] Попытка реконнекта {_reconnectAttempts}/{maxReconnectAttempts}");
            UpdateStatus($"Реконнект... ({_reconnectAttempts}/{maxReconnectAttempts})");

            // Очищаем старое соединение
            if (networkManager.IsListening || networkManager.IsConnectedClient)
            {
                networkManager.Shutdown();
            }

            // Пересоздаём NetworkManager для чистого состояния
            ResetNetworkManager();

            // Подключаемся к последнему серверу
            ConnectToServer(_lastServerIp, _lastServerPort);
        }

        /// <summary>
        /// Сбросить NetworkManager (для чистого состояния)
        /// </summary>
        private void ResetNetworkManager()
        {
            if (networkManager != null)
            {
                OnDisable();
                networkManager.Shutdown();
                UnityEngine.Object.Destroy(networkManager);
            }
            networkManager = gameObject.AddComponent<Unity.Netcode.NetworkManager>();
            OnEnable();
        }

        /// <summary>
        /// Ручной реконнект (по кнопке UI)
        /// </summary>
        public void Reconnect()
        {
            if (_lastServerIp == null)
            {
                UpdateStatus("Нет сохранённого сервера для реконнекта");
                return;
            }

            Debug.Log($"[Network] Ручной реконнект к {_lastServerIp}:{_lastServerPort}");
            UpdateStatus($"Подключение к {_lastServerIp}:{_lastServerPort}...");

            ResetNetworkManager();
            ConnectToServer(_lastServerIp, _lastServerPort);
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

            // Сохраняем для реконнекта
            _lastServerIp = targetIp;
            _lastServerPort = targetPort;
            _isReconnecting = false;
            _reconnectAttempts = 0;

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
                // Отменяем реконнект
                CancelInvoke(nameof(TryReconnect));
                _isReconnecting = false;
                _reconnectAttempts = 0;

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
