using System;
using System.Collections.Generic;
using ProjectC.Trade.Service;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Серверная абстракция NPC-трейдера. Перемещает товары между рынками
    /// каждый тик, создавая базовый поток и стабилизируя экономику.
    ///
    /// Портировано из старого NPCTrader.cs с адаптацией (T-X1 rename → MarketTrader):
    ///   • использует <see cref="MarketState"/> (POCO) вместо LocationMarket (SO со state)
    ///   • условия вынесены в читабельный enum
    /// </summary>
    [Serializable]
    public class MarketTrader
    {
        [Header("Identity")]
        public string traderId = "";
        public string traderName = "";

        [Header("Route")]
        public string fromLocationId = "";
        public string toLocationId = "";

        [Header("Cargo")]
        public string itemId = "";
        public int minVolumePerTick = 3;
        public int maxVolumePerTick = 8;

        [Header("Condition")]
        public TradeCondition condition = TradeCondition.Always;
        public float conditionValue = 0.3f;

        public bool ShouldTrade(MarketState fromMarket, MarketState toMarket)
        {
            if (fromMarket == null || toMarket == null) return false;
            var fromItem = fromMarket.GetItem(itemId);
            var toItem = toMarket.GetItem(itemId);
            if (fromItem == null || toItem == null || fromItem.config == null || toItem.config == null) return false;

            switch (condition)
            {
                case TradeCondition.Always:
                    return true;
                case TradeCondition.SupplyThreshold:
                    return fromItem.supplyFactor >= conditionValue;
                case TradeCondition.PriceThreshold:
                {
                    float basePrice = toItem.config.basePrice;
                    if (basePrice <= 0f) return false;
                    return (toItem.currentPrice / basePrice) >= conditionValue;
                }
                case TradeCondition.DemandThreshold:
                    return toItem.demandFactor >= conditionValue;
                default:
                    return true;
            }
        }

        public void ExecuteTrade(IReadOnlyDictionary<string, MarketState> markets)
        {
            if (!markets.TryGetValue(fromLocationId, out var fromMarket) ||
                !markets.TryGetValue(toLocationId, out var toMarket))
            {
                Debug.LogWarning($"[MarketTrader {traderName}] Маршрут не найден: {fromLocationId} → {toLocationId}");
                return;
            }

            var fromItem = fromMarket.GetItem(itemId);
            var toItem = toMarket.GetItem(itemId);
            if (fromItem == null || toItem == null) return;

            int volume = UnityEngine.Random.Range(minVolumePerTick, maxVolumePerTick + 1);
            if (fromItem.availableStock < volume) volume = Mathf.Max(1, fromItem.availableStock);

            // "покупает" у источника → спрос ↑, сток ↓
            fromItem.availableStock -= volume;
            fromItem.demandFactor = Mathf.Clamp(
                fromItem.demandFactor + volume * PriceFormula.DEMAND_PER_UNIT_BOUGHT,
                PriceFormula.DEMAND_MIN, PriceFormula.DEMAND_MAX);
            fromItem.version++;
            fromItem.RecalculatePrice();

            // "продаёт" в назначении → предложение ↑, сток ↑
            toItem.availableStock += volume;
            toItem.supplyFactor = Mathf.Clamp(
                toItem.supplyFactor + volume * PriceFormula.SUPPLY_PER_UNIT_SOLD,
                PriceFormula.SUPPLY_MIN, PriceFormula.SUPPLY_MAX);
            toItem.version++;
            toItem.RecalculatePrice();
        }

        public static MarketTrader CreateDefault(
            string id, string name, string fromLoc, string toLoc, string item,
            int minV, int maxV, TradeCondition cond = TradeCondition.Always, float condVal = 0.3f)
        {
            return new MarketTrader
            {
                traderId = id,
                traderName = name,
                fromLocationId = fromLoc,
                toLocationId = toLoc,
                itemId = item,
                minVolumePerTick = minV,
                maxVolumePerTick = maxV,
                condition = cond,
                conditionValue = condVal
            };
        }
    }

    public enum TradeCondition
    {
        Always,
        SupplyThreshold,
        PriceThreshold,
        DemandThreshold
    }
}
