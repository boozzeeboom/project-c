// T-DOCK-01 stub: статический реестр DockStationController (server + client).
// Полная реализация в T-DOCK-05 (с LocalPlayerStation для клиента).

using System.Collections.Generic;

namespace ProjectC.Docking.Network
{
    public static class DockingZoneRegistry
    {
        private static readonly Dictionary<string, DockStationController> _stationsById
            = new Dictionary<string, DockStationController>();

        public static IReadOnlyDictionary<string, DockStationController> All => _stationsById;

        public static void Register(DockStationController station)
        {
            if (station == null) return;
            var def = station.StationDefinition;
            if (def == null || string.IsNullOrEmpty(def.StationId)) return;
            _stationsById[def.StationId] = station;
        }

        public static void Unregister(DockStationController station)
        {
            if (station == null) return;
            var def = station.StationDefinition;
            if (def == null) return;
            if (_stationsById.TryGetValue(def.StationId, out var existing) && existing == station)
                _stationsById.Remove(def.StationId);
        }

        public static DockStationController GetStation(string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return null;
            _stationsById.TryGetValue(stationId, out var s);
            return s;
        }

        public static void Clear() => _stationsById.Clear();
    }
}
