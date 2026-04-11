using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Эффект деградации систем — при полёте выше максимальной границы коридора.
    /// Применяется в Zone DangerUpper (выше maxAltitude + criticalUpperMargin).
    /// 
    /// Уменьшает тягу, маневренность и другие характеристики корабля.
    /// В будущем можно добавить отключение систем, замерзание и т.д.
    /// </summary>
    public class SystemDegradationEffect
    {
        private Rigidbody _rb;
        private Transform _transform;

        [Header("Параметры Деградации")]
        [Tooltip("Множитель тяги (1.0 = нормально, 0.5 = 50% тяги)")]
        [Range(0f, 1f)]
        public float thrustMultiplier = 0.5f;

        [Tooltip("Множитель силы рыскания (курсвой поворот)")]
        [Range(0f, 1f)]
        public float yawMultiplier = 0.6f;

        [Tooltip("Множитель силы тангажа")]
        [Range(0f, 1f)]
        public float pitchMultiplier = 0.6f;

        [Tooltip("Множитель вертикальной силы")]
        [Range(0f, 1f)]
        public float verticalMultiplier = 0.5f;

        [Tooltip("Дополнительное сопротивление воздуха")]
        public float extraDrag = 0.3f;

        /// <summary>
        /// Конструктор
        /// </summary>
        public SystemDegradationEffect(Rigidbody rb, Transform transform)
        {
            _rb = rb;
            _transform = transform;
        }

        /// <summary>
        /// Применить деградацию к характеристикам корабля.
        /// Возвращает модификаторы для применения в ShipController.
        /// </summary>
        /// <param name="severity">Степень деградации (0-1)</param>
        /// <returns>Модификаторы (thrust, yaw, pitch, vertical, drag)</returns>
        public DegradationModifiers GetModifiers(float severity)
        {
            //severity 0 = нет деградации, 1 = максимальная
            float s = Mathf.Clamp01(severity);

            // Интерполяция между нормальными и деградированными значениями
            return new DegradationModifiers
            {
                thrust = Mathf.Lerp(1f, thrustMultiplier, s),
                yaw = Mathf.Lerp(1f, yawMultiplier, s),
                pitch = Mathf.Lerp(1f, pitchMultiplier, s),
                vertical = Mathf.Lerp(1f, verticalMultiplier, s),
                extraDrag = extraDrag * s
            };
        }

        /// <summary>
        /// Применить дополнительное сопротивление воздуха напрямую к Rigidbody.
        /// </summary>
        public void ApplyExtraDrag(float severity)
        {
            if (severity <= 0f) return;

            float drag = extraDrag * Mathf.Clamp01(severity);
            _rb.linearDamping += drag;
        }

        /// <summary>
        /// Рассчитать степень деградации на основе высоты над границей.
        /// </summary>
        /// <param name="currentAlt">Текущая высота</param>
        /// <param name="criticalAlt">Критическая высота (maxAlt + criticalUpperMargin)</param>
        /// <param name="maxHeight">Максимальная высота для расчёта (м над критической)</param>
        /// <returns>Степень деградации (0-1)</returns>
        public static float CalculateSeverity(float currentAlt, float criticalAlt, float maxHeight = 500f)
        {
            float heightAbove = currentAlt - criticalAlt;
            if (heightAbove <= 0f) return 0f;

            // Линейная интерполяция: 0 на критической высоте, 1 на maxHeight выше
            return Mathf.Clamp01(heightAbove / maxHeight);
        }
    }

    /// <summary>
    /// Модификаторы деградации — применяются к характеристикам корабля.
    /// </summary>
    public struct DegradationModifiers
    {
        /// <summary>Множитель тяги (1.0 = нормально)</summary>
        public float thrust;

        /// <summary>Множитель силы рыскания (1.0 = нормально)</summary>
        public float yaw;

        /// <summary>Множитель силы тангажа (1.0 = нормально)</summary>
        public float pitch;

        /// <summary>Множитель вертикальной силы (1.0 = нормально)</summary>
        public float vertical;

        /// <summary>Дополнительное сопротивление воздуха</summary>
        public float extraDrag;
    }
}
