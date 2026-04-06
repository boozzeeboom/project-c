using System;
using UnityEngine;
using ProjectC.Trade;

namespace ProjectC.Trade
{
    /// <summary>
    /// Данные конкретного предмета в рынке локации.
    /// Serializable — хранится внутри LocationMarket.
    /// Формула цены (GDD_22 секция 4):
    ///   currentPrice = basePrice × (1 + demandFactor - supplyFactor)
    /// </summary>
    [Serializable]
    public class MarketItem
    {
        [Tooltip("Ссылка на определение товара")]
        public TradeItemDefinition item;

        [Header("Auto-calculated")]
        [Tooltip("Базовая цена (копируется из item.basePrice)")]
        public float basePrice;

        [Tooltip("Текущая цена (вычисляется по формуле)")]
        public float currentPrice;

        [Tooltip("Доступный сток (сколько NPC может продать)")]
        public int availableStock = 50;

        [Tooltip("Фактор спроса: 0.0 … 1.5 (растёт при покупках)")]
        [Range(0f, 1.5f)]
        public float demandFactor;

        [Tooltip("Фактор предложения: 0.0 … 1.5 (растёт при продажах)")]
        [Range(0f, 1.5f)]
        public float supplyFactor;

        /// <summary>
        /// Инициализация из TradeItemDefinition
        /// </summary>
        public void InitFromItem()
        {
            if (item != null)
            {
                basePrice = item.basePrice;
                if (currentPrice == 0f)
                    currentPrice = basePrice;
            }
        }

        /// <summary>
        /// Пересчёт текущей цены по формуле
        /// </summary>
        public void RecalculatePrice()
        {
            if (item == null) return;

            basePrice = item.basePrice;
            currentPrice = basePrice * (1f + demandFactor - supplyFactor);

            // Ограничение максимума ×5 (anti-exploit GDD_22 секция 10)
            float maxPrice = basePrice * 5f;
            currentPrice = Mathf.Min(currentPrice, maxPrice);

            // Минимум — половина базовой цены
            float minPrice = basePrice * 0.5f;
            currentPrice = Mathf.Max(currentPrice, minPrice);
        }

        /// <summary>
        /// Обновить спрос (покупка игроком)
        /// demandFactor += quantity × 0.02 (GDD_25 секция 5.4)
        /// </summary>
        public void UpdateDemand(int quantity)
        {
            demandFactor += quantity * 0.02f;
            demandFactor = Mathf.Clamp(demandFactor, 0f, 1.5f);
            RecalculatePrice();
        }

        /// <summary>
        /// Обновить предложение (продажа игроком)
        /// supplyFactor += quantity × 0.02
        /// </summary>
        public void UpdateSupply(int quantity)
        {
            supplyFactor += quantity * 0.02f;
            supplyFactor = Mathf.Clamp(supplyFactor, 0f, 1.5f);
            availableStock += quantity;
            RecalculatePrice();
        }

        /// <summary>
        /// Затухание спроса/предложения (GDD_25 секция 5.2)
        /// factor *= 0.95 каждый тик
        /// </summary>
        public void DecayFactors(float decayRate = 0.95f)
        {
            demandFactor *= decayRate;
            supplyFactor *= decayRate;
            // Округляем до 3 знаков чтобы избежать дрейфа
            demandFactor = Mathf.Round(demandFactor * 1000f) / 1000f;
            supplyFactor = Mathf.Round(supplyFactor * 1000f) / 1000f;
            RecalculatePrice();
        }
    }
}
