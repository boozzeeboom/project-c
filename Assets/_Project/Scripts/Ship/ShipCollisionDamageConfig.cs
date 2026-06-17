// =====================================================================================
// ShipCollisionDamageConfig.cs — SO параметры столкновений (T-CARGO-04)
// =====================================================================================
// Параметры урона/протечки груза при столкновении корабля.
// Правится в инспекторе, не в коде. Default loader: Resources/ShipCollisionDamage.asset
// + hardcoded fallback с warning если asset не найден.
// Паттерн: см. v2-so-config-default-fallback skill.
// =====================================================================================
using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Параметры обработки столкновений с грузом корабля.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipCollisionDamage",
                     menuName = "ProjectC/Ship/Collision Damage",
                     order = 101)]
    public class ShipCollisionDamageConfig : ScriptableObject
    {
        [Header("Thresholds")]
        [Tooltip("Минимальная энергия столкновения (col.impulse.magnitude) для срабатывания. " +
                 "Меньше — игнорируется (мелкие контакты).")]
        [Min(0f)]
        public float impactEnergyThreshold = 5f;

        [Header("Leak (dangerous items)")]
        [Tooltip("Шанс протечки для каждого опасного предмета (0..1). 0.05 = 5%.")]
        [Range(0f, 1f)]
        public float leakChancePerDangerous = 0.05f;

        [Tooltip("Доля потерянного количества при протечке (0..1). 0.1 = 10% от стака.")]
        [Range(0f, 1f)]
        public float leakPercentOfStack = 0.10f;

        [Header("Fragile items")]
        [Tooltip("Шанс повреждения каждого хрупкого предмета (0..1). 0.10 = 10%.")]
        [Range(0f, 1f)]
        public float fragileChancePerItem = 0.10f;

        [Header("Debug")]
        [Tooltip("Подробные логи при каждом столкновении")]
        public bool verboseLogging = true;

        // ========================================================
        // Default loader (паттерн v2-so-config-default-fallback)
        // ========================================================
        private static ShipCollisionDamageConfig _default;

        public static ShipCollisionDamageConfig Default
        {
            get
            {
                if (_default != null) return _default;
                _default = Resources.Load<ShipCollisionDamageConfig>("ShipCollisionDamage");
                if (_default == null)
                {
                    Debug.LogWarning(
                        "[ShipCollisionDamageConfig] Asset 'Resources/ShipCollisionDamage' not found. " +
                        "Using in-memory hardcoded defaults. " +
                        "Create one via Assets > Create > ProjectC > Ship > Collision Damage.");
                    _default = CreateInstance<ShipCollisionDamageConfig>();
                    // defaults уже заданы в полях выше
                }
                return _default;
            }
        }
    }
}
