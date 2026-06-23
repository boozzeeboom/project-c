// T-NS00: NpcShipRoute + NpcShipDemandCategory — один leg маршрута NPC-корабля.
// Pattern: ShipCargoRegistry entries (Trade/Core/...).
// Convention: один class = один .cs файл (Unity 6: T-DOCK-13c fix).

using ProjectC.Player;
using UnityEngine;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Категория спроса маршрута. В M1 = Generic (random routing).
    /// В v2 — динамически пересчитывается из TradeWorld.MarketTick.
    /// См. docs/NPC_others_peacfull/pc_ship/03_V2_ARCHITECTURE.md §6.2.
    /// </summary>
    public enum NpcShipDemandCategory : byte
    {
        Generic = 0,    // M1: random traffic
        HighDemand,     // v2: route to station with low stock
        LowDemand,      // v2: route to station with high stock
        Contract,       // v2: tied to player contract
    }

    /// <summary>
    /// Один leg маршрута NPC-корабля.
    /// Один schedule = list of routes (round-trip / loop / random).
    /// Поля fromLocationId/toLocationId синк с DockStationDefinition.LocationId и MarketZone.LocationId.
    /// </summary>
    [System.Serializable]
    public struct NpcShipRoute
    {
        [Tooltip("LocationId первой станции (sync с DockStationDefinition.LocationId).")]
        public string fromLocationId;       // "PRIMIUM"

        [Tooltip("LocationId второй станции (sync с DockStationDefinition.LocationId).")]
        public string toLocationId;         // "PRIMIUM_TEST_ZONE_2"

        [Tooltip("Сколько секунд NPC стоит на pad'е (Docked + Loading).")]
        public float dwellTimeSec;          // 600 = 10 мин на станции (Q5: 30-90 сек Loading)

        [Tooltip("Сколько секунд длится перелёт. Вычисляется при инициализации из дистанции.")]
        public float flightDurationSec;     // 1200 = 20 мин в полёте

        [Tooltip("Класс корабля, предпочтительный для этого маршрута.")]
        public ShipFlightClass preferredShipClass;

        [Tooltip("M1: всегда Generic. V2: пересчитывается из TradeWorld.MarketTick.")]
        public NpcShipDemandCategory demandCategory;

        [Tooltip("Крейсерская высота полёта от точки старта (м). По умолчанию 30.")]
        public float cruiseAltitude;        // 30м по умолчанию
    }
}