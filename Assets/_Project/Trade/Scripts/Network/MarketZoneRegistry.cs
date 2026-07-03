using System.Collections.Generic;
using ProjectC.Trade.Config;
using UnityEngine;

namespace ProjectC.Trade.Network
{
    /// <summary>
    /// Реестр всех <see cref="MarketZone"/> в сцене.
    /// MARKET-ID-REFACTOR: ключи нормализуются через <see cref="MarketConfigCollector.NormalizeLocationId"/>.
    ///
    /// Серверная часть: при OnEnable на сервере MarketZone регистрирует себя здесь.
    /// Используется <see cref="MarketServer"/> для валидации позиции игрока при RPC
    /// и для получения списка nearby ships.
    ///
    /// Клиентская часть: <see cref="LocalPlayerZone"/> — зона, в которой сейчас
    /// находится локальный игрок (определяется по SphereCollider trigger на
    /// клиентском экземпляре MarketZone). Используется <see cref="Player.NetworkPlayer"/>
    /// чтобы открыть рынок по нажатию E.
    /// </summary>
    public static class MarketZoneRegistry
    {
        private static readonly Dictionary<string, MarketZone> _zones = new Dictionary<string, MarketZone>();
        private static MarketZone _localPlayerZone;

        public static IReadOnlyDictionary<string, MarketZone> All => _zones;

        /// <summary>
        /// Зона, в которой сейчас локальный игрок (null если не в зоне).
        /// </summary>
        public static MarketZone LocalPlayerZone
        {
            get => _localPlayerZone;
            set => _localPlayerZone = value;
        }

        public static void Register(MarketZone zone)
        {
            if (zone == null) return;
            var key = MarketConfigCollector.NormalizeLocationId(zone.LocationId);
            if (string.IsNullOrEmpty(key)) return;
            _zones[key] = zone;
        }

        public static void Unregister(MarketZone zone)
        {
            if (zone == null) return;
            var key = MarketConfigCollector.NormalizeLocationId(zone.LocationId);
            if (string.IsNullOrEmpty(key)) return;
            if (_zones.TryGetValue(key, out var existing) && existing == zone)
                _zones.Remove(key);
            if (_localPlayerZone == zone) _localPlayerZone = null;
        }

        public static MarketZone Get(string locationId)
        {
            var key = MarketConfigCollector.NormalizeLocationId(locationId);
            if (string.IsNullOrEmpty(key)) return null;
            _zones.TryGetValue(key, out var z);
            return z;
        }

        public static void Clear()
        {
            _zones.Clear();
            _localPlayerZone = null;
        }
    }
}
