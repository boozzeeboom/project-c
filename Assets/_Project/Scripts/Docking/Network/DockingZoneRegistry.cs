// T-DOCK-05: DockingZoneRegistry — статический реестр DockStationController + клиентские трекеры.
//
// Серверная часть: идентична MarketZoneRegistry (Register/Unregister/Get).
// Клиентская часть: LocalPlayerStation / LocalPlayerShipStation — зона, в которой сейчас
// находится локальный игрок (определяется через OuterCommZone.PollLocalPlayerZone).
// T-key handler (T-DOCK-08) использует эти трекеры для проверки «в зоне ли связи».

using System.Collections.Generic;

namespace ProjectC.Docking.Network
{
    public static class DockingZoneRegistry
    {
        private static readonly Dictionary<string, DockStationController> _stationsById
            = new Dictionary<string, DockStationController>();

        private static readonly Dictionary<string, DockStationController> _stationsByLocation
            = new Dictionary<string, DockStationController>();

        // Клиентские трекеры (T-DOCK-05: OuterCommZone обновляет их в PollLocalPlayerZone)
        public static DockStationController LocalPlayerStation { get; set; }
        public static DockStationController LocalPlayerShipStation { get; set; }

        public static IReadOnlyDictionary<string, DockStationController> All => _stationsById;

        public static void Register(DockStationController station)
        {
            if (station == null) return;
            var def = station.StationDefinition;
            if (def == null || string.IsNullOrEmpty(def.StationId))
            {
                UnityEngine.Debug.LogError($"[DockingZoneRegistry] station {station.gameObject.name} has no StationId", station);
                return;
            }
            _stationsById[def.StationId] = station;
            if (!string.IsNullOrEmpty(def.LocationId))
                _stationsByLocation[def.LocationId] = station;
        }

        public static void Unregister(DockStationController station)
        {
            if (station == null) return;
            var def = station.StationDefinition;
            if (def == null) return;
            if (_stationsById.TryGetValue(def.StationId, out var existing) && existing == station)
                _stationsById.Remove(def.StationId);
            if (!string.IsNullOrEmpty(def.LocationId)
                && _stationsByLocation.TryGetValue(def.LocationId, out var existingLoc)
                && existingLoc == station)
                _stationsByLocation.Remove(def.LocationId);
            if (LocalPlayerStation == station) LocalPlayerStation = null;
            if (LocalPlayerShipStation == station) LocalPlayerShipStation = null;
        }

        public static DockStationController GetStation(string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return null;
            _stationsById.TryGetValue(stationId, out var s);
            return s;
        }

        public static DockStationController GetByLocation(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return null;
            _stationsByLocation.TryGetValue(locationId, out var s);
            return s;
        }

        public static void Clear()
        {
            _stationsById.Clear();
            _stationsByLocation.Clear();
            LocalPlayerStation = null;
            LocalPlayerShipStation = null;
        }
    }
}
