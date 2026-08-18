using System.Collections.Generic;
using System.Linq;
using ProjectC.Trade.Config;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Repository;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Серверный singleton, держащий всё runtime-состояние контрактной подсистемы:
    ///   • ContractRuntimeStore: ContractsById, LocationOffers, ActiveByPlayer, TerminalHistory
    ///   • Словарь долгов игроков (playerId → ContractDebt)
    ///   • Validated ContractCatalog с locations, distances и contract type definitions
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
        private readonly ContractRuntimeStore _runtimeStore = new ContractRuntimeStore();
        private readonly Dictionary<ulong, ContractDebt> _playerDebts = new Dictionary<ulong, ContractDebt>();

        // Immutable catalog reference for locations, route distances and contract types.
        private readonly ContractCatalog _catalog;
        private readonly bool _ownsCatalog;

        // Кэш базовой цены по itemId (для расчёта reward в CreateConfigured).
        // Заполняется из TradeItemDefinition через Resolver.
        // Используется ContractData.CreateConfigured.
        private readonly Dictionary<string, float> _itemBasePrice = new Dictionary<string, float>();

        public IReadOnlyDictionary<string, ContractData> AvailableContracts => _runtimeStore.ContractsById;
        public IReadOnlyDictionary<ulong, List<string>> PlayerContracts => _runtimeStore.ActiveByPlayer;
        public IReadOnlyDictionary<ulong, ContractDebt> PlayerDebts => _playerDebts;
        public ContractCatalog Catalog => _catalog;

        public bool IsInitialized { get; private set; }

        // Не перезаписываем сохранение после corruption/future-schema rejection.
        private bool _persistenceWriteBlocked;

        // === Tunables ===
        [Header("Tunables")]
        public int MaxActiveContractsPerPlayer = 3;
        public bool AutoRegenerateContracts = true;
        public bool AutoInitContracts = true;

        /// <summary>
        /// Максимальное количество Completed/Failed records на одного игрока.
        /// Active/Pending records никогда не удаляются этой политикой.
        /// </summary>
        public int MaxTerminalRecordsPerPlayer = 50;


        // ========================================================
        // INITIALIZATION
        // ========================================================

        private ContractWorld(ContractCatalog catalog, bool ownsCatalog)
        {
            _catalog = catalog;
            _ownsCatalog = ownsCatalog;
        }

        public static ContractWorld CreateAndInitialize(
            IPlayerDataRepository repository,
            ContractWorldItemResolver resolver,
            bool autoInitContracts = true,
            ContractCatalog catalog = null)
        {
            bool ownsCatalog = catalog == null;
            var resolvedCatalog = catalog ?? ContractCatalog.CreateDefaultRuntime();
            if (!resolvedCatalog.Validate(out var errors))
            {
                Debug.LogError($"[ContractWorld] ContractCatalog is invalid: {string.Join("; ", errors)}");
                if (ownsCatalog) Object.Destroy(resolvedCatalog);
                throw new System.InvalidOperationException("ContractCatalog validation failed");
            }

            var w = new ContractWorld(resolvedCatalog, ownsCatalog);
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
            Debug.Log($"[ContractWorld] инициализирован: items={_itemBasePrice.Count}, contracts={_runtimeStore.ContractsById.Count}, loadStatus={loadStatus}");
        }

        public void Shutdown()
        {
            SaveAll();

            _runtimeStore.Clear();
            _playerDebts.Clear();
            _itemBasePrice.Clear();
            _persistenceWriteBlocked = false;
            IsInitialized = false;
            if (_ownsCatalog && _catalog != null) Object.Destroy(_catalog);
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
        public bool SaveAll()
        {
            return RepositoryTransactionScope.Execute(Repository, SaveAllCore);
        }

        private bool SaveAllCore()
        {
            if (Repository == null || _persistenceWriteBlocked) return false;

            _runtimeStore.RebuildTerminalHistory();
            var data = new ContractSaveData();

            // Contracts
            data.contracts.AddRange(_runtimeStore.ContractsById.Values);

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
            foreach (var kvp in _runtimeStore.ActiveByPlayer)
            {
                data.playerContracts.Add(new PlayerContractEntry
                {
                    playerId = kvp.Key,
                    contractIds = new List<string>(kvp.Value)
                });
            }

            // Location → contract IDs
            foreach (var kvp in _runtimeStore.LocationOffers)
            {
                data.locationContracts.Add(new LocationContractEntry
                {
                    locationId = kvp.Key,
                    contractIds = new List<string>(kvp.Value)
                });
            }

            if (!Repository.SaveContracts(data)) return false;
            PruneTerminalRecordsAfterSuccessfulPersistence();
            return true;
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

            foreach (var contractId in new List<string>(_runtimeStore.TerminalHistory))
            {
                if (!_runtimeStore.ContractsById.TryGetValue(contractId, out var contract)
                    || contract == null
                    || (contract.state != ContractState.Completed && contract.state != ContractState.Failed))
                {
                    _runtimeStore.TerminalHistory.Remove(contractId);
                    continue;
                }

                if (!terminalByPlayer.TryGetValue(contract.assignedPlayerId, out var records))
                {
                    records = new List<KeyValuePair<string, ContractData>>();
                    terminalByPlayer[contract.assignedPlayerId] = records;
                }
                records.Add(new KeyValuePair<string, ContractData>(contractId, contract));
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
                    if (!_runtimeStore.ContractsById.TryGetValue(contractId, out var contract)) continue;

                    _runtimeStore.RemoveContract(contractId);
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
            _runtimeStore.Clear();
            foreach (var c in data.contracts ?? new List<ContractData>())
            {
                if (c == null || string.IsNullOrEmpty(c.contractId)) continue;

                // Legacy snapshots may contain lowercase or padded location IDs.
                c.fromLocationId = MarketConfigCollector.NormalizeLocationId(c.fromLocationId);
                c.toLocationId = MarketConfigCollector.NormalizeLocationId(c.toLocationId);
                _runtimeStore.ContractsById[c.contractId] = c;
            }

            // Debts — reconstruct ContractDebt objects
            _playerDebts.Clear();
            foreach (var d in data.debts ?? new List<ContractDebtEntry>())
            {
                if (d != null)
                    _playerDebts[d.playerId] = new ContractDebt(d.playerId, d.currentDebt, d.lastDecayTime);
            }

            // Player → contract IDs
            _runtimeStore.ActiveByPlayer.Clear();
            foreach (var e in data.playerContracts ?? new List<PlayerContractEntry>())
            {
                if (e == null) continue;

                var activeIds = new List<string>();
                foreach (var contractId in e.contractIds ?? new List<string>())
                {
                    if (!string.IsNullOrEmpty(contractId) && !activeIds.Contains(contractId))
                        activeIds.Add(contractId);
                }

                _runtimeStore.ActiveByPlayer[e.playerId] = activeIds;
            }

            // Удаляем устаревшие active-index ссылки из старых snapshots.
            PruneAllPlayerContractIndexes();

            // Location → contract IDs
            _runtimeStore.LocationOffers.Clear();
            foreach (var e in data.locationContracts ?? new List<LocationContractEntry>())
            {
                string locationId = MarketConfigCollector.NormalizeLocationId(e.locationId);
                if (string.IsNullOrEmpty(locationId)) continue;

                if (!_runtimeStore.LocationOffers.TryGetValue(locationId, out var contractIds))
                {
                    contractIds = new List<string>();
                    _runtimeStore.LocationOffers[locationId] = contractIds;
                }

                foreach (var contractId in e.contractIds ?? new List<string>())
                {
                    if (string.IsNullOrEmpty(contractId)
                        || !_runtimeStore.ContractsById.TryGetValue(contractId, out var contract)
                        || contract == null
                        || contract.state != ContractState.Pending
                        || contract.fromLocationId != locationId)
                        continue;

                    if (!contractIds.Contains(contractId))
                        contractIds.Add(contractId);
                }
            }

            _runtimeStore.RebuildTerminalHistory();

            Debug.Log($"[ContractWorld] Loaded {_runtimeStore.ContractsById.Count} contracts, {_playerDebts.Count} debts from repository; status={loadStatus}");
            return loadStatus;
        }

        // ========================================================
        // DISTANCE TABLE
        // ========================================================

        public float GetDistance(string fromLocationId, string toLocationId)
        {
            if (_catalog != null
                && _catalog.TryGetDistance(fromLocationId, toLocationId, out var distanceKm)
                && distanceKm > 0f)
            {
                return distanceKm;
            }

            return 100f;
        }

        public bool IsValidLocation(string locationId)
            => _catalog != null && _catalog.HasLocation(locationId);

        // ========================================================
        // ITEMS (для ContractData.CreateConfigured)
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
            if (_catalog == null) return;
            foreach (var locationId in _catalog.GetEnabledLocationIds())
                GenerateContractsForLocation(locationId);
        }

        /// <summary>
        /// Сгенерировать доступные delivery-контракты для локации.
        /// Публикуемые варианты и их параметры берутся из ContractCatalog.
        /// Receipt публикуется только если он явно включён в каталоге.
        /// </summary>
        public void GenerateContractsForLocation(string fromLocationId)
        {
            fromLocationId = MarketConfigCollector.NormalizeLocationId(fromLocationId);
            if (!IsValidLocation(fromLocationId)) return;

            if (!_runtimeStore.LocationOffers.ContainsKey(fromLocationId))
                _runtimeStore.LocationOffers[fromLocationId] = new List<string>();

            // Сбрасываем только текущую доску офферов.
            // Active/Completed/Failed records нельзя удалять во время регенерации:
            // _runtimeStore.ContractsById также является registry для уже принятых контрактов.
            var previousOfferIds = new List<string>(_runtimeStore.LocationOffers[fromLocationId]);
            _runtimeStore.LocationOffers[fromLocationId].Clear();
            foreach (var cid in previousOfferIds)
            {
                if (!_runtimeStore.ContractsById.TryGetValue(cid, out var previousContract))
                    continue;

                if (previousContract.state == ContractState.Pending)
                {
                    _runtimeStore.ContractsById.Remove(cid);
                }
                else
                {
                    Debug.LogWarning($"[ContractWorld] Сохраняю {previousContract.state} contract {cid} при регенерации доски {fromLocationId}");
                }
            }

            var allItemIds = Resolver != null ? Resolver.AllItemIds : new List<string>();
            if (allItemIds == null || allItemIds.Count == 0)
            {
                Debug.LogWarning($"[ContractWorld] GenerateContractsForLocation({fromLocationId}): нет товаров, пропускаю");
                return;
            }

            var destinations = _catalog.GetEnabledLocationIds();
            destinations.Remove(fromLocationId);
            if (destinations.Count == 0)
            {
                Debug.LogWarning($"[ContractWorld] GenerateContractsForLocation({fromLocationId}): нет доступных destinations");
                return;
            }

            string itemId = allItemIds[Random.Range(0, allItemIds.Count)];
            int quantity = Random.Range(2, 8);
            string toLocationId = destinations[Random.Range(0, destinations.Count)];
            float distance = GetDistance(fromLocationId, toLocationId);
            float basePrice = GetItemBasePrice(itemId);

            foreach (var definition in _catalog.GetPublishableContractTypes())
            {
                if (definition == null)
                    continue;

                float timeLimit = Mathf.Max(0f, definition.timeLimitSeconds);

                var contract = ContractData.CreateConfigured(
                    definition.type,
                    itemId,
                    quantity,
                    fromLocationId,
                    toLocationId,
                    basePrice,
                    distance,
                    0f,
                    definition.rewardMultiplier,
                    timeLimit,
                    definition.isReceiptContract);
                _runtimeStore.AddPendingOffer(fromLocationId, contract);
            }
        }

        // ========================================================
        // QUERIES
        // ========================================================

        public ContractData GetContract(string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return null;
            return _runtimeStore.ContractsById.TryGetValue(contractId, out var c) ? c : null;
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
            if (_runtimeStore.ActiveByPlayer.TryGetValue(clientId, out var l)) return l;
            return new List<string>();
        }

        public int GetPlayerActiveCount(ulong clientId)
        {
            if (!_runtimeStore.ActiveByPlayer.TryGetValue(clientId, out var ids)) return 0;

            int activeCount = 0;
            foreach (var contractId in ids)
            {
                if (_runtimeStore.ContractsById.TryGetValue(contractId, out var contract)
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
            foreach (var playerId in new List<ulong>(_runtimeStore.ActiveByPlayer.Keys))
            {
                PrunePlayerContractIndex(playerId);
            }
        }

        private void PrunePlayerContractIndex(ulong clientId)
        {
            if (!_runtimeStore.ActiveByPlayer.TryGetValue(clientId, out var ids)) return;

            for (int i = ids.Count - 1; i >= 0; i--)
            {
                string contractId = ids[i];
                if (_runtimeStore.ContractsById.TryGetValue(contractId, out var contract)
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


        public ContractData[] GetAvailableForLocation(string locationId)
        {
            locationId = MarketConfigCollector.NormalizeLocationId(locationId);
            if (string.IsNullOrEmpty(locationId)
                || !_runtimeStore.LocationOffers.TryGetValue(locationId, out var ids))
                return new ContractData[0];
            var result = new List<ContractData>();
            foreach (var cid in ids)
            {
                if (_runtimeStore.ContractsById.TryGetValue(cid, out var c)
                    && c.state == ContractState.Pending)
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
                if (_runtimeStore.ContractsById.TryGetValue(cid, out var c) && c.state == ContractState.Active)
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
            return RepositoryTransactionScope.Execute(
                Repository,
                () => TryAcceptCore(clientId, contractId));
        }

private ContractOpResult TryAcceptCore(ulong clientId, string contractId)
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

            // 3. Проверка лимита активных контрактов.
            // Чистим старые snapshots и считаем только реально Active records.
            if (!_runtimeStore.ActiveByPlayer.ContainsKey(clientId))
                _runtimeStore.ActiveByPlayer[clientId] = new List<string>();
            else
                PrunePlayerContractIndex(clientId);

            if (GetPlayerActiveCount(clientId) >= MaxActiveContractsPerPlayer)
                return ContractOpResult.Fail(ContractResultCode.MaxActiveReached,
                    $"Максимум {MaxActiveContractsPerPlayer} активных контрактов!");

            // Receipt принимается как Active без автоматической выдачи:
            // физическая выдача выполняется отдельной атомарной ReceiveCargo-операцией.
            contract.Activate(clientId);
            _runtimeStore.MarkActive(contract, clientId);

            if (!SaveAll())
            {
                // Accept не считается успешным, если Active state нельзя надёжно
                // записать. Возвращаем оффер на исходную доску и не оставляем
                // частично применённый active index.
                contract.state = ContractState.Pending;
                contract.assignedPlayerId = 0;
                contract.timeRemaining = contract.timeLimit;
                contract.terminalAtUtcTicks = 0L;
                _runtimeStore.AddPendingOffer(contract.fromLocationId, contract);
                _persistenceWriteBlocked = true;
                Debug.LogError($"[ContractWorld] accept persistence failed for {contract.contractId}; operation rolled back");
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);
            }

            return ContractOpResult.Ok($"Контракт принят: {contract.GetTypeDisplayName(_catalog)}", contract);
        }

        /// <summary>
        /// Выдать Receipt cargo в конкретный корабль. Операция отдельна от Accept:
        /// сервер проверяет владельца, вместимость и сохраняет contract-owned запись.
        /// </summary>
        public ContractOpResult TryReceiveReceiptCargo(
            ulong clientId,
            string contractId,
            ulong shipNetworkObjectId,
            ShipClass shipClass)
        {
            return RepositoryTransactionScope.Execute(
                Repository,
                () => TryReceiveReceiptCargoCore(clientId, contractId, shipNetworkObjectId, shipClass));
        }

        private ContractOpResult TryReceiveReceiptCargoCore(
            ulong clientId,
            string contractId,
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
            if (!contract.isReceiptContract)
                return ContractOpResult.Fail(ContractResultCode.UnsupportedContractType, "Для этого контракта выдача груза не требуется.");
            if (shipNetworkObjectId == 0 || contract.quantity <= 0 || TradeWorld.Instance == null
                || TradeWorld.Instance.Resolver == null || Repository == null)
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);

            // Повторный запрос на тот же корабль идемпотентен. Другой корабль запрещён:
            // contract-owned cargo не переносится между кораблями.
            if (contract.receiptCargoIssuedQuantity > 0)
            {
                if (contract.receiptCargoIssuedQuantity == contract.quantity
                    && contract.receiptCargoShipNetworkObjectId == shipNetworkObjectId
                    && contract.receiptCargoShipClass == shipClass)
                {
                    return ContractOpResult.Ok("Груз по расписке уже выдан.", contract);
                }

                return ContractOpResult.Fail(ContractResultCode.ContractNotActive,
                    "Груз по расписке уже закреплён за другим кораблём.");
            }

            var cargo = TradeWorld.Instance.GetOrLoadCargo(shipNetworkObjectId, shipClass);
            if (cargo == null)
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);

            var cargoBefore = cargo.SaveToList();
            if (!cargo.TryAddContractOwned(
                    contract.itemId,
                    contract.quantity,
                    contract.contractId,
                    clientId,
                    TradeWorld.Instance.Resolver,
                    out var cargoFail))
            {
                return ContractOpResult.Fail(
                    cargoFail == "cargo_max_weight"
                        || cargoFail == "cargo_max_volume"
                        || cargoFail == "cargo_max_slots"
                        ? ContractResultCode.WarehouseFull
                        : ContractResultCode.InternalError,
                    null);
            }

            if (!Repository.SetCargo(shipNetworkObjectId, cargo.SaveToList()))
            {
                cargo.LoadFrom(cargoBefore);
                _persistenceWriteBlocked = true;
                Debug.LogError("[ContractWorld] Receipt cargo persistence failed; issuance rolled back");
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);
            }

            contract.receiptCargoIssuedQuantity = contract.quantity;
            contract.receiptCargoShipNetworkObjectId = shipNetworkObjectId;
            contract.receiptCargoShipClass = shipClass;
            contract.receiptCargoReturnedToReserve = false;

            if (!SaveAll())
            {
                cargo.LoadFrom(cargoBefore);
                Repository.SetCargo(shipNetworkObjectId, cargoBefore);
                contract.receiptCargoIssuedQuantity = 0;
                contract.receiptCargoShipNetworkObjectId = 0;
                contract.receiptCargoShipClass = ShipClass.Light;
                contract.receiptCargoReturnedToReserve = false;
                _persistenceWriteBlocked = true;
                Debug.LogError("[ContractWorld] Receipt issuance snapshot failed; cargo and contract rolled back");
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);
            }

            TradeWorld.Instance.NotifyCargoChanged(shipNetworkObjectId);
            return ContractOpResult.Ok("Груз по расписке выдан в трюм.", contract);
        }

        /// <summary>
        /// Завершить контракт. Идентично legacy ContractSystem.CompleteContractServerRpc:437-556.
        /// Для delivery-контрактов перед выдачей reward сервер атомарно списывает
        /// нужный item из трюма указанного корабля и/или склада в destination.
        /// Receipt-контракты списывают только contract-owned cargo из корабля,
        /// записанного сервером при ReceiveCargo.
        /// </summary>
        public ContractOpResult TryComplete(
            ulong clientId,
            string contractId,
            string completionLocationId,
            ulong shipNetworkObjectId,
            ShipClass shipClass)
        {
            return RepositoryTransactionScope.Execute(
                Repository,
                () => TryCompleteCore(
                    clientId,
                    contractId,
                    completionLocationId,
                    shipNetworkObjectId,
                    shipClass));
        }

        private ContractOpResult TryCompleteCore(
            ulong clientId,
            string contractId,
            string completionLocationId,
            ulong shipNetworkObjectId,
            ShipClass shipClass)
        {
            var contract = GetContract(contractId);
            if (contract == null)
                return ContractOpResult.Fail(ContractResultCode.ContractNotFound, "Контракт не найден!");

            if (contract.state == ContractState.Completed && contract.assignedPlayerId == clientId)
                return ContractOpResult.Ok("Контракт уже завершён.", contract);

            if (contract.state != ContractState.Active)
                return ContractOpResult.Fail(ContractResultCode.ContractNotActive, "Контракт не активен!");

            if (contract.assignedPlayerId != clientId)
                return ContractOpResult.Fail(ContractResultCode.ContractNotAssigned, "Это не ваш контракт!");

            // 3. Проверка таймера
            if (contract.timeLimit > 0f && contract.timeRemaining <= 0f)
            {
                int receiptIssuedBefore = contract.receiptCargoIssuedQuantity;
                ulong receiptShipBefore = contract.receiptCargoShipNetworkObjectId;
                ShipClass receiptClassBefore = contract.receiptCargoShipClass;
                bool receiptReturnedBefore = contract.receiptCargoReturnedToReserve;
                float timeRemainingBefore = contract.timeRemaining;
                float expiryCreditsBefore = Repository != null ? Repository.GetCredits(clientId) : 0f;
                var debt = GetOrCreateDebt(clientId);
                float debtBefore = debt.CurrentDebt;
                List<WarehouseEntry> receiptCargoBefore = null;
                if (contract.isReceiptContract && receiptIssuedBefore > 0 && TradeWorld.Instance != null)
                {
                    receiptCargoBefore = TradeWorld.Instance.GetCargoSnapshot(
                        receiptShipBefore,
                        receiptClassBefore);
                }

                if (contract.isReceiptContract && !TryReturnReceiptCargoToReserve(contract))
                    return ContractOpResult.Fail(ContractResultCode.InternalError, null);

                contract.Fail();
                _runtimeStore.MarkTerminal(contract);
                HandleFailedContract(contract, clientId, receiptIssuedBefore);
                if (!SaveAll())
                {
                    contract.state = ContractState.Active;
                    contract.assignedPlayerId = clientId;
                    contract.timeRemaining = timeRemainingBefore;
                    contract.terminalAtUtcTicks = 0L;
                    contract.receiptCargoIssuedQuantity = receiptIssuedBefore;
                    contract.receiptCargoShipNetworkObjectId = receiptShipBefore;
                    contract.receiptCargoShipClass = receiptClassBefore;
                    contract.receiptCargoReturnedToReserve = receiptReturnedBefore;
                    _runtimeStore.MarkActiveAgain(contract, clientId);
                    debt.CurrentDebt = debtBefore;
                    if (Repository != null && !Repository.SetCredits(clientId, expiryCreditsBefore))
                        Debug.LogError("[ContractWorld] timer-expiry rollback failed for credits");

                    if (receiptCargoBefore != null && TradeWorld.Instance != null && receiptShipBefore != 0)
                    {
                        var cargo = TradeWorld.Instance.GetOrLoadCargo(receiptShipBefore, receiptClassBefore);
                        if (cargo != null)
                        {
                            cargo.LoadFrom(receiptCargoBefore);
                            if (!Repository.SetCargo(receiptShipBefore, receiptCargoBefore))
                                Debug.LogError("[ContractWorld] timer-expiry rollback failed for receipt cargo");
                            TradeWorld.Instance.NotifyCargoChanged(receiptShipBefore);
                        }
                    }

                    _persistenceWriteBlocked = true;
                    Debug.LogError($"[ContractWorld] timer-expiry persistence failed for {contract.contractId}; state rolled back");
                    return ContractOpResult.Fail(ContractResultCode.InternalError, null);
                }
                return ContractOpResult.Fail(ContractResultCode.TimerExpired, "Время контракта истекло!");
            }

            // 4. Проверка локации
            if (completionLocationId != contract.toLocationId)
                return ContractOpResult.Fail(ContractResultCode.WrongDestination,
                    $"Вы не в целевой локации! Нужно: {contract.toLocationId}");

            // Receipt settlement consumes the exact contract-owned cargo entry.
            if (contract.isReceiptContract)
            {
                return TryCompleteReceiptCore(
                    clientId,
                    contract,
                    shipNetworkObjectId,
                    shipClass);
            }

            // 6. Delivery-контракт требует доказанного server-side списания.
            if (TradeWorld.Instance == null)
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);

            var tradeWorld = TradeWorld.Instance;
            var cargoBefore = shipNetworkObjectId != 0
                ? tradeWorld.GetCargoSnapshot(shipNetworkObjectId, shipClass)
                : null;
            var warehouseBefore = tradeWorld.GetWarehouseSnapshot(clientId, contract.toLocationId);
            float creditsBefore = Repository != null ? Repository.GetCredits(clientId) : 0f;

            if (!tradeWorld.TryConsumeDeliveryCargo(
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
            _runtimeStore.MarkTerminal(contract);

            // Начисляем награду. При ошибке записи возвращаем cargo/warehouse
            // к снимку до completion и не переводим контракт в успешный результат.
            if (Repository == null || !Repository.SetCredits(clientId, creditsBefore + contract.reward))
            {
                contract.state = ContractState.Active;
                contract.terminalAtUtcTicks = 0L;
                _runtimeStore.MarkActiveAgain(contract, clientId);
                RestoreDeliveryState(
                    clientId,
                    contract.toLocationId,
                    shipNetworkObjectId,
                    shipClass,
                    cargoBefore,
                    warehouseBefore,
                    creditsBefore);
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);
            }

            if (!SaveAll())
            {
                contract.state = ContractState.Active;
                contract.terminalAtUtcTicks = 0L;
                _runtimeStore.MarkActiveAgain(contract, clientId);
                RestoreDeliveryState(
                    clientId,
                    contract.toLocationId,
                    shipNetworkObjectId,
                    shipClass,
                    cargoBefore,
                    warehouseBefore,
                    creditsBefore);
                _persistenceWriteBlocked = true;
                Debug.LogError("[ContractWorld] completion persistence failed; state rolled back and further writes blocked");
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);
            }

            return ContractOpResult.Ok($"Контракт завершён! Награда: {contract.reward:F0} CR", contract, contract.reward);
        }

        private ContractOpResult TryCompleteReceiptCore(
            ulong clientId,
            ContractData contract,
            ulong shipNetworkObjectId,
            ShipClass shipClass)
        {
            if (contract.receiptCargoIssuedQuantity != contract.quantity
                || contract.receiptCargoShipNetworkObjectId == 0
                || contract.receiptCargoShipNetworkObjectId != shipNetworkObjectId
                || contract.receiptCargoShipClass != shipClass)
            {
                return ContractOpResult.Fail(ContractResultCode.CargoMissing, null);
            }

            if (TradeWorld.Instance == null || TradeWorld.Instance.Resolver == null || Repository == null)
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);

            var cargo = TradeWorld.Instance.GetOrLoadCargo(
                contract.receiptCargoShipNetworkObjectId,
                contract.receiptCargoShipClass);
            if (cargo == null)
                return ContractOpResult.Fail(ContractResultCode.CargoMissing, null);

            int ownedQuantity = cargo.GetContractOwnedQuantity(
                contract.itemId,
                contract.contractId,
                clientId);
            if (ownedQuantity != contract.quantity)
                return ContractOpResult.Fail(ContractResultCode.CargoMissing, null);

            var cargoBefore = cargo.SaveToList();
            if (!cargo.TryRemoveContractOwned(
                    contract.itemId,
                    contract.quantity,
                    contract.contractId,
                    clientId,
                    out _))
            {
                return ContractOpResult.Fail(ContractResultCode.CargoMissing, null);
            }

            if (!Repository.SetCargo(contract.receiptCargoShipNetworkObjectId, cargo.SaveToList()))
            {
                cargo.LoadFrom(cargoBefore);
                _persistenceWriteBlocked = true;
                Debug.LogError("[ContractWorld] Receipt settlement cargo persistence failed; settlement aborted");
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);
            }

            int issuedBefore = contract.receiptCargoIssuedQuantity;
            ulong shipBefore = contract.receiptCargoShipNetworkObjectId;
            ShipClass classBefore = contract.receiptCargoShipClass;
            bool returnedBefore = contract.receiptCargoReturnedToReserve;
            float creditsBefore = Repository.GetCredits(clientId);

            contract.receiptCargoIssuedQuantity = 0;
            contract.receiptCargoShipNetworkObjectId = 0;
            contract.receiptCargoShipClass = ShipClass.Light;
            contract.receiptCargoReturnedToReserve = false;
            contract.Complete();
            _runtimeStore.MarkTerminal(contract);

            if (!Repository.SetCredits(clientId, creditsBefore + contract.reward))
            {
                contract.state = ContractState.Active;
                contract.terminalAtUtcTicks = 0L;
                contract.receiptCargoIssuedQuantity = issuedBefore;
                contract.receiptCargoShipNetworkObjectId = shipBefore;
                contract.receiptCargoShipClass = classBefore;
                contract.receiptCargoReturnedToReserve = returnedBefore;
                _runtimeStore.MarkActiveAgain(contract, clientId);
                cargo.LoadFrom(cargoBefore);
                Repository.SetCargo(shipBefore, cargoBefore);
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);
            }

            if (!SaveAll())
            {
                contract.state = ContractState.Active;
                contract.terminalAtUtcTicks = 0L;
                contract.receiptCargoIssuedQuantity = issuedBefore;
                contract.receiptCargoShipNetworkObjectId = shipBefore;
                contract.receiptCargoShipClass = classBefore;
                contract.receiptCargoReturnedToReserve = returnedBefore;
                _runtimeStore.MarkActiveAgain(contract, clientId);
                Repository.SetCredits(clientId, creditsBefore);
                cargo.LoadFrom(cargoBefore);
                Repository.SetCargo(shipBefore, cargoBefore);
                _persistenceWriteBlocked = true;
                Debug.LogError("[ContractWorld] Receipt settlement snapshot failed; state rolled back and further writes blocked");
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);
            }

            TradeWorld.Instance.NotifyCargoChanged(shipBefore);
            return ContractOpResult.Ok($"Контракт завершён! Награда: {contract.reward:F0} CR", contract, contract.reward);
        }

        private bool TryReturnReceiptCargoToReserve(ContractData contract)
        {
            if (contract == null || !contract.isReceiptContract) return true;
            if (contract.receiptCargoIssuedQuantity <= 0)
            {
                contract.receiptCargoReturnedToReserve = true;
                contract.receiptCargoShipNetworkObjectId = 0;
                contract.receiptCargoShipClass = ShipClass.Light;
                return true;
            }

            if (TradeWorld.Instance == null || Repository == null) return false;
            var cargo = TradeWorld.Instance.GetOrLoadCargo(
                contract.receiptCargoShipNetworkObjectId,
                contract.receiptCargoShipClass);
            if (cargo == null) return false;

            int ownedQuantity = cargo.GetContractOwnedQuantity(
                contract.itemId,
                contract.contractId,
                contract.assignedPlayerId);
            if (ownedQuantity != contract.receiptCargoIssuedQuantity) return false;

            var cargoBefore = cargo.SaveToList();
            if (!cargo.TryRemoveContractOwned(
                    contract.itemId,
                    contract.receiptCargoIssuedQuantity,
                    contract.contractId,
                    contract.assignedPlayerId,
                    out _))
            {
                return false;
            }

            if (!Repository.SetCargo(contract.receiptCargoShipNetworkObjectId, cargo.SaveToList()))
            {
                cargo.LoadFrom(cargoBefore);
                return false;
            }

            ulong shipId = contract.receiptCargoShipNetworkObjectId;
            contract.receiptCargoIssuedQuantity = 0;
            contract.receiptCargoShipNetworkObjectId = 0;
            contract.receiptCargoShipClass = ShipClass.Light;
            contract.receiptCargoReturnedToReserve = true;
            TradeWorld.Instance.NotifyCargoChanged(shipId);
            return true;
        }

        private void RestoreDeliveryState(
            ulong clientId,
            string locationId,
            ulong shipNetworkObjectId,
            ShipClass shipClass,
            List<WarehouseEntry> cargoBefore,
            List<WarehouseEntry> warehouseBefore,
            float expiryCreditsBefore)
        {
            if (Repository == null) return;

            if (!Repository.SetCredits(clientId, expiryCreditsBefore))
                Debug.LogError("[ContractWorld] completion rollback failed for credits");

            if (cargoBefore != null)
            {
                var cargo = TradeWorld.Instance.GetOrLoadCargo(shipNetworkObjectId, shipClass);
                cargo.LoadFrom(cargoBefore);
                if (!Repository.SetCargo(shipNetworkObjectId, cargoBefore))
                    Debug.LogError("[ContractWorld] completion rollback failed for cargo");
                TradeWorld.Instance.NotifyCargoChanged(shipNetworkObjectId);
            }

            var warehouse = TradeWorld.Instance.GetOrLoadWarehouse(clientId, locationId);
            warehouse.LoadFrom(warehouseBefore);
            if (!Repository.SetWarehouse(clientId, locationId, warehouseBefore))
                Debug.LogError("[ContractWorld] completion rollback failed for warehouse");
        }

        /// <summary>Провалить контракт (отмена игрока или авто-fail по таймеру).</summary>
        public ContractOpResult TryFail(ulong clientId, string contractId, bool isManual)
        {
            return RepositoryTransactionScope.Execute(
                Repository,
                () => TryFailCore(clientId, contractId, isManual));
        }

        private ContractOpResult TryFailCore(ulong clientId, string contractId, bool isManual)
        {
            var contract = GetContract(contractId);
            if (contract == null)
                return ContractOpResult.Fail(ContractResultCode.ContractNotFound, "Контракт не найден!");

            if (contract.state != ContractState.Active)
                return ContractOpResult.Fail(ContractResultCode.ContractNotActive, "Контракт не активен!");

            if (contract.assignedPlayerId != clientId)
                return ContractOpResult.Fail(ContractResultCode.ContractNotAssigned, "Это не ваш контракт!");

            int receiptIssuedBefore = contract.receiptCargoIssuedQuantity;
            ulong receiptShipBefore = contract.receiptCargoShipNetworkObjectId;
            ShipClass receiptClassBefore = contract.receiptCargoShipClass;
            bool receiptReturnedBefore = contract.receiptCargoReturnedToReserve;
            List<WarehouseEntry> receiptCargoBefore = null;
            if (contract.isReceiptContract && receiptIssuedBefore > 0 && TradeWorld.Instance != null)
            {
                receiptCargoBefore = TradeWorld.Instance.GetCargoSnapshot(
                    contract.receiptCargoShipNetworkObjectId,
                    contract.receiptCargoShipClass);
            }

            var debt = GetOrCreateDebt(clientId);
            float debtBefore = debt.CurrentDebt;
            if (contract.isReceiptContract && !TryReturnReceiptCargoToReserve(contract))
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);

            contract.Fail();
            _runtimeStore.MarkTerminal(contract);
            HandleFailedContract(contract, clientId, receiptIssuedBefore);

            if (!SaveAll())
            {
                contract.state = ContractState.Active;
                contract.terminalAtUtcTicks = 0L;
                contract.receiptCargoIssuedQuantity = receiptIssuedBefore;
                contract.receiptCargoShipNetworkObjectId = receiptShipBefore;
                contract.receiptCargoShipClass = receiptClassBefore;
                contract.receiptCargoReturnedToReserve = receiptReturnedBefore;
                _runtimeStore.MarkActiveAgain(contract, clientId);
                debt.CurrentDebt = debtBefore;

                if (receiptCargoBefore != null && TradeWorld.Instance != null && receiptShipBefore != 0)
                {
                    var cargo = TradeWorld.Instance.GetOrLoadCargo(receiptShipBefore, receiptClassBefore);
                    if (cargo != null)
                    {
                        cargo.LoadFrom(receiptCargoBefore);
                        if (!Repository.SetCargo(receiptShipBefore, receiptCargoBefore))
                            Debug.LogError("[ContractWorld] failure rollback failed for receipt cargo");
                        TradeWorld.Instance.NotifyCargoChanged(receiptShipBefore);
                    }
                }

                _persistenceWriteBlocked = true;
                Debug.LogError("[ContractWorld] failure persistence failed; contract state rolled back and further writes blocked");
                return ContractOpResult.Fail(ContractResultCode.InternalError, null);
            }

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
        public void HandleFailedContract(ContractData contract, ulong playerId, int receiptCargoIssuedQuantity = -1)
        {
            var debt = GetOrCreateDebt(playerId);

            if (contract.isReceiptContract)
            {
                // Долг возникает только если сервер действительно выдал cargo.
                int issuedQuantity = receiptCargoIssuedQuantity >= 0
                    ? receiptCargoIssuedQuantity
                    : contract.receiptCargoIssuedQuantity;
                if (issuedQuantity <= 0) return;

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
            return RepositoryTransactionScope.Execute(
                Repository,
                () => TickCore(deltaTime, now));
        }

        private List<(ulong playerId, string contractId, ContractData contract)> TickCore(float deltaTime, float now)
        {
            var expired = new List<(ulong, string, ContractData)>();
            var activePlayerIds = new List<ulong>(_runtimeStore.ActiveByPlayer.Keys);
            var contractSnapshots = new List<(
                ulong playerId,
                string contractId,
                ContractData contract,
                ContractState state,
                ulong assignedPlayerId,
                float timeRemaining,
                long terminalAtUtcTicks,
                int receiptIssuedQuantity,
                ulong receiptShipNetworkObjectId,
                ShipClass receiptShipClass,
                bool receiptReturnedToReserve,
                List<WarehouseEntry> cargo)>();
            var debtSnapshots = new List<(ulong playerId, float currentDebt, float lastDecayTime)>();
            var debtKeysBefore = new HashSet<ulong>(_playerDebts.Keys);
            var creditsBefore = new Dictionary<ulong, float>();

            foreach (var playerId in activePlayerIds)
            {
                if (Repository != null)
                    creditsBefore[playerId] = Repository.GetCredits(playerId);
            }

            foreach (var debtPair in _playerDebts)
            {
                debtSnapshots.Add((
                    debtPair.Key,
                    debtPair.Value.CurrentDebt,
                    debtPair.Value.LastDecayTime));
            }

            System.Action rollback = () =>
            {
                foreach (var snapshot in contractSnapshots)
                {
                    snapshot.contract.state = snapshot.state;
                    snapshot.contract.assignedPlayerId = snapshot.assignedPlayerId;
                    snapshot.contract.timeRemaining = snapshot.timeRemaining;
                    snapshot.contract.terminalAtUtcTicks = snapshot.terminalAtUtcTicks;
                    snapshot.contract.receiptCargoIssuedQuantity = snapshot.receiptIssuedQuantity;
                    snapshot.contract.receiptCargoShipNetworkObjectId = snapshot.receiptShipNetworkObjectId;
                    snapshot.contract.receiptCargoShipClass = snapshot.receiptShipClass;
                    snapshot.contract.receiptCargoReturnedToReserve = snapshot.receiptReturnedToReserve;
                    _runtimeStore.ContractsById[snapshot.contractId] = snapshot.contract;
                    _runtimeStore.MarkActiveAgain(snapshot.contract, snapshot.playerId);

                    if (snapshot.cargo == null
                        || TradeWorld.Instance == null
                        || snapshot.receiptShipNetworkObjectId == 0)
                    {
                        continue;
                    }

                    var cargo = TradeWorld.Instance.GetOrLoadCargo(
                        snapshot.receiptShipNetworkObjectId,
                        snapshot.receiptShipClass);
                    if (cargo == null) continue;

                    cargo.LoadFrom(snapshot.cargo);
                    if (Repository != null
                        && !Repository.SetCargo(snapshot.receiptShipNetworkObjectId, snapshot.cargo))
                    {
                        Debug.LogError($"[ContractWorld] Tick rollback failed for cargo of {snapshot.contractId}");
                    }
                    TradeWorld.Instance.NotifyCargoChanged(snapshot.receiptShipNetworkObjectId);
                }

                foreach (var debtSnapshot in debtSnapshots)
                {
                    if (!_playerDebts.TryGetValue(debtSnapshot.playerId, out var debt))
                    {
                        debt = new ContractDebt(
                            debtSnapshot.playerId,
                            debtSnapshot.currentDebt,
                            debtSnapshot.lastDecayTime);
                        _playerDebts[debtSnapshot.playerId] = debt;
                    }

                    debt.CurrentDebt = debtSnapshot.currentDebt;
                    debt.LastDecayTime = debtSnapshot.lastDecayTime;
                }

                foreach (var playerId in new List<ulong>(_playerDebts.Keys))
                {
                    if (!debtKeysBefore.Contains(playerId))
                        _playerDebts.Remove(playerId);
                }

                if (Repository != null)
                {
                    foreach (var creditsPair in creditsBefore)
                    {
                        if (!Repository.SetCredits(creditsPair.Key, creditsPair.Value))
                        {
                            Debug.LogError($"[ContractWorld] Tick rollback failed for credits of player {creditsPair.Key}");
                        }
                    }
                }

                expired.Clear();
            };

            // Snapshot every active contract before ticking. Runtime indexes can be
            // mutated by MarkTerminal, so iterate stable copies and restore the whole
            // batch if any persistence step fails.
            foreach (var playerId in activePlayerIds)
            {
                if (!_runtimeStore.ActiveByPlayer.TryGetValue(playerId, out var activeIds))
                    continue;

                var idsCopy = new List<string>(activeIds);
                foreach (var contractId in idsCopy)
                {
                    if (!_runtimeStore.ContractsById.TryGetValue(contractId, out var contract))
                        continue;
                    if (contract == null || contract.state != ContractState.Active)
                        continue;

                    int receiptIssuedBefore = contract.receiptCargoIssuedQuantity;
                    ulong receiptShipBefore = contract.receiptCargoShipNetworkObjectId;
                    ShipClass receiptClassBefore = contract.receiptCargoShipClass;
                    List<WarehouseEntry> receiptCargoBefore = null;
                    if (contract.isReceiptContract
                        && receiptIssuedBefore > 0
                        && TradeWorld.Instance != null
                        && receiptShipBefore != 0)
                    {
                        receiptCargoBefore = TradeWorld.Instance.GetCargoSnapshot(
                            receiptShipBefore,
                            receiptClassBefore);
                    }

                    contractSnapshots.Add((
                        playerId,
                        contractId,
                        contract,
                        contract.state,
                        contract.assignedPlayerId,
                        contract.timeRemaining,
                        contract.terminalAtUtcTicks,
                        receiptIssuedBefore,
                        receiptShipBefore,
                        receiptClassBefore,
                        contract.receiptCargoReturnedToReserve,
                        receiptCargoBefore));

                    contract.TickTimer(deltaTime);
                    _runtimeStore.ContractsById[contractId] = contract;

                    if (contract.state != ContractState.Failed)
                        continue;

                    if (contract.isReceiptContract && !TryReturnReceiptCargoToReserve(contract))
                    {
                        rollback();
                        _persistenceWriteBlocked = true;
                        Debug.LogError($"[ContractWorld] expired Receipt contract {contractId} could not return cargo; tick rolled back");
                        return expired;
                    }

                    _runtimeStore.MarkTerminal(contract);
                    HandleFailedContract(contract, playerId, receiptIssuedBefore);
                    expired.Add((playerId, contractId, contract));
                }
            }

            // Decay долгов. LastDecayTime is part of the persisted state, so a
            // change there also requires SaveAll even when CurrentDebt stays at zero.
            bool anyDebtStateChanged = false;
            foreach (var debtPair in _playerDebts)
            {
                var debt = debtPair.Value;
                float beforeDebt = debt.CurrentDebt;
                float beforeDecayTime = debt.LastDecayTime;
                debt.CheckAndApplyDecay(now);
                if (debt.CurrentDebt != beforeDebt || debt.LastDecayTime != beforeDecayTime)
                    anyDebtStateChanged = true;
            }

            if (expired.Count > 0 || anyDebtStateChanged)
            {
                if (!SaveAll())
                {
                    rollback();
                    _persistenceWriteBlocked = true;
                    Debug.LogError("[ContractWorld] tick persistence failed; contract/debt/cargo state rolled back and further writes blocked");
                    return expired;
                }
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
            locationId = MarketConfigCollector.NormalizeLocationId(locationId);
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
            string typeLocalizationKey = null;
            string typeUiClass = null;
            if (_catalog != null
                && _catalog.TryGetContractType(c.type, out var typeDefinition)
                && typeDefinition != null)
            {
                typeLocalizationKey = typeDefinition.localizationKey;
                typeUiClass = typeDefinition.uiClass;
            }

            return new ContractDto
            {
                contractId = c.contractId,
                type = (byte)c.type,
                typeLocalizationKey = typeLocalizationKey,
                typeUiClass = typeUiClass,
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
                isReceiptContract = c.isReceiptContract,
                receiptCargoIssued = c.receiptCargoIssuedQuantity > 0,
                receiptCargoShipNetworkObjectId = c.receiptCargoShipNetworkObjectId
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
