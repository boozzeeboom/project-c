using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Эффект турбулентности — тряска корабля при выходе за нижнюю границу коридора.
    /// Применяется в Zone DangerLower (ниже minAltitude).
    /// 
    /// Использует случайные силы для имитации турбулентности от Завесы.
    /// </summary>
    public class TurbulenceEffect
    {
        private Rigidbody _rb;
        private Transform _transform;

        [Header("Параметры Турбулентности")]
        [Tooltip("Сила турбулентности (масштаб случайных сил)")]
        public float turbulenceIntensity = 5f;

        [Tooltip("Частота обновления турбулентности (раз в секунд)")]
        public float updateInterval = 0.1f;

        [Tooltip("Масштаб вертикальной тряски (относительно горизонтальной)")]
        public float verticalMultiplier = 1.5f;

        [Tooltip("Масштаб горизонтальной тряски")]
        public float horizontalMultiplier = 0.8f;

        // Таймер для обновления
        private float _updateTimer;

        // Текущая случайная сила
        private Vector3 _currentTurbulenceForce;

        public TurbulenceEffect(Rigidbody rb, Transform transform)
        {
            _rb = rb;
            _transform = transform;
        }

        /// <summary>
        /// Обновить турбулентность. Вызывать в FixedUpdate.
        /// </summary>
        /// <param name="severity">Степень турбулентности (0-1, где 1 = максимальная)</param>
        public void Update(float severity, float dt)
        {
            if (severity <= 0f) return;

            _updateTimer += dt;

            // Обновляем случайную силу с фиксированным интервалом
            if (_updateTimer >= updateInterval)
            {
                _updateTimer = 0f;
                GenerateTurbulenceForce(severity);
            }

            // Применяем силу
            _rb.AddForce(_currentTurbulenceForce, ForceMode.Force);
        }

        /// <summary>
        /// Сгенерировать новую случайную силу турбулентности.
        /// Силы масштабируются на массу корабля чтобы быть значимыми.
        /// </summary>
        private void GenerateTurbulenceForce(float severity)
        {
            // Случайные значения [-1, 1]
            float horizontalX = Random.Range(-1f, 1f);
            float horizontalZ = Random.Range(-1f, 1f);
            float verticalY = Random.Range(-1f, 1f);

            // Масштабируем на массу корабля чтобы силы были значимыми
            float mass = _rb.mass;

            // Применяем множители
            _currentTurbulenceForce = new Vector3(
                horizontalX * horizontalMultiplier * turbulenceIntensity * severity * mass,
                verticalY * verticalMultiplier * turbulenceIntensity * severity * mass,
                horizontalZ * horizontalMultiplier * turbulenceIntensity * severity * mass
            );
        }

        /// <summary>
        /// Рассчитать степень турбулентности на основе глубины ниже границы.
        /// </summary>
        /// <param name="currentAlt">Текущая высота</param>
        /// <param name="minAlt">Минимальная высота коридора</param>
        /// <param name="maxDepth">Максимальная глубина для расчёта (м)</param>
        /// <returns>Степень турбулентности (0-1)</returns>
        public static float CalculateSeverity(float currentAlt, float minAlt, float maxDepth = 200f)
        {
            float depthBelow = minAlt - currentAlt;
            if (depthBelow <= 0f) return 0f;

            // Линейная интерполяция: 0 на границе, 1 на maxDepth ниже
            return Mathf.Clamp01(depthBelow / maxDepth);
        }
    }
}
