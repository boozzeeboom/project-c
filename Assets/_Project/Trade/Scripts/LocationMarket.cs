using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Trade;

namespace ProjectC.Trade
{
    /// <summary>
    /// ScriptableObject рынка локации — содержит цены и наличие товаров.
    /// Каждая локация (город, платформа, анклав) имеет свой рынок.
    /// Цены динамические — обновляются через MarketItem.RecalculatePrice().
    /// </summary>
    [CreateAssetMenu(fileName = "Market_", menuName = "ProjectC/Trade/Location Market")]
    public class LocationMarket : ScriptableObject
    {
        [Header("Location Info")]
        [Tooltip("ID локации (primium, secundus, tertius, quartus)")]
        public string locationId;

        [Tooltip("Название локации")]
        public string locationName;

        [Header("Market Items")]
        [Tooltip("Список товаров рынка")]
        public List<MarketItem> items = new List<MarketItem>();

        /// <summary>
        /// Инициализировать все предметы из их TradeItemDefinition
        /// </summary>
        public void InitItems()
        {
            foreach (var marketItem in items)
            {
                if (marketItem != null)
                    marketItem.InitFromItem();
            }
        }

        /// <summary>
        /// Пересчитать цены всех товаров
        /// </summary>
        public void RecalculatePrices()
        {
            foreach (var marketItem in items)
            {
                if (marketItem != null)
                    marketItem.RecalculatePrice();
            }
        }

        /// <summary>
        /// Получить цену товара в этой локации
        /// </summary>
        public float GetPrice(string itemId)
        {
            var marketItem = items.Find(m => m.item != null && m.item.itemId == itemId);
            return marketItem?.currentPrice ?? 0f;
        }

        /// <summary>
        /// Получить доступный сток товара
        /// </summary>
        public int GetStock(string itemId)
        {
            var marketItem = items.Find(m => m.item != null && m.item.itemId == itemId);
            return marketItem?.availableStock ?? 0;
        }

        /// <summary>
        /// Обновить спрос для товара
        /// </summary>
        public void UpdateDemand(string itemId, int quantity)
        {
            var marketItem = items.Find(m => m.item != null && m.item.itemId == itemId);
            if (marketItem != null)
                marketItem.UpdateDemand(quantity);
        }

        /// <summary>
        /// Обновить предложение для товара
        /// </summary>
        public void UpdateSupply(string itemId, int quantity)
        {
            var marketItem = items.Find(m => m.item != null && m.item.itemId == itemId);
            if (marketItem != null)
                marketItem.UpdateSupply(quantity);
        }

        /// <summary>
        /// Затухание спроса/предложения (tick-система)
        /// </summary>
        public void DecaySupplyAndDemand(float decayRate = 0.95f)
        {
            foreach (var marketItem in items)
            {
                if (marketItem != null)
                    marketItem.DecayFactors(decayRate);
            }
        }

        private void OnValidate()
        {
            // Авто-инициализация при изменении в инспекторе
            foreach (var marketItem in items)
            {
                if (marketItem != null)
                {
                    if (marketItem.item != null && marketItem.basePrice == 0f)
                        marketItem.InitFromItem();
                    if (marketItem.item != null && marketItem.currentPrice == 0f)
                        marketItem.RecalculatePrice();
                }
            }
        }

        private void OnEnable()
        {
            // Переинициализация при загрузке
            InitItems();
        }
    }
}
