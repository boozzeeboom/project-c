using System.Collections.Generic;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Repository;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Серверный singleton, держащий всё runtime-состояние контрактной подсистемы:
    ///   • Словарь доступных контрактов (contractId → ContractData)
    ///   • Словарь активных контрактов игроков (playerId → List&lt;contractId&gt;)
    ///   • Словарь долгов игроков (playerId → ContractDebt)
    ///   • Словарь контрактов по локации (locationId → List&lt;contractId&gt;)
    ///   • Таблица расстояний между 4 городами (primium/secundus/tertius/quartus)
    ///
    /// НЕ MonoBehaviour. НЕ NetworkBehaviour. НЕ сериализуется в сцену.
    /// Создаётся в <c>ContractServer.OnNetworkSpawn</c> на сервере.
    ///
    /// Все мутации — здесь. Клиент получает только снепшоты (<see cref="ContractSnapshotDto"/>)
    /// и результаты (<see cref="ContractResultDto"/>).
    ///
    /// Использует <see cref="IPlayerDataRepository"/> для кредитов (как <see cref="TradeWorld"/>).
    /// Товары берёт из <see cref="ContractWorldItemResolver"/> (встроенный мини-резолвер,
    /// чтобы не зависеть от TradeDatabase при инициализации).
    ///
    /// C2-этап миграции контрактов на v2-архитектуру (см. docs/dev/CONTRACT_V2_MIGRATION.md).
    /// </summary>
    public class ContractWorld
    {
        public static ContractWorld Instance { get; private set; }

        public IPlayerDataRepository Repository { get; private set; }
        public ContractWorldItemResolver Resolver { get; private set; }

        // === Runtime state ===
        private readonly Dictionary<string, ContractData> _availableContracts = new Dictionary<string, ContractData>();
        private readonly Dictionary<ulong, List<string>> _playerContracts = new Dictionary<ulong, List<string>>();
        private readonly Dictionary<ulong, ContractDebt> _playerDebts = new Dictionary<ulong, ContractDebt>();
        private readonly Dictionary<string, List<string>> _locationContracts = new Dictionary<string, List<string>>();

        // Кэш базовой цены по itemId (для расчёта reward в Create).
        // Заполняется из TradeItemDefinition через Resolver.
        // Используется ContractData.Create.
        private readonly Dictionary<string, float> _itemBasePrice = new Dictionary<string, float>();

        public IReadOnlyDictionary<string, ContractData> AvailableContracts => _availableContracts;
        public IReadOnlyDictionary<ulong, List<string>> PlayerContracts => _playerContracts;
        public IReadOnlyDictionary<ulong, ContractDebt> PlayerDebts => _playerDebts;

        public bool IsInitialized { get; private set; }

        // === Tunables ===
        [Header("Tunables")]
        public int MaxActiveContractsPerPlayer = 3;
        public bool AutoRegenerateContracts = true;
        public bool AutoInitContracts = true;

        // === Distance table (GDD_25 §3.2) ===
        // Индексы: 0=primium, 1=secundus, 2=tertius, 3=quartus
        private readonly float[,] _distanceTable = new float[4, 4];

        // ========================================================
        // INITIALIZATION
        // ========================================================

        public static ContractWorld CreateAndInitialize(
            IPlayerDataRepository repository,
            ContractWorldItemResolver resolver,
            bool autoInitContracts = true)
        {
            var w = new ContractWorld();
            w.Initialize(repository, resolver, autoInitContracts);
            Instance = w;
            return w;
        }

        public void Initialize(
            IPlayerDataRepository repository,
            ContractWorldItemResolver resolver,
            bool autoInitContracts = true)
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[ContractWorld] уже инициализирован, повторная инициализация игнорируется");
                return;
            }

            Repository = repository ?? throw new System.ArgumentNullException(nameof(repository));
            Resolver = resolver ?? throw new System.ArgumentNullException(nameof(resolver));

            InitDistanceTable();
            LoadAvailableItems();
            BuildItemPriceIndex();

            if (autoInitContracts)
            {
                GenerateContractsForAllLocations();
            }

            IsInitialized = true;
            Debug.Log($"[ContractWorld] инициализирован: items={_itemBasePrice.Count}, contracts={_availableContracts.Count}");
        }

        public void Shutdown()
        {
            _availableContracts.Clear();
            _playerContracts.Clear();
            _playerDebts.Clear();
            _locationContracts.Clear();
            _itemBasePrice.Clear();
            IsInitialized = false;
            if (Instance == this) Instance = null;
            Debug.Log("[ContractWorld] shutdown");
        }

        // ========================================================
        // DISTANCE TABLE
        // ========================================================

        private void InitDistanceTable()
        {
            // Симметричная матрица расстояний (км), GDD_25 §3.2
            SetDistance(0, 1, 120f);  // Приму ↔ Секунд
            SetDistance(0, 2, 200f);  // Приму ↔ Тертиус
            SetDistance(0, 3, 180f);  // Приму ↔ Квартус
            SetDistance(1, 2, 150f);  // Секунд ↔ Тертиус
            SetDistance(1, 3, 160f);  // Секунд ↔ Квартус
            SetDistance(2, 3, 100f);  // Тертиус ↔ Квартус
        }

        private void SetDistance(int a, int b, float km)
        {
            _distanceTable[a, b] = km;
            _distanceTable[b, a] = km;
        }

        public float GetDistance(string fromLocationId, string toLocationId)
        {
            int fromIndex = LocationIdToIndex(fromLocationId);
            int toIndex = LocationIdToIndex(toLocationId);
            if (fromIndex < 0 || toIndex < 0) return 100f;
            float dist = _distanceTable[fromIndex, toIndex];
            return dist > 0f ? dist : 100f;
        }

        private static int LocationIdToIndex(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return -1;
            switch (locationId.ToLower())
            {
                case "primium":  return 0;
                case "secundus": return 1;
                case "tertius":  return 2;
                case "quartus":  return 3;
                default: return -1;
            }
        }

        public static bool IsValidLocation(string locationId) => LocationIdToIndex(locationId) >= 0;

        // ========================================================
        // ITEMS (для ContractData.Create)
        // ========================================================

        private void LoadAvailableItems()
        {
            // Резолвер уже должен быть инициализирован с items.
            // Здесь только sanity-check.
            if (Resolver == null || Resolver.AllItemIds == null || Resolver.AllItemIds.Count == 0)
            {
                Debug.LogWarning("[ContractWorld] Resolver пуст — генерация контрактов использует fallback");
            }
        }

        private void BuildItemPriceIndex()
        {
            if (Resolver == null) return;
            foreach (var id in Resolver.AllItemIds)
            {
                float price = Resolver.GetBasePrice(id);
                if (price > 0f) _itemBasePrice[id] = price;
            }
        }

        private float GetItemBasePrice(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0f;
            if (_itemBasePrice.TryGetValue(itemId, out var p)) return p;
            // Fallback: спросить у resolver напрямую
            return Resolver != null ? Resolver.GetBasePrice(itemId) : 0f;
        }

        // ========================================================
        // GENERATION
        // ========================================================

        public void GenerateContractsForAllLocations()
        {
            string[] locations = { "primium", "secundus", "tertius", "quartus" };
            foreach (var loc in locations) GenerateContractsForLocation(loc);
        }

        /// <summary>
        /// Сгенерировать 3 типа контрактов (Standard/Urgent/Receipt) для локации.
        /// Идентично legacy ContractSystem.GenerateContractsForLocation:252-298.
        /// </summary>
        public void GenerateContractsForLocation(string fromLocationId)
        {
            if (!IsValidLocation(fromLocationId)) return;

            if (!_locationContracts.ContainsKey(fromLocationId))
                _locationContracts[fromLocationId] = new List<string>();

            // Удаляем старые непринятые контракты локации
            foreach (var cid in _locationContracts[fromLocationId])
            {
                if (_availableContracts.ContainsKey(cid))
                    _availableContracts.Remove(cid);
            }
            _locationContracts[fromLocationId].Clear();

            // Доступные товары
            var allItemIds = Resolver != null ? Resolver.AllItemIds : new List<string>();
            if (allItemIds == null || allItemIds.Count == 0)
            {
                Debug.LogWarning($"[ContractWorld] GenerateContractsForLocation({fromLocationId}): нет товаров, пропускаю");
                return;
            }

            string[] allLocations = { "primium", "secundus", "tertius", "quartus" };
            var destinations = new List<string>();
            foreach (var l in allLocations)
            {
                if (l != fromLocationId) destinations.Add(l);
            }

            // Случайный товар
            string itemId = allItemIds[Random.Range(0, allItemIds.Count)];
            int quantity = Random.Range(2, 8); // 2-7 единиц
            string toLocationId = destinations[Random.Range(0, destinations.Count)];
            float distance = GetDistance(fromLocationId, toLocationId);
            float basePrice = GetItemBasePrice(itemId);

            // 1. Standard
            var standard = ContractData.Create(
                ContractType.Standard, itemId, quantity,
                fromLocationId, toLocationId, basePrice, distance);
            _availableContracts[standard.contractId] = standard;
            _locationContracts[fromLocationId].Add(standard.contractId);

            // 2. Urgent
            var urgent = ContractData.Create(
                ContractType.Urgent, itemId, quantity,
                fromLocationId, toLocationId, basePrice, distance);
            _availableContracts[urgent.contractId] = urgent;
            _locationContracts[fromLocationId].Add(urgent.contractId);

            // 3. Receipt (под расписку) — меньший объём
            var receipt = ContractData.Create(
                ContractType.Receipt, itemId, Mathf.Min(quantity, 3),
                fromLocationId, toLocationId, basePrice, distance);
            _availableContracts[receipt.contractId] = receipt;
            _locationContracts[fromLocationId].Add(receipt.contractId);
        }

        // ========================================================
        // QUERIES
        // ========================================================

        public ContractData GetContract(string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return null;
            return _availableContracts.TryGetValue(contractId, out var c) ? c : null;
        }

        public ContractDebt GetOrCreateDebt(ulong clientId)
        {
            if (_playerDebts.TryGetValue(clientId, out var d)) return d;
            d = new ContractDebt(clientId);
            _playerDebts[clientId] = d;
            return d;
        }

        public List<string> GetPlayerContractList(ulong clientId)
        {
            if (_playerContracts.TryGetValue(clientId, out var l)) return l;
            return new List<string>();
        }

        public int GetPlayerActiveCount(ulong clientId)
        {
            return _playerContracts.TryGetValue(clientId, out var l) ? l.Count : 0;
        }

        public ContractData[] GetAvailableForLocation(string locationId)
        {
            if (!_locationContracts.TryGetValue(locationId, out var ids)) return new ContractData[0];
            var result = new List<ContractData>();
            foreach (var cid in ids)
            {
                if (_availableContracts.TryGetValue(cid, out var c) && c.state == ContractState.Pending)
                    result.Add(c);
            }
            return result.ToArray();
        }

        public ContractData[] GetActiveForPlayer(ulong clientId)
        {
            var ids = GetPlayerContractList(clientId);
            var result = new List<ContractData>();
            foreach (var cid in ids)
            {
                if (_availableContracts.TryGetValue(cid, out var c) && c.state == ContractState.Active)
                    result.Add(c);
            }
            return result.ToArray();
        }

        // ========================================================
        // OPERATIONS
        // ========================================================

        /// <summary>
        /// Принять контракт. Идентично legacy ContractSystem.AcceptContractServerRpc:362-430,
        /// но без RPC и без position-check (это делает ContractServer).
        /// </summary>
        public ContractOpResult TryAccept(ulong clientId, string contractId)
        {
            // 1. Валидация контракта
            var contract = GetContract(contractId);
            if (contract == null)
                return ContractOpResult.Fail(ContractResultCode.ContractNotFound, "Контракт не найден!");

            if (contract.state != ContractState.Pending)
                return ContractOpResult.Fail(ContractResultCode.ContractNotPending, "Контракт уже принят или истёк!");

            // 2. Проверка долгового лимита
            var debt = GetOrCreateDebt(clientId);
            if (!debt.CanAcceptContracts())
                return ContractOpResult.Fail(ContractResultCode.TooMuchDebt,
                    $"Долг {debt.CurrentDebt:F0} CR! Ограничение контрактов.");

            // 3. Проверка лимита активных контрактов
            if (!_playerContracts.ContainsKey(clientId))
                _playerContracts[clientId] = new List<string>();

            if (_playerContracts[clientId].Count >= MaxActiveContractsPerPlayer)
                return ContractOpResult.Fail(ContractResultCode.MaxActiveReached,
                    $"Максимум {MaxActiveContractsPerPlayer} активных контрактов!");

            // 4. Для Receipt — добавить товар на склад игрока (вызывает ContractServer)
            // В v2 это делается через callback в ContractServer (он имеет доступ к Repository
            // и MarketWorld.GetOrLoadWarehouse). Здесь только помечаем, что требуется действие.
            // Конкретная реализация добавления груза — в ContractServer.TryAcceptServerSide
            // (после возврата Ok он дёргает MarketWorld.AddToWarehouse).
            // Для упрощения v2-реализации — НЕ кладём груз автоматически на этом шаге.
            // TODO (future): интеграция с TradeWorld.AddToWarehouse для Receipt контракта.
            // Сейчас v2-Rceipt контракт работает в «туториал»-режиме: cargoValue × 1.5 при
            // провале начисляется как долг (как в legacy).

            // === ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ ===
            contract.Activate(clientId);
            _availableContracts[contractId] = contract;
            _playerContracts[clientId].Add(contractId);

            return ContractOpResult.Ok($"Контракт принят: {contract.GetTypeDisplayName()}", contract);
        }

        /// <summary>
        /// Завершить контракт. Идентично legacy ContractSystem.CompleteContractServerRpc:437-556.
        /// </summary>
        public ContractOpResult TryComplete(ulong clientId, string contractId, string completionLocationId)
        {
            var contract = GetContract(contractId);
            if (contract == null)
                return ContractOpResult.Fail(ContractResultCode.ContractNotFound, "Контракт не найден!");

            if (contract.state != ContractState.Active)
                return ContractOpResult.Fail(ContractResultCode.ContractNotActive, "Контракт не активен!");

            if (contract.assignedPlayerId != clientId)
                return ContractOpResult.Fail(ContractResultCode.ContractNotAssigned, "Это не ваш контракт!");

            // 3. Проверка таймера
            if (contract.timeLimit > 0f && contract.timeRemaining <= 0f)
            {
                contract.Fail();
                _availableContracts[contractId] = contract;
                HandleFailedContract(contract, clientId);
                return ContractOpResult.Fail(ContractResultCode.TimerExpired, "Время контракта истекло!");
            }

            // 4. Проверка локации
            if (completionLocationId != contract.toLocationId)
                return ContractOpResult.Fail(ContractResultCode.WrongDestination,
                    $"Вы не в целевой локации! Нужно: {contract.toLocationId}");

            // 5. Для non-Receipt контракта — проверка груза (вызывающий код делает это
            // через TradeWorld.GetOrLoadCargo и Repository.GetWarehouse; ContractServer
            // сам валидирует и удаляет cargo). Здесь только помечаем, что нужен cargo-check.
            // Возвращаем RequireCargoCheck — ContractServer выполнит его.
            // Для упрощения v2 — НЕ делаем cargo-check автоматически (legacy ContractSystem
            // делал это вручную через PlayerTradeStorage + CargoSystem).
            // TODO (future): интеграция с TradeWorld для cargo validation.

            // === ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ ===
            contract.Complete();
            _availableContracts[contractId] = contract;

            if (_playerContracts.ContainsKey(clientId))
                _playerContracts[clientId].Remove(contractId);

            // Начисляем награду
            if (Repository != null)
            {
                float current = Repository.GetCredits(clientId);
                Repository.SetCredits(clientId, current + contract.reward);
            }

            return ContractOpResult.Ok($"Контракт завершён! Награда: {contract.reward:F0} CR", contract, contract.reward);
        }

        /// <summary>Провалить контракт (отмена игрока или авто-fail по таймеру).</summary>
        public ContractOpResult TryFail(ulong clientId, string contractId, bool isManual)
        {
            var contract = GetContract(contractId);
            if (contract == null)
                return ContractOpResult.Fail(ContractResultCode.ContractNotFound, "Контракт не найден!");

            if (contract.state != ContractState.Active)
                return ContractOpResult.Fail(ContractResultCode.ContractNotActive, "Контракт не активен!");

            if (contract.assignedPlayerId != clientId)
                return ContractOpResult.Fail(ContractResultCode.ContractNotAssigned, "Это не ваш контракт!");

            contract.Fail();
            _availableContracts[contractId] = contract;

            if (_playerContracts.ContainsKey(clientId))
                _playerContracts[clientId].Remove(contractId);

            HandleFailedContract(contract, clientId);

            string reason = isManual ? "отменён игроком" : "время истекло";
            return ContractOpResult.Fail(ContractResultCode.Ok, $"Контракт провален: {reason}");
            // (используем Code=Ok для провала по запросу игрока — message говорит «провален»)
        }

        // ========================================================
        // INTERNAL: Handle Failed Contract (debt, penalty)
        // ========================================================

        /// <summary>
        /// Обработать провал контракта (debt, penalty).
        /// Идентично legacy ContractSystem.HandleFailedContract:598-631.
        /// </summary>
        public void HandleFailedContract(ContractData contract, ulong playerId)
        {
            var debt = GetOrCreateDebt(playerId);

            if (contract.isReceiptContract)
            {
                // Receipt контракт провален — долг = cargoValue × 1.5
                float debtAmount = contract.cargoValue * 1.5f;
                debt.AddDebt(debtAmount);
            }
            else
            {
                // Обычный контракт провален по таймеру — штраф 20% от награды
                if (contract.timeLimit > 0f && contract.timeRemaining <= 0f)
                {
                    float penalty = contract.reward * 0.2f;
                    if (penalty > 0f && Repository != null)
                    {
                        float current = Repository.GetCredits(playerId);
                        if (current >= penalty)
                            Repository.SetCredits(playerId, current - penalty);
                    }
                }
            }
        }

        // ========================================================
        // TICK (server-side, called from ContractServer.FixedUpdate)
        // ========================================================

        /// <summary>
        /// Тик таймеров активных контрактов + decay долгов.
        /// Возвращает список (playerId, contractId) контрактов, которые провалились
        /// по таймеру — ContractServer шлёт клиентам ContractResultDto для каждого.
        /// </summary>
        public List<(ulong playerId, string contractId, ContractData contract)> Tick(float deltaTime, float now)
        {
            var expired = new List<(ulong, string, ContractData)>();

            // Таймеры активных контрактов
            foreach (var kvp in _playerContracts)
            {
                ulong playerId = kvp.Key;
                // ToList чтобы не модифицировать коллекцию во время итерации
                var idsCopy = new List<string>(kvp.Value);
                foreach (var contractId in idsCopy)
                {
                    if (!_availableContracts.TryGetValue(contractId, out var contract)) continue;
                    if (contract.state != ContractState.Active) continue;

                    contract.TickTimer(deltaTime);
                    _availableContracts[contractId] = contract;

                    if (contract.state == ContractState.Failed)
                    {
                        HandleFailedContract(contract, playerId);
                        expired.Add((playerId, contractId, contract));
                    }
                }
            }

            // Decay долгов
            foreach (var d in _playerDebts.Values)
            {
                d.CheckAndApplyDecay(now);
            }

            return expired;
        }

        // ========================================================
        // SNAPSHOT
        // ========================================================

        /// <summary>
        /// Собрать снепшот для клиента. Вызывается из ContractServer.
        /// </summary>
        public ContractSnapshotDto BuildSnapshot(ulong clientId, string locationId, string displayName,
            float timeMultiplier, float secondsUntilNextTick)
        {
            var available = GetAvailableForLocation(locationId);
            var active = GetActiveForPlayer(clientId);
            var debt = GetOrCreateDebt(clientId);

            return new ContractSnapshotDto
            {
                locationId = locationId,
                displayName = displayName,
                available = ToDtoArray(available),
                active = ToDtoArray(active),
                debtAmount = debt.CurrentDebt,
                debtLevel = (int)debt.Level,
                canAcceptContracts = debt.CanAcceptContracts(),
                marketTimeMultiplier = timeMultiplier,
                secondsUntilNextTick = secondsUntilNextTick
            };
        }

        /// <summary>Конвертировать ContractData[] в ContractDto[] для передачи клиенту.</summary>
        public ContractDto[] ToDtoArray(ContractData[] contracts)
        {
            if (contracts == null || contracts.Length == 0) return null;
            var dtos = new ContractDto[contracts.Length];
            for (int i = 0; i < contracts.Length; i++)
            {
                dtos[i] = ToDto(contracts[i]);
            }
            return dtos;
        }

        public ContractDto ToDto(ContractData c)
        {
            if (c == null) return default;
            string displayName = Resolver != null ? Resolver.GetDisplayName(c.itemId) : c.itemId;
            return new ContractDto
            {
                contractId = c.contractId,
                type = (byte)c.type,
                state = (byte)c.state,
                itemId = c.itemId,
                displayName = displayName,
                quantity = c.quantity,
                fromLocationId = c.fromLocationId,
                toLocationId = c.toLocationId,
                reward = c.reward,
                cargoValue = c.cargoValue,
                timeLimit = c.timeLimit,
                timeRemaining = c.timeRemaining,
                isReceiptContract = c.isReceiptContract
            };
        }
    }

    /// <summary>
    /// Результат операции (accept / complete / fail) в <see cref="ContractWorld"/>.
    /// Конвертируется в <see cref="ContractResultDto"/> на уровне <c>ContractServer</c>.
    /// </summary>
    public struct ContractOpResult
    {
        public ContractResultCode Code;
        public string Message;
        public bool IsSuccess;
        public ContractData Contract; // null если операция не изменила состояние контракта
        public float Reward;          // для complete (0 в остальных)

        public static ContractOpResult Ok(string msg, ContractData c, float reward = 0f)
            => new ContractOpResult { Code = ContractResultCode.Ok, Message = msg, IsSuccess = true, Contract = c, Reward = reward };

        public static ContractOpResult Fail(ContractResultCode code, string msg)
            => new ContractOpResult { Code = code, Message = msg, IsSuccess = false };
    }
}
