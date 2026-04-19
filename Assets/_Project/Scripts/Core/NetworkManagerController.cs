using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System;
using System.Collections;
using ProjectC.Items;
using ProjectC.Player;

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
            // Получаем или создаём NetworkManager
            networkManager = GetComponent<Unity.Netcode.NetworkManager>();
            if (networkManager == null)
            {
                networkManager = gameObject.AddComponent<Unity.Netcode.NetworkManager>();
            }

            // Проверяем и добавляем UnityTransport если нужно
            var transport = GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport == null)
            {
                transport = gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            }

            // Настраиваем NetworkConfig на использование UnityTransport
            if (networkManager.NetworkConfig.NetworkTransport == null)
            {
                networkManager.NetworkConfig.NetworkTransport = transport;
            }

            DontDestroyOnLoad(gameObject);

            // СЕССИЯ DIAGNOSTIC: Создаём TradeDebugTools для отладки
            CreateTradeDebugTools();

            // Автоматический запуск Dedicated Server если передан аргумент -server
            if (IsDedicatedServerMode())
            {
                Debug.Log("[Network] Запуск в режиме Dedicated Server");
                Invoke(nameof(StartServer), 0.5f);
            }
        }
        
        /// <summary>
        /// Создать TradeDebugTools для диагностики торговли.
        /// Это позволяет всегда видеть склад клиента на экране.
        /// </summary>
        private void CreateTradeDebugTools()
        {
            var existing = FindObjectsByType<ProjectC.Trade.TradeDebugTools>(FindObjectsInactive.Include);
            if (existing.Length > 0)
            {
                Debug.Log("[NMC] TradeDebugTools already exists, skipping creation");
                return;
            }
            
            var debugObj = new GameObject("TradeDebugTools");
            debugObj.transform.SetParent(transform); // Parent к NMC для сохранения при смене сцены
            var debugTools = debugObj.AddComponent<ProjectC.Trade.TradeDebugTools>();
            debugObj.SetActive(true);
        }
        
        private void Start()
        {
            // NetworkManager.Singleton устанавливается в Start()
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
            Debug.Log($"[NMC] HandleClientConnected: clientId={clientId}, IsServer={networkManager.IsServer}, IsClient={networkManager.IsClient}");
            Debug.Log($"[NMC] ConnectedClients.Count={networkManager.ConnectedClients.Count}");
            
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

            if (networkManager.IsListening || networkManager.IsConnectedClient)
            {
                networkManager.Shutdown();
            }

            ConnectToServer(_lastServerIp, _lastServerPort);
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

            _isReconnecting = false;
            _reconnectAttempts = 0;

            // Просто Shutdown + reconnect
            if (networkManager.IsListening || networkManager.IsConnectedClient)
            {
                networkManager.Shutdown();
            }

            ConnectToServer(_lastServerIp, _lastServerPort);
        }

        public void StartHost()
        {
            Debug.Log("[NMC] StartHost() called");
            StartCoroutine(StartHostCoroutine());
        }

        public IEnumerator StartHostCoroutine()
        {
            // Защита от конфликта порта - проверяем не слушает ли уже
            if (networkManager.IsListening)
            {
                Debug.LogWarning("[Network] Already listening! Shutting down first...");
                networkManager.Shutdown();
                
                // REFACTORED (R3-002): Используем корутину вместо Thread.Sleep
                // Это не блокирует UI thread
                yield return new WaitForSecondsRealtime(0.25f);
            }

            try
            {
                networkManager.StartHost();
                UpdateStatus("Хост запущен");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] Failed to start host: {ex.Message}");
                UpdateStatus("Ошибка запуска хоста - порт занят?");
            }
        }

        /// <summary>
        /// Запустить сервер (dedicated, без клиента)
        /// </summary>
        public void StartServer()
        {
            StartServerCoroutine();
        }

        private IEnumerator StartServerCoroutine()
        {
            // Защита от конфликта порта
            if (networkManager.IsListening)
            {
                Debug.LogWarning("[Network] Already listening! Shutting down first...");
                networkManager.Shutdown();
                
                // REFACTORED (R3-002): Используем корутину вместо Thread.Sleep
                yield return new WaitForSecondsRealtime(0.25f);
            }

            try
            {
                networkManager.StartServer();
                UpdateStatus($"Сервер запущен на порту {serverPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] Failed to start server: {ex.Message}");
                UpdateStatus("Ошибка запуска сервера - порт занят?");
            }
        }

        /// <summary>
        /// Подключиться к серверу как клиент
        /// </summary>
        public void ConnectToServer(string ipAddress = null, ushort port = 0)
        {
            StartCoroutine(ConnectToServerCoroutine(ipAddress, port));
        }

        private IEnumerator ConnectToServerCoroutine(string ipAddress = null, ushort port = 0)
        {
            string targetIp = string.IsNullOrEmpty(ipAddress) ? serverIp : ipAddress;
            ushort targetPort = port == 0 ? serverPort : port;

            // Сохраняем для реконнекта
            _lastServerIp = targetIp;
            _lastServerPort = targetPort;
            _isReconnecting = false;
            _reconnectAttempts = 0;

            // Защита от конфликта - если уже слушаем, shutdown
            if (networkManager.IsListening)
            {
                Debug.LogWarning("[Network] Already listening! Shutting down before connect...");
                networkManager.Shutdown();
                
                // REFACTORED (R3-002): Используем корутину вместо Thread.Sleep
                yield return new WaitForSecondsRealtime(0.25f);
            }

            Debug.Log($"[Network] ConnectToServer: {targetIp}:{targetPort}");

            var transport = networkManager.NetworkConfig.NetworkTransport;
            if (transport is UnityTransport unityTransport)
            {
                unityTransport.SetConnectionData(targetIp, targetPort);
                Debug.Log($"[Network] Transport настроен на {targetIp}:{targetPort}");
            }
            else
            {
                Debug.LogError("[Network] UnityTransport не найден в NetworkConfig!");
            }

            UpdateStatus($"Подключение к {targetIp}:{targetPort}...");

            try
            {
                Debug.Log("[NMC] StartClient() called");
                networkManager.StartClient();
                
                // DIAGNOSTIC: Проверяем состояние сети после StartClient
                Debug.Log($"[NMC] After StartClient: IsServer={networkManager.IsServer}, IsClient={networkManager.IsClient}, IsListening={networkManager.IsListening}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] Failed to start client: {ex.Message}");
                UpdateStatus("Ошибка подключения");
            }
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

                // Сохраняем инвентарь перед отключением
                var player = FindAnyObjectByType<NetworkPlayer>();
                if (player != null)
                {
                    var inventoryField = typeof(NetworkPlayer).GetField("_inventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (inventoryField != null)
                    {
                        var inventory = inventoryField.GetValue(player) as Inventory;
                        if (inventory != null && inventory.GetTotalItemCount() > 0)
                        {
                            inventory.SaveToPrefs();
                        }
                    }
                }

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
