// =====================================================================================
// ShipClassMapping.cs — пара маппинга FlightClass → CargoClass (T-CARGO-01)
// =====================================================================================
using System;
using ProjectC.Player; // ShipFlightClass
using ProjectC.Trade.Core;
using UnityEngine;
namespace ProjectC.Ship
{
    /// <summary>
    /// Одна запись маппинга. Редактируется в инспекторе
    /// <see cref="ShipClassMappingConfig"/> (ScriptableObject).
    /// </summary>
    [Serializable]
    public struct ShipClassMapping
    {
        [Tooltip("Физический класс корабля (из ProjectC.Player.ShipFlightClass)")]
        public ShipFlightClass flightClass;

        [Tooltip("Грузовой класс корабля (для лимитов трюма)")]
        public ShipClass cargoClass;
    }
}
