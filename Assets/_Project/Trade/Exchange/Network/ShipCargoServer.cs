// =====================================================================================
// ShipCargoServer.cs — серверный NetworkBehaviour для cargo-операций на корабле (T-CARGO-UI-02)
// =====================================================================================
// Назначение: принимает RPC от клиента для перемещения предметов между
// инвентарём игрока и трюмом корабля (inventory ↔ ship cargo).
//
// Паттерн: ExchangeServer (Trade/Exchange/Network/). Ставится в BootstrapScene
// рядом с NetworkManager. DontDestroyOnLoad.
//
// Операции:
//   StoreToCargo  — забрать предметы из инвентаря → положить в трюм корабля
//   RetrieveFromCargo — забрать из трюма → положить в инвентарь
//
// Зависимости:
//   • InventoryWorld (должен быть инициализирован InventoryServer)
//   • TradeWorld (должен быть инициализирован MarketServer)
// =====================================================================================

using System.Collections.Generic;
using ProjectC.Items;
using ProjectC.Player;
using ProjectC.Trade.Core;
using ProjectC.Trade.Dto;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Trade.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public class ShipCargoServer : NetworkBehaviour
    {
        public static ShipCargoServer Instance { get; private set; }

        [Header("Rate Limiting")]
        [Tooltip("Макс операций в минуту на клиента (0 = без лимита)")]
        [SerializeField] private int _maxOpsPerMinute = 30;

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

            Debug.Log("[ShipCargoServer] OnNetworkSpawn: ready");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (Instance == this) Instance = null;
        }

        // ========================================================
        // CLIENT → SERVER RPCs
        // ========================================================

        /// <summary>
        /// Переместить предметы из инвентаря игрока в трюм корабля.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestStoreToCargoRpc(
            ulong shipNetId,
            int inventoryItemId,
            int count,
            RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[ShipCargoServer][StoreToCargo] ENTER clientId={clientId} ship={shipNetId} itemId={inventoryItemId} count={count}");

            try
            {
                if (!IsReadyOrResult(clientId, 0)) return;
                if (!CheckRateLimitOrResult(clientId, 0)) return;

                // --- Валидация ---
                if (shipNetId == 0 || inventoryItemId <= 0 || count <= 0)
                {
                    SendResult(clientId, CreateFailResult("Неверные параметры", 0));
                    return;
                }

                // Найти корабль
                var ship = FindShipController(shipNetId);
                if (ship == null)
                {
                    SendResult(clientId, CreateFailResult("Корабль не найден", 0));
                    return;
                }

                // Получить ShipClass
                var shipClass = ProjectC.Ship.ShipClassMappingConfig.Default.Resolve(ship.ShipFlightClass)
                                ?? ShipClass.Medium;
                var tradeWorld = TradeWorld.Instance;
                if (tradeWorld == null)
                {
                    SendResult(clientId, CreateFailResult("Сервер торговли не готов", 0));
                    return;
                }

                var invWorld = InventoryWorld.Instance;
                if (invWorld == null)
                {
                    SendResult(clientId, CreateFailResult("Сервер инвентаря не готов", 0));
                    return;
                }

                // Узнаём itemId и itemType из ItemDatabase
                // Для этого нужно найти itemData в InventoryWorld.ItemDatabase
                // InventoryWorld хранит Dictionary<int, ItemData>
                // ItemData имеет поле itemType
                var itemData = invWorld.GetItemDefinition(inventoryItemId);
                if (itemData == null)
                {
                    SendResult(clientId, CreateFailResult("Предмет не найден в базе", 0));
                    return;
                }

                // --- Шаг 1: забрать из инвентаря ---
                var invResult = invWorld.RemoveItems(clientId, inventoryItemId, itemData.itemType, count);
                if (!invResult.IsSuccess)
                {
                    SendResult(clientId, CreateFailResult(
                        $"Недостаточно предметов: {invResult.message ?? "ошибка"}", 0));
                    return;
                }

                // --- Шаг 2: положить в трюм ---
                // Cargo использует строковые itemId. Используем itemData.itemName
                // (то же имя что и в TradeItemDefinition.itemId для совместимости).
                string cargoItemId = itemData.itemName;

                var cargo = tradeWorld.GetOrLoadCargo(shipNetId, shipClass);
                if (!cargo.TryAdd(cargoItemId, count, tradeWorld.Resolver, out var cargoFail))
                {
                    // ROLLBACK: вернуть в инвентарь
                    RollbackAddItems(invWorld, clientId, inventoryItemId, itemData.itemType, count);
                    SendResult(clientId, CreateFailResult($"Трюм полон: {cargoFail}", 0));
                    return;
                }

                // --- Шаг 3: персист ---
                tradeWorld.Repository.SetCargo(shipNetId, cargo.SaveToList());
                // OnCargoChanged вызывается изнутри TradeWorld; ShipController.UpdateTelemetryState
                // (5 Hz) подхватит изменения в течение 200ms.

                // Push inventory snapshot
                if (Items.Network.InventoryServer.Instance != null)
                    Items.Network.InventoryServer.Instance.PushSnapshot(clientId);

                SendResult(clientId, new ShipCargoResultDto
                {
                    success = true,
                    message = $"+{count} '{itemData.itemName}' в трюме",
                    op = 0,
                    cargoDelta = count,
                    inventoryDelta = -count,
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ShipCargoServer][StoreToCargo] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                try { SendResult(clientId, CreateFailResult($"Ошибка: {ex.Message}", 0)); } catch { }
            }
        }

        /// <summary>
        /// Переместить предметы из трюма корабля в инвентарь игрока.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestRetrieveFromCargoRpc(
            ulong shipNetId,
            string cargoItemId,
            int count,
            int inventoryItemId,
            RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[ShipCargoServer][RetrieveFromCargo] ENTER clientId={clientId} ship={shipNetId} cargoItem={cargoItemId} count={count} invItemId={inventoryItemId}");

            try
            {
                if (!IsReadyOrResult(clientId, 1)) return;
                if (!CheckRateLimitOrResult(clientId, 1)) return;

                if (shipNetId == 0 || string.IsNullOrEmpty(cargoItemId) || count <= 0 || inventoryItemId <= 0)
                {
                    SendResult(clientId, CreateFailResult("Неверные параметры", 1));
                    return;
                }

                var ship = FindShipController(shipNetId);
                if (ship == null)
                {
                    SendResult(clientId, CreateFailResult("Корабль не найден", 1));
                    return;
                }

                var shipClass = ProjectC.Ship.ShipClassMappingConfig.Default.Resolve(ship.ShipFlightClass)
                                ?? ShipClass.Medium;
                var tradeWorld = TradeWorld.Instance;
                if (tradeWorld == null)
                {
                    SendResult(clientId, CreateFailResult("Сервер торговли не готов", 1));
                    return;
                }

                var invWorld = InventoryWorld.Instance;
                if (invWorld == null)
                {
                    SendResult(clientId, CreateFailResult("Сервер инвентаря не готов", 1));
                    return;
                }

                var itemData = invWorld.GetItemDefinition(inventoryItemId);
                if (itemData == null)
                {
                    SendResult(clientId, CreateFailResult("Предмет не найден в базе", 1));
                    return;
                }

                // --- Шаг 1: забрать из трюма ---
                var cargo = tradeWorld.GetOrLoadCargo(shipNetId, shipClass);
                if (!cargo.TryRemove(cargoItemId, count, out var cargoFail))
                {
                    SendResult(clientId, CreateFailResult($"Недостаточно в трюме: {cargoFail}", 1));
                    return;
                }

                // --- Шаг 2: положить в инвентарь ---
                int added = 0;
                for (int i = 0; i < count; i++)
                {
                    var addResult = invWorld.AddItemDirect(clientId, inventoryItemId, itemData.itemType);
                    if (!addResult.IsSuccess)
                    {
                        // ROLLBACK: вернуть в трюм + откатить уже добавленное
                        cargo.TryAdd(cargoItemId, count, tradeWorld.Resolver, out _);
                        RollbackAddItems(invWorld, clientId, inventoryItemId, itemData.itemType, added);
                        SendResult(clientId, CreateFailResult(
                            $"Инвентарь полон: {addResult.message ?? "ошибка"}", 1));
                        return;
                    }
                    added++;
                }

                // --- Шаг 3: персист ---
                tradeWorld.Repository.SetCargo(shipNetId, cargo.SaveToList());
                // OnCargoChanged вызывается изнутри TradeWorld; ShipController.UpdateTelemetryState
                // (5 Hz) подхватит изменения в течение 200ms.

                if (Items.Network.InventoryServer.Instance != null)
                    Items.Network.InventoryServer.Instance.PushSnapshot(clientId);

                SendResult(clientId, new ShipCargoResultDto
                {
                    success = true,
                    message = $"+{count} '{itemData.itemName}' в инвентаре",
                    op = 1,
                    cargoDelta = -count,
                    inventoryDelta = count,
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ShipCargoServer][RetrieveFromCargo] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                try { SendResult(clientId, CreateFailResult($"Ошибка: {ex.Message}", 1)); } catch { }
            }
        }

        // ========================================================
        // HELPERS
        // ========================================================

        private bool IsReadyOrResult(ulong clientId, byte op)
        {
            if (TradeWorld.Instance == null)
            {
                SendResult(clientId, CreateFailResult("Сервер торговли ещё не готов", op));
                return false;
            }
            if (InventoryWorld.Instance == null)
            {
                SendResult(clientId, CreateFailResult("Сервер инвентаря ещё не готов", op));
                return false;
            }
            return true;
        }

        private bool CheckRateLimitOrResult(ulong clientId, byte op)
        {
            if (_maxOpsPerMinute <= 0) return true;
            float now = Time.realtimeSinceStartup;
            if (!_opTimestamps.TryGetValue(clientId, out var list))
            {
                list = new List<float>();
                _opTimestamps[clientId] = list;
            }
            list.RemoveAll(t => (now - t) > 60f);
            if (list.Count >= _maxOpsPerMinute)
            {
                SendResult(clientId, CreateFailResult("Слишком много операций. Подождите минуту.", op));
                return false;
            }
            list.Add(now);
            return true;
        }

        private static ShipController FindShipController(ulong shipNetId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;
            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(shipNetId, out var no)) return null;
            return no != null ? no.GetComponent<ShipController>() : null;
        }

        private void SendResult(ulong clientId, ShipCargoResultDto dto)
        {
            var target = FindNetworkPlayer(clientId);
            if (target == null)
            {
                Debug.LogWarning($"[ShipCargoServer] Cannot find NetworkPlayer for client {clientId}");
                return;
            }
            target.ReceiveShipCargoResultTargetRpc(dto);
        }

        private static NetworkPlayer FindNetworkPlayer(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;
            if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            return client.PlayerObject?.GetComponent<NetworkPlayer>();
        }

        private static ShipCargoResultDto CreateFailResult(string message, byte op)
        {
            return new ShipCargoResultDto
            {
                success = false,
                message = message,
                op = op,
                cargoDelta = 0,
                inventoryDelta = 0,
            };
        }

        private static void RollbackAddItems(InventoryWorld invWorld, ulong clientId,
            int itemId, ItemType itemType, int count)
        {
            if (count <= 0) return;
            var rollbackResult = invWorld.RemoveItems(clientId, itemId, itemType, count);
            if (!rollbackResult.IsSuccess)
            {
                Debug.LogWarning($"[ShipCargoServer] Rollback не удался: {rollbackResult.message}");
            }
        }
    }
}
