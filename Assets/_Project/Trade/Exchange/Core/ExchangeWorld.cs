using ProjectC.Items;
using ProjectC.Items.Dto;
using ProjectC.Trade.Config;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// T-E02: Серверная POCO-логика обмена ресурсами (Pack/Unpack).
    ///
    /// НЕ NetworkBehaviour. НЕ MonoBehaviour. Создаётся в ExchangeServer.OnNetworkSpawn.
    ///
    /// Pack (Inventory → Warehouse):
    ///   1. Удалить N единиц предмета из инвентаря игрока.
    ///   2. Добавить M коробок на склад игрока (на локации).
    ///   3. Rollback: если склад отказал, вернуть предметы в инвентарь.
    ///
    /// Unpack (Warehouse → Inventory):
    ///   1. Удалить M коробок со склада.
    ///   2. Добавить N единиц предмета в инвентарь.
    ///   3. Rollback: если инвентарь отказал, вернуть коробки на склад.
    ///
    /// Mutates in-place: warehouse (from TradeWorld cache) + inventory (via InventoryWorld).
    /// Вызывающий (ExchangeServer) отвечает за Repository.SetWarehouse после Pack/Unpack.
    /// </summary>
    public class ExchangeWorld
    {
        private readonly ResourceExchangeResolver _resolver;

        public ExchangeWorld(ResourceExchangeResolver resolver)
        {
            _resolver = resolver;
        }

        /// <summary>
        /// Упаковать: инвентарь → склад.
        /// </summary>
        /// <param name="clientId">ID игрока</param>
        /// <param name="locationId">ID локации рынка (для склада)</param>
        /// <param name="inventoryItemId">itemId из ItemDatabase</param>
        /// <param name="itemType">ItemType предмета</param>
        /// <param name="rate">Курс обмена (должен соответствовать inventoryItemId)</param>
        /// <param name="countToRemove">Сколько единиц забрать из инвентаря (должно быть кратно rate.inventoryQty)</param>
        /// <returns>ExchangeResult</returns>
        public ExchangeResult Pack(ulong clientId, string locationId,
            int inventoryItemId, ItemType itemType, ExchangeRateEntry rate, int countToRemove)
        {
            // --- Валидация ---
            if (rate.warehouseQty <= 0 || rate.inventoryQty <= 0)
                return ExchangeResult.Fail("Внутренняя ошибка: некорректный курс обмена", rate.warehouseItemId);

            if (countToRemove <= 0 || countToRemove % rate.inventoryQty != 0)
                return ExchangeResult.Fail(
                    $"Количество должно быть кратно {rate.inventoryQty}",
                    rate.warehouseItemId);

            if (string.IsNullOrEmpty(locationId))
                return ExchangeResult.Fail("Нет локации рынка", rate.warehouseItemId);

            // --- Подготовка ---
            var invWorld = InventoryWorld.Instance;
            if (invWorld == null)
                return ExchangeResult.Fail("Внутренняя ошибка: инвентарь не инициализирован", rate.warehouseItemId);

            var tradeWorld = TradeWorld.Instance;
            if (tradeWorld == null)
                return ExchangeResult.Fail("Внутренняя ошибка: торговля не инициализирована", rate.warehouseItemId);

            int warehouseDelta = (countToRemove / rate.inventoryQty) * rate.warehouseQty;

            // --- Шаг 1: Забрать из инвентаря ---
            var invResult = invWorld.RemoveItems(clientId, inventoryItemId, itemType, countToRemove);
            if (!invResult.IsSuccess)
            {
                return ExchangeResult.Fail(
                    $"Недостаточно предметов в инвентаре: {invResult.message ?? "ошибка"}",
                    rate.warehouseItemId);
            }

            // --- Шаг 2: Добавить на склад ---
            var warehouse = tradeWorld.GetOrLoadWarehouse(clientId, locationId);
            if (!warehouse.TryAdd(rate.warehouseItemId, warehouseDelta, tradeWorld.Resolver, out var failReason))
            {
                // ROLLBACK: вернуть предметы в инвентарь
                RollbackAddItems(invWorld, clientId, inventoryItemId, itemType, countToRemove);

                return ExchangeResult.Fail($"Склад не может принять: {failReason}", rate.warehouseItemId);
            }

            // --- Шаг 3: Персист (вызывающий сохранит warehouse через Repository) ---
            return ExchangeResult.Ok(
                $"+{warehouseDelta} '{rate.displayName}' на складе",
                rate.warehouseItemId, warehouseDelta, -countToRemove);
        }

        /// <summary>
        /// Распаковать: склад → инвентарь.
        /// </summary>
        /// <param name="clientId">ID игрока</param>
        /// <param name="locationId">ID локации рынка (для склада)</param>
        /// <param name="rate">Курс обмена</param>
        /// <param name="countToRemove">Сколько коробок забрать со склада (должно быть кратно rate.warehouseQty)</param>
        /// <returns>ExchangeResult</returns>
        public ExchangeResult Unpack(ulong clientId, string locationId,
            ExchangeRateEntry rate, int countToRemove)
        {
            // --- Валидация ---
            if (rate.warehouseQty <= 0 || rate.inventoryQty <= 0)
                return ExchangeResult.Fail("Внутренняя ошибка: некорректный курс обмена", rate.warehouseItemId);

            if (countToRemove <= 0 || countToRemove % rate.warehouseQty != 0)
                return ExchangeResult.Fail(
                    $"Количество должно быть кратно {rate.warehouseQty}",
                    rate.warehouseItemId);

            if (string.IsNullOrEmpty(locationId))
                return ExchangeResult.Fail("Нет локации рынка", rate.warehouseItemId);

            // --- Подготовка ---
            var invWorld = InventoryWorld.Instance;
            if (invWorld == null)
                return ExchangeResult.Fail("Внутренняя ошибка: инвентарь не инициализирован", rate.warehouseItemId);

            var tradeWorld = TradeWorld.Instance;
            if (tradeWorld == null)
                return ExchangeResult.Fail("Внутренняя ошибка: торговля не инициализирована", rate.warehouseItemId);

            int inventoryDelta = (countToRemove / rate.warehouseQty) * rate.inventoryQty;

            // Разрешаем inventoryItemId (itemName → int) через resolver
            int inventoryItemId = _resolver.ResolveInventoryItemId(rate.inventoryItemName);
            if (inventoryItemId <= 0)
                return ExchangeResult.Fail(
                    $"Внутренняя ошибка: предмет '{rate.inventoryItemName}' не найден в БД",
                    rate.warehouseItemId);

            var itemType = _resolver.GetItemType(inventoryItemId);

            // --- Шаг 1: Забрать со склада ---
            var warehouse = tradeWorld.GetOrLoadWarehouse(clientId, locationId);
            if (!warehouse.TryRemove(rate.warehouseItemId, countToRemove, out var whFailReason))
            {
                return ExchangeResult.Fail(
                    $"Недостаточно товара на складе: {whFailReason}",
                    rate.warehouseItemId);
            }

            // --- Шаг 2: Добавить в инвентарь ---
            bool addAllSucceeded = TryAddItemsToInventory(invWorld, clientId, inventoryItemId, itemType, inventoryDelta);
            if (!addAllSucceeded)
            {
                // ROLLBACK: вернуть коробки на склад
                warehouse.TryAdd(rate.warehouseItemId, countToRemove, tradeWorld.Resolver, out _);

                return ExchangeResult.Fail(
                    $"Инвентарь полон: не удалось добавить {inventoryDelta} предметов",
                    rate.warehouseItemId);
            }

            // --- Шаг 3: Персист ---
            return ExchangeResult.Ok(
                $"+{inventoryDelta} '{rate.displayName}' в инвентаре",
                rate.warehouseItemId, -countToRemove, inventoryDelta);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        /// <summary>
        /// Добавить N предметов в инвентарь, по одному за раз.
        /// Если хотя бы один не добавился — откатить все добавленные.
        /// </summary>
        private static bool TryAddItemsToInventory(InventoryWorld invWorld, ulong clientId,
            int itemId, ItemType itemType, int count)
        {
            int added = 0;
            for (int i = 0; i < count; i++)
            {
                var result = invWorld.AddItemDirect(clientId, itemId, itemType);
                if (!result.IsSuccess)
                {
                    // Откатываем то, что уже добавили
                    RollbackAddItems(invWorld, clientId, itemId, itemType, added);
                    return false;
                }
                added++;
            }
            return true;
        }

        /// <summary>
        /// Откат: удалить N штук предмета из инвентаря (для rollback после ошибки склада).
        /// Игнорирует ошибки — если не удалось откатить, логирует Warning.
        /// </summary>
        private static void RollbackAddItems(InventoryWorld invWorld, ulong clientId,
            int itemId, ItemType itemType, int count)
        {
            if (count <= 0) return;
            var rollbackResult = invWorld.RemoveItems(clientId, itemId, itemType, count);
            if (!rollbackResult.IsSuccess)
            {
                Debug.LogWarning($"[ExchangeWorld] Rollback не удался: {rollbackResult.message}");
            }
        }
    }
}
