// T-NS-BZ01: NpcBuildZoneRegistry — статический реестр NpcProximityZoneBuilds.
// Pattern: NpcShipZoneRegistry (PeacefulShip/Network/NpcShipZoneRegistry.cs).
// Server-only, без потокобезопасности — всё в одном потоке.

using System.Collections.Generic;
using ProjectC.PeacefulShip.Stations;

namespace ProjectC.PeacefulShip.Network
{
    /// <summary>
    /// Статический реестр building-зон обхода (NpcProximityZoneBuilds).
    /// Регистрация в OnEnable, удаление в OnDisable.
    /// Используется NpcProximityZone.FindClosestBuildConflict для поиска препятствий.
    /// Pattern: NpcShipZoneRegistry (PeacefulShip/Network/NpcShipZoneRegistry.cs).
    /// </summary>
    public static class NpcBuildZoneRegistry
    {
        private static readonly List<NpcProximityZoneBuilds> _all = new List<NpcProximityZoneBuilds>();

        /// <summary>Read-only список всех зарегистрированных building-зон.</summary>
        public static IReadOnlyList<NpcProximityZoneBuilds> All => _all;

        /// <summary>Регистрация зоны. Идемпотентно — повторный Register игнорируется.</summary>
        public static void Register(NpcProximityZoneBuilds zone)
        {
            if (zone == null || _all.Contains(zone)) return;
            _all.Add(zone);
        }

        /// <summary>Удаление зоны из реестра (OnDisable).</summary>
        public static void Unregister(NpcProximityZoneBuilds zone)
        {
            _all.Remove(zone);
        }

        /// <summary>Полная очистка (shutdown / scene unload).</summary>
        public static void Clear()
        {
            _all.Clear();
        }
    }
}
