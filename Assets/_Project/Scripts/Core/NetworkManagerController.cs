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

            // C1-cleanup (2026-06-05): CreateTradeDebugTools() удалён — TradeDebugTools.cs мёртв.
            // Окно TradeWindow/MarketWindow + dev-консоль достаточно для отладки.

            // FIX (2026-06-04): Создаём MarketClientState программно как root GO —
            // см. подробности в docs/Markets/FIXES_HISTORY.md 2026-06-04
            // и singleton теряется при выгрузке Bootstrap. Раньше [MarketClientState] в
            // BootstrapScene был child — DontDestroyOnLoad на child упадёт, singleton
            // теряется при выгрузке Bootstrap. Программный root гарантирует переживание стриминга.
            CreateMarketClientState();

            // C2-этап миграции контрактов: ContractClientState тоже auto-spawn
            // (аналогично MarketClientState, чтобы выживал при стриминге сцен).
            CreateContractClientState();

            // Phase 1 (INVENTORY_V2_REFACTOR.md): InventoryClientState тоже auto-spawn.
            // Создаётся root GO сразу, чтобы AddComponent→Awake→DontDestroyOnLoad отработали
            // (см. FIX 2026-06-04 — на child DontDestroyOnLoad падает, singleton теряется).
            CreateInventoryClientState();

            // Ship Key Subsystem (docs/Ships/Key-subsystem/00_OVERVIEW.md):
            // ShipKeyClientState — клиентская проекция привязок корабль↔ключ.
            // Тот же паттерн, что и InventoryClientState.
            CreateShipKeyClientState();

            // MetaRequirement Subsystem (docs/MetaRequirement/00_OVERVIEW.md, R2-META-REQ-001):
            // MetaRequirementClientState — клиентская проекция требований для ЛЮБЫХ
            // interactable'ов (корабли, блоки, двери, NPC и т.д.).
            // Оба singleton'а сосуществуют — ShipKeyClientState продолжает обслуживать
            // старые Target RPC от ShipKeyServer, а MetaRequirementClientState — новые
            // Target RPC от MetaRequirementRegistry. Через 1-2 релиз-цикла первый удалим.
            CreateMetaRequirementClientState();

            // T-G04: GatheringClientState — клиентская проекция сбора ресурсов.
            // Подписывается на ReceiveGatherResultTargetRpc (NetworkPlayer.cs), эмитит events
            // для GatheringToastController.
            CreateGatheringClientState();

            // T-C05: CraftingClientState — клиентская проекция крафта.
            // Подписывается на ReceiveCraftingResultTargetRpc + ReceiveCraftingSnapshotTargetRpc,
            // эмитит events для CraftingProgressController + CraftingWindow.
            CreateCraftingClientState();

            // T-E03: ExchangeClientState — клиентская проекция обменника.
            // Принимает результат Pack/Unpack от ExchangeServer.
            CreateExchangeClientState();

            // Автоматический запуск Dedicated Server
            if (IsDedicatedServerMode())
            {
                Debug.Log("[Network] Запуск в режиме Dedicated Server");
                Invoke(nameof(StartServer), 0.5f);
            }
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

        /// <summary>
        /// Phase 1 (INVENTORY_V2_REFACTOR.md): Создать InventoryClientState как root GameObject.
        /// Паттерн идентичен CreateMarketClientState / CreateContractClientState.
        /// InventoryClientState — проекция server-state инвентаря; UI (TAB-колесо + P-таб
        /// CharacterWindow) подписывается на её события, как MarketClientState для рынка.
        /// </summary>
        private void CreateInventoryClientState()
        {
            var existing = FindObjectsByType<ProjectC.Items.Client.InventoryClientState>(FindObjectsInactive.Include);
            foreach (var inst in existing)
            {
                if (inst != null && inst.transform.parent == null)
                {
                    Debug.Log("[NMC] InventoryClientState already root, skipping creation");
                    return;
                }
            }
            if (existing.Length > 0)
            {
                Debug.LogWarning($"[NMC] Found {existing.Length} non-root InventoryClientState in scene — DontDestroyOnLoad would fail. Creating root replacement.");
            }
            var go = new GameObject("[InventoryClientState]");
            go.AddComponent<ProjectC.Items.Client.InventoryClientState>();
            Debug.Log("[NMC] Created [InventoryClientState] as root GameObject");
        }

        /// <summary>
        /// Ship Key Subsystem: Создать ShipKeyClientState как root GameObject.
        /// Паттерн идентичен CreateInventoryClientState.
        /// ShipKeyClientState — клиентская проекция привязок корабль↔ключ; UI toast
        /// (ShipKeyToast) подписывается на её события.
        /// </summary>
        private void CreateShipKeyClientState()
        {
            var existing = FindObjectsByType<ProjectC.Ship.Key.ShipKeyClientState>(FindObjectsInactive.Include);
            foreach (var inst in existing)
            {
                if (inst != null && inst.transform.parent == null)
                {
                    Debug.Log("[NMC] ShipKeyClientState already root, skipping creation");
                    return;
                }
            }
            if (existing.Length > 0)
            {
                Debug.LogWarning($"[NMC] Found {existing.Length} non-root ShipKeyClientState in scene — DontDestroyOnLoad would fail. Creating root replacement.");
            }
            var go = new GameObject("[ShipKeyClientState]");
            go.AddComponent<ProjectC.Ship.Key.ShipKeyClientState>();
            Debug.Log("[NMC] Created [ShipKeyClientState] as root GameObject");
        }

        /// <summary>
        /// MetaRequirement Subsystem (docs/MetaRequirement/00_OVERVIEW.md, R2-META-REQ-001):
        /// Создать MetaRequirementClientState как root GameObject.
        /// Паттерн идентичен CreateShipKeyClientState.
        /// </summary>
        private void CreateMetaRequirementClientState()
        {
            var existing = FindObjectsByType<ProjectC.MetaRequirement.MetaRequirementClientState>(FindObjectsInactive.Include);
            foreach (var inst in existing)
            {
                if (inst != null && inst.transform.parent == null)
                {
                    Debug.Log("[NMC] MetaRequirementClientState already root, skipping creation");
                    return;
                }
            }
            if (existing.Length > 0)
            {
                Debug.LogWarning($"[NMC] Found {existing.Length} non-root MetaRequirementClientState in scene — DontDestroyOnLoad would fail. Creating root replacement.");
            }
            var go = new GameObject("[MetaRequirementClientState]");
            go.AddComponent<ProjectC.MetaRequirement.MetaRequirementClientState>();
            Debug.Log("[NMC] Created [MetaRequirementClientState] as root GameObject");
        }

        /// <summary>
        /// T-G04: GatheringClientState singleton. Паттерн идентичен CreateMetaRequirementClientState.
        /// </summary>
        private void CreateGatheringClientState()
        {
            var existing = FindObjectsByType<ProjectC.ResourceNode.GatheringClientState>(FindObjectsInactive.Include);
            foreach (var inst in existing)
            {
                if (inst != null && inst.transform.parent == null)
                {
                    Debug.Log("[NMC] GatheringClientState already root, skipping creation");
                    return;
                }
            }
            if (existing.Length > 0)
            {
                Debug.LogWarning($"[NMC] Found {existing.Length} non-root GatheringClientState in scene — DontDestroyOnLoad would fail. Creating root replacement.");
            }
            var go = new GameObject("[GatheringClientState]");
            go.AddComponent<ProjectC.ResourceNode.GatheringClientState>();
            Debug.Log("[NMC] Created [GatheringClientState] as root GameObject");
        }

        /// <summary>
        /// T-C05: CraftingClientState singleton. Паттерн идентичен CreateGatheringClientState.
        /// </summary>
        private void CreateCraftingClientState()
        {
            var existing = FindObjectsByType<ProjectC.Crafting.CraftingClientState>(FindObjectsInactive.Include);
            foreach (var inst in existing)
            {
                if (inst != null && inst.transform.parent == null)
                {
                    Debug.Log("[NMC] CraftingClientState already root, skipping creation");
                    return;
                }
            }
            if (existing.Length > 0)
            {
                Debug.LogWarning($"[NMC] Found {existing.Length} non-root CraftingClientState in scene — DontDestroyOnLoad would fail. Creating root replacement.");
            }
            var go = new GameObject("[CraftingClientState]");
            go.AddComponent<ProjectC.Crafting.CraftingClientState>();
            Debug.Log("[NMC] Created [CraftingClientState] as root GameObject");
        }

        /// <summary>
        /// T-E03: ExchangeClientState singleton. Паттерн идентичен CreateCraftingClientState.
        /// </summary>
        private void CreateExchangeClientState()
        {
            var existing = FindObjectsByType<ProjectC.Trade.Client.ExchangeClientState>(FindObjectsInactive.Include);
            foreach (var inst in existing)
            {
                if (inst != null && inst.transform.parent == null)
                {
                    Debug.Log("[NMC] ExchangeClientState already root, skipping creation");
                    return;
                }
            }
            if (existing.Length > 0)
            {
                Debug.LogWarning($"[NMC] Found {existing.Length} non-root ExchangeClientState in scene — DontDestroyOnLoad would fail. Creating root replacement.");
            }
            var go = new GameObject("[ExchangeClientState]");
            go.AddComponent<ProjectC.Trade.Client.ExchangeClientState>();
            Debug.Log("[NMC] Created [ExchangeClientState] as root GameObject");
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

                // NOTE (cleanup Phase 9, 2026-06-05): legacy _inventory.SaveToPrefs() убран —
                // v2 серверный инвентарь авторитативен, persistence = ответственность сервера.

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
