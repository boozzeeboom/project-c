using System;
using System.Collections.Generic;
using ProjectC.Trade.Service;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Глобальное событие рынка — временно изменяет спрос/предложение на товары.
    ///
    /// Портировано из старого MarketEvent.cs. Изменения:
    ///   • работает с <see cref="MarketState"/> (POCO) вместо LocationMarket (SO)
    ///   • duration и cooldown в СЕКУНДАХ (time-based), а не в тиках — теперь не зависит
    ///     от частоты тиков, одинаково ведёт себя при multiplier 1x и 10x
    /// </summary>
    [Serializable]
    public class MarketEvent
    {
        [Header("Identity")]
        public string eventId = "";
        public string displayName = "";
        public string displayIcon = "⚡";

        [Header("Target")]
        [Tooltip("ID затрагиваемого товара. 'ALL' = все товары на затронутых рынках")]
        public string affectedItemId = "ALL";
        public string[] affectedLocations = new[] { "ALL" };

        [Header("Effect")]
        [Range(0.5f, 3f)]
        public float demandMultiplier = 1f;
        [Range(0f, 2f)]
        public float supplyMultiplier = 1f;

        [Header("Duration (seconds)")]
        public float durationSeconds = 1800f;       // 30 мин по умолчанию
        public float cooldownSeconds = 7200f;       // 2 часа

        [Header("Trigger")]
        public TriggerType triggerType = TriggerType.Manual;
        public float triggerValue = 0.8f;

        [Header("State (runtime)")]
        public bool isActive;
        public float remainingSeconds;
        public float cooldownRemaining;
        public float startTimeUnscaled;

        public void Activate(float nowUnscaled)
        {
            isActive = true;
            remainingSeconds = durationSeconds;
            startTimeUnscaled = nowUnscaled;
        }

        public void Deactivate()
        {
            isActive = false;
            cooldownRemaining = cooldownSeconds;
        }

        public bool IsExpired() => isActive && remainingSeconds <= 0f;

        public bool ShouldTrigger(IReadOnlyDictionary<string, MarketState> markets, float chanceRoll = -1f)
        {
            switch (triggerType)
            {
                case TriggerType.Manual: return false;
                case TriggerType.DemandThreshold:
                    foreach (var market in markets.Values)
                    {
                        if (!IsLocationAffected(market.locationId)) continue;
                        if (affectedItemId == "ALL")
                        {
                            foreach (var kv in market.Items)
                            {
                                if (kv.Value != null && kv.Value.demandFactor >= triggerValue) return true;
                            }
                        }
                        else
                        {
                            var item = market.GetItem(affectedItemId);
                            if (item != null && item.demandFactor >= triggerValue) return true;
                        }
                    }
                    return false;
                case TriggerType.Random:
                    return chanceRoll >= 0f && chanceRoll <= triggerValue;
                default: return false;
            }
        }

        public void Tick(float dtSeconds)
        {
            if (isActive)
            {
                remainingSeconds -= dtSeconds;
                if (remainingSeconds <= 0f) Deactivate();
            }
            else if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= dtSeconds;
                if (cooldownRemaining < 0f) cooldownRemaining = 0f;
            }
        }

        public void ApplyToMarket(MarketState market)
        {
            if (!IsLocationAffected(market.locationId)) return;
            if (affectedItemId == "ALL")
            {
                foreach (var kv in market.Items)
                {
                    var s = kv.Value;
                    if (s == null) continue;
                    s.demandFactor = Mathf.Clamp(s.demandFactor * demandMultiplier, PriceFormula.DEMAND_MIN, PriceFormula.DEMAND_MAX);
                    s.supplyFactor = Mathf.Clamp(s.supplyFactor * supplyMultiplier, PriceFormula.SUPPLY_MIN, PriceFormula.SUPPLY_MAX);
                    s.version++;
                    s.RecalculatePrice();
                }
            }
            else
            {
                var s = market.GetItem(affectedItemId);
                if (s == null) return;
                s.demandFactor = Mathf.Clamp(s.demandFactor * demandMultiplier, PriceFormula.DEMAND_MIN, PriceFormula.DEMAND_MAX);
                s.supplyFactor = Mathf.Clamp(s.supplyFactor * supplyMultiplier, PriceFormula.SUPPLY_MIN, PriceFormula.SUPPLY_MAX);
                s.version++;
                s.RecalculatePrice();
            }
        }

        public void RemoveFromMarket(MarketState market)
        {
            if (!IsLocationAffected(market.locationId)) return;
            if (affectedItemId == "ALL")
            {
                foreach (var kv in market.Items)
                {
                    var s = kv.Value;
                    if (s == null) continue;
                    if (demandMultiplier > 0f)
                        s.demandFactor = Mathf.Clamp(s.demandFactor / demandMultiplier, PriceFormula.DEMAND_MIN, PriceFormula.DEMAND_MAX);
                    if (supplyMultiplier > 0f)
                        s.supplyFactor = Mathf.Clamp(s.supplyFactor / supplyMultiplier, PriceFormula.SUPPLY_MIN, PriceFormula.SUPPLY_MAX);
                    s.version++;
                    s.RecalculatePrice();
                }
            }
            else
            {
                var s = market.GetItem(affectedItemId);
                if (s == null) return;
                if (demandMultiplier > 0f)
                    s.demandFactor = Mathf.Clamp(s.demandFactor / demandMultiplier, PriceFormula.DEMAND_MIN, PriceFormula.DEMAND_MAX);
                if (supplyMultiplier > 0f)
                    s.supplyFactor = Mathf.Clamp(s.supplyFactor / supplyMultiplier, PriceFormula.SUPPLY_MIN, PriceFormula.SUPPLY_MAX);
                s.version++;
                s.RecalculatePrice();
            }
        }

        public bool IsLocationAffected(string locationId)
        {
            if (affectedLocations == null) return false;
            for (int i = 0; i < affectedLocations.Length; i++)
            {
                var loc = affectedLocations[i];
                if (string.IsNullOrEmpty(loc)) continue;
                if (loc == "ALL" || loc == locationId) return true;
            }
            return false;
        }
    }

    public enum TriggerType
    {
        Manual,
        DemandThreshold,
        Random
    }
}
