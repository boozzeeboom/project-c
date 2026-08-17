using System;
using System.Collections.Generic;

namespace ProjectC.Trade.Dto
{
    /// <summary>
    /// Serializable DTO for persisting market runtime state through IPlayerDataRepository.
    /// Used by TradeWorld.SaveAll / TradeWorld.LoadAll.
    ///
    /// Сохраняет:
    ///   • MarketItemState runtime поля (availableStock, demandFactor, supplyFactor, eventMultiplier, version)
    ///   • MarketEvent runtime поля (isActive, remainingSeconds, cooldownRemaining, startTimeUnscaled)
    ///
    /// НЕ сохраняет:
    ///   • currentPrice (пересчитывается из factors)
    ///   • config-ссылки / basePrice (из SO, не runtime)
    ///   • NPC-трейдеров (hardcoded, нет runtime-мутаций для сохранения)
    /// </summary>
    [Serializable]
    public class MarketSaveData
    {
        /// <summary>Current market save schema. Legacy snapshots without this field are schema 0.</summary>
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;

        /// <summary>Per-location market item states.</summary>
        public List<MarketLocationSaveEntry> markets = new List<MarketLocationSaveEntry>();

        /// <summary>Active/cooldown market events.</summary>
        public List<MarketEventSaveEntry> events = new List<MarketEventSaveEntry>();

        public bool HasData =>
            (markets != null && markets.Count > 0)
            || (events != null && events.Count > 0);
    }

    /// <summary>
    /// Serializable entry for one location's market items.
    /// </summary>
    [Serializable]
    public class MarketLocationSaveEntry
    {
        public string locationId;
        public List<MarketItemSaveEntry> items = new List<MarketItemSaveEntry>();
    }

    /// <summary>
    /// Serializable entry for a single item's runtime state.
    /// </summary>
    [Serializable]
    public class MarketItemSaveEntry
    {
        public string itemId;
        public int availableStock;
        public float demandFactor;
        public float supplyFactor;
        public float eventMultiplier;
        public int version;
    }

    /// <summary>
    /// Serializable entry for a market event's runtime state.
    /// </summary>
    [Serializable]
    public class MarketEventSaveEntry
    {
        public string eventId;
        public bool isActive;
        public float remainingSeconds;
        public float cooldownRemaining;
        public float startTimeUnscaled;
    }
}
