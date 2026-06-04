using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade.Network
{
    /// <summary>
    /// Реестр всех <see cref="MarketZone"/> в сцене.
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
            if (zone == null || string.IsNullOrEmpty(zone.LocationId)) return;
            _zones[zone.LocationId] = zone;
        }

        public static void Unregister(MarketZone zone)
        {
            if (zone == null || string.IsNullOrEmpty(zone.LocationId)) return;
            if (_zones.TryGetValue(zone.LocationId, out var existing) && existing == zone)
                _zones.Remove(zone.LocationId);
            if (_localPlayerZone == zone) _localPlayerZone = null;
        }

        public static MarketZone Get(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return null;
            _zones.TryGetValue(locationId, out var z);
            return z;
        }

        public static void Clear()
        {
            _zones.Clear();
            _localPlayerZone = null;
        }
    }
}
