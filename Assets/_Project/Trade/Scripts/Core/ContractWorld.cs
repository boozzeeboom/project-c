using System.Collections.Generic;
using System.Linq;
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

        // Не перезаписываем сохранение после corruption/future-schema rejection.
        private bool _persistenceWriteBlocked;

        // === Tunables ===
        [Header("Tunables")]
        public int MaxActiveContractsPerPlayer = 3;
        public bool AutoRegenerateContracts = true;
        public bool AutoInitContracts = true;
        public float StandardContractTimeLimitSeconds { get; private set; } = 300f;
        public float UrgentContractTimeLimitSeconds { get; private set; } = 150f;
        public float ReceiptContractTimeLimitSeconds { get; private set; } = 600f;

        /// <summary>
        /// Максимальное количество Completed/Failed records на одного игрока.
        /// Active/Pending records никогда не удаляются этой политикой.
        /// </summary>
        public int MaxTerminalRecordsPerPlayer = 50;

        // === Distance table (GDD_25 §3.2) ===
        // Индексы: 0=primium, 1=secundus, 2=tertius, 3=quartus
        private readonly float[,] _distanceTable = new float[4, 4];

        // ========================================================
        // INITIALIZATION
        // ========================================================

        public static ContractWorld CreateAndInitialize(
            IPlayerDataRepository repository,
            ContractWorldItemResolver resolver,
            bool autoInitContracts = true,
            float standardContractTimeLimitSeconds = 300f,
            float urgentContractTimeLimitSeconds = 150f,
            float receiptContractTimeLimitSeconds = 600f)
        {
            var w = new ContractWorld
            {
                StandardContractTimeLimitSeconds = Mathf.Max(0f, standardContractTimeLimitSeconds),
                UrgentContractTimeLimitSeconds = Mathf.Max(0f, urgentContractTimeLimitSeconds),
                ReceiptContractTimeLimitSeconds = Mathf.Max(0f, receiptContractTimeLimitSeconds)
            };
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

            // Try to restore persisted contracts first.
            RepositoryLoadStatus loadStatus = LoadAll();
            _persistenceWriteBlocked = loadStatus == RepositoryLoadStatus.CorruptSave
                || loadStatus == RepositoryLoadStatus.UnsupportedSchema;

            if (loadStatus == RepositoryLoadStatus.NoSaveFound && autoInitContracts)
            {
                GenerateContractsForAllLocations();
                SaveAll();
            }

            IsInitialized = true;
            Debug.Log($"[ContractWorld] инициализирован: items={_itemBasePrice.Count}, contracts={_availableContracts.Count}, loadStatus={loadStatus}");
        }

        public void Shutdown()
        {
            SaveAll();

            _availableContracts.Clear();
            _playerContracts.Clear();
            _playerDebts.Clear();
            _locationContracts.Clear();
            _itemBasePrice.Clear();
            _persistenceWriteBlocked = false;
            IsInitialized = false;
            if (Instance == this) Instance = null;
            Debug.Log("[ContractWorld] shutdown");
        }

        // ========================================================
        // PERSISTENCE
        // ========================================================

        /// <summary>
        /// Save all contract state to IPlayerDataRepository.
        /// Called after every mutation (accept/complete/fail/tick) and on Shutdown.
        /// </summary>
        public void SaveAll()
        {
            if (Repository == null || _persistenceWriteBlocked) return;

            var data = new ContractSaveData();

            // Contracts
            data.contracts.AddRange(_availableContracts.Values);

            // Debts
            foreach (var kvp in _playerDebts)
            {
                data.debts.Add(new ContractDebtEntry
                {
                    playerId = kvp.Key,
                    currentDebt = kvp.Value.CurrentDebt,
                    lastDecayTime = kvp.Value.LastDecayTime
                });
            }

            // Player → contract IDs
            foreach (var kvp in _playerContracts)
            {
                data.playerContracts.Add(new PlayerContractEntry
                {
                    playerId = kvp.Key,
                    contractIds = new List<string>(kvp.Value)
                });
            }

            // Location → contract IDs
            foreach (var kvp in _locationContracts)
            {
                data.locationContracts.Add(new LocationContractEntry
                {
                    locationId = kvp.Key,
                    contractIds = new List<string>(kvp.Value)
                });
            }

            if (Repository.SaveContracts(data))
                PruneTerminalRecordsAfterSuccessfulPersistence();
        }

        /// <summary>
        /// Удалить старые terminal records только после успешной записи snapshot.
        /// Active/Pending records, debts и indexes текущих активных контрактов
        /// не затрагиваются.
        /// </summary>
        private void PruneTerminalRecordsAfterSuccessfulPersistence()
        {
            int maxRecords = Mathf.Max(0, MaxTerminalRecordsPerPlayer);
            var terminalByPlayer = new Dictionary<ulong, List<KeyValuePair<string, ContractData>>>();

            foreach (var kvp in _availableContracts)
            {
                var contract = kvp.Value;
                if (contract == null
                    || (contract.state != ContractState.Completed && contract.state != ContractState.Failed))
                    continue;

                if (!terminalByPlayer.TryGetValue(contract.assignedPlayerId, out var records))
                {
                    records = new List<KeyValuePair<string, ContractData>>();
                    terminalByPlayer[contract.assignedPlayerId] = records;
                }
                records.Add(kvp);
            }

            int removed = 0;
            foreach (var playerRecords in terminalByPlayer)
            {
                var records = playerRecords.Value;
                if (records.Count <= maxRecords) continue;

                records.Sort(CompareTerminalRecords);
                int removeCount = records.Count - maxRecords;
                for (int i = 0; i < removeCount; i++)
                {
                    string contractId = records[i].Key;
                    if (!_availableContracts.TryGetValue(contractId, out var contract)) continue;

                    _availableContracts.Remove(contractId);
                    RemovePlayerContractReference(contract.assignedPlayerId, contractId);
                    RemoveContractFromLocationBoard(contract.fromLocationId, contractId);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Debug.Log($"[ContractWorld] terminal retention removed {removed} records; limit={maxRecords} per player");
            }
        }

        private static int CompareTerminalRecords(
            KeyValuePair<string, ContractData> left,
            KeyValuePair<string, ContractData> right)
        {
            long leftTicks = left.Value != null ? left.Value.terminalAtUtcTicks : 0L;
            long rightTicks = right.Value != null ? right.Value.terminalAtUtcTicks : 0L;

            if (leftTicks == rightTicks)
                return string.CompareOrdinal(left.Key, right.Key);
            if (leftTicks == 0L) return -1;
            if (rightTicks == 0L) return 1;
            return leftTicks < rightTicks ? -1 : 1;
        }

        /// <summary>
        /// Load contract state from IPlayerDataRepository.
        /// Returns an explicit persistence status so valid-empty, missing,
        /// corrupt and unsupported snapshots cannot collapse into one boolean.
        /// </summary>
        private RepositoryLoadStatus LoadAll()
        {
            if (Repository == null) return RepositoryLoadStatus.CorruptSave;

            RepositoryLoadStatus loadStatus = Repository.TryLoadContracts(out var data);
            if (loadStatus == RepositoryLoadStatus.NoSaveFound)
                return loadStatus;

            if (loadStatus == RepositoryLoadStatus.CorruptSave
                || loadStatus == RepositoryLoadStatus.UnsupportedSchema)
            {
                Debug.LogError($"[ContractWorld] contracts snapshot rejected: {loadStatus}");
                return loadStatus;
            }

            if (data == null)
            {
                Debug.LogError("[ContractWorld] repository returned a successful contract load status with null data");
                return RepositoryLoadStatus.CorruptSave;
            }

            // Contracts
            _availableContracts.Clear();
            foreach (var c in data.contracts ?? new List<ContractData>())
            {
                if (c != null && !string.IsNullOrEmpty(c.contractId))
                    _availableContracts[c.contractId] = c;
            }

            // Debts — reconstruct ContractDebt objects
            _playerDebts.Clear();
            foreach (var d in data.debts ?? new List<ContractDebtEntry>())
            {
                if (d != null)
                    _playerDebts[d.playerId] = new ContractDebt(d.playerId, d.currentDebt, d.lastDecayTime);
            }

            // Player → contract IDs
            _playerContracts.Clear();
            foreach (var e in data.playerContracts ?? new List<PlayerContractEntry>())
            {
                _playerContracts[e.playerId] = new List<string>(e.contractIds ?? new List<string>());
            }

            // Удаляем устаревшие active-index ссылки из старых snapshots.
            PruneAllPlayerContractIndexes();

            // Location → contract IDs
            _locationContracts.Clear();
            foreach (var e in data.locationContracts ?? new List<LocationContractEntry>())
            {
                if (!string.IsNullOrEmpty(e.locationId))
                    _locationContracts[e.locationId] = new List<string>(e.contractIds ?? new List<string>());
            }

            Debug.Log($"[ContractWorld] Loaded {_availableContracts.Count} contracts, {_playerDebts.Count} debts from repository; status={loadStatus}");
            return loadStatus;
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
        /// Сгенерировать доступные delivery-контракты для локации.
        /// Receipt временно не публикуется: его физическая выдача и ownership flow
        /// не реализованы и не должны быть доступны игроку.
        /// </summary>
        public void GenerateContractsForLocation(string fromLocationId)
        {
            if (!IsValidLocation(fromLocationId)) return;

            if (!_locationContracts.ContainsKey(fromLocationId))
                _locationContracts[fromLocationId] = new List<string>();

            // Сбрасываем только текущую доску офферов.
            // Active/Completed/Failed records нельзя удалять во время регенерации:
            // _availableContracts также является registry для уже принятых контрактов.
            var previousOfferIds = new List<string>(_locationContracts[fromLocationId]);
            _locationContracts[fromLocationId].Clear();
            foreach (var cid in previousOfferIds)
            {
                if (!_availableContracts.TryGetValue(cid, out var previousContract))
                    continue;

                if (previousContract.state == ContractState.Pending)
                {
                    _availableContracts.Remove(cid);
                }
                else
                {
                    Debug.LogWarning($"[ContractWorld] Сохраняю {previousContract.state} contract {cid} при регенерации доски {fromLocationId}");
                }
            }

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
                fromLocationId, toLocationId, basePrice, distance,
                0f, StandardContractTimeLimitSeconds, UrgentContractTimeLimitSeconds,
                ReceiptContractTimeLimitSeconds);
            _availableContracts[standard.contractId] = standard;
            _locationContracts[fromLocationId].Add(standard.contractId);

            // 2. Urgent
            var urgent = ContractData.Create(
                ContractType.Urgent, itemId, quantity,
                fromLocationId, toLocationId, basePrice, distance,
                0f, StandardContractTimeLimitSeconds, UrgentContractTimeLimitSeconds,
                ReceiptContractTimeLimitSeconds);
            _availableContracts[urgent.contractId] = urgent;
            _locationContracts[fromLocationId].Add(urgent.contractId);

            // Receipt намеренно не создаём до реализации полного acceptance/settlement flow.
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
            if (!_playerContracts.TryGetValue(clientId, out var ids)) return 0;

            int activeCount = 0;
            foreach (var contractId in ids)
            {
                if (_availableContracts.TryGetValue(contractId, out var contract)
                    && contract != null
                    && contract.state == ContractState.Active
                    && contract.assignedPlayerId == clientId)
                {
                    activeCount++;
                }
            }
            return activeCount;
        }

        private void PruneAllPlayerContractIndexes()
        {
            foreach (var playerId in new List<ulong>(_playerContracts.Keys))
            {
                PrunePlayerContractIndex(playerId);
            }
        }

        private void PrunePlayerContractIndex(ulong clientId)
        {
            if (!_playerContracts.TryGetValue(clientId, out var ids)) return;

            for (int i = ids.Count - 1; i >= 0; i--)
            {
                string contractId = ids[i];
                if (_availableContracts.TryGetValue(contractId, out var contract)
                    && contract != null
                    && contract.state == ContractState.Active
                    && contract.assignedPlayerId == clientId)
                {
                    continue;
                }

                Debug.LogWarning($"[ContractWorld] Удаляю устаревшую active-index ссылку {contractId} для player {clientId}");
                ids.RemoveAt(i);
            }
        }

        private void RemovePlayerContractReference(ulong clientId, string contractId)
        {
            if (_playerContracts.TryGetValue(clientId, out var ids))
            {
                ids.Remove(contractId);
            }
        }

        private void RemoveContractFromLocationBoard(string locationId, string contractId)
        {
            if (_locationContracts.TryGetValue(locationId, out var ids))
            {
                ids.Remove(contractId);
            }
        }

        public ContractData[] GetAvailableForLocation(string locationId)
        {
            if (!_locationContracts.TryGetValue(locationId, out var ids)) return new ContractData[0];
            var result = new List<ContractData>();
            foreach (var cid in ids)
            {
                if (_availableContracts.TryGetValue(cid, out var c)
                    && c.state == ContractState.Pending
                    && !c.isReceiptContract)
                {
                    result.Add(c);
                }
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

            if (contract.isReceiptContract)
            {
                return ContractOpResult.Fail(
                    ContractResultCode.UnsupportedContractType,
                    "Контракты «под расписку» временно недоступны.");
            }

            // 2. Проверка долгового лимита
            var debt = GetOrCreateDebt(clientId);
            if (!debt.CanAcceptContracts())
                return ContractOpResult.Fail(ContractResultCode.TooMuchDebt,
                    $"Долг {debt.CurrentDebt:F0} CR! Ограничение контрактов.");

            // 3. Проверка лимита активных контрактов.
            // Чистим старые snapshots и считаем только реально Active records.
            if (!_playerContracts.ContainsKey(clientId))
                _playerContracts[clientId] = new List<string>();
            else
                PrunePlayerContractIndex(clientId);

            if (GetPlayerActiveCount(clientId) >= MaxActiveContractsPerPlayer)
                return ContractOpResult.Fail(ContractResultCode.MaxActiveReached,
                    $"Максимум {MaxActiveContractsPerPlayer} активных контрактов!");

            // 4. Receipt-контракты отфильтрованы выше и не проходят acceptance flow.
            // Это преднамеренный fail-closed режим до определения физической выдачи
            // товара, ownership и settlement policy.

            // === ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ ===
            contract.Activate(clientId);
            _availableContracts[contractId] = contract;
            if (!_playerContracts[clientId].Contains(contractId))
                _playerContracts[clientId].Add(contractId);
            RemoveContractFromLocationBoard(contract.fromLocationId, contractId);

            SaveAll();

            return ContractOpResult.Ok($"Контракт принят: {contract.GetTypeDisplayName()}", contract);
        }

        /// <summary>
        /// Завершить контракт. Идентично legacy ContractSystem.CompleteContractServerRpc:437-556.
        /// Для delivery-контрактов перед выдачей reward сервер атомарно списывает
        /// нужный item из трюма указанного корабля и/или склада в destination.
        /// Receipt-контракты намеренно не используют этот путь до отдельного решения
        /// по MKT-CON-004 (физическая выдача товара при accept).
        /// </summary>
        public ContractOpResult TryComplete(
            ulong clientId,
            string contractId,
            string completionLocationId,
            ulong shipNetworkObjectId,
            ShipClass shipClass)
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
                RemovePlayerContractReference(clientId, contractId);
                HandleFailedContract(contract, clientId);
                SaveAll();
                return ContractOpResult.Fail(ContractResultCode.TimerExpired, "Время контракта истекло!");
            }

            // 4. Проверка локации
            if (completionLocationId != contract.toLocationId)
                return ContractOpResult.Fail(ContractResultCode.WrongDestination,
                    $"Вы не в целевой локации! Нужно: {contract.toLocationId}");

            // 5. Receipt не может попасть сюда для новых контрактов, но старые
            // persisted records должны завершаться безопасно, без выдачи reward.
            if (contract.isReceiptContract)
            {
                return ContractOpResult.Fail(
                    ContractResultCode.UnsupportedContractType,
                    "Контракты «под расписку» временно недоступны; завершите его отменой.");
            }

            // 6. Delivery-контракт требует доказанного server-side списания.
            if (TradeWorld.Instance == null)
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);

            if (!TradeWorld.Instance.TryConsumeDeliveryCargo(
                    clientId,
                    contract.toLocationId,
                    shipNetworkObjectId,
                    shipClass,
                    contract.itemId,
                    contract.quantity,
                    out var cargoFail))
            {
                return ContractOpResult.Fail(
                    cargoFail == "cargo_missing"
                        ? ContractResultCode.CargoMissing
                        : ContractResultCode.InternalError,
                    null);
            }

            // === ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ ===
            // Cargo уже сохранён до этой точки. Только после этого меняем state
            // и начисляем reward — delivery без груза не может выдать награду.
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

            SaveAll();

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

            RemovePlayerContractReference(clientId, contractId);
            HandleFailedContract(contract, clientId);

            SaveAll();

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
                        RemovePlayerContractReference(playerId, contractId);
                        HandleFailedContract(contract, playerId);
                        expired.Add((playerId, contractId, contract));
                    }
                }
            }

            // Decay долгов
            bool anyDecay = false;
            foreach (var d in _playerDebts.Values)
            {
                float before = d.CurrentDebt;
                d.CheckAndApplyDecay(now);
                if (d.CurrentDebt != before) anyDecay = true;
            }

            // Save if anything expired or debts changed
            if (expired.Count > 0 || anyDecay)
                SaveAll();

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
