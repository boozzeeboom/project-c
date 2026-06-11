using System;
using UnityEngine;

namespace ProjectC.Trade.Config
{
    /// <summary>
    /// Одна запись курса обмена: N пикаблов (inventory) = M коробок (warehouse).
    /// Симметричный курс — pack/unpack по одному и тому же соотношению.
    ///
    /// Пример:
    ///   warehouseItemId = "resource_iron_box"
    ///   inventoryItemName = "Железная руда"  (совпадает с ItemData.itemName)
    ///   inventoryQty = 100   (100 единиц в инвентаре)
    ///   warehouseQty = 1     (1 коробка на складе)
    ///
    /// T-E01 (2026-06-11): базовая конфигурация. rate пока симметричный,
    /// без комиссии — комиссия появится в T-E02 как отдельный параметр.
    /// </summary>
    [Serializable]
    public struct ExchangeRateEntry
    {
        [Header("Warehouse (market) side")]
        [Tooltip("itemId товара на складе рынка (TradeItemDefinition.itemId)")]
        public string warehouseItemId;

        [Tooltip("Сколько коробок на складе за одну операцию")]
        public int warehouseQty;

        [Header("Inventory (pickup) side")]
        [Tooltip("itemName пикабла (ItemData.itemName) в инвентаре")]
        public string inventoryItemName;

        [Tooltip("Сколько пикаблов в инвентаре за одну операцию")]
        public int inventoryQty;

        [Header("Display")]
        [Tooltip("Отображаемое название операции (напр. 'Упаковать руду')")]
        public string displayName;
    }
}
