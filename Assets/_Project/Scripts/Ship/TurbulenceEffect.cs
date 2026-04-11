using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Эффект турбулентности — тряска корабля при выходе за нижнюю границу коридора.
    /// Применяется в Zone DangerLower (ниже minAltitude).
    ///
    /// Использует случайные силы для имитации турбулентности от Завесы.
    /// Поддерживает Cinemachine Impulse (если пакет установлен) для тряски камеры.
    /// Интенсивность зависит от класса корабля (Light трясёт сильнее, Heavy — слабее).
    /// 
    /// Cinemachine определяется автоматически через Reflection — никаких define symbol не нужно.
    /// </summary>
    public class TurbulenceEffect
    {
        private Rigidbody _rb;
        private Transform _transform;

        [Header("Параметры Турбулентности")]
        [Tooltip("Базовая сила турбулентности (умножается на массу корабля и severity)")]
        public float turbulenceIntensity = 15f;

        [Tooltip("Частота обновления турбулентности (раз в секунд). Меньше = более резкая тряска")]
        public float updateInterval = 0.05f;

        [Tooltip("Множитель вертикальной тряски")]
        public float verticalMultiplier = 2.5f;

        [Tooltip("Множитель горизонтальной тряски")]
        public float horizontalMultiplier = 1.8f;

        [Tooltip("Дополнительный множитель силы (общий усилитель)")]
        public float forceMultiplier = 50f;

        [Tooltip("Базовая сила турбулентности — зависит от класса корабля")]
        public float baseForce = 600f;

        // Таймер для обновления
        private float _updateTimer;

        // Текущая случайная сила
        private Vector3 _currentTurbulenceForce;

        // Cinemachine Impulse — определяется через Reflection (runtime, без define symbols)
        private bool _hasCinemachine;
        private object _impulseSource; // CinemachineImpulseSource (reflection)
        private System.Type _impulseSourceType;

        public TurbulenceEffect(Rigidbody rb, Transform transform)
        {
            _rb = rb;
            _transform = transform;
            _tryInitializeCinemachine();
        }

        /// <summary>
        /// Установить множитель силы турбулентности на основе класса корабля.
        /// Light — трясёт сильнее, HeavyII — очень устойчивый.
        /// </summary>
        public void SetShipClassMultiplier(ProjectC.Player.ShipFlightClass shipClass)
        {
            switch (shipClass)
            {
                case ProjectC.Player.ShipFlightClass.Light:
                    baseForce = 800f;   // Light трясёт сильнее
                    break;
                case ProjectC.Player.ShipFlightClass.Medium:
                    baseForce = 600f;   // Medium баланс
                    break;
                case ProjectC.Player.ShipFlightClass.Heavy:
                    baseForce = 400f;   // Heavy меньше трясёт
                    break;
                case ProjectC.Player.ShipFlightClass.HeavyII:
                    baseForce = 300f;   // HeavyII очень устойчив
                    break;
                default:
                    baseForce = 600f;   // fallback на Medium
                    break;
            }
        }

        /// <summary>
        /// Обновить турбулентность. Вызывать в FixedUpdate.
        /// Применяет случайные силы И моменты вращения для реалистичной тряски.
        /// </summary>
        /// <param name="severity">Степень турбулентности (0-1, где 1 = максимальная)</param>
        public void Update(float severity, float dt)
        {
            if (severity <= 0f) return;

            turbulenceIntensity = Mathf.Clamp01(severity);

            _updateTimer += dt;

            // Обновляем случайную силу с фиксированным интервалом
            if (_updateTimer >= updateInterval)
            {
                _updateTimer = 0f;
                GenerateTurbulenceForce(severity);
                GenerateTurbulenceTorque(severity);
            }

            // Применяем силу и момент
            _rb.AddForce(_currentTurbulenceForce, ForceMode.Force);
            _rb.AddTorque(_currentTurbulenceTorque, ForceMode.Force);

            // Cinemachine Impulse при высокой турбулентности
            if (turbulenceIntensity > 0.3f)
            {
                ApplyTurbulenceImpulse(turbulenceIntensity);
            }
        }

        // Текущий момент турбулентности
        private Vector3 _currentTurbulenceTorque;

        /// <summary>
        /// Сгенерировать случайный момент вращения для тряски (крен, тангаж, рыскание).
        /// </summary>
        private void GenerateTurbulenceTorque(float severity)
        {
            float mass = _rb.mass;
            float totalTorque = turbulenceIntensity * severity * mass * forceMultiplier * 0.3f;

            // Случайные моменты вращения (крен сильнее, тангаж слабее, рыскание минимальный)
            _currentTurbulenceTorque = new Vector3(
                Random.Range(-1f, 1f) * verticalMultiplier * totalTorque,   // крен (roll)
                Random.Range(-0.3f, 0.3f) * totalTorque,                     // рыскание (yaw)
                Random.Range(-1f, 1f) * horizontalMultiplier * totalTorque   // тангаж (pitch)
            );
        }

        /// <summary>
        /// Сгенерировать новую случайную силу турбулентности.
        /// Силы масштабируются на массу корабля и общий множитель чтобы быть ОЧЕНЬ заметными.
        /// </summary>
        private void GenerateTurbulenceForce(float severity)
        {
            // Случайные значения [-1, 1]
            float horizontalX = Random.Range(-1f, 1f);
            float horizontalZ = Random.Range(-1f, 1f);
            float verticalY = Random.Range(-1f, 1f);

            // Масштабируем на массу корабля и множители
            float mass = _rb.mass;
            float totalForce = turbulenceIntensity * severity * mass * forceMultiplier;

            // Применяем множители по осям
            _currentTurbulenceForce = new Vector3(
                horizontalX * horizontalMultiplier * totalForce,
                verticalY * verticalMultiplier * totalForce,
                horizontalZ * horizontalMultiplier * totalForce
            );

            // Debug для калибровки
            Debug.Log($"[Turbulence] Force: {_currentTurbulenceForce.magnitude:F0}N, severity: {severity:F2}, mass: {mass:F0}");
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

        // ============================================================
        // Cinemachine Impulse — тряска камеры при турбулентности
        // Определяется через Reflection — работает с любой версией Cinemachine
        // ============================================================

        private void _tryInitializeCinemachine()
        {
            // Пытаемся найти тип CinemachineImpulseSource через reflection
            _impulseSourceType = System.Type.GetType("Cinemachine.CinemachineImpulseSource, Cinemachine");
            
            if (_impulseSourceType == null)
            {
                // Пробуем альтернативное имя (Cinemachine 3.x)
                _impulseSourceType = System.Type.GetType("Unity.Cinemachine.CinemachineImpulseSource, Unity.Cinemachine");
            }

            if (_impulseSourceType == null)
            {
                // Cinemachine не установлен
                _hasCinemachine = false;
                Debug.Log("[TurbulenceEffect] Cinemachine not detected — impulse disabled (install Cinemachine package for camera shake)");
                return;
            }

            _hasCinemachine = true;

            // Пытаемся найти существующий компонент
            _impulseSource = _rb.GetComponent(_impulseSourceType);
            
            if (_impulseSource == null)
            {
                // Добавляем CinemachineDefaultImpulseSource
                var defaultImpulseType = System.Type.GetType("Cinemachine.CinemachineDefaultImpulseSource, Cinemachine");
                if (defaultImpulseType == null)
                {
                    defaultImpulseType = System.Type.GetType("Unity.Cinemachine.CinemachineDefaultImpulseSource, Unity.Cinemachine");
                }

                if (defaultImpulseType != null)
                {
                    _impulseSource = _rb.gameObject.AddComponent(defaultImpulseType);
                    Debug.Log("[TurbulenceEffect] Cinemachine Impulse Source added (CinemachineDefaultImpulseSource)");
                }
                else
                {
                    Debug.LogWarning("[TurbulenceEffect] Cinemachine found but CinemachineDefaultImpulseSource not available");
                }
            }
            else
            {
                Debug.Log("[TurbulenceEffect] Cinemachine Impulse Source found on ship");
            }
        }

        private void ApplyTurbulenceImpulse(float severity)
        {
            if (!_hasCinemachine || _impulseSource == null || _impulseSourceType == null)
                return;

            // Вызываем GenerateImpulse(severity * 2f) через reflection
            var method = _impulseSourceType.GetMethod("GenerateImpulse", new[] { typeof(float) });
            if (method != null)
            {
                method.Invoke(_impulseSource, new object[] { severity * 2f });
            }
            else
            {
                // Пробуем GenerateImpulse(Vector3)
                var methodVec = _impulseSourceType.GetMethod("GenerateImpulse", new[] { typeof(Vector3) });
                if (methodVec != null)
                {
                    methodVec.Invoke(_impulseSource, new object[] { Vector3.up * severity * 2f });
                }
            }
        }
    }
}
