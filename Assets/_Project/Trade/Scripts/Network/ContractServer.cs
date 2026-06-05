using System.Collections.Generic;
using ProjectC.Player;
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

        [Header("Behavior")]
        [Tooltip("Макс активных контрактов на игрока")]
        [SerializeField] private int maxActiveContractsPerPlayer = 3;

        [Tooltip("Автогенерация новых контрактов когда доска пуста")]
        [SerializeField] private bool autoRegenerateContracts = true;

        [Tooltip("Инициализировать контракты при старте (для всех 4 локаций)")]
        [SerializeField] private bool autoInitContracts = true;

        [Header("Rate Limiting")]
        [Tooltip("Макс операций в минуту на клиента (0 = без лимита)")]
        [SerializeField] private int maxOpsPerMinute = 30;

        // === Runtime ===
        private IPlayerDataRepository _repository;
        private ContractWorldItemResolver _resolver;

        // Per-client rate limiting
        private readonly Dictionary<ulong, List<float>> _opTimestamps = new Dictionary<ulong, List<float>>();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;

            if (!IsServer)
            {
                enabled = false;
                return;
            }

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
            ContractWorld.CreateAndInitialize(_repository, _resolver, autoInitContracts);
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
                ContractZoneRegistry.Clear();
            }
            if (Instance == this) Instance = null;
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

            // Re-snapshot чтобы UI увидел обновлённый active[]
            if (r.IsSuccess)
            {
                var snap = ContractWorld.Instance.BuildSnapshot(clientId, contract.fromLocationId, zone.DisplayName, 1f, 0f);
                SendSnapshotToClient(clientId, snap);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestCompleteRpc(string contractId, RpcParams rpcParams = default)
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
            // Валидация позиции: игрок должен быть в toLocationId
            if (!ValidateInZone(clientId, contract.toLocationId, out _))
            {
                SendResultToOwner(clientId, ContractResultDto_Fail(ContractResultCode.WrongDestination, contractId, 0, 0, clientId));
                return;
            }

            var r = ContractWorld.Instance.TryComplete(clientId, contractId, contract.toLocationId);
            var dto = BuildResultDto(clientId, r, contractId);
            SendResultToOwner(clientId, dto);

            // Re-snapshot
            if (r.IsSuccess)
            {
                var zone = ContractZoneRegistry.Get(contract.toLocationId);
                var snap = ContractWorld.Instance.BuildSnapshot(clientId, contract.toLocationId,
                    zone != null ? zone.DisplayName : contract.toLocationId, 1f, 0f);
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

            // Re-snapshot с локации, на которой был контракт
            if (r.Contract != null)
            {
                var zone = ContractZoneRegistry.Get(r.Contract.fromLocationId);
                var snap = ContractWorld.Instance.BuildSnapshot(clientId, r.Contract.fromLocationId,
                    zone != null ? zone.DisplayName : r.Contract.fromLocationId, 1f, 0f);
                SendSnapshotToClient(clientId, snap);
            }
        }

        // ========================================================
        // SERVER → CLIENT RPCs
        // ========================================================

        [Rpc(SendTo.Owner)]
        private void ReceiveContractSnapshotClientRpc(ContractSnapshotDto snapshot, RpcParams rpcParams = default)
        {
            ProjectC.Trade.Client.ContractClientState.Instance?.OnSnapshotReceived(snapshot);
        }

        [Rpc(SendTo.Owner)]
        private void ReceiveContractResultClientRpc(ContractResultDto result, RpcParams rpcParams = default)
        {
            ProjectC.Trade.Client.ContractClientState.Instance?.OnTradeResultReceived(result);
        }

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

                // Re-snapshot с локации отправления (где была доска)
                var zone = ContractZoneRegistry.Get(contract.fromLocationId);
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
                message = ContractClientState_LocalizeResultCode(code), // server-side fallback (на случай если клиент не имеет ContractClientState)
                reward = reward,
                newCredits = newCredits,
                newDebt = newDebt
            };
        }

        /// <summary>Server-side минимальная локализация (на случай если ContractClientState недоступен). Полная — в ContractClientState.LocalizeResultCode.</summary>
        private static string ContractClientState_LocalizeResultCode(ContractResultCode code)
        {
            switch (code)
            {
                case ContractResultCode.Ok: return "OK";
                case ContractResultCode.NotInZone: return "Вы должны быть в зоне NPC-агента";
                case ContractResultCode.ContractNotFound: return "Контракт не найден";
                case ContractResultCode.ContractNotPending: return "Контракт уже принят или истёк";
                case ContractResultCode.ContractNotActive: return "Контракт не активен";
                case ContractResultCode.ContractNotAssigned: return "Это не ваш контракт";
                case ContractResultCode.MaxActiveReached: return "Слишком много активных контрактов";
                case ContractResultCode.TooMuchDebt: return "Слишком большой долг";
                case ContractResultCode.TimerExpired: return "Время контракта истекло";
                case ContractResultCode.WrongDestination: return "Вы не в целевой локации";
                case ContractResultCode.CargoMissing: return "Нет нужного груза";
                case ContractResultCode.WarehouseFull: return "Нет места на складе";
                case ContractResultCode.ItemNotFound: return "Товар не найден";
                case ContractResultCode.RateLimited: return "Слишком много запросов";
                default: return code.ToString();
            }
        }

        // ========================================================
        // UTILS
        // ========================================================

        private bool ValidateInZone(ulong clientId, string locationId, out ContractZone zone)
        {
            zone = ContractZoneRegistry.Get(locationId);
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

        private static NetworkPlayer FindNetworkPlayer(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;
            if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            return client.PlayerObject?.GetComponent<NetworkPlayer>();
        }
    }
}
