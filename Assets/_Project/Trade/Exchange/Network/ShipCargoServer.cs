// =====================================================================================
// ShipCargoServer.cs — серверный NetworkBehaviour для cargo-операций на корабле (T-CARGO-UI-02)
// =====================================================================================
// Назначение: принимает RPC от клиента для перемещения предметов между
// инвентарём игрока и трюмом корабля (inventory ↔ ship cargo) ЧЕРЕЗ ОБМЕННЫЙ КУРС.
//
// ПАТТЕРН: ExchangeServer (Trade/Exchange/Network/) + ExchangeWorld.Pack/Unpack.
// ОБЯЗАТЕЛЬНО использует ResourceExchangeResolver + ExchangeRateConfig (DefaultExchangeRate.asset).
// Без курса операция отклоняется — НЕТ прямого 1:1 переноса.
//
// Операции:
//   StoreToCargo  — N×rate.inventoryQty pickable → M×rate.warehouseQty boxes в трюм
//   RetrieveFromCargo — N×rate.warehouseQty boxes → M×rate.inventoryQty pickable в инвентарь
//
// Зависимости:
//   • InventoryWorld (должен быть инициализирован InventoryServer)
//   • TradeWorld (должен быть инициализирован MarketServer)
//   • ExchangeRateConfig (назначить в инспекторе!)
// =====================================================================================

using System.Collections.Generic;
using ProjectC.Items;
using ProjectC.Player;
using ProjectC.Ship.Key;
using ProjectC.Trade.Config;
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

        [Header("Exchange Rate Config (ОБЯЗАТЕЛЬНО)")]
        [Tooltip("DefaultExchangeRate.asset — без него операции отклоняются.")]
        [SerializeField] private ExchangeRateConfig _exchangeRateConfig;

        [Header("Rate Limiting")]
        [Tooltip("Макс операций в минуту на клиента (0 = без лимита)")]
        [SerializeField] private int _maxOpsPerMinute = 30;

        private ResourceExchangeResolver _resolver;
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

            if (_exchangeRateConfig == null)
            {
                Debug.LogError("[ShipCargoServer] exchangeRateConfig не присвоен! ShipCargoServer отключён.");
                enabled = false;
                return;
            }

            _resolver = new ResourceExchangeResolver(_exchangeRateConfig);
            Debug.Log($"[ShipCargoServer] OnNetworkSpawn: rates={_exchangeRateConfig.rates?.Count ?? 0}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _resolver = null;
            if (Instance == this) Instance = null;
        }

        // ========================================================
        // CLIENT → SERVER RPCs
        // ========================================================

        /// <summary>
        /// Упаковать pickable предметы из инвентаря в cargo-ящики корабля (через курс).
        /// count должен быть кратен rate.inventoryQty (обычно 100).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
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

                if (shipNetId == 0 || inventoryItemId <= 0 || count <= 0)
                {
                    SendResult(clientId, CreateFailResult("Неверные параметры", 0));
                    return;
                }

                var ship = FindShipController(shipNetId);
                if (ship == null)
                {
                    SendResult(clientId, CreateFailResult("Корабль не найден", 0));
                    return;
                }

                // P5: ownership guard — только владелец может грузить в трюм
                if (!KeyRodInstanceWorld.IsOwnerOfShip(clientId, shipNetId))
                {
                    SendResult(clientId, CreateFailResult("Вы не владелец этого корабля", 0));
                    return;
                }

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

                // --- Резолвим itemName → ExchangeRateEntry ---
                string itemName = _resolver.GetInventoryItemName(inventoryItemId);
                if (string.IsNullOrEmpty(itemName))
                {
                    SendResult(clientId, CreateFailResult("Предмет не найден в БД", 0));
                    return;
                }

                var rate = _resolver.FindRateForItemName(itemName);
                if (rate == null)
                {
                    SendResult(clientId, CreateFailResult(
                        $"Предмет '{itemName}' не поддерживает упаковку в трюм", 0));
                    return;
                }

                var r = rate.Value;

                // Валидация кратности
                if (count % r.inventoryQty != 0)
                {
                    SendResult(clientId, CreateFailResult(
                        $"Количество должно быть кратно {r.inventoryQty} (курс: {r.inventoryQty} шт = {r.warehouseQty} ящик)", 0));
                    return;
                }

                int boxesToAdd = (count / r.inventoryQty) * r.warehouseQty;

                var itemData = invWorld.GetItemDefinition(inventoryItemId);

                // --- Шаг 1: забрать из инвентаря ---
                var invResult = invWorld.RemoveItems(clientId, inventoryItemId, itemData.itemType, count);
                if (!invResult.IsSuccess)
                {
                    SendResult(clientId, CreateFailResult(
                        $"Недостаточно предметов: {invResult.message ?? "ошибка"}", 0));
                    return;
                }

                // --- Шаг 2: положить в трюм (warehouseItemId — id ящика) ---
                var cargo = tradeWorld.GetOrLoadCargo(shipNetId, shipClass);
                if (!cargo.TryAdd(r.warehouseItemId, boxesToAdd, tradeWorld.Resolver, out var cargoFail))
                {
                    // ROLLBACK: вернуть предметы в инвентарь (AddItemDirect, НЕ RemoveItems)
                    RollbackReturnItems(invWorld, clientId, inventoryItemId, itemData.itemType, count);
                    SendResult(clientId, CreateFailResult($"Трюм полон: {cargoFail}", 0));
                    return;
                }

                // --- Шаг 3: персист ---
                tradeWorld.Repository.SetCargo(shipNetId, cargo.SaveToList());
                tradeWorld.NotifyCargoChanged(shipNetId);

                if (Items.Network.InventoryServer.Instance != null)
                    Items.Network.InventoryServer.Instance.PushSnapshot(clientId);

                SendResult(clientId, new ShipCargoResultDto
                {
                    success = true,
                    message = $"+{boxesToAdd} '{r.displayName}' в трюме\n(упаковано {count} × {itemName})",
                    op = 0,
                    cargoDelta = boxesToAdd,
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
        /// Распаковать cargo-ящики корабля в pickable предметы инвентаря (через курс).
        /// count должен быть кратен rate.warehouseQty (обычно 1).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestRetrieveFromCargoRpc(
            ulong shipNetId,
            string cargoItemId,
            int count,
            RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[ShipCargoServer][RetrieveFromCargo] ENTER clientId={clientId} ship={shipNetId} cargoItem={cargoItemId} count={count}");

            try
            {
                if (!IsReadyOrResult(clientId, 1)) return;
                if (!CheckRateLimitOrResult(clientId, 1)) return;

                if (shipNetId == 0 || string.IsNullOrEmpty(cargoItemId) || count <= 0)
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

                // P5: ownership guard — только владелец может разгружать трюм
                if (!KeyRodInstanceWorld.IsOwnerOfShip(clientId, shipNetId))
                {
                    SendResult(clientId, CreateFailResult("Вы не владелец этого корабля", 1));
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

                // --- Резолвим warehouseItemId → ExchangeRateEntry ---
                var rate = _resolver.FindRateForWarehouseItem(cargoItemId);
                if (rate == null)
                {
                    SendResult(clientId, CreateFailResult(
                        $"Товар '{cargoItemId}' не поддерживает распаковку из трюма", 1));
                    return;
                }

                var r = rate.Value;

                // Валидация кратности
                if (count % r.warehouseQty != 0)
                {
                    SendResult(clientId, CreateFailResult(
                        $"Количество должно быть кратно {r.warehouseQty} (курс: {r.warehouseQty} ящик = {r.inventoryQty} шт)", 1));
                    return;
                }

                int itemsToAdd = (count / r.warehouseQty) * r.inventoryQty;

                // Разрешаем inventoryItemId из itemName
                int inventoryItemId = _resolver.ResolveInventoryItemId(r.inventoryItemName);
                if (inventoryItemId <= 0)
                {
                    SendResult(clientId, CreateFailResult(
                        $"Предмет '{r.inventoryItemName}' не найден в БД", 1));
                    return;
                }

                var itemType = _resolver.GetItemType(inventoryItemId);

                // --- Шаг 1: забрать из трюма ---
                var cargo = tradeWorld.GetOrLoadCargo(shipNetId, shipClass);
                if (!cargo.TryRemove(cargoItemId, count, out var cargoFail))
                {
                    SendResult(clientId, CreateFailResult($"Недостаточно в трюме: {cargoFail}", 1));
                    return;
                }

                // --- Шаг 2: положить в инвентарь (с rollback) ---
                int added = 0;
                for (int i = 0; i < itemsToAdd; i++)
                {
                    var addResult = invWorld.AddItemDirect(clientId, inventoryItemId, itemType);
                    if (!addResult.IsSuccess)
                    {
                        // ROLLBACK: вернуть в трюм + откатить уже добавленное
                        cargo.TryAdd(cargoItemId, count, tradeWorld.Resolver, out _);
                        RollbackRemoveItems(invWorld, clientId, inventoryItemId, itemType, added);
                        SendResult(clientId, CreateFailResult(
                            $"Инвентарь полон: {addResult.message ?? "ошибка"}", 1));
                        return;
                    }
                    added++;
                }

                // --- Шаг 3: персист ---
                tradeWorld.Repository.SetCargo(shipNetId, cargo.SaveToList());
                tradeWorld.NotifyCargoChanged(shipNetId);

                if (Items.Network.InventoryServer.Instance != null)
                    Items.Network.InventoryServer.Instance.PushSnapshot(clientId);

                SendResult(clientId, new ShipCargoResultDto
                {
                    success = true,
                    message = $"+{itemsToAdd} '{r.inventoryItemName}' в инвентаре\n(распаковано {count} × {r.displayName})",
                    op = 1,
                    cargoDelta = -count,
                    inventoryDelta = itemsToAdd,
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
            if (_resolver == null)
            {
                SendResult(clientId, CreateFailResult("Сервер cargo-обменника ещё не готов", op));
                return false;
            }
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

        /// <summary>Rollback REMOVE: добавить предметы обратно в инвентарь (для StoreToCargo).</summary>
        private static void RollbackReturnItems(InventoryWorld invWorld, ulong clientId,
            int itemId, ItemType itemType, int count)
        {
            if (count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                var result = invWorld.AddItemDirect(clientId, itemId, itemType);
                if (!result.IsSuccess)
                {
                    Debug.LogWarning($"[ShipCargoServer] RollbackReturnItems [{i}/{count}]: {result.message}");
                }
            }
        }

        /// <summary>Rollback ADD: удалить из инвентаря (для RetrieveFromCargo).</summary>
        private static void RollbackRemoveItems(InventoryWorld invWorld, ulong clientId,
            int itemId, ItemType itemType, int count)
        {
            if (count <= 0) return;
            var rollbackResult = invWorld.RemoveItems(clientId, itemId, itemType, count);
            if (!rollbackResult.IsSuccess)
            {
                Debug.LogWarning($"[ShipCargoServer] RollbackRemoveItems не удался: {rollbackResult.message}");
            }
        }
    }
}
