using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade.Network
{
    /// <summary>
    /// Статический реестр всех <see cref="ContractZone"/> в сцене/сценах.
    /// Аналог <c>MarketZoneRegistry</c> для рынков.
    ///
    /// Используется:
    ///   • <see cref="ContractServer"/> — для проверки позиции игрока при RPC.
    ///   • <see cref="Client.ContractInteractor"/> — для поиска ближайшей зоны при E.
    ///   • <see cref="ContractZone"/> — для регистрации/разрегистрации при OnEnable/OnDisable.
    ///
    /// Идемпотентная регистрация (если зона с таким locationId уже есть — перезаписываем).
    /// DontDestroyOnLoad не делаем (зоны привязаны к сценам WorldScene_X_Z; при unload
    /// они автоматически разрегистрируются через OnDisable).
    ///
    /// C2-этап миграции контрактов на v2-архитектуру (см. docs/dev/CONTRACT_V2_MIGRATION.md).
    /// </summary>
    public static class ContractZoneRegistry
    {
        private static readonly Dictionary<string, ContractZone> _zones = new Dictionary<string, ContractZone>();

        /// <summary>Все зарегистрированные зоны (для FindNearestZone и обхода).</summary>
        public static IReadOnlyDictionary<string, ContractZone> All => _zones;

        /// <summary>Текущая зона, в которой находится local player. Заполняется в ContractZone.PollLocalPlayerZone().</summary>
        public static ContractZone LocalPlayerZone { get; set; }

        public static void Register(ContractZone zone)
        {
            if (zone == null || string.IsNullOrEmpty(zone.LocationId)) return;
            if (_zones.TryGetValue(zone.LocationId, out var existing) && existing != zone)
            {
                Debug.LogWarning($"[ContractZoneRegistry] зона '{zone.LocationId}' уже зарегистрирована (existing={existing.name}, new={zone.name}), перезаписываю");
            }
            _zones[zone.LocationId] = zone;
        }

        public static void Unregister(ContractZone zone)
        {
            if (zone == null || string.IsNullOrEmpty(zone.LocationId)) return;
            if (_zones.TryGetValue(zone.LocationId, out var existing) && existing == zone)
            {
                _zones.Remove(zone.LocationId);
            }
            if (LocalPlayerZone == zone) LocalPlayerZone = null;
        }

        public static ContractZone Get(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return null;
            return _zones.TryGetValue(locationId, out var z) ? z : null;
        }

        public static void Clear()
        {
            _zones.Clear();
            LocalPlayerZone = null;
        }
    }
}
