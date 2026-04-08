using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Trade;

namespace ProjectC.Trade
{
    /// <summary>
    /// NPC-трейдер — серверная абстракция (НЕ NetworkBehaviour).
    /// Каждый тик автоматически перемещает товары между локациями,
    /// создавая базовый поток товаров и стабилизируя рынок.
    /// Сессия 6: Tick-система + динамическая экономика.
    /// </summary>
    [Serializable]
    public class NPCTrader
    {
        [Header("Trader Info")]
        [Tooltip("Уникальный ID трейдера")]
        public string traderId;

        [Tooltip("Отображаемое имя")]
        public string traderName;

        [Header("Route")]
        [Tooltip("ID локации отправления")]
        public string fromLocationId;

        [Tooltip("ID локации назначения")]
        public string toLocationId;

        [Header("Cargo")]
        [Tooltip("ID перевозимого товара")]
        public string itemId;

        [Tooltip("Минимальный объём за тик")]
        public int minVolumePerTick = 3;

        [Tooltip("Максимальный объём за тик")]
        public int maxVolumePerTick = 8;

        [Header("Trading Logic")]
        [Tooltip("Тип условия торговли")]
        public TradeCondition condition = TradeCondition.Always;

        [Tooltip("Порог для условной торговли (supplyFactor или priceMultiplier)")]
        public float conditionValue = 0.3f;

        /// <summary>
        /// Проверить, должен ли трейдер торговать в этом тике
        /// </summary>
        public bool ShouldTrade(LocationMarket fromMarket, LocationMarket toMarket)
        {
            if (fromMarket == null || toMarket == null) return false;

            var fromItem = fromMarket.GetItem(itemId);
            var toItem = toMarket.GetItem(itemId);

            if (fromItem == null || toItem == null) return false;

            switch (condition)
            {
                case TradeCondition.Always:
                    return true;

                case TradeCondition.SupplyThreshold:
                    // Торгуем только если в локации отправления избыток товара
                    return fromItem.supplyFactor >= conditionValue;

                case TradeCondition.PriceThreshold:
                    // Торгуем только если цена в локации назначения выше базовой × conditionValue
                    float priceRatio = toItem.currentPrice / toItem.basePrice;
                    return priceRatio >= conditionValue;

                case TradeCondition.DemandThreshold:
                    // Торгуем только если в локации назначения высокий спрос
                    return toItem.demandFactor >= conditionValue;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Получить случайный объём груза для этого тика
        /// </summary>
        public int GetVolumeForTick()
        {
            return UnityEngine.Random.Range(minVolumePerTick, maxVolumePerTick + 1);
        }

        /// <summary>
        /// Выполнить торговую операцию:
        /// 1. В локации отправления: NPC "покупает" → demandFactor растёт
        /// 2. В локации назначения: NPC "продаёт" → supplyFactor растёт, stock растёт
        /// </summary>
        public void ExecuteTrade(Dictionary<string, LocationMarket> markets)
        {
            if (!markets.ContainsKey(fromLocationId) || !markets.ContainsKey(toLocationId))
            {
                Debug.LogWarning($"[NPCTrader {traderName}] Маршрут не найден: {fromLocationId} → {toLocationId}");
                return;
            }

            var fromMarket = markets[fromLocationId];
            var toMarket = markets[toLocationId];

            var fromItem = fromMarket.GetItem(itemId);
            var toItem = toMarket.GetItem(itemId);

            if (fromItem == null || toItem == null) return;

            int volume = GetVolumeForTick();

            // Ограничение: нельзя забрать больше чем есть в стоке
            if (fromItem.availableStock < volume)
            {
                volume = Mathf.Max(1, fromItem.availableStock);
            }

            // Локация отправления: NPC "покупает" у рынка
            fromItem.availableStock -= volume;
            fromMarket.UpdateDemand(itemId, volume * 0.02f);

            // Локация назначения: NPC "продаёт" на рынок
            toItem.availableStock += volume;
            toMarket.UpdateSupply(itemId, volume * 0.02f);
        }

        /// <summary>
        /// Создать трейдера по умолчанию (для инициализации в TradeMarketServer)
        /// </summary>
        public static NPCTrader CreateDefault(string traderId, string traderName,
            string fromLocation, string toLocation, string itemId,
            int minVol, int maxVol, TradeCondition condition = TradeCondition.Always,
            float conditionValue = 0.3f)
        {
            return new NPCTrader
            {
                traderId = traderId,
                traderName = traderName,
                fromLocationId = fromLocation,
                toLocationId = toLocation,
                itemId = itemId,
                minVolumePerTick = minVol,
                maxVolumePerTick = maxVol,
                condition = condition,
                conditionValue = conditionValue
            };
        }
    }

    /// <summary>
    /// Условия торговли для NPC-трейдеров
    /// </summary>
    public enum TradeCondition
    {
        /// <summary>Торговать всегда каждый тик</summary>
        Always,

        /// <summary>Торговать только если supplyFactor >= conditionValue в локации отправления</summary>
        SupplyThreshold,

        /// <summary>Торговать только если цена/базовая >= conditionValue в локации назначения</summary>
        PriceThreshold,

        /// <summary>Торговать только если demandFactor >= conditionValue в локации назначения</summary>
        DemandThreshold
    }
}
