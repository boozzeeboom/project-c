using System;
using UnityEngine;
using ProjectC.Trade;

namespace ProjectC.Trade
{
    /// <summary>
    /// Данные конкретного предмета в рынке локации.
    /// Serializable — хранится внутри LocationMarket.
    /// Формула цены (GDD_25 секция 5.1):
    ///   currentPrice = basePrice × (1 + demandFactor - supplyFactor) × eventMultiplier
    /// Сессия 6: добавлен elastic decay, eventMultiplier, isDirty для delta-отправки.
    /// </summary>
    [Serializable]
    public class MarketItem
    {
        [Tooltip("Ссылка на определение товара")]
        public TradeItemDefinition item;

        [Header("Item ID — для восстановления ссылки")]
        [Tooltip("ID предмета (сохраняется даже если item ссылка теряется)")]
        public string itemId;

        [Header("Stock")]
        [Tooltip("Базовый сток для пассивной регенерации")]
        public int initialStock = 50;

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

        [Header("Session 6: Dynamic Economy")]
        [Tooltip("Множитель от глобальных событий (1.0 = нет события)")]
        public float eventMultiplier = 1f;

        [Tooltip("Флаг изменения для delta-отправки клиентам")]
        public bool isDirty;

        /// <summary>
        /// Инициализация из TradeItemDefinition
        /// </summary>
        public void InitFromItem()
        {
            if (item != null)
            {
                itemId = item.itemId; // Сохраняем ID для восстановления
                basePrice = item.basePrice;
                if (currentPrice == 0f)
                    currentPrice = basePrice;
            }
        }

        /// <summary>
        /// Пересчёт текущей цены по формуле (GDD_25 секция 5.1):
        /// currentPrice = basePrice × (1 + demandFactor - supplyFactor) × eventMultiplier
        /// </summary>
        public void RecalculatePrice()
        {
            // Сессия 8D: Восстановление item ссылки по itemId если она потерялась
            if (item == null && !string.IsNullOrEmpty(itemId))
            {
                var db = FindTradeDatabase();
                if (db != null)
                {
                    item = db.GetItemById(itemId);
                    if (item != null)
                    {
                        basePrice = item.basePrice;
                        Debug.Log($"[MarketItem] Восстановлена ссылка на item: {itemId} (basePrice={basePrice})");
                    }
                }
            }

            if (item == null)
            {
                // Сессия 8D: Если item всё ещё null, используем сохранённый basePrice
                if (basePrice <= 0f)
                {
                    Debug.LogWarning($"[MarketItem] item == null И basePrice=0! itemId={itemId ?? "unknown"}. Не могу пересчитать цену.");
                    currentPrice = 0f;
                    return;
                }
                // Используем сохранённый basePrice — он был установлен при InitFromItem()
                Debug.LogWarning($"[MarketItem] item == null для {itemId ?? "unknown"}, использую сохранённый basePrice={basePrice}");
            }
            else
            {
                basePrice = item.basePrice;
            }

            currentPrice = basePrice * (1f + demandFactor - supplyFactor) * eventMultiplier;

            // Ограничение максимума ×5 (anti-exploit GDD_25 секция 11)
            float maxPrice = basePrice * 5f;
            currentPrice = Mathf.Min(currentPrice, maxPrice);

            // Минимум — половина базовой цены
            float minPrice = basePrice * 0.5f;
            currentPrice = Mathf.Max(currentPrice, minPrice);
        }

        private static TradeDatabase FindTradeDatabase()
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TradeDatabase");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<TradeDatabase>(path);
            }
#endif
            return UnityEngine.Resources.Load<TradeDatabase>("Trade/TradeItemDatabase");
        }

        /// <summary>
        /// Обновить спрос (покупка игроком)
        /// demandFactor += quantity × 0.02 (GDD_25 секция 5.4)
        /// </summary>
        public void UpdateDemand(int quantity)
        {
            demandFactor += quantity * 0.02f;
            demandFactor = Mathf.Clamp(demandFactor, 0f, 1.5f);
            isDirty = true;
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
            isDirty = true;
            RecalculatePrice();
        }

        /// <summary>
        /// Затухание спроса/предложения — ELASTIC "КАЧЕЛИ" (GDD_25 секция 5.2, Сессия 6)
        /// Эффективный множитель: decayRate × (1 - elasticStrength) = 0.92 × 0.92 = 0.846 за тик
        /// Из пика 1.5 возврат к норме за ~80 мин (16 тиков по 5 мин)
        /// </summary>
        public void DecayFactors(float decayRate = 0.92f, float elasticStrength = 0.08f)
        {
            // Экспоненциальное затухание + эластичный возврат
            float effectiveMultiplier = decayRate * (1f - elasticStrength); // 0.846
            demandFactor *= effectiveMultiplier;
            supplyFactor *= effectiveMultiplier;

            // Минимальный порог — обнуляем мелочь чтобы избежать дрейфа
            if (demandFactor < 0.01f) demandFactor = 0f;
            if (supplyFactor < 0.01f) supplyFactor = 0f;

            // Округляем до 3 знаков
            demandFactor = Mathf.Round(demandFactor * 1000f) / 1000f;
            supplyFactor = Mathf.Round(supplyFactor * 1000f) / 1000f;

            isDirty = true;
            RecalculatePrice();
        }

        /// <summary>
        /// Применить множитель от глобального события
        /// </summary>
        public void ApplyEventMultiplier(float multiplier)
        {
            eventMultiplier = multiplier;
            isDirty = true;
            RecalculatePrice();
        }

        /// <summary>
        /// Сбросить множитель события (когда событие окончилось)
        /// </summary>
        public void ResetEventMultiplier()
        {
            eventMultiplier = 1f;
            isDirty = true;
            RecalculatePrice();
        }
    }
}
