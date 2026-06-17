// =====================================================================================
// ShipClass.cs — единый enum грузового класса корабля (Project C: The Clouds)
// =====================================================================================
// T-CARGO-01: перенесён из ProjectC.Player (CargoSystem.cs) в ProjectC.Trade.Core.
//   • Раньше: enum жил в Trade/Scripts/CargoSystem.cs в namespace ProjectC.Player
//     (путаница: рядом с ProjectC.Player.ShipFlightClass).
//   • Теперь: Trade владеет, потому что Cargo — подсистема Trade. ShipController
//     импортирует через `using ProjectC.Trade.Core;`.
//
// Лимиты по классу — в ShipClassLimits (CargoData.cs:138) и в
// ShipClassMappingConfig (Assets/_Project/Data/Ship/ShipClassMapping.asset).
// =====================================================================================

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Грузовой класс корабля — определяет лимиты трюма (слоты/вес/объём).
    /// НЕ путать с ProjectC.Player.ShipFlightClass (физика полёта).
    /// Маппинг FlightClass → CargoClass: ShipClassMappingConfig (SO, inspector-editable).
    /// </summary>
    public enum ShipClass
    {
        Light,      // Лёгкий: 4 слота, 100 кг, 3 м³
        Medium,     // Средний: 10 слотов, 500 кг, 12 м³
        HeavyI,     // Тяжёлый I: 20 слотов, 2000 кг, 40 м³
        HeavyII     // Тяжёлый II: 30 слотов, 5000 кг, 80 м³
    }
}
