// =====================================================================================
// ShipCargoLimitsConfig.cs — SO конфиг лимитов трюма для классов кораблей (T-CARGO-06)
// =====================================================================================
// Заменяет hardcoded switch в ShipClassLimits.Get(). Правится в инспекторе,
// не в коде. Default loader: Resources/ShipCargoLimits.asset + fallback с warning.
// Паттерн: см. v2-so-config-default-fallback skill.
// =====================================================================================
using ProjectC.Trade.Core; // ShipClass
using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Одна запись лимита корабля: класс → (слоты, вес, объём, коэффициент штрафа).
    /// </summary>
    [System.Serializable]
    public struct ShipCargoLimitEntry
    {
        [Tooltip("Грузовой класс (Light/Medium/HeavyI/HeavyII)")]
        public ShipClass shipClass;

        [Header("Лимиты трюма")]
        public int maxSlots;
        public float maxWeight;
        public float maxVolume;
        public float penaltyFactor;
    }

    /// <summary>
    /// ScriptableObject: список лимитов для всех классов.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipCargoLimits",
                     menuName = "ProjectC/Ship/Cargo Limits",
                     order = 102)]
    public class ShipCargoLimitsConfig : ScriptableObject
    {
        [Tooltip("Лимиты для каждого класса корабля. Должны включать все 4 ShipClass.")]
        public ShipCargoLimitEntry[] entries = new ShipCargoLimitEntry[]
        {
            new ShipCargoLimitEntry
            {
                shipClass = ShipClass.Light,
                maxSlots = 4, maxWeight = 100f, maxVolume = 3f, penaltyFactor = 0.05f
            },
            new ShipCargoLimitEntry
            {
                shipClass = ShipClass.Medium,
                maxSlots = 10, maxWeight = 500f, maxVolume = 12f, penaltyFactor = 0.08f
            },
            new ShipCargoLimitEntry
            {
                shipClass = ShipClass.HeavyI,
                maxSlots = 20, maxWeight = 2000f, maxVolume = 40f, penaltyFactor = 0.10f
            },
            new ShipCargoLimitEntry
            {
                shipClass = ShipClass.HeavyII,
                maxSlots = 30, maxWeight = 5000f, maxVolume = 80f, penaltyFactor = 0.12f
            },
        };

        private static ShipCargoLimitsConfig _default;

        public static ShipCargoLimitsConfig Default
        {
            get
            {
                if (_default != null) return _default;
                _default = Resources.Load<ShipCargoLimitsConfig>("ShipCargoLimits");
                if (_default == null)
                {
                    Debug.LogWarning(
                        "[ShipCargoLimitsConfig] Asset 'Resources/ShipCargoLimits' not found. " +
                        "Using in-memory hardcoded defaults. " +
                        "Create one via Assets > Create > ProjectC > Ship > Cargo Limits.");
                    _default = CreateInstance<ShipCargoLimitsConfig>();
                }
                return _default;
            }
        }

        /// <summary>
        /// Резолвинг по классу. Возвращает null если не найдено.
        /// </summary>
        public ShipCargoLimitEntry? Resolve(ShipClass cls)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].shipClass == cls)
                    return entries[i];
            }
            return null;
        }
    }
}
