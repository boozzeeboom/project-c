using System.Collections.Generic;
using ProjectC.Items;
using ProjectC.Player;
using ProjectC.Trade.Config;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;
using ProjectC.Trade.Network;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Trade.Network
{
    /// <summary>
    /// T-E03: Серверный NetworkBehaviour для Resources Exchanger (Pack/Unpack).
    ///
    /// Паттерн: MarketServer (Trade/Network/). Ставится в BootstrapScene рядом
    /// с NetworkManager. DontDestroyOnLoad.
    ///
    /// Ответственности:
    ///   • OnNetworkSpawn — создать ExchangeWorld + ResourceExchangeResolver
    ///   • Принимать PackRpc / UnpackRpc от клиентов
    ///   • Валидировать зону (игрок в MarketZone с locationId)
    ///   • Слать ExchangeResultDto обратно клиенту
    ///
    /// Зависимости:
    ///   • TradeWorld (должен быть инициализирован MarketServer.OnNetworkSpawn)
    ///   • MarketZoneRegistry (для zone check)
    ///   • ExchangeRateConfig (инспектор)
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ExchangeServer : NetworkBehaviour
    {
        public static ExchangeServer Instance { get; private set; }

        [Header("Setup")]
        [Tooltip("Конфигурация курсов обмена (item-ratio pairs)")]
        [SerializeField] private ExchangeRateConfig exchangeRateConfig;

        [Header("Rate Limiting")]
        [Tooltip("Макс операций в минуту на клиента (0 = без лимита)")]
        [SerializeField] private int maxOpsPerMinute = 30;

        // Runtime
        private ExchangeWorld _world;
        private ResourceExchangeResolver _resolver;

        // Per-client rate limiting
        private readonly Dictionary<ulong, List<float>> _opTimestamps = new Dictionary<ulong, List<float>>();
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;

            // T-E04 DIAG: подробный лог.
            Debug.Log("[ExchangeServer] OnNetworkSpawn: IsServer=" + IsServer + " exchangeRateConfig=" + (exchangeRateConfig != null ? exchangeRateConfig.name : "NULL") + " GlobalObjectIdHash=" + NetworkObjectId);

            if (!IsServer)
            {
                enabled = false;
                return;
            }

            // Валидация конфига
            if (exchangeRateConfig == null)
            {
                Debug.LogError("[ExchangeServer] exchangeRateConfig не присвоен! ExchangeServer отключён.");
                enabled = false;
                return;
            }

            // TradeWorld может быть ещё не создан MarketServer.OnNetworkSpawn()
            // (NGO не гарантирует порядок спавна между NetworkObjects).
            // Откладываем инициализацию на корутину.
            StartCoroutine(InitWhenReady());
        }

        private System.Collections.IEnumerator InitWhenReady()
        {
            float timeout = 10f;
            while (TradeWorld.Instance == null && timeout > 0f)
            {
                yield return null;
                timeout -= Time.deltaTime;
            }

            if (TradeWorld.Instance == null)
            {
                Debug.LogError("[ExchangeServer] TradeWorld не инициализирован после 10с таймаута! ExchangeServer отключён.");
                enabled = false;
                yield break;
            }

            _resolver = new ResourceExchangeResolver(exchangeRateConfig);
            _world = new ExchangeWorld(_resolver);

            Debug.Log($"[ExchangeServer] инициализирован: rates={exchangeRateConfig.rates?.Count ?? 0}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _world = null;
            _resolver = null;
            if (Instance == this) Instance = null;
        }

        // ========================================================
        // CLIENT → SERVER RPCs
        // ========================================================

        /// <summary>
        /// Запросить PACK (инвентарь → склад).
        /// Клиент шлёт: locationId, inventoryItemId (int из ItemDatabase),
        /// countToRemove (сколько предметов забрать, кратно rate.inventoryQty).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestPackRpc(string locationId, int inventoryItemId,
            int countToRemove, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[ExchangeServer][Pack] ENTER clientId={clientId} locationId='{locationId}' inventoryItemId={inventoryItemId} countToRemove={countToRemove}");

            try
            {
                if (!IsReadyOrResult(clientId, 0)) { Debug.Log($"[ExchangeServer][Pack] EXIT: not ready"); return; }
                if (!CheckRateLimitOrResult(clientId, 0)) { Debug.Log($"[ExchangeServer][Pack] EXIT: rate-limited"); return; }

                // Валидация зоны
                if (!ValidateInZone(clientId, locationId, out var zone))
                {
                    Debug.Log($"[ExchangeServer][Pack] EXIT: not in zone (locationId={locationId}, zone={(zone == null ? "null" : zone.LocationId)})");
                    SendResult(clientId, CreateFailResult("Вы не в зоне рынка", 0));
                    return;
                }
                Debug.Log($"[ExchangeServer][Pack] in zone OK, looking up item...");

                // Найти rate по item-имени
                string itemName = _resolver.GetInventoryItemName(inventoryItemId);
                if (string.IsNullOrEmpty(itemName))
                {
                    Debug.Log($"[ExchangeServer][Pack] EXIT: itemName not found for ID={inventoryItemId}");
                    SendResult(clientId, CreateFailResult("Предмет не найден в БД", 0));
                    return;
                }
                Debug.Log($"[ExchangeServer][Pack] itemName='{itemName}', looking up rate...");

                var rate = _resolver.FindRateForItemName(itemName);
                if (rate == null)
                {
                    Debug.Log($"[ExchangeServer][Pack] EXIT: rate not found for '{itemName}'");
                    SendResult(clientId, CreateFailResult(
                        $"Предмет '{itemName}' не поддерживает упаковку", 0));
                    return;
                }
                Debug.Log($"[ExchangeServer][Pack] rate OK: inventoryQty={rate.Value.inventoryQty} warehouseQty={rate.Value.warehouseQty}, calling _world.Pack...");

                var r = _resolver.GetItemType(inventoryItemId);
                var result = _world.Pack(clientId, locationId, inventoryItemId, r,
                    rate.Value, countToRemove);
                Debug.Log($"[ExchangeServer][Pack] _world.Pack returned: success={result.IsSuccess} message='{result.Message}' whDelta={result.WarehouseDelta} invDelta={result.InventoryDelta}");

                if (result.IsSuccess)
                {
                    // Персист склада
                    var wh = TradeWorld.Instance.GetOrLoadWarehouse(clientId, locationId);
                    TradeWorld.Instance.Repository.SetWarehouse(
                        clientId, locationId, wh.SaveToList());
                    Debug.Log($"[ExchangeServer][Pack] warehouse persisted");
                }

                // T-E04 FIX: PushSnapshot инвентаря — AddItemDirect не вызывает SendSnapshot,
                // клиент не узнает об изменении (как QuestServer делает).
                if (ProjectC.Items.Network.InventoryServer.Instance != null)
                {
                    ProjectC.Items.Network.InventoryServer.Instance.PushSnapshot(clientId);
                    Debug.Log($"[ExchangeServer][Pack] PushSnapshot called");
                }
                else
                {
                    Debug.LogWarning($"[ExchangeServer][Pack] InventoryServer.Instance == null, push skipped");
                }

                // T-E04 FIX: PushPlayerSnapshot для MarketServer — иначе UI MarketWindow.warehouse не обновится
                // (MarketClientState не получит свежий snapshot склада после Pack).
                if (ProjectC.Trade.Network.MarketServer.Instance != null)
                {
                    ProjectC.Trade.Network.MarketServer.Instance.PushPlayerSnapshot(clientId);
                    Debug.Log($"[ExchangeServer][Pack] MarketServer.PushPlayerSnapshot called");
                }

                SendResult(clientId, ToDto(result, op: 0));
                Debug.Log($"[ExchangeServer][Pack] SendResult OK: success={result.IsSuccess}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ExchangeServer][Pack] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                try { SendResult(clientId, CreateFailResult($"Внутренняя ошибка: {ex.Message}", 0)); } catch { }
            }
        }

        /// <summary>
        /// Запросить UNPACK (склад → инвентарь).
        /// Клиент шлёт: locationId, warehouseItemId (string из TradeItemDefinition),
        /// countToRemove (сколько коробок забрать, кратно rate.warehouseQty).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestUnpackRpc(string locationId, string warehouseItemId,
            int countToRemove, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[ExchangeServer][Unpack] ENTER clientId={clientId} locationId='{locationId}' warehouseItemId='{warehouseItemId}' countToRemove={countToRemove}");

            try
            {
                if (!IsReadyOrResult(clientId, 1)) { Debug.Log($"[ExchangeServer][Unpack] EXIT: not ready"); return; }
                if (!CheckRateLimitOrResult(clientId, 1)) { Debug.Log($"[ExchangeServer][Unpack] EXIT: rate-limited"); return; }

                // Валидация зоны
                if (!ValidateInZone(clientId, locationId, out var zone))
                {
                    Debug.Log($"[ExchangeServer][Unpack] EXIT: not in zone (locationId={locationId}, zone={(zone == null ? "null" : zone.LocationId)})");
                    SendResult(clientId, CreateFailResult("Вы не в зоне рынка", 1));
                    return;
                }
                Debug.Log($"[ExchangeServer][Unpack] in zone OK, looking up rate...");

                var rate = _resolver.FindRateForWarehouseItem(warehouseItemId);
                if (rate == null)
                {
                    Debug.Log($"[ExchangeServer][Unpack] EXIT: rate not found for '{warehouseItemId}'");
                    SendResult(clientId, CreateFailResult(
                        $"Товар '{warehouseItemId}' не поддерживает распаковку", 1));
                    return;
                }
                Debug.Log($"[ExchangeServer][Unpack] rate OK: inventoryQty={rate.Value.inventoryQty} warehouseQty={rate.Value.warehouseQty}, calling _world.Unpack...");

                var result = _world.Unpack(clientId, locationId, rate.Value, countToRemove);
                Debug.Log($"[ExchangeServer][Unpack] _world.Unpack returned: success={result.IsSuccess} message='{result.Message}' whDelta={result.WarehouseDelta} invDelta={result.InventoryDelta}");

                if (result.IsSuccess)
                {
                    // Персист склада
                    var wh = TradeWorld.Instance.GetOrLoadWarehouse(clientId, locationId);
                    TradeWorld.Instance.Repository.SetWarehouse(
                        clientId, locationId, wh.SaveToList());
                    Debug.Log($"[ExchangeServer][Unpack] warehouse persisted");
                }

                // T-E04 FIX: PushSnapshot инвентаря при любом результате
                if (ProjectC.Items.Network.InventoryServer.Instance != null)
                {
                    ProjectC.Items.Network.InventoryServer.Instance.PushSnapshot(clientId);
                    Debug.Log($"[ExchangeServer][Unpack] PushSnapshot called");
                }
                else
                {
                    Debug.LogWarning($"[ExchangeServer][Unpack] InventoryServer.Instance == null, push skipped");
                }

                // T-E04 FIX: PushPlayerSnapshot для MarketServer — иначе UI MarketWindow.warehouse не обновится.
                if (ProjectC.Trade.Network.MarketServer.Instance != null)
                {
                    ProjectC.Trade.Network.MarketServer.Instance.PushPlayerSnapshot(clientId);
                    Debug.Log($"[ExchangeServer][Unpack] MarketServer.PushPlayerSnapshot called");
                }

                SendResult(clientId, ToDto(result, op: 1));
                Debug.Log($"[ExchangeServer][Unpack] SendResult OK: success={result.IsSuccess}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ExchangeServer][Unpack] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                try { SendResult(clientId, CreateFailResult($"Внутренняя ошибка: {ex.Message}", 1)); } catch { }
            }
        }

        // ========================================================
        // RESULT DELIVERY
        // ========================================================

        private void SendResult(ulong clientId, ExchangeResultDto dto)
        {
            var target = FindNetworkPlayer(clientId);
            if (target == null)
            {
                Debug.LogWarning($"[ExchangeServer] Cannot find NetworkPlayer for client {clientId}");
                return;
            }
            target.ReceiveExchangeResultTargetRpc(dto);
        }

        private static ExchangeResultDto ToDto(ExchangeResult r, byte op)
        {
            return new ExchangeResultDto
            {
                success = r.IsSuccess,
                message = r.Message,
                op = op,
                warehouseItemId = r.WarehouseItemId,
                warehouseDelta = r.WarehouseDelta,
                inventoryDelta = r.InventoryDelta
            };
        }

        private static ExchangeResultDto CreateFailResult(string message, byte op)
        {
            return new ExchangeResultDto
            {
                success = false,
                message = message,
                op = op,
                warehouseItemId = null,
                warehouseDelta = 0,
                inventoryDelta = 0
            };
        }

        // ========================================================
        // VALIDATION HELPERS
        // ========================================================

        /// <summary>
        /// Проверить готовность сервера + отправить fail-результат если не готов.
        /// </summary>
        private bool IsReadyOrResult(ulong clientId, byte op)
        {
            if (_world == null || _resolver == null)
            {
                Debug.LogWarning("[ExchangeServer] Не готов (world/resolver null) — шлю fail клиенту");
                SendResult(clientId, CreateFailResult("Сервер обменника ещё не готов (инициализация)", op));
                return false;
            }
            if (TradeWorld.Instance == null)
            {
                Debug.LogWarning("[ExchangeServer] TradeWorld.Instance == null — шлю fail клиенту");
                SendResult(clientId, CreateFailResult("Сервер обменника ещё не готов (TradeWorld)", op));
                return false;
            }
            return true;
        }

        private bool ValidateInZone(ulong clientId, string locationId, out MarketZone zone)
        {
            zone = MarketZoneRegistry.Get(locationId);
            if (zone == null) return false;
            return zone.IsPlayerInZone(clientId);
        }

        /// <summary>
        /// Rate-limit с отправкой fail-результата клиенту.
        /// </summary>
        private bool CheckRateLimitOrResult(ulong clientId, byte op)
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
                SendResult(clientId, CreateFailResult("Слишком много операций. Подождите минуту.", op));
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
