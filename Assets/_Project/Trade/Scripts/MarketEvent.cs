using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Trade;

namespace ProjectC.Trade
{
    /// <summary>
    /// Глобальное событие рынка — временно изменяет цены на определённые товары.
    /// Serializable — хранится в списке на TradeMarketServer.
    /// Сессия 6: Tick-система + динамическая экономика.
    /// </summary>
    [Serializable]
    public class MarketEvent
    {
        [Header("Event Info")]
        [Tooltip("Уникальный ID события")]
        public string eventId;

        [Tooltip("Отображаемое имя")]
        public string displayName;

        [Tooltip("Иконка события (эмодзи)")]
        public string displayIcon = "⚡";

        [Header("Target")]
        [Tooltip("ID затрагиваемого товара (\"ALL\" = все товары)")]
        public string affectedItemId = "ALL";

        [Tooltip("Затрагиваемые локации ([\"ALL\"] = все, или конкретные ID)")]
        public string[] affectedLocations = new[] { "ALL" };

        [Header("Effect")]
        [Tooltip("Множитель спроса (1.4 = спрос +40%)")]
        [Range(0.5f, 3f)]
        public float demandMultiplier = 1f;

        [Tooltip("Множитель предложения (0.5 = предложение -50%)")]
        [Range(0f, 2f)]
        public float supplyMultiplier = 1f;

        [Header("Duration")]
        [Tooltip("Длительность в тиках")]
        public int durationTicks = 6;

        [Tooltip("Оставшиеся тики (обратный отсчёт)")]
        public int remainingTicks;

        [Tooltip("Кулдаун после окончания (в тиках)")]
        public int cooldownTicks = 24;

        [Tooltip("Оставшийся кулдаун")]
        public int cooldownRemaining;

        [Header("Trigger")]
        [Tooltip("Тип триггера")]
        public TriggerType triggerType = TriggerType.Manual;

        [Tooltip("Значение триггера (порог demandFactor или шанс)")]
        public float triggerValue = 0.8f;

        [Header("State")]
        [Tooltip("Активно ли событие")]
        public bool isActive;

        [Tooltip("Время создания (Time.time)")]
        public float startTime;

        /// <summary>
        /// Проверить, должен ли сработать триггер
        /// </summary>
        public bool ShouldTrigger(Dictionary<string, LocationMarket> markets)
        {
            switch (triggerType)
            {
                case TriggerType.Manual:
                    return false; // Только ручное создание

                case TriggerType.DemandThreshold:
                    // Проверяем, есть ли на любом рынке demandFactor >= triggerValue для целевого товара
                    foreach (var market in markets.Values)
                    {
                        if (!IsLocationAffected(market.locationId)) continue;

                        if (affectedItemId == "ALL")
                        {
                            foreach (var item in market.items)
                            {
                                if (item != null && item.demandFactor >= triggerValue)
                                    return true;
                            }
                        }
                        else
                        {
                            var marketItem = market.GetItem(affectedItemId);
                            if (marketItem != null && marketItem.demandFactor >= triggerValue)
                                return true;
                        }
                    }
                    return false;

                case TriggerType.Random:
                    // Случайный триггер с шансом triggerValue (0.05 = 5%)
                    return UnityEngine.Random.value <= triggerValue;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Уменьшить оставшиеся тики на 1
        /// </summary>
        public void Tick()
        {
            if (!isActive) return;

            remainingTicks--;

            if (remainingTicks <= 0)
            {
                isActive = false;
                cooldownRemaining = cooldownTicks;
            }
        }

        /// <summary>
        /// Проверить, истекло ли событие
        /// </summary>
        public bool IsExpired()
        {
            return isActive && remainingTicks <= 0;
        }

        /// <summary>
        /// Применить эффект события к рынку локации
        /// </summary>
        public void ApplyToMarket(LocationMarket market, string itemId)
        {
            if (!IsLocationAffected(market.locationId)) return;

            var marketItem = market.GetItem(itemId);
            if (marketItem == null) return;

            // Применяем множители к demand/supply
            marketItem.demandFactor = Mathf.Clamp(
                marketItem.demandFactor * demandMultiplier, 0f, 1.5f);
            marketItem.supplyFactor = Mathf.Clamp(
                marketItem.supplyFactor * supplyMultiplier, 0f, 1.5f);

            marketItem.isDirty = true;
            marketItem.RecalculatePrice();
        }

        /// <summary>
        /// Применить событие ко всем целевым товарам на рынке
        /// </summary>
        public void ApplyToAllAffectedItems(LocationMarket market)
        {
            if (!IsLocationAffected(market.locationId)) return;

            if (affectedItemId == "ALL")
            {
                foreach (var item in market.items)
                {
                    if (item != null)
                    {
                        item.demandFactor = Mathf.Clamp(
                            item.demandFactor * demandMultiplier, 0f, 1.5f);
                        item.supplyFactor = Mathf.Clamp(
                            item.supplyFactor * supplyMultiplier, 0f, 1.5f);
                        item.isDirty = true;
                        item.RecalculatePrice();
                    }
                }
            }
            else
            {
                ApplyToMarket(market, affectedItemId);
            }
        }

        /// <summary>
        /// Убрать эффект события (сбросить множители)
        /// </summary>
        public void RemoveFromMarket(LocationMarket market, string itemId)
        {
            var marketItem = market.GetItem(itemId);
            if (marketItem == null) return;

            // "Обратный" множитель — делим на demandMultiplier
            if (demandMultiplier != 0f)
            {
                marketItem.demandFactor = Mathf.Clamp(
                    marketItem.demandFactor / demandMultiplier, 0f, 1.5f);
            }
            if (supplyMultiplier != 0f)
            {
                marketItem.supplyFactor = Mathf.Clamp(
                    marketItem.supplyFactor / supplyMultiplier, 0f, 1.5f);
            }

            marketItem.isDirty = true;
            marketItem.RecalculatePrice();
        }

        /// <summary>
        /// Убрать эффект события со всех целевых товаров
        /// </summary>
        public void RemoveFromAllAffectedItems(LocationMarket market)
        {
            if (!IsLocationAffected(market.locationId)) return;

            if (affectedItemId == "ALL")
            {
                foreach (var item in market.items)
                {
                    if (item != null)
                    {
                        if (demandMultiplier != 0f)
                        {
                            item.demandFactor = Mathf.Clamp(
                                item.demandFactor / demandMultiplier, 0f, 1.5f);
                        }
                        if (supplyMultiplier != 0f)
                        {
                            item.supplyFactor = Mathf.Clamp(
                                item.supplyFactor / supplyMultiplier, 0f, 1.5f);
                        }
                        item.isDirty = true;
                        item.RecalculatePrice();
                    }
                }
            }
            else
            {
                RemoveFromMarket(market, affectedItemId);
            }
        }

        /// <summary>
        /// Проверить, затрагивает ли событие эту локацию
        /// </summary>
        private bool IsLocationAffected(string locationId)
        {
            foreach (var loc in affectedLocations)
            {
                if (loc == "ALL" || loc == locationId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Уменьшить кулдаун на 1
        /// </summary>
        public void TickCooldown()
        {
            if (cooldownRemaining > 0)
                cooldownRemaining--;
        }

        /// <summary>
        /// Активировать событие
        /// </summary>
        public void Activate()
        {
            isActive = true;
            remainingTicks = durationTicks;
            startTime = Time.time;
        }

        /// <summary>
        /// Деактивировать и запустить кулдаун
        /// </summary>
        public void Deactivate()
        {
            isActive = false;
            cooldownRemaining = cooldownTicks;
        }
    }

    /// <summary>
    /// Типы триггеров для рыночных событий
    /// </summary>
    public enum TriggerType
    {
        /// <summary>Ручное создание (из кода или админ-команды)</summary>
        Manual,

        /// <summary>Срабатывает когда demandFactor >= triggerValue</summary>
        DemandThreshold,

        /// <summary>Срабатывает случайно с шансом triggerValue каждый тик</summary>
        Random
    }
}
