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
                Debug.Log("[NMC] Creating new NetworkManager component");
                networkManager = gameObject.AddComponent<Unity.Netcode.NetworkManager>();
            }

            // Принудительная инициализация NetworkConfig через создание свойства
            var netConfig = networkManager.NetworkConfig;
            Debug.Log($"[NMC] Awake: NM={networkManager}, NetConfig={netConfig}");

            // Проверяем и добавляем UnityTransport если нужно
            var transport = GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport == null)
            {
                Debug.Log("[NMC] Creating UnityTransport in Awake");
                transport = gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            }

            // Настраиваем NetworkConfig на использование UnityTransport
            if (netConfig != null && netConfig.NetworkTransport == null)
            {
                Debug.Log("[NMC] Setting transport in NetworkConfig");
                netConfig.NetworkTransport = transport;
            }

            DontDestroyOnLoad(gameObject);

            // СЕССИЯ DIAGNOSTIC: Создаём TradeDebugTools для отладки
            CreateTradeDebugTools();

            // FIX (2026-06-04): Создаём MarketClientState программно как root GO —
            // см. подробности в docs/Markets/FIXES_HISTORY.md 2026-06-04
            // и singleton теряется при выгрузке Bootstrap. Раньше [MarketClientState] в
            // BootstrapScene был child — DontDestroyOnLoad на child упадёт, singleton
            // теряется при выгрузке Bootstrap. Программный root гарантирует переживание стриминга.
            CreateMarketClientState();

            // C2-этап миграции контрактов: ContractClientState тоже auto-spawn
            // (аналогично MarketClientState, чтобы выживал при стриминге сцен).
            CreateContractClientState();

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
            var debugTools = debugObj.AddComponent<ProjectC.Trade.TradeDebugTools>();
            // Parenting AFTER DontDestroyOnLoad to ensure debugObj is a root GameObject
            debugObj.transform.SetParent(transform);
            debugObj.SetActive(true);
        }

        /// <summary>
        /// FIX (2026-06-04): Создать MarketClientState как root GameObject.
        /// AddComponent запускает MarketClientState.Awake, который делает
        /// DontDestroyOnLoad(gameObject) — это работает ТОЛЬКО если GO root.
        /// Если в сцене уже есть root-инстанс — ничего не делаем.
        /// Если в сцене есть child-инстанс — создаём root (DontDestroyOnLoad
        /// на child упадёт), старый child в сцене остаётся как «мёртвый» компонент
        /// (MarketClientState.Instance = null в нём, т.к. Instance уже занят нашим root).
        /// </summary>
        private void CreateMarketClientState()
        {
            var existing = FindObjectsByType<ProjectC.Trade.Client.MarketClientState>(FindObjectsInactive.Include);
            foreach (var inst in existing)
            {
                if (inst != null && inst.transform.parent == null)
                {
                    Debug.Log("[NMC] MarketClientState already root, skipping creation");
                    return;
                }
            }
            if (existing.Length > 0)
            {
                Debug.LogWarning($"[NMC] Found {existing.Length} non-root MarketClientState in scene — DontDestroyOnLoad would fail. Creating root replacement.");
            }
            var go = new GameObject("[MarketClientState]");
            go.AddComponent<ProjectC.Trade.Client.MarketClientState>();
            Debug.Log("[NMC] Created [MarketClientState] as root GameObject");
        }

        /// <summary>
        /// FIX (C2-этап миграции контрактов): Создать ContractClientState как root GameObject.
        /// Паттерн идентичен CreateMarketClientState — см. FIX 2026-06-04 в MarketClientState.
        /// </summary>
        private void CreateContractClientState()
        {
            var existing = FindObjectsByType<ProjectC.Trade.Client.ContractClientState>(FindObjectsInactive.Include);
            foreach (var inst in existing)
            {
                if (inst != null && inst.transform.parent == null)
                {
                    Debug.Log("[NMC] ContractClientState already root, skipping creation");
                    return;
                }
            }
            if (existing.Length > 0)
            {
                Debug.LogWarning($"[NMC] Found {existing.Length} non-root ContractClientState in scene — DontDestroyOnLoad would fail. Creating root replacement.");
            }
            var go = new GameObject("[ContractClientState]");
            go.AddComponent<ProjectC.Trade.Client.ContractClientState>();
            Debug.Log("[NMC] Created [ContractClientState] as root GameObject");
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

            // Get NetworkManager
            if (networkManager == null)
            {
                networkManager = GetComponent<Unity.Netcode.NetworkManager>();
            }

            if (networkManager == null)
            {
                Debug.LogError("[NMC] NetworkManager component not found!");
                return;
            }

            Debug.Log($"[NMC] Using local NM: {networkManager}, IsListening: {networkManager.IsListening}");

            // Get or create UnityTransport on THIS GameObject
            var transport = GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport == null)
            {
                Debug.Log("[NMC] Creating UnityTransport component");
                transport = gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            }

            // Configure transport connection data
            transport.SetConnectionData("127.0.0.1", serverPort);
            Debug.Log($"[NMC] Transport configured: {serverIp}:{serverPort}");

            // Check if already listening
            if (networkManager.IsListening)
            {
                Debug.LogWarning("[NMC] Already listening! Shutting down first...");
                networkManager.Shutdown();
            }

            // Start host - NGO handles NetworkConfig internally
            Debug.Log("[NMC] Calling StartHost()...");
            networkManager.StartHost();
            Debug.Log($"[NMC] StartHost() completed. IsHost={networkManager.IsHost}, IsServer={networkManager.IsServer}");
        }


        /// <summary>
        /// Запустить сервер (dedicated, без клиента)
        /// </summary>
        public void StartServer()
        {
            Debug.Log("[NMC] StartServer() called");

            // Get NetworkManager
            if (networkManager == null)
            {
                networkManager = GetComponent<Unity.Netcode.NetworkManager>();
            }

            if (networkManager == null)
            {
                Debug.LogError("[NMC] NetworkManager component not found!");
                return;
            }

            // Get or create UnityTransport
            var transport = GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport == null)
            {
                Debug.Log("[NMC] Creating UnityTransport component");
                transport = gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            }

            // Configure transport - server listens on all interfaces
            transport.SetConnectionData("0.0.0.0", serverPort);
            Debug.Log($"[NMC] Transport configured for server: 0.0.0.0:{serverPort}");

            // Check if already listening
            if (networkManager.IsListening)
            {
                Debug.LogWarning("[NMC] Already listening! Shutting down first...");
                networkManager.Shutdown();
            }

            // Start server - NGO handles NetworkConfig internally
            Debug.Log("[NMC] Calling StartServer()...");
            networkManager.StartServer();
            Debug.Log($"[NMC] StartServer() completed. IsServer={networkManager.IsServer}");
            UpdateStatus($"Сервер запущен на порту {serverPort}");
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

            // Save for reconnect
            _lastServerIp = targetIp;
            _lastServerPort = targetPort;
            _isReconnecting = false;
            _reconnectAttempts = 0;

            // If already listening, shutdown first
            if (networkManager.IsListening)
            {
                Debug.LogWarning("[NMC] Already listening! Shutting down before connect...");
                networkManager.Shutdown();
                yield return new WaitForSecondsRealtime(0.25f);
            }

            // Get or create UnityTransport
            var transport = GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogWarning("[NMC] Creating UnityTransport for client");
                transport = gameObject.AddComponent<UnityTransport>();
            }

            // Configure transport with server address
            transport.SetConnectionData(targetIp, targetPort);
            Debug.Log($"[NMC] Client transport configured: {targetIp}:{targetPort}");

            UpdateStatus($"Подключение к {targetIp}:{targetPort}...");

            try
            {
                Debug.Log("[NMC] Starting client...");
                networkManager.StartClient();
                Debug.Log($"[NMC] StartClient() completed. IsClient={networkManager.IsClient}, IsListening={networkManager.IsListening}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NMC] Failed to start client: {ex.Message}");
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
