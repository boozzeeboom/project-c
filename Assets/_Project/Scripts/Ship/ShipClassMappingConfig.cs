// =====================================================================================
// ShipClassMappingConfig.cs — SO конфиг маппинга FlightClass → CargoClass (T-CARGO-01)
// =====================================================================================
// Default загружается из Resources/ShipClassMapping.asset. Если asset не найден —
// создаётся in-memory с hardcoded defaults (Light→Light, Medium→Medium,
// Heavy→HeavyI, HeavyII→HeavyII) и логируется warning.
//
// Паттерн: см. v2-so-config-default-fallback skill.
// =====================================================================================
using System.Collections.Generic;
using ProjectC.Player; // ShipFlightClass
using ProjectC.Trade.Core;
using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Конфигурация маппинга физического класса корабля в грузовой.
    /// Правится в инспекторе, не в коде. Один asset на проект.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipClassMapping",
                     menuName = "ProjectC/Ship/Class Mapping",
                     order = 100)]
    public class ShipClassMappingConfig : ScriptableObject
    {
        [Tooltip("Список маппингов. Должен покрывать все 4 ShipFlightClass.")]
        public List<ShipClassMapping> mappings = new List<ShipClassMapping>();

        private static ShipClassMappingConfig _default;

        /// <summary>
        /// Загружает asset из Resources/. Если не найден — создаёт
        /// in-memory с hardcoded defaults (с warning-логом).
        /// </summary>
        public static ShipClassMappingConfig Default
        {
            get
            {
                if (_default != null) return _default;
                _default = Resources.Load<ShipClassMappingConfig>("ShipClassMapping");
                if (_default == null)
                {
                    Debug.LogWarning(
                        "[ShipClassMappingConfig] Asset 'Resources/ShipClassMapping' not found. " +
                        "Using in-memory hardcoded defaults. " +
                        "Create one via Assets > Create > ProjectC > Ship > Class Mapping.");
                    _default = CreateInstance<ShipClassMappingConfig>();
                    _default.mappings = new List<ShipClassMapping>
                    {
                        new ShipClassMapping { flightClass = ShipFlightClass.Light,   cargoClass = ShipClass.Light   },
                        new ShipClassMapping { flightClass = ShipFlightClass.Medium,  cargoClass = ShipClass.Medium  },
                        new ShipClassMapping { flightClass = ShipFlightClass.Heavy,   cargoClass = ShipClass.HeavyI  },
                        new ShipClassMapping { flightClass = ShipFlightClass.HeavyII, cargoClass = ShipClass.HeavyII },
                    };
                }
                return _default;
            }
        }

        /// <summary>
        /// Резолв: FlightClass → CargoClass. Если маппинг не найден — возвращает null
        /// (вызывающий код решает, какой fallback использовать).
        /// </summary>
        public ShipClass? Resolve(ShipFlightClass flight)
        {
            if (mappings == null) return null;
            for (int i = 0; i < mappings.Count; i++)
            {
                if (mappings[i].flightClass == flight)
                    return mappings[i].cargoClass;
            }
            return null;
        }
    }
}
