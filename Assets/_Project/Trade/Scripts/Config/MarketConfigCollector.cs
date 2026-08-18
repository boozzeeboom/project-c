
// MARKET-ID-REFACTOR: Static helper — нормализация locationId + авто-сбор MarketConfig
// из всех MarketZone в загруженных сценах.
// См. docs/Markets/MARKET_ID_REFACTOR_DESIGN.md

using System.Collections.Generic;
using ProjectC.Trade.Network;
using UnityEngine;

namespace ProjectC.Trade.Config
{
    public static class MarketConfigCollector
    {
        /// <summary>
        /// Нормализует locationId для использования в качестве ключа Dictionary.
        /// Все реестры и доменные lookup должны использовать эту функцию при
        /// регистрации, чтении и сохранении locationId.
        /// </summary>
        public static string NormalizeLocationId(string id)
            => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim().ToUpperInvariant();

        /// <summary>
        /// Собирает уникальные MarketConfig из всех MarketZone во ВСЕХ загруженных сценах.
        /// Дедупликация по нормализованному locationId.
        /// Вызывается MarketServer.OnNetworkSpawn для авто-конфигурации TradeWorld.
        /// </summary>
        public static List<MarketConfig> CollectFromLoadedScenes()
        {
            var zones = Object.FindObjectsByType<MarketZone>(
                FindObjectsInactive.Include);
            var seen = new HashSet<string>();
            var configs = new List<MarketConfig>();
            foreach (var zone in zones)
            {
                var cfg = zone.Config;
                if (cfg == null) continue;
                var normId = NormalizeLocationId(cfg.locationId);
                if (string.IsNullOrEmpty(normId) || !seen.Add(normId)) continue;
                configs.Add(cfg);
            }
            return configs;
        }

        /// <summary>
        /// Мержит сценарные MarketConfig'ы (из CollectFromLoadedScenes) с ручным списком
        /// MarketServer.marketConfigs. Ручной список — fallback для обратной совместимости.
        /// </summary>
        public static List<MarketConfig> MergeWithManualList(
            List<MarketConfig> sceneConfigs,
            List<MarketConfig> manualConfigs)
        {
            var merged = new List<MarketConfig>(sceneConfigs);
            if (manualConfigs == null || manualConfigs.Count == 0) return merged;

            var seen = new HashSet<string>();
            foreach (var c in sceneConfigs)
            {
                if (c != null) seen.Add(NormalizeLocationId(c.locationId));
            }
            foreach (var cfg in manualConfigs)
            {
                if (cfg == null) continue;
                var normId = NormalizeLocationId(cfg.locationId);
                if (!string.IsNullOrEmpty(normId) && seen.Add(normId))
                    merged.Add(cfg);
            }
            return merged;
        }
    }
}
