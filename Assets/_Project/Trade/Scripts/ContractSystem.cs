using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

namespace ProjectC.Trade
{
    /// <summary>
    /// Серверный менеджер контрактов — авторитетный источник всей контрактной логики.
    /// GDD_25 секция 6: Контрактная Система.
    /// GDD_23 секция 7.1: Торговые репутации.
    ///
    /// Сессия 7: ContractSystem.
    /// Принцип: Клиент запрашивает → Сервер валидирует → Сервер считает → ClientRpc результат.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ContractSystem : NetworkBehaviour
    {
        public static ContractSystem Instance { get; private set; }

        [Header("Contract Settings")]
        [Tooltip("Максимум активных контрактов на игрока")]
        [SerializeField] private int maxActiveContractsPerPlayer = 3;

        [Tooltip("Автогенерация новых контрактов когда доска пуста")]
        [SerializeField] private bool autoRegenerateContracts = true;

        [Header("Distance Table (км) — GDD_25 секция 3.2")]
        [Tooltip("Расстояния между локациями (упрощённая таблица)")]
        [SerializeField] private float[,] distanceTable = new float[4, 4];

        [Header("NPCTrader Routes — используем для генерации контрактов")]
        [Tooltip("Автоматически инициализировать при старте")]
        [SerializeField] private bool autoInitContracts = true;

        // Все доступные контракты (сервер)
        private Dictionary<string, ContractData> _availableContracts = new Dictionary<string, ContractData>();

        // Активные контракты игроков (playerId → список contractId)
        private Dictionary<ulong, List<string>> _playerContracts = new Dictionary<ulong, List<string>>();

        // Долги игроков (playerId → PlayerDebt)
        private Dictionary<ulong, PlayerDebt> _playerDebts = new Dictionary<ulong, PlayerDebt>();

        // Сгенерированные контракенты по локации (locationId → список contractId)
        private Dictionary<string, List<string>> _locationContracts = new Dictionary<string, List<string>>();

        // ID счётчик (для статистики)
        // private int _contractIdCounter = 0;

        // Базовые товары для контрактов (из TradeDatabase)
        private List<TradeItemDefinition> _availableItems = new List<TradeItemDefinition>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            InitServerSide();
        }

        private void Start()
        {
            // Fallback: если OnNetworkSpawn не вызвался
            if (IsServer)
            {
                InitServerSide();
            }
        }

        private void InitServerSide()
        {
            if (!IsServer) return;

            // Инициализация таблицы расстояний (GDD_25 секция 3.2)
            InitDistanceTable();

            // Загрузка доступных товаров
            LoadAvailableItems();

            // Генерация начальных контрактов для каждой локации
            if (autoInitContracts)
            {
                GenerateContractsForAllLocations();
            }
        }

        // ==================== ТАБЛИЦА РАССТОЯНИЙ ====================

        /// <summary>
        /// Инициализировать таблицу расстояний между 4 городами.
        /// GDD_25 секция 3.2: Основные маршруты.
        /// Индексы: 0=primium, 1=secundus, 2=tertius, 3=quartus
        /// </summary>
        private void InitDistanceTable()
        {
            // Симметричная матрица расстояний (км)
            // Прим: Приму → Секунд = 120, Приму → Тертиус = 200, и т.д.
            SetDistance(0, 1, 120f);  // Приму ↔ Секунд
            SetDistance(0, 2, 200f);  // Приму ↔ Тертиус
            SetDistance(0, 3, 180f);  // Приму ↔ Квартус
            SetDistance(1, 2, 150f);  // Секунд ↔ Тертиус
            SetDistance(1, 3, 160f);  // Секунд ↔ Квартус
            SetDistance(2, 3, 100f);  // Тертиус ↔ Квартус
        }

        private void SetDistance(int a, int b, float km)
        {
            distanceTable[a, b] = km;
            distanceTable[b, a] = km;
        }

        /// <summary>
        /// Получить расстояние между двумя локациями (км)
        /// </summary>
        public float GetDistance(string fromLocationId, string toLocationId)
        {
            int fromIndex = LocationIdToIndex(fromLocationId);
            int toIndex = LocationIdToIndex(toLocationId);

            if (fromIndex < 0 || toIndex < 0) return 100f; // fallback

            float dist = distanceTable[fromIndex, toIndex];
            return dist > 0f ? dist : 100f; // fallback
        }

        private int LocationIdToIndex(string locationId)
        {
            switch (locationId.ToLower())
            {
                case "primium": return 0;
                case "secundus": return 1;
                case "tertius": return 2;
                case "quartus": return 3;
                default: return -1;
            }
        }

        // ==================== ЗАГРУЗКА ТОВАРОВ ====================

        private void LoadAvailableItems()
        {
            // Приоритет: загрузка из TradeDatabase (единственный источник истины)
            var db = FindTradeDatabase();
            if (db != null && db.allItems != null)
            {
                foreach (var item in db.allItems)
                {
                    if (item != null && !string.IsNullOrEmpty(item.itemId))
                    {
                        _availableItems.Add(item);
                    }
                }
            }

            // Fallback: если база не найдена — AssetDatabase (Editor)
            if (_availableItems.Count == 0)
            {
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TradeItemDefinition");
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var item = UnityEditor.AssetDatabase.LoadAssetAtPath<TradeItemDefinition>(path);
                    if (item != null && !string.IsNullOrEmpty(item.itemId))
                    {
                        _availableItems.Add(item);
                    }
                }
#endif
            }

            // Fallback 2: программные товары если вообще ничего нет
            if (_availableItems.Count == 0)
            {
                CreateFallbackItems();
            }
        }

        private static TradeDatabase FindTradeDatabase()
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TradeDatabase");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<TradeDatabase>(path);
            }
#endif
            return Resources.Load<TradeDatabase>("Trade/TradeItemDatabase");
        }

        /// <summary>
        /// Fallback: создать базовые товары если ScriptableObject не найдены
        /// </summary>
        private void CreateFallbackItems()
        {
            _availableItems.Add(CreateFallbackItem("mesium_canister_v01", "Мезий (канистра)", 10f, 10f, 0.5f, 1, true, false));
            _availableItems.Add(CreateFallbackItem("antigrav_ingot_v01", "Антигравий (слиток)", 50f, 5f, 0.2f, 1, false, false));
            _availableItems.Add(CreateFallbackItem("mnp_container_v01", "МНП (контейнер)", 100f, 3f, 0.3f, 1, false, true));
            _availableItems.Add(CreateFallbackItem("latex_roll_v01", "Латекс (рулон)", 5f, 8f, 1f, 1, false, false));
            _availableItems.Add(CreateFallbackItem("engine_block_v01", "Двигатель (блок)", 500f, 50f, 2f, 2, false, true));
            _availableItems.Add(CreateFallbackItem("armor_plate_v01", "Броня (плита)", 200f, 30f, 1.5f, 2, false, false));
            _availableItems.Add(CreateFallbackItem("food_crate_v01", "Продовольствие", 8f, 15f, 1f, 1, false, false));
        }

        private TradeItemDefinition CreateFallbackItem(string id, string name, float price, float weight, float volume, int slots, bool isDangerous, bool isFragile)
        {
            var item = ScriptableObject.CreateInstance<TradeItemDefinition>();
            item.itemId = id;
            item.displayName = name;
            item.basePrice = price;
            item.weight = weight;
            item.volume = volume;
            item.slots = slots;
            item.isDangerous = isDangerous;
            item.isFragile = isFragile;
            item.name = $"FallbackItem_{id}";
            return item;
        }

        // ==================== ГЕНЕРАЦИЯ КОНТРАКТОВ ====================

        /// <summary>
        /// Сгенерировать контракты для всех 4 локаций
        /// </summary>
        private void GenerateContractsForAllLocations()
        {
            string[] locations = { "primium", "secundus", "tertius", "quartus" };
            foreach (var loc in locations)
            {
                GenerateContractsForLocation(loc);
            }
        }

        /// <summary>
        /// Сгенерировать 3 типа контрактов для локации (Standard, Urgent, Receipt)
        /// Утверждено решение 1A: динамическая генерация
        /// </summary>
        public void GenerateContractsForLocation(string fromLocationId)
        {
            if (!_locationContracts.ContainsKey(fromLocationId))
                _locationContracts[fromLocationId] = new List<string>();

            // Удаляем старые непринятые контракты локации
            foreach (var cid in _locationContracts[fromLocationId])
            {
                if (_availableContracts.ContainsKey(cid))
                    _availableContracts.Remove(cid);
            }
            _locationContracts[fromLocationId].Clear();

            // Получаем все возможные целевые локации
            string[] allLocations = { "primium", "secundus", "tertius", "quartus" };
            var destinations = allLocations.Where(l => l != fromLocationId).ToList();

            // Выбираем случайный товар
            TradeItemDefinition item = _availableItems[UnityEngine.Random.Range(0, _availableItems.Count)];
            int quantity = UnityEngine.Random.Range(2, 8); // 2-7 единиц

            // Выбираем случайную целевую локацию
            string toLocationId = destinations[UnityEngine.Random.Range(0, destinations.Count)];
            float distance = GetDistance(fromLocationId, toLocationId);

            // Генерируем 3 типа контрактов
            // 1. Standard
            var standard = ContractData.Create(
                ContractType.Standard, item.itemId, quantity,
                fromLocationId, toLocationId, item.basePrice, distance);
            _availableContracts[standard.contractId] = standard;
            _locationContracts[fromLocationId].Add(standard.contractId);

            // 2. Urgent
            var urgent = ContractData.Create(
                ContractType.Urgent, item.itemId, quantity,
                fromLocationId, toLocationId, item.basePrice, distance);
            _availableContracts[urgent.contractId] = urgent;
            _locationContracts[fromLocationId].Add(urgent.contractId);

            // 3. Receipt (под расписку) — меньший объём, больше времени
            var receipt = ContractData.Create(
                ContractType.Receipt, item.itemId, Mathf.Min(quantity, 3),
                fromLocationId, toLocationId, item.basePrice, distance);
            _availableContracts[receipt.contractId] = receipt;
            _locationContracts[fromLocationId].Add(receipt.contractId);
        }

        // ==================== SERVERRPC: КОНТРАКТЫ ====================

        /// <summary>
        /// Запросить доступные контракты для локации
        /// </summary>
        [ServerRpc]
        public void RequestAvailableContractsServerRpc(string locationId, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Генерируем новые если авто-регенерация включена
            if (autoRegenerateContracts)
            {
                if (!_locationContracts.ContainsKey(locationId) || _locationContracts[locationId].Count == 0)
                {
                    GenerateContractsForLocation(locationId);
                }
            }

            // Собираем доступные контракты для этой локации
            var contracts = new List<ContractData>();
            if (_locationContracts.ContainsKey(locationId))
            {
                foreach (var cid in _locationContracts[locationId])
                {
                    if (_availableContracts.ContainsKey(cid))
                    {
                        var contract = _availableContracts[cid];
                        if (contract.state == ContractState.Pending)
                        {
                            contracts.Add(contract);
                        }
                    }
                }
            }

            // Также отправляем активные контракты этого игрока
            var activeContracts = new List<ContractData>();
            if (_playerContracts.ContainsKey(clientId))
            {
                foreach (var cid in _playerContracts[clientId])
                {
                    if (_availableContracts.ContainsKey(cid))
                    {
                        var contract = _availableContracts[cid];
                        if (contract.state == ContractState.Active)
                        {
                            activeContracts.Add(contract);
                        }
                    }
                }
            }

            // Отправляем клиенту (двойной формат: доступные|активные)
            DispatchContractsToClient(clientId, contracts.ToArray(), activeContracts.ToArray(), locationId);
        }

        /// <summary>
        /// Принять контракт
        /// Утверждено решение 4A: для Receipt — сервер автоматически загружает товар на склад
        /// </summary>
        [ServerRpc]
        public void AcceptContractServerRpc(string contractId, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // 1. Валидация контракта
            if (!_availableContracts.ContainsKey(contractId))
            {
                DispatchContractResultToClient(clientId, false, "Контракт не найден!", 0f);
                return;
            }

            var contract = _availableContracts[contractId];
            if (contract.state != ContractState.Pending)
            {
                DispatchContractResultToClient(clientId, false, "Контракт уже принят или истёк!", 0f);
                return;
            }

            // 2. Проверка долгового лимита (PlayerDebt)
            var debt = GetOrCreatePlayerDebt(clientId);
            if (!debt.CanAcceptContracts())
            {
                DispatchContractResultToClient(clientId, false, $"Долг {debt.CurrentDebt:F0} CR! Ограничение контрактов.", 0f);
                return;
            }

            // 3. Проверка лимита активных контрактов
            if (!_playerContracts.ContainsKey(clientId))
                _playerContracts[clientId] = new List<string>();

            if (_playerContracts[clientId].Count >= maxActiveContractsPerPlayer)
            {
                DispatchContractResultToClient(clientId, false, $"Максимум {maxActiveContractsPerPlayer} активных контрактов!", 0f);
                return;
            }

            // 4. Для Receipt — загрузить товар на склад игрока
            if (contract.isReceiptContract)
            {
                var item = _availableItems.Find(i => i.itemId == contract.itemId);
                if (item == null)
                {
                    DispatchContractResultToClient(clientId, false, "Товар контракта не найден!", 0f);
                    return;
                }

                var storage = FindPlayerStorage(clientId);
                if (storage == null)
                {
                    DispatchContractResultToClient(clientId, false, "Склад игрока не найден!", 0f);
                    return;
                }

                if (!storage.AddContractItem(item, contract.quantity))
                {
                    DispatchContractResultToClient(clientId, false, "Нет места на складе для груза контракта!", 0f);
                    return;
                }
            }

            // === ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ ===

            // Активируем контракт
            contract.Activate(clientId);
            _availableContracts[contractId] = contract;
            _playerContracts[clientId].Add(contractId);

            DispatchContractResultToClient(clientId, true, $"Контракт принят: {contract.GetTypeDisplayName()}", contract.reward);
        }

        /// <summary>
        /// Завершить контракт (у NPC-агента в целевой локации)
        /// Утверждено решение 2A: проверка позиции — игрок должен быть в целевой локации
        /// </summary>
        [ServerRpc]
        public void CompleteContractServerRpc(string contractId, string completionLocationId, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // 1. Валидация контракта
            if (!_availableContracts.ContainsKey(contractId))
            {
                Debug.LogWarning($"[ContractSystem] Контракт {contractId} не найден! Доступно: {_availableContracts.Count}");
                DispatchContractResultToClient(clientId, false, "Контракт не найден!", 0f);
                return;
            }

            var contract = _availableContracts[contractId];

            if (contract.state != ContractState.Active)
            {
                Debug.LogWarning($"[ContractSystem] Контракт не активен: state={contract.state}");
                DispatchContractResultToClient(clientId, false, "Контракт не активен!", 0f);
                return;
            }

            // 2. Проверка что это контракт этого игрока
            if (contract.assignedPlayerId != clientId)
            {
                DispatchContractResultToClient(clientId, false, "Это не ваш контракт!", 0f);
                return;
            }

            // 3. Проверка таймера
            if (contract.timeLimit > 0f && contract.timeRemaining <= 0f)
            {
                contract.Fail();
                _availableContracts[contractId] = contract;
                HandleFailedContract(contract, clientId);
                DispatchContractResultToClient(clientId, false, "Время контракта истекло!", 0f);
                return;
            }

            // 4. Проверка позиции (игрок в целевой локации?)
            if (completionLocationId != contract.toLocationId)
            {
                DispatchContractResultToClient(clientId, false, $"Вы не в целевой локации! Нужно: {contract.toLocationId}", 0f);
                return;
            }

            // 5. Проверка наличия груза (для не-Receipt контрактов)
            if (!contract.isReceiptContract)
            {
                var storage = FindPlayerStorage(clientId);
                bool hasCargo = false;

                if (storage != null)
                {
                    // Сессия 8: Устанавливаем правильную локацию и загружаем склад
                    bool needLoad = (storage.currentLocationId != completionLocationId);
                    if (storage.warehouse.Count == 0)
                    {
                        needLoad = true;
                    }

                    if (needLoad)
                    {
                        storage.currentLocationId = completionLocationId;
                        storage.Load();
                    }

                    var warehouseItem = storage.warehouse.Find(w => w.item != null && w.item.itemId == contract.itemId);
                    if (warehouseItem != null && warehouseItem.quantity >= contract.quantity)
                    {
                        hasCargo = true;
                        storage.RemoveItem(contract.itemId, contract.quantity);
                    }
                }
                else
                {
                    Debug.LogWarning($"[ContractSystem] PlayerStorage не найден для игрока {clientId}!");
                }

                // Если на складе нет — проверяем трюм корабля
                if (!hasCargo)
                {
                    ProjectC.Player.CargoSystem cargo = null;
                    var ships = FindObjectsByType<ProjectC.Player.ShipController>(FindObjectsInactive.Include);

                    foreach (var ship in ships)
                    {
                        var cs = ship.GetComponent<ProjectC.Player.CargoSystem>();
                        if (cs != null)
                        {
                            cargo = cs;
                            break;
                        }
                    }

                    if (cargo != null)
                    {
                        var cargoItem = cargo.cargo.Find(c => c.item != null && c.item.itemId == contract.itemId);
                        if (cargoItem != null && cargoItem.quantity >= contract.quantity)
                        {
                            hasCargo = true;
                            cargo.RemoveCargo(contract.itemId, contract.quantity);
                        }
                    }
                    else
                    {
                        Debug.LogError($"[ContractSystem] CargoSystem не найден! ShipController: {ships.Length}");
                    }
                }

                if (!hasCargo)
                {
                    DispatchContractResultToClient(clientId, false, $"Нет груза {contract.itemId} x{contract.quantity}! На складе или в трюме.", 0f);
                    return;
                }
            }

            // === ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ ===

            // Завершаем контракт
            contract.Complete();
            _availableContracts[contractId] = contract;

            // Удаляем из активных
            if (_playerContracts.ContainsKey(clientId))
                _playerContracts[clientId].Remove(contractId);

            // Начисляем награду
            var credits = FindPlayerCredits(clientId);
            if (credits != null)
            {
                credits.Credits += contract.reward;
            }

            DispatchContractResultToClient(clientId, true, $"Контракт завершён! Награда: {contract.reward:F0} CR", contract.reward);
        }

        /// <summary>
        /// Провалить контракт (вручную или по таймеру)
        /// </summary>
        [ServerRpc]
        public void FailContractServerRpc(string contractId, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            FailContractInternal(contractId, clientId, true);
        }

        /// <summary>
        /// Внутренний метод провала контракта
        /// </summary>
        private void FailContractInternal(string contractId, ulong clientId, bool isManual)
        {
            if (!_availableContracts.ContainsKey(contractId)) return;

            var contract = _availableContracts[contractId];
            if (contract.state != ContractState.Active) return;
            if (contract.assignedPlayerId != clientId) return;

            contract.Fail();
            _availableContracts[contractId] = contract;

            // Удаляем из активных
            if (_playerContracts.ContainsKey(clientId))
                _playerContracts[clientId].Remove(contractId);

            HandleFailedContract(contract, clientId);

            string reason = isManual ? "отменён игроком" : "время истекло";
            DispatchContractResultToClient(clientId, false, $"Контракт провален: {reason}", 0f);
        }

        // ==================== ОБРАБОТКА ПРОВАЛА ====================

        /// <summary>
        /// Обработать провал контракта (долг, репутация)
        /// GDD_25 секция 6.2: Система «Под расписку»
        /// </summary>
        private void HandleFailedContract(ContractData contract, ulong playerId)
        {
            var debt = GetOrCreatePlayerDebt(playerId);

            // Для Receipt контракта — долг = cargoValue × 1.5
            if (contract.isReceiptContract)
            {
                float debtAmount = contract.cargoValue * 1.5f;
                debt.AddDebt(debtAmount);

                // Для Receipt: вернуть товар на рынок отправления (если есть)
                // TODO: вернуть товар в сток локации отправления
            }
            else
            {
                // Для обычных контрактов — меньший штраф
                // Только если груз был получен (Receipt) или если таймер истёк
                if (contract.timeLimit > 0f && contract.timeRemaining <= 0f)
                {
                    // Таймер истёк — небольшой штраф
                    float penalty = contract.reward * 0.2f; // 20% от награды
                    if (penalty > 0f)
                    {
                        var credits = FindPlayerCredits(playerId);
                        if (credits != null && credits.Credits >= penalty)
                        {
                            credits.Credits -= penalty;
                        }
                    }
                }
            }

            // TODO: репутация НП -30 (когда система репутации будет реализована)
        }

        // ==================== ОБНОВЛЕНИЕ ТАЙМЕРОВ ====================

        private void FixedUpdate()
        {
            if (!IsServer) return;

            // Обновляем таймеры активных контрактов
            foreach (var kvp in _playerContracts)
            {
                ulong playerId = kvp.Key;
                foreach (var contractId in kvp.Value.ToList())
                {
                    if (_availableContracts.ContainsKey(contractId))
                    {
                        var contract = _availableContracts[contractId];
                        if (contract.state == ContractState.Active)
                        {
                            contract.TickTimer(Time.fixedDeltaTime);
                            _availableContracts[contractId] = contract;

                            // Авто-провал при истечении таймера
                            if (contract.state == ContractState.Failed)
                            {
                                HandleFailedContract(contract, playerId);
                                DispatchContractResultToClient(playerId, false, $"Контракт {contractId} провален: время истекло!", 0f);
                            }
                        }
                    }
                }
            }

            // Обновляем долги игроков (затухание)
            foreach (var kvp in _playerDebts)
            {
                kvp.Value.CheckAndApplyDecay();
            }
        }

        // ==================== CLIENTRPC ====================

        /// <summary>
        /// Отправить доступные контракты клиенту
        /// </summary>
        private void DispatchContractsToClient(ulong clientId, ContractData[] availableContracts, ContractData[] activeContracts, string locationId)
        {
            string serializedAvailable = SerializeContracts(availableContracts);
            string serializedActive = SerializeContracts(activeContracts);
            string combined = serializedAvailable + "|||" + serializedActive;

            var player = FindPlayerNetworkPlayer(clientId);
            if (player != null)
            {
                player.ContractListClientRpc(combined, locationId);
            }
            else
            {
                Debug.LogWarning($"[ContractSystem] Не удалось найти NetworkPlayer для клиента {clientId}");
            }
        }

        /// <summary>
        /// Отправить результат контракта клиенту
        /// </summary>
        private void DispatchContractResultToClient(ulong clientId, bool success, string message, float reward)
        {
            var player = FindPlayerNetworkPlayer(clientId);
            if (player != null)
            {
                player.ContractResultClientRpc(success, message, reward);
            }
        }

        // ==================== УТИЛИТЫ ====================

        /// <summary>
        /// Сериализовать массив контрактов в примитивные массивы для RPC
        /// </summary>
        private string SerializeContracts(ContractData[] contracts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in contracts)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append($"{c.contractId},{(int)c.type},{c.itemId},{c.quantity},{c.fromLocationId},{c.toLocationId},{c.reward:F0},{c.cargoValue:F0},{c.timeLimit:F0},{c.timeRemaining:F0},{c.isReceiptContract}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Десериализовать контракты из строки
        /// </summary>
        public static ContractData[] DeserializeContracts(string data)
        {
            if (string.IsNullOrEmpty(data)) return new ContractData[0];

            var parts = data.Split('|');
            var contracts = new List<ContractData>();

            foreach (var part in parts)
            {
                var fields = part.Split(',');
                if (fields.Length < 11) continue;

                var contract = new ContractData
                {
                    contractId = fields[0],
                    type = (ContractType)int.Parse(fields[1]),
                    itemId = fields[2],
                    quantity = int.Parse(fields[3]),
                    fromLocationId = fields[4],
                    toLocationId = fields[5],
                    reward = float.Parse(fields[6]),
                    cargoValue = float.Parse(fields[7]),
                    timeLimit = float.Parse(fields[8]),
                    timeRemaining = float.Parse(fields[9]),
                    isReceiptContract = bool.Parse(fields[10])
                };
                contracts.Add(contract);
            }

            return contracts.ToArray();
        }

        /// <summary>
        /// Получить или создать PlayerDebt для игрока
        /// </summary>
        private PlayerDebt GetOrCreatePlayerDebt(ulong playerId)
        {
            if (_playerDebts.ContainsKey(playerId))
                return _playerDebts[playerId];

            var player = FindPlayerNetworkPlayer(playerId);
            if (player == null)
            {
                // Создаём заглушку
                var fallbackDebt = new GameObject($"PlayerDebt_{playerId}").AddComponent<PlayerDebt>();
                fallbackDebt.Init(playerId);
                DontDestroyOnLoad(fallbackDebt.gameObject);
                _playerDebts[playerId] = fallbackDebt;
                return fallbackDebt;
            }

            var debt = player.GetComponent<PlayerDebt>();
            if (debt == null)
            {
                debt = player.gameObject.AddComponent<PlayerDebt>();
                debt.Init(playerId);
            }

            _playerDebts[playerId] = debt;
            return debt;
        }

        // ==================== ПОИСК КОМПОНЕНТОВ ====================

        private ProjectC.Player.NetworkPlayer FindPlayerNetworkPlayer(ulong clientId)
        {
            var players = FindObjectsByType<ProjectC.Player.NetworkPlayer>(FindObjectsInactive.Include);

            // Сначала ищем точное совпадение OwnerClientId
            foreach (var player in players)
            {
                if (player.IsSpawned && player.OwnerClientId == clientId)
                {
                    return player;
                }
            }

            // Fallback для Host (clientId=0): ищем любого заспавненного игрока
            if (clientId == 0)
            {
                foreach (var player in players)
                {
                    if (player.IsSpawned || player.IsOwner)
                    {
                        return player;
                    }
                }
            }

            Debug.LogWarning($"[ContractSystem] Не нашёл NetworkPlayer для clientId={clientId}. Всего игроков: {players.Length}");
            return null;
        }

        private PlayerTradeStorage FindPlayerStorage(ulong clientId)
        {
            var player = FindPlayerNetworkPlayer(clientId);
            if (player == null) return null;

            var storage = player.GetComponent<PlayerTradeStorage>();
            if (storage == null)
            {
                storage = player.gameObject.AddComponent<PlayerTradeStorage>();
            }
            return storage;
        }

        private PlayerCreditsManager FindPlayerCredits(ulong clientId)
        {
            var player = FindPlayerNetworkPlayer(clientId);
            if (player == null) return null;

            var credits = player.GetComponent<PlayerCreditsManager>();
            if (credits == null)
            {
                credits = player.gameObject.AddComponent<PlayerCreditsManager>();
            }
            return credits;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
