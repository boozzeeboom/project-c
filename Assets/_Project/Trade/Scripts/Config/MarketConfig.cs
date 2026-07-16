using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade.Config
{
    /// <summary>
    /// ScriptableObject — конфигурация рынка конкретной локации.
    /// READ-ONLY на сервере: хранит только базовые параметры (id, набор товаров).
    /// Runtime-состояние (текущие цены, сток, спрос) — в <see cref="ProjectC.Trade.Core.MarketState"/>.
    ///
    /// Заменяет старый <c>LocationMarket</c>, который держал mutable state в SO и терял его
    /// при рестарте сцены / перезагрузке Addressables.
    /// </summary>
    public enum MarketTradeMode
    {
        BuyAndSell = 0,
        BuyOnly     = 1,
        SellOnly    = 2
    }

    [CreateAssetMenu(fileName = "MarketConfig_", menuName = "ProjectC/Trade/Market Config")]
    public class MarketConfig : ScriptableObject
    {
        [Header("Location")]
        [Tooltip("Уникальный id локации: primium / secundus / tertius / quartus")]
        public string locationId = "";

        [Tooltip("Отображаемое имя (например 'Примум')")]
        public string displayName = "";

        [TextArea(2, 4)]
        [Tooltip("Описание (для UI / лора)")]
        public string description = "";

        [Header("Trade Mode")]
        [Tooltip("Режим торговли: BuyAndSell / BuyOnly / SellOnly")]
        public MarketTradeMode tradeMode = MarketTradeMode.BuyAndSell;

        [Tooltip("Если true — рынок скупает ЛЮБОЙ товар по цене из GlobalBuyPriceConfig")]
        public bool buyAnyItem = false;

        [Tooltip("Ссылка на глобальные цены скупки (используется при buyAnyItem=true)")]
        public GlobalBuyPriceConfig globalBuyPriceConfig;

        [Header("Commissions")]
        [Tooltip("Доля от цены, получаемая игроком при ПРОДАЖЕ на рынок (0..1). 0.8 = 80%")]
        [Range(0f, 1f)]
        public float sellCommission = 0.8f;

        [Tooltip("Множитель к цене при ПОКУПКЕ у рынка. 1.0 = без наценки, 1.2 = +20%")]
        [Range(1f, 2f)]
        public float buyCommission = 1.0f;

        [Header("Price Corridor")]
        [Tooltip("Минимальная цена относительно basePrice. 0.5 = не ниже 50% от базовой")]
        [Range(0.1f, 1f)]
        public float priceFloorRatio = 0.5f;

        [Tooltip("Максимальная цена относительно basePrice. 5.0 = не выше 500% от базовой")]
        [Range(1f, 10f)]
        public float priceCeilingRatio = 5.0f;

        [Tooltip("Полупериод затухания demand/supply в секундах. 1800 = 30 мин")]
        [Range(60f, 86400f)]
        public float decayHalfLifeSeconds = 1800f;

        [Header("Items")]
        [Tooltip("Конфигурации товаров этого рынка. Runtime-состояние в MarketState.")]
        public List<MarketItemConfig> items = new List<MarketItemConfig>();

        public MarketItemConfig GetItemConfig(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || items == null) return null;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].itemId == itemId) return items[i];
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(locationId))
                locationId = locationId.ToUpperInvariant();
        }
#endif
    }
}
