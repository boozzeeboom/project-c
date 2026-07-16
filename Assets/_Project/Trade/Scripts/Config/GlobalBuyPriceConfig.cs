using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade.Config
{
    /// <summary>
    /// Глобальный конфиг цен скупки. Используется когда MarketConfig.buyAnyItem = true.
    /// Содержит фиксированную цену скупки для каждого товара — рынок принимает
    /// любой товар по этой цене, независимо от собственного items[].
    /// </summary>
    [CreateAssetMenu(fileName = "GlobalBuyPriceConfig", menuName = "ProjectC/Trade/Global Buy Price Config")]
    public class GlobalBuyPriceConfig : ScriptableObject
    {
        [Tooltip("Цены скупки для всех товаров, которые рынок может принимать.")]
        public List<GlobalBuyPriceEntry> entries = new List<GlobalBuyPriceEntry>();

        public float GetBuyPrice(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || entries == null) return -1f;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].itemId == itemId)
                    return entries[i].buyPrice;
            }
            return -1f;
        }
    }

    [Serializable]
    public class GlobalBuyPriceEntry
    {
        [Tooltip("Уникальный id товара (как в TradeItemDefinition.itemId)")]
        public string itemId = "";

        [Tooltip("Ссылка на TradeItemDefinition (для UI: иконка, название)")]
        public TradeItemDefinition definition;

        [Tooltip("Цена скупки за единицу (CR)")]
        public float buyPrice = 1f;
    }
}
