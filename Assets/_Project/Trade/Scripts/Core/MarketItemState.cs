using ProjectC.Trade.Config;
using ProjectC.Trade.Service;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Серверное runtime-состояние одной позиции рынка. POCO.
    ///
    /// Содержит:
    ///   • availableStock — текущий сток
    ///   • currentPrice — текущая цена (рассчитывается по формуле)
    ///   • demandFactor / supplyFactor — динамические факторы 0..1.5
    ///   • eventMultiplier — глобальный множитель события 0.5..2.0
    ///   • version — инкремент на каждое изменение (для delta-sync)
    ///
    /// Использует <see cref="PriceFormula"/> для расчёта цены (time-based decay, клемпинг).
    /// </summary>
    public class MarketItemState
    {
        // --- Статическая часть (из MarketItemConfig) ---
        public readonly MarketItemConfig config;
        public string ItemId => config != null ? config.itemId : "";

        // --- Динамическая часть ---
        public int availableStock;
        public float currentPrice;
        public float demandFactor;
        public float supplyFactor;
        public float eventMultiplier = 1f;
        public int version;

        public MarketItemState(MarketItemConfig config)
        {
            this.config = config;
        }

        /// <summary>
        /// Пересчитать цену по формуле с клемпингом.
        /// </summary>
        public void RecalculatePrice()
        {
            currentPrice = PriceFormula.CalculatePrice(
                config != null ? config.basePrice : 0f,
                demandFactor,
                supplyFactor,
                eventMultiplier);
        }
    }
}
