using ProjectC.Items;
using ProjectC.Trade.Config;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// T-E01: Слой маппинга между Inventory (pickable items) и Warehouse (market items).
    ///
    /// НЕ содержит состояние. НЕ делает мутации.
    /// Только: найти пару, проверить кратность, посчитать количество.
    ///
    /// Использует существующий API InventoryWorld (CountOf, GetItemDefinition)
    /// и TradeWorld (GetOrLoadWarehouse, TryAdd/TryRemove) — НЕ ломает существующий код.
    ///
    /// T-E01 (2026-06-11): RateEntry.inventoryItemName маппится на ItemData.itemName.
    /// ID lookup — последовательный перебор _itemDatabase через GetItemDefinition+GetItemCount,
    /// zero-touch к InventoryWorld.
    /// </summary>
    public class ResourceExchangeResolver
    {
        // T-E04: Default instance для клиентского UI (auto-load из Resources)
        private static ResourceExchangeResolver _defaultInstance;

        /// <summary>
        /// T-E04: Default инстанс для клиентского UI. Загружает ExchangeRateConfig
        /// из Resources/Exchange/DefaultExchangeRate. Если ассет не создан — возвращает null.
        /// </summary>
        public static ResourceExchangeResolver Default
        {
            get
            {
                if (_defaultInstance == null)
                {
                    var cfg = Resources.Load<ExchangeRateConfig>("Exchange/DefaultExchangeRate");
                    if (cfg != null)
                        _defaultInstance = new ResourceExchangeResolver(cfg);
                }
                return _defaultInstance;
            }
        }

        private readonly ExchangeRateConfig _config;

        public ResourceExchangeResolver(ExchangeRateConfig config)
        {
            _config = config;
        }

        // ============================================================
        // LOOKUP
        // ============================================================

        /// <summary>
        /// Найти RateEntry для PACK (инвентарь → склад).
        /// Ищет в ExchangeRateConfig по inventoryItemName == itemData.itemName.
        /// </summary>
        public ExchangeRateEntry? FindRateForItemName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || _config?.rates == null)
                return null;
            return _config.FindByInventoryItemName(itemName);
        }

        /// <summary>
        /// Найти RateEntry для UNPACK (склад → инвентарь).
        /// Ищет в ExchangeRateConfig по warehouseItemId.
        /// </summary>
        public ExchangeRateEntry? FindRateForWarehouseItem(string warehouseItemId)
        {
            if (string.IsNullOrEmpty(warehouseItemId) || _config == null)
                return null;
            return _config.FindByWarehouseItemId(warehouseItemId);
        }

        // ============================================================
        // ID RESOLUTION: inventory itemName → int itemId
        // ============================================================

        /// <summary>
        /// Найти int itemId в InventoryWorld.ItemDatabase по ItemData.itemName.
        /// Zero-touch: использует GetItemCount + GetItemDefinition (уже public).
        /// Возвращает 0 если не найден (0 = invalid).
        /// </summary>
        public int ResolveInventoryItemId(string itemName)
        {
            var world = InventoryWorld.Instance;
            if (world == null) return 0;
            if (string.IsNullOrEmpty(itemName)) return 0;

            string trimmedName = itemName.Trim();
            foreach (var kvp in world.GetAllItems())
            {
                if (kvp.Value != null && kvp.Value.itemName != null && kvp.Value.itemName.Trim() == trimmedName)
                    return kvp.Key;
            }

            // DIAG: если не нашли — логируем
            Debug.LogWarning($"[ResourceExchangeResolver] ResolveInventoryItemId не нашёл '{itemName}' среди {world.GetItemCount()} предметов");
            return 0;
        }

        /// <summary>
        /// Получить ItemData.itemName по itemId.
        /// </summary>
        public string GetInventoryItemName(int itemId)
        {
            var world = InventoryWorld.Instance;
            if (world == null) return null;
            var def = world.GetItemDefinition(itemId);
            return def?.itemName;
        }

        // ============================================================
        // QUANTITY MATH
        // ============================================================

        /// <summary>
        /// Сколько полных упаковок (PACK) может сделать игрок?
        /// Использует InventoryWorld.CountOf — уже public.
        /// </summary>
        public int CalcMaxPack(ulong clientId, int itemId, ExchangeRateEntry rate)
        {
            var world = InventoryWorld.Instance;
            if (world == null) return 0;

            int haveCount = world.CountOf(clientId, itemId);
            if (haveCount < rate.inventoryQty) return 0;

            return haveCount / rate.inventoryQty;
        }

        /// <summary>
        /// Вычислить количества для PACK-операции инвентарь → склад.
        /// </summary>
        public (int takeFromInventory, int addToWarehouse) CalcPackAmounts(
            int playerHasCount, ExchangeRateEntry rate)
        {
            if (playerHasCount < rate.inventoryQty)
                return (0, 0);

            int packs = playerHasCount / rate.inventoryQty;
            return (packs * rate.inventoryQty, packs * rate.warehouseQty);
        }

        /// <summary>
        /// Вычислить количества для UNPACK-операции склад → инвентарь.
        /// </summary>
        public (int takeFromWarehouse, int addToInventory) CalcUnpackAmounts(
            int warehouseHasCount, ExchangeRateEntry rate)
        {
            if (warehouseHasCount < rate.warehouseQty)
                return (0, 0);

            int boxes = warehouseHasCount / rate.warehouseQty;
            return (boxes * rate.warehouseQty, boxes * rate.inventoryQty);
        }

        // ============================================================
        // ITEM TYPE INFERENCE
        // ============================================================

        /// <summary>
        /// Получить ItemType для itemId из InventoryWorld.
        /// </summary>
        public ItemType GetItemType(int itemId)
        {
            var world = InventoryWorld.Instance;
            if (world == null) return ItemType.Resources;
            var def = world.GetItemDefinition(itemId);
            return def != null ? def.itemType : ItemType.Resources;
        }
    }
}
