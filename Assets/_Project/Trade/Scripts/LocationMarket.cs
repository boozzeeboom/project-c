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
                {
                    marketItem.InitFromItem();
                    // Сессия 8E: Clamp факторов при загрузке — защита от Inspector-значений > 1.5
                    marketItem.demandFactor = Mathf.Clamp(marketItem.demandFactor, 0f, 1.5f);
                    marketItem.supplyFactor = Mathf.Clamp(marketItem.supplyFactor, 0f, 1.5f);
                }
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
        /// Получить MarketItem по ID
        /// </summary>
        public MarketItem GetItem(string itemId)
        {
            return items.Find(m => m.item != null && m.item.itemId == itemId);
        }

        /// <summary>
        /// Обновить спрос для товара (int quantity)
        /// </summary>
        public void UpdateDemand(string itemId, int quantity)
        {
            var marketItem = items.Find(m => m.item != null && m.item.itemId == itemId);
            if (marketItem != null)
                marketItem.UpdateDemand(quantity);
        }

        /// <summary>
        /// Обновить спрос для товара (float delta — для серверной торговли)
        /// </summary>
        public void UpdateDemand(string itemId, float delta)
        {
            var marketItem = items.Find(m => m.item != null && m.item.itemId == itemId);
            if (marketItem != null)
            {
                marketItem.demandFactor = Mathf.Clamp(marketItem.demandFactor + delta, 0f, 1.5f);
                marketItem.RecalculatePrice();
            }
        }

        /// <summary>
        /// Обновить предложение для товара (int quantity)
        /// </summary>
        public void UpdateSupply(string itemId, int quantity)
        {
            var marketItem = items.Find(m => m.item != null && m.item.itemId == itemId);
            if (marketItem != null)
                marketItem.UpdateSupply(quantity);
        }

        /// <summary>
        /// Обновить предложение для товара (float delta — для серверной торговли)
        /// </summary>
        public void UpdateSupply(string itemId, float delta)
        {
            var marketItem = items.Find(m => m.item != null && m.item.itemId == itemId);
            if (marketItem != null)
            {
                marketItem.supplyFactor = Mathf.Clamp(marketItem.supplyFactor + delta, 0f, 1.5f);
                marketItem.RecalculatePrice();
            }
        }

        /// <summary>
        /// Затухание спроса/предложения — ELASTIC "КАЧЕЛИ" (Сессия 6)
        /// + пассивная регенерация стока (+2% от initialStock за тик)
        /// </summary>
        public void DecaySupplyAndDemand(float decayRate = 0.92f, float elasticStrength = 0.08f)
        {
            foreach (var marketItem in items)
            {
                if (marketItem != null)
                {
                    // Пассивная регенерация стока: +2% от базового за тик
                    int regenAmount = Mathf.Max(1, Mathf.RoundToInt(marketItem.initialStock * 0.02f));
                    if (marketItem.availableStock < marketItem.initialStock)
                    {
                        marketItem.availableStock += regenAmount;
                        marketItem.isDirty = true;
                    }

                    marketItem.DecayFactors(decayRate, elasticStrength);
                }
            }
        }

        /// <summary>
        /// Получить список изменённых предметов (для delta-отправки)
        /// </summary>
        public List<MarketItem> GetDirtyItems()
        {
            var dirty = new List<MarketItem>();
            foreach (var marketItem in items)
            {
                if (marketItem != null && marketItem.isDirty)
                    dirty.Add(marketItem);
            }
            return dirty;
        }

        /// <summary>
        /// Сбросить isDirty у всех предметов
        /// </summary>
        public void ClearDirtyFlags()
        {
            foreach (var marketItem in items)
            {
                if (marketItem != null)
                    marketItem.isDirty = false;
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
