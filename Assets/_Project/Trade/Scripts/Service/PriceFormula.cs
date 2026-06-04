using ProjectC.Trade.Core;
using UnityEngine;

namespace ProjectC.Trade.Service
{
    /// <summary>
    /// Формула цены + time-based decay. Выделено в static helpers, чтобы
    /// использовать и в TradeWorld (POCO, server-only), и в тестах, и в
    /// возможных диагностических UI.
    ///
    /// Заменяет логику из старого MarketItem.cs (которая была в SO со state).
    /// </summary>
    public static class PriceFormula
    {
        // Допустимые диапазоны факторов (те же, что в старом MarketItem после 8E)
        public const float DEMAND_MIN = 0f;
        public const float DEMAND_MAX = 1.5f;
        public const float SUPPLY_MIN = 0f;
        public const float SUPPLY_MAX = 1.5f;

        // Допустимые диапазоны множителей
        public const float EVENT_MULT_MIN = 0.5f;
        public const float EVENT_MULT_MAX = 2.0f;

        // Клемпинг цены
        public const float PRICE_FLOOR_RATIO = 0.5f;   // ≥ 50% от basePrice
        public const float PRICE_CEILING_RATIO = 5.0f; // ≤ 500% от basePrice

        // Влияние игрока на спрос/предложение (GDD_25 секция 5.4)
        public const float DEMAND_PER_UNIT_BOUGHT = 0.02f;
        public const float SUPPLY_PER_UNIT_SOLD = 0.02f;

        // Time-based decay (полупериод 30 минут)
        public const float DEFAULT_HALF_LIFE_SECONDS = 1800f;

        /// <summary>
        /// Рассчитать цену с клемпингом.
        /// </summary>
        public static float CalculatePrice(float basePrice, float demand, float supply, float eventMult)
        {
            if (basePrice <= 0f) return 0f;

            demand = Mathf.Clamp(demand, DEMAND_MIN, DEMAND_MAX);
            supply = Mathf.Clamp(supply, SUPPLY_MIN, SUPPLY_MAX);
            eventMult = Mathf.Clamp(eventMult, EVENT_MULT_MIN, EVENT_MULT_MAX);

            float price = basePrice * (1f + demand - supply) * eventMult;
            float floor = basePrice * PRICE_FLOOR_RATIO;
            float ceiling = basePrice * PRICE_CEILING_RATIO;
            return Mathf.Clamp(price, floor, ceiling);
        }

        /// <summary>
        /// Time-based экспоненциальное затухание фактора.
        /// Возвращает factor * exp(-k*dt) — при dt=halfLife возвращает factor/2.
        /// </summary>
        public static float DecayFactor(float factor, float dtSeconds, float halfLifeSeconds = DEFAULT_HALF_LIFE_SECONDS)
        {
            if (factor <= 0f || dtSeconds <= 0f || halfLifeSeconds <= 0f) return factor;
            float k = Mathf.Log(2f) / halfLifeSeconds;
            float result = factor * Mathf.Exp(-k * dtSeconds);
            // Обнуляем мелочь, чтоб не было дрейфа
            if (result < 0.001f) return 0f;
            return result;
        }

        /// <summary>
        /// Применить покупку: demand растёт, цена пересчитывается.
        /// </summary>
        public static void ApplyBuy(MarketItemState s, int quantity)
        {
            if (s == null || quantity <= 0) return;
            s.demandFactor = Mathf.Clamp(s.demandFactor + quantity * DEMAND_PER_UNIT_BOUGHT, DEMAND_MIN, DEMAND_MAX);
            s.version++;
            s.RecalculatePrice();
        }

        /// <summary>
        /// Применить продажу: supply растёт, сток растёт, цена пересчитывается.
        /// </summary>
        public static void ApplySell(MarketItemState s, int quantity)
        {
            if (s == null || quantity <= 0) return;
            s.supplyFactor = Mathf.Clamp(s.supplyFactor + quantity * SUPPLY_PER_UNIT_SOLD, SUPPLY_MIN, SUPPLY_MAX);
            s.availableStock += quantity;
            s.version++;
            s.RecalculatePrice();
        }

        /// <summary>
        /// Пассивная регенерация стока (вызывается каждый tick).
        /// </summary>
        public static void RegenerateStock(MarketItemState s)
        {
            if (s == null || s.config == null) return;
            int cap = s.config.initialStock;
            if (s.availableStock >= cap) return;
            int add = Mathf.Max(1, Mathf.RoundToInt(cap * Mathf.Clamp01(s.config.regenPerTick)));
            s.availableStock = Mathf.Min(cap, s.availableStock + add);
            s.version++;
        }
    }
}
