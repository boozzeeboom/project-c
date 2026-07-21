// =====================================================================================
// ShipDamageConfig.cs — SO параметры системы повреждений корпуса корабля
// =====================================================================================
// maxHull по ShipFlightClass, armorHull, формула «энергия столкновения → урон»,
// множитель скоростей при поломке, стоимость ремонта.
// Правится в инспекторе, не в коде. Default loader: Resources/ShipDamage.asset
// + hardcoded fallback с warning если asset не найден.
// Паттерн: см. ShipCollisionDamageConfig (v2-so-config-default-fallback).
// =====================================================================================
using UnityEngine;
using ProjectC.Player; // ShipFlightClass

namespace ProjectC.Ship.Combat
{
    /// <summary>
    /// Параметры системы повреждений корпуса корабля.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipDamage",
                     menuName = "ProjectC/Ship/Damage Config",
                     order = 110)]
    public class ShipDamageConfig : ScriptableObject
    {
        [Header("Hull HP по классу корабля")]
        [Tooltip("Max HP корпуса для лёгкого корабля")]
        [Min(1)] public int maxHullLight = 100;
        [Tooltip("Max HP корпуса для среднего корабля")]
        [Min(1)] public int maxHullMedium = 200;
        [Tooltip("Max HP корпуса для тяжёлого корабля")]
        [Min(1)] public int maxHullHeavy = 400;
        [Tooltip("Max HP корпуса для тяжёлого II корабля")]
        [Min(1)] public int maxHullHeavyII = 600;

        [Header("Armor")]
        [Tooltip("Броня корпуса — вычитается из finalDamage боевого оружия (DamageCalculator). " +
                 "Меняется типом оружия через DamageType: Physical ×1, Ballistic ×1, Antigrav ×0.5, Mesium ×0.")]
        [Min(0)] public int armorHull = 5;

        [Header("Столкновения → урон корпуса")]
        [Tooltip("Минимальная энергия столкновения (col.impulse.magnitude) для урона корпусу. " +
                 "Меньше — игнорируется (мелкие контакты).")]
        [Min(0f)] public float collisionEnergyThreshold = 8f;

        [Tooltip("Коэффициент конвертации энергии столкновения в урон корпуса. " +
                 "Формула: hullDamage = floor((energy - threshold) * collisionDamageCoefficient).")]
        [Min(0f)] public float collisionDamageCoefficient = 0.5f;

        [Tooltip("Максимальный урон от одного столкновения (cap, чтобы один удар не уничтожил корабль).")]
        [Min(1)] public int collisionDamageCap = 50;

        [Tooltip("Минимальная скорость сближения (col.relativeVelocity.magnitude, м/с) для урона. " +
                 "Отсекает 'ложные' удары при выталкивании из геометрии (penetration resolution) на отстыковке, " +
                 "когда impulse огромный, но реального сближения нет.")]
        [Min(0f)] public float minCollisionRelativeSpeed = 3f;

        [Tooltip("Грейс-период (сек) после отстыковки, в течение которого урон корпусу от столкновений игнорируется. " +
                 "Даёт физике 'осесть' после снятия kinematic, не ломая корабль о док.")]
        [Min(0f)] public float postUndockGraceSeconds = 3f;

        [Header("Поломка (0 HP)")]
        [Tooltip("Множитель скоростей при поломке (0 HP). 0.1 = 10% от нормальных скоростей.")]
        [Range(0f, 1f)] public float brokenSpeedMultiplier = 0.1f;

        [Header("Ремонт")]
        [Tooltip("❌ УСТАРЕЛО: стоимость теперь задаётся на NPC RepairManager (_hullRepairCost). Оставлено для обратной совместимости.")]
        [Min(0)] [HideInInspector] public int repairCostCredits = 300;

        [Header("Debug")]
        [Tooltip("Подробные логи в консоль при каждом изменении HP")]
        public bool verboseLogging = true;

        // ========================================================
        // Default loader (паттерн v2-so-config-default-fallback)
        // ========================================================
        private static ShipDamageConfig _default;

        public static ShipDamageConfig Default
        {
            get
            {
                if (_default != null) return _default;
                _default = Resources.Load<ShipDamageConfig>("ShipDamage");
                if (_default == null)
                {
                    Debug.LogWarning(
                        "[ShipDamageConfig] Asset 'Resources/ShipDamage' not found. " +
                        "Using in-memory hardcoded defaults. " +
                        "Create one via Assets > Create > ProjectC > Ship > Damage Config.");
                    _default = CreateInstance<ShipDamageConfig>();
                }
                return _default;
            }
        }

        /// <summary>
        /// Получить maxHull для указанного класса корабля.
        /// </summary>
        public int GetMaxHull(ShipFlightClass flightClass)
        {
            switch (flightClass)
            {
                case ShipFlightClass.Light:   return maxHullLight;
                case ShipFlightClass.Medium:  return maxHullMedium;
                case ShipFlightClass.Heavy:   return maxHullHeavy;
                case ShipFlightClass.HeavyII: return maxHullHeavyII;
                default: return maxHullMedium;
            }
        }
    }
}
