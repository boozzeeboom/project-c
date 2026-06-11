using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade.Config
{
    /// <summary>
    /// ScriptableObject — конфигурация курсов обмена для Resources Exchanger.
    /// READ-ONLY на сервере. Содержит список пар (warehouseItemId ↔ inventoryItemName)
    /// с симметричным курсом.
    ///
    /// T-E01 (2026-06-11): один ассет на проект. В будущем можно разделять
    /// по локациям (MarketConfig.locationId → свой ExchangeRateConfig).
    ///
    /// Создаётся через меню: ProjectC/Trade/Exchange Rate Config.
    /// </summary>
    [CreateAssetMenu(fileName = "ExchangeRateConfig_", menuName = "ProjectC/Trade/Exchange Rate Config")]
    public class ExchangeRateConfig : ScriptableObject
    {
        [Header("Exchange Rates")]
        [Tooltip("Список пар: warehouse item ↔ inventory item. Проверяется в порядке списка.")]
        public List<ExchangeRateEntry> rates = new List<ExchangeRateEntry>();

        /// <summary>
        /// Найти запись по warehouseItemId.
        /// </summary>
        public ExchangeRateEntry? FindByWarehouseItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || rates == null) return null;
            for (int i = 0; i < rates.Count; i++)
            {
                if (rates[i].warehouseItemId == itemId)
                    return rates[i];
            }
            return null;
        }

        /// <summary>
        /// Найти запись по inventoryItemName.
        /// </summary>
        public ExchangeRateEntry? FindByInventoryItemName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || rates == null) return null;
            for (int i = 0; i < rates.Count; i++)
            {
                if (rates[i].inventoryItemName == itemName)
                    return rates[i];
            }
            return null;
        }
    }
}
