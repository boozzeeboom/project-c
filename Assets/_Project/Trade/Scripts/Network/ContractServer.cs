using System.Collections.Generic;
using ProjectC.Core;
using ProjectC.Player;
using ProjectC.Ship.Key;
using ProjectC.Trade.Config;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Repository;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Trade.Network
{
    /// <summary>
    /// Главный сетевой компонент контрактной подсистемы. NetworkBehaviour, ставится
    /// в Bootstrap сцене рядом с <see cref="MarketServer"/>. DontDestroyOnLoad.
    ///
    /// Ответственности:
    ///   • При OnNetworkSpawn (на сервере) — инициализировать <see cref="ContractWorld"/>.
    ///   • Принимать RPC от клиентов (list / accept / complete / fail).
    ///   • Валидировать позицию (игрок в <see cref="ContractZone"/> с нужным locationId).
    ///   • Делегировать операции в <see cref="ContractWorld"/>.
    ///   • Слать обновления (<see cref="ContractSnapshotDto"/> + <see cref="ContractResultDto"/>) клиентам.
    ///   • Тикать таймеры активных контрактов в FixedUpdate; авто-fail при истечении.
    ///
    /// C2-этап миграции контрактов на v2-архитектуру (см. docs/dev/CONTRACT_V2_MIGRATION.md).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ContractServer : NetworkBehaviour
    {
        public static ContractServer Instance { get; private set; }

        [Header("Setup")]
        [Tooltip("База данных TradeItemDefinition'ов (опционально — для автоподключения items к ContractWorld)")]
        [SerializeField] private TradeDatabase tradeDatabase;

        [Tooltip("Каталог locations, distances и contract types. Если не задан, загружается Resources/ContractCatalog.asset.")]
        [SerializeField] private ContractCatalog contractCatalog;

        [Header("Behavior")]
        [Tooltip("Макс активных контрактов на игрока")]
        [SerializeField] private int maxActiveContractsPerPlayer = 3;

        [Tooltip("Автогенерация новых контрактов когда доска пуста")]
        [SerializeField] private bool autoRegenerateContracts = true;

        [Tooltip("Инициализировать контракты при старте для всех enabled locations из ContractCatalog")]
        [SerializeField] private bool autoInitContracts = true;

        [Header("Rate Limiting")]
        [Tooltip("Макс операций в минуту на клиента (0 = без лимита)")]
        [SerializeField] private int maxOpsPerMinute = 30;

        // === Runtime ===
        private IPlayerDataRepository _repository;
        private ContractWorldItemResolver _resolver;

        // Per-client rate limiting
        private readonly Dictionary<ulong, List<float>> _opTimestamps = new Dictionary<ulong, List<float>>();
        private bool _registrySessionAcquired;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;

            if (!IsServer)
            {
                enabled = false;
                return;
            }

            var resolvedCatalog = ResolveContractCatalog();
            if (resolvedCatalog != null && !resolvedCatalog.Validate(out var catalogErrors))
            {
                Debug.LogError($"[ContractServer] ContractCatalog invalid: {string.Join("; ", catalogErrors)}");
                enabled = false;
                if (Instance == this) Instance = null;
                return;
            }

            MarketZoneRegistry.AcquireServerSession(this);
            _registrySessionAcquired = true;

            // 1. Repository — реюз из TradeWorld если есть, иначе PlayerPrefsRepository
            if (TradeWorld.Instance != null && TradeWorld.Instance.Repository != null)
            {
                _repository = TradeWorld.Instance.Repository;
            }
            else
            {
                _repository = new PlayerPrefsRepository();
            }

            // 2. Resolver — собираем items из TradeDatabase (если есть) ИЛИ дефолтный набор
            _resolver = BuildResolver();

            // 3. ContractWorld
            ContractWorld.CreateAndInitialize(
                _repository,
                _resolver,
                autoInitContracts,
                resolvedCatalog);
            ContractWorld.Instance.MaxActiveContractsPerPlayer = maxActiveContractsPerPlayer;
            ContractWorld.Instance.AutoRegenerateContracts = autoRegenerateContracts;

            Debug.Log($"[ContractServer] инициализирован: items={_resolver.Count}, repository={_repository.GetType().Name}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer)
            {
                if (ContractWorld.Instance != null) ContractWorld.Instance.Shutdown();
                // C2-refactor: ContractZoneRegistry удалён — теперь используется MarketZoneRegistry.
            }
            if (_registrySessionAcquired)
            {
                MarketZoneRegistry.ReleaseServerSession(this);
                _registrySessionAcquired = false;
            }
            if (Instance == this) Instance = null;
        }

        private ContractCatalog ResolveContractCatalog()
        {
            if (contractCatalog != null) return contractCatalog;

            var loaded = Resources.Load<ContractCatalog>("ContractCatalog");
            if (loaded != null) return loaded;

            Debug.LogWarning("[ContractServer] ContractCatalog не найден в инспекторе или Resources. Используется runtime fallback.");
            return null;
        }

        private ContractWorldItemResolver BuildResolver()
        {
            var r = ContractWorldItemResolver.CreateWithDefaults();

            if (tradeDatabase != null && tradeDatabase.allItems != null)
            {
                // Подмешиваем items из TradeItemDatabase (если скрипт-референс проставлен)
                foreach (var item in tradeDatabase.allItems)
                {
                    if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
                    r.AddItem(item.itemId, item.displayName, item.basePrice);
                }
            }
            return r;
        }

        // ========================================================
        // CLIENT → SERVER RPCs
        // ========================================================

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestListRpc(string locationId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (!ValidateInZone(clientId, locationId, out var zone))
            {
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.NotInZone, "", 0, 0, clientId));
                return;
            }
            if (ContractWorld.Instance == null) return;

            // Авто-регенерация если пусто
            if (ContractWorld.Instance.AutoRegenerateContracts)
            {
                var available = ContractWorld.Instance.GetAvailableForLocation(locationId);
                if (available == null || available.Length == 0)
                {
                    ContractWorld.Instance.GenerateContractsForLocation(locationId);
                }
            }

            var snapshot = ContractWorld.Instance.BuildSnapshot(clientId, locationId, zone.DisplayName, 1f, 0f);
            SendSnapshotToClient(clientId, snapshot);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestAcceptRpc(string contractId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (ContractWorld.Instance == null) return;

            // Найти локацию контракта
            var contract = ContractWorld.Instance.GetContract(contractId);
            if (contract == null)
            {
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.ContractNotFound, contractId, 0, 0, clientId));
                return;
            }
            if (!ValidateInZone(clientId, contract.fromLocationId, out var zone))
            {
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.NotInZone, contractId, 0, 0, clientId));
                return;
            }

            var r = ContractWorld.Instance.TryAccept(clientId, contractId);
            var dto = BuildResultDto(clientId, r, contractId);
            SendResultToOwner(clientId, dto);

            // T-X5/T-Q15: publish ContractAcceptedEvent → ContractMetaBridge.
            if (r.IsSuccess)
            {
                WorldEventBus.Publish(new ContractAcceptedEvent
                {
                    PlayerId = clientId,
                    TimestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ContractId = contractId,
                    FromNpcId = contract.fromLocationId
                });

                // Re-snapshot чтобы UI увидел обновлённый active[]
                var snap = ContractWorld.Instance.BuildSnapshot(clientId, contract.fromLocationId, zone.DisplayName, 1f, 0f);
                SendSnapshotToClient(clientId, snap);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestReceiveCargoRpc(string contractId, ulong shipNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (ContractWorld.Instance == null) return;

            var contract = ContractWorld.Instance.GetContract(contractId);
            if (contract == null)
            {
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.ContractNotFound, contractId, 0, 0, clientId));
                return;
            }

            if (!contract.isReceiptContract)
            {
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.UnsupportedContractType, contractId, 0, 0, clientId));
                return;
            }

            if (!ValidateInZone(clientId, contract.fromLocationId, out var zone))
            {
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.NotInZone, contractId, 0, 0, clientId));
                return;
            }

            if (shipNetworkObjectId == 0
                || !zone.IsShipInZone(shipNetworkObjectId)
                || !KeyRodInstanceWorld.IsOwnerOfShip(clientId, shipNetworkObjectId))
            {
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.CargoMissing, contractId, 0, 0, clientId));
                return;
            }

            var shipClass = ResolveShipClass(shipNetworkObjectId);
            var r = ContractWorld.Instance.TryReceiveReceiptCargo(
                clientId,
                contractId,
                shipNetworkObjectId,
                shipClass);
            SendResultToOwner(clientId, BuildResultDto(clientId, r, contractId));

            if (r.IsSuccess)
            {
                var snap = ContractWorld.Instance.BuildSnapshot(
                    clientId,
                    contract.fromLocationId,
                    zone.DisplayName,
                    1f,
                    0f);
                SendSnapshotToClient(clientId, snap);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestCompleteRpc(string contractId, ulong shipNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (ContractWorld.Instance == null) return;

            var contract = ContractWorld.Instance.GetContract(contractId);
            if (contract == null)
            {
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.ContractNotFound, contractId, 0, 0, clientId));
                return;
            }
            // Валидация позиции: игрок должен быть в toLocationId.
            if (!ValidateInZone(clientId, contract.toLocationId, out var zone))
            {
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.WrongDestination, contractId, 0, 0, clientId));
                return;
            }

            // Для Receipt и delivery контрактов сервер проверяет ship ownership.
            // Receipt cargo никогда не ищется в warehouse и должен оставаться
            // в том же корабле, который был записан при ReceiveCargo.
            if (contract.isReceiptContract && shipNetworkObjectId != 0)
            {
                if (!zone.IsShipInZone(shipNetworkObjectId)
                    || !KeyRodInstanceWorld.IsOwnerOfShip(clientId, shipNetworkObjectId))
                {
                    SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.CargoMissing, contractId, 0, 0, clientId));
                    return;
                }
            }

            // Для delivery-контрактов shipNetworkObjectId — только hint источника
            // груза. Сервер сам проверяет, что корабль находится в destination zone
            // и принадлежит отправителю. 0 разрешён: тогда TradeWorld проверит
            // только destination warehouse (товар мог быть заранее разгружен).
            if (!contract.isReceiptContract && shipNetworkObjectId != 0)
            {
                if (!zone.IsShipInZone(shipNetworkObjectId))
                {
                    SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.CargoMissing, contractId, 0, 0, clientId));
                    return;
                }

                if (!KeyRodInstanceWorld.IsOwnerOfShip(clientId, shipNetworkObjectId))
                {
                    SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.CargoMissing, contractId, 0, 0, clientId));
                    return;
                }
            }

            var shipClass = ResolveShipClass(shipNetworkObjectId);
            var r = ContractWorld.Instance.TryComplete(
                clientId,
                contractId,
                contract.toLocationId,
                shipNetworkObjectId,
                shipClass);
            var dto = BuildResultDto(clientId, r, contractId);
            SendResultToOwner(clientId, dto);

            // T-X5/T-Q15: publish ContractCompletedEvent → ContractMetaBridge.
            if (r.IsSuccess)
            {
                WorldEventBus.Publish(new ContractCompletedEvent
                {
                    PlayerId = clientId,
                    TimestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ContractId = contractId,
                    WasReceipt = contract.isReceiptContract
                });

                var snapshotZone = MarketZoneRegistry.Get(contract.toLocationId);
                var snap = ContractWorld.Instance.BuildSnapshot(clientId, contract.toLocationId,
                    snapshotZone != null ? snapshotZone.DisplayName : contract.toLocationId, 1f, 0f);
                SendSnapshotToClient(clientId, snap);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestFailRpc(string contractId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (ContractWorld.Instance == null) return;

            var r = ContractWorld.Instance.TryFail(clientId, contractId, isManual: true);
            var dto = BuildResultDto(clientId, r, contractId);
            SendResultToOwner(clientId, dto);

            // T-X5/T-Q15: publish ContractFailedEvent → ContractMetaBridge.
            if (r.IsSuccess)
            {
                WorldEventBus.Publish(new ContractFailedEvent
                {
                    PlayerId = clientId,
                    TimestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ContractId = contractId,
                    DebtIncurred = false // T-Q15: full debt instrumentation — out of scope.
                });

                // Re-snapshot с локации, на которой был контракт
                if (r.Contract != null)
                {
                    var zone = MarketZoneRegistry.Get(r.Contract.fromLocationId);
                    var snap = ContractWorld.Instance.BuildSnapshot(clientId, r.Contract.fromLocationId,
                        zone != null ? zone.DisplayName : r.Contract.fromLocationId, 1f, 0f);
                    SendSnapshotToClient(clientId, snap);
                }
            }
        }

        // ========================================================
        // SERVER → CLIENT RPCs
        // ========================================================


        // ========================================================
        // TICK
        // ========================================================

        private void FixedUpdate()
        {
            if (!IsServer) return;
            if (ContractWorld.Instance == null) return;

            var expired = ContractWorld.Instance.Tick(Time.fixedDeltaTime, Time.realtimeSinceStartup);
            foreach (var (playerId, contractId, contract) in expired)
            {
                // Шлём result клиенту (auto-fail по таймеру)
                var dto = new ContractResultDto
                {
                    code = (byte)ContractResultCode.TimerExpired,
                    contractId = contractId,
                    success = false,
                    message = $"Контракт {contractId} провален: время истекло!",
                    reward = 0f,
                    newCredits = _repository != null ? _repository.GetCredits(playerId) : 0f,
                    newDebt = ContractWorld.Instance.GetOrCreateDebt(playerId).CurrentDebt,
                    updatedContract = ContractWorld.Instance.ToDto(contract)
                };
                SendResultToOwner(playerId, dto);

                // T-X5/T-Q15: publish ContractFailedEvent для auto-fail by timer.
                WorldEventBus.Publish(new ContractFailedEvent
                {
                    PlayerId = playerId,
                    TimestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ContractId = contractId,
                    DebtIncurred = ContractWorld.Instance.GetOrCreateDebt(playerId).CurrentDebt > 0
                });

                // Re-snapshot с локации отправления (где была доска)
                var zone = MarketZoneRegistry.Get(contract.fromLocationId);
                if (zone != null)
                {
                    var snap = ContractWorld.Instance.BuildSnapshot(playerId, contract.fromLocationId,
                        zone.DisplayName, 1f, 0f);
                    SendSnapshotToClient(playerId, snap);
                }
            }
        }

        // ========================================================
        // SEND HELPERS
        // ========================================================

        /// <summary>
        /// T-Q16 fix: push fresh contract snapshot к клиенту (используется QuestServer.GiveCredits
        /// и любым другим server-side изменением credits). Rebuilds snapshot с текущими данными
        /// + last known location (если есть). Без зоны — базовая версия (без displayName).
        /// </summary>
        public void PushPlayerSnapshot(ulong clientId)
        {
            if (!IsServer) return;
            if (ContractWorld.Instance == null) return;

            // T-Q16: get player's current zone (best-effort) — для отображения market name.
            // Если неизвестно — fallback на пустую строку + 0 multipler.
            string locationId = "";
            string displayName = "";
            float timeMult = 1f;
            float nextTick = 0f;
            var snap = ContractWorld.Instance.BuildSnapshot(clientId, locationId, displayName, timeMult, nextTick);
            SendSnapshotToClient(clientId, snap);
        }

        private void SendSnapshotToClient(ulong clientId, ContractSnapshotDto snapshot)
        {
            var target = FindNetworkPlayer(clientId);
            if (target == null) return;
            target.ReceiveContractSnapshotTargetRpc(snapshot);
        }

        private void SendResultToOwner(ulong clientId, ContractResultDto dto)
        {
            var target = FindNetworkPlayer(clientId);
            if (target == null) return;
            target.ReceiveContractResultTargetRpc(dto);
        }

        // ========================================================
        // DTO BUILDERS
        // ========================================================

        private ContractResultDto BuildResultDto(ulong clientId, ContractOpResult r, string contractId)
        {
            float newCredits = 0f;
            float newDebt = 0f;
            if (ContractWorld.Instance != null)
            {
                if (_repository != null) newCredits = _repository.GetCredits(clientId);
                newDebt = ContractWorld.Instance.GetOrCreateDebt(clientId).CurrentDebt;
            }

            return new ContractResultDto
            {
                code = (byte)r.Code,
                contractId = contractId,
                success = r.IsSuccess,
                message = r.Message,
                reward = r.Reward,
                newCredits = newCredits,
                newDebt = newDebt,
                updatedContract = r.Contract != null ? ContractWorld.Instance.ToDto(r.Contract) : (ContractDto?)null
            };
        }

        private ContractResultDto ContractResultDto_Fail(ContractResultCode code, string contractId, float reward, float newDebt, ulong clientId)
        {
            float newCredits = 0f;
            if (ContractWorld.Instance != null && _repository != null)
            {
                newCredits = _repository.GetCredits(clientId);
            }
            return new ContractResultDto
            {
                code = (byte)code,
                contractId = contractId,
                success = false,
                // The client localizes resultCode. Do not leak enum names such as NotInZone.
                message = null,
                reward = reward,
                newCredits = newCredits,
                newDebt = newDebt
            };
        }

        // ========================================================
        // UTILS
        // ========================================================

        private bool ValidateInZone(ulong clientId, string locationId, out MarketZone zone)
        {
            // C2-refactor: используем MarketZone вместо ContractZone (ContractZone удалён).
            // MarketZone уже знает игроков в зоне (PlayersInZone) — ContractZone теперь не нужен.
            zone = MarketZoneRegistry.Get(locationId);
            if (zone == null) return false;
            return zone.IsPlayerInZone(clientId);
        }

        private bool CheckRateLimit(ulong clientId)
        {
            if (maxOpsPerMinute <= 0) return true;
            float now = Time.realtimeSinceStartup;
            if (!_opTimestamps.TryGetValue(clientId, out var list))
            {
                list = new List<float>();
                _opTimestamps[clientId] = list;
            }
            list.RemoveAll(t => (now - t) > 60f);
            if (list.Count >= maxOpsPerMinute)
            {
                // F2 из аудита: слать fail-результат клиенту
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.RateLimited, "", 0, 0, clientId));
                return false;
            }
            list.Add(now);
            return true;
        }

        private static ShipClass ResolveShipClass(ulong shipNetworkObjectId)
        {
            if (shipNetworkObjectId == 0)
                return ShipClass.Light;
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
                return ShipClass.Light;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(shipNetworkObjectId, out var no))
                return ShipClass.Light;

            var ship = no.GetComponent<ShipController>();
            if (ship == null)
                return ShipClass.Light;
            return ProjectC.Ship.ShipClassMappingConfig.Default.Resolve(ship.ShipFlightClass) ?? ShipClass.Light;
        }

        private static NetworkPlayer FindNetworkPlayer(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;
            if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            return client.PlayerObject?.GetComponent<NetworkPlayer>();
        }
    }
}
