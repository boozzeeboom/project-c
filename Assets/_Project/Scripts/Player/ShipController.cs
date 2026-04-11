using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using ProjectC.Ship;

namespace ProjectC.Player
{
    /// <summary>
    /// Классы кораблей для физики полёта — определяют характеристики движения.
    /// НЕ путать с ProjectC.Player.ShipClass (грузовые характеристики из CargoSystem).
    /// </summary>
    public enum ShipFlightClass
    {
        Light,      // Лёгкий: маневренный, быстрый, слабый
        Medium,     // Средний: баланс (дефолт)
        Heavy,      // Тяжёлый: медленный, устойчивый, мощный
        HeavyII     // Тяжёлый II: очень медленный, очень устойчивый
    }

    /// <summary>
    /// Сетевой контроллер корабля v2.1 — "Живые Баржи" + Altitude Corridor System (Сессия 2)
    /// Smooth movement с Mathf.SmoothDamp для frame-rate независимого сглаживания.
    /// Кооп-пилотирование: несколько игроков могут управлять одновременно.
    /// Ввод суммируется на сервере. NetworkTransform(ServerAuthority) реплицирует всем.
    ///
    /// Сессия 1: Core Smooth Movement — плавные баржи, не истребители.
    /// Сессия 2: Altitude Corridor System — коридоры высот, турбулентность, деградация.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    public class ShipController : NetworkBehaviour
    {
        [Header("Класс Корабля")]
        [Tooltip("Класс определяет характеристики полёта. НЕ путать с грузовым классом (CargoSystem).")]
        [SerializeField] private ShipFlightClass shipFlightClass = ShipFlightClass.Medium;

        [Header("Тяга")]
        [SerializeField] private float thrustForce = 650f;
        [SerializeField] private float maxSpeed = 40f;

        [Header("Вращение")]
        [Tooltip("Сила рыскания (курсовой поворот A/D). ~25°/s для лёгкого корабля")]
        [SerializeField] private float yawForce = 25f;
        [Tooltip("Сила тангажа (нос вверх/вниз мышь). ~20°/s")]
        [SerializeField] private float pitchForce = 20f;

        [Header("Вертикальное движение")]
        [SerializeField] private float verticalForce = 120f;

        [Header("Smooth Movement (Lerp)")]
        [Tooltip("Время сглаживания рыскания (курсового поворота)")]
        [SerializeField] private float yawSmoothTime = 0.6f;
        [Tooltip("Время сглаживания тангажа (носа вверх/вниз)")]
        [SerializeField] private float pitchSmoothTime = 0.7f;
        [Tooltip("Время сглаживания лифта (вертикального движения)")]
        [SerializeField] private float liftSmoothTime = 1.0f;
        [Tooltip("Время разгона/торможения тяги")]
        [SerializeField] private float thrustSmoothTime = 0.3f;
        [Tooltip("Время затухания рыскания без ввода (инерция баржи)")]
        [SerializeField] private float yawDecayTime = 1.0f;
        [Tooltip("Время затухания тангажа без ввода")]
        [SerializeField] private float pitchDecayTime = 0.8f;

        [Header("Антигравитация")]
        [SerializeField] [Range(0f, 1.5f)] private float antiGravity = 1f;

        [Header("Аэродинамика")]
        [SerializeField] private float linearDrag = 0.4f;
        [SerializeField] private float angularDrag = 8.0f;

        [Header("Стабилизация")]
        [Tooltip("Сила стабилизации тангажа (возврат к горизонту)")]
        [SerializeField] private float pitchStabForce = 15.0f;
        [Tooltip("Сила стабилизации крена (возврат к 0)")]
        [SerializeField] private float rollStabForce = 20.0f;
        [Tooltip("Максимальный угол тангажа (±градусы)")]
        [SerializeField] private float maxPitchAngle = 20f;
        [Tooltip("Автоматическая стабилизация при отсутствии ввода")]
        [SerializeField] private bool autoStabilize = true;

        [Header("Коридор Высот (Сессия 2)")]
        [Tooltip("Ссылка на систему коридоров (назначить из сцены или оставить null для автопоиска)")]
        [SerializeField] private AltitudeCorridorSystem corridorSystem;

        [Header("Cargo (Сессия 2)")]
        [Tooltip("Система груза корабля (влияет на скорость)")]
        [SerializeField] private ProjectC.Player.CargoSystem cargoSystem;

        // Rigidbody
        private Rigidbody _rb;

        // Список пилотов (кооп-управление)
        private HashSet<ulong> _pilots = new HashSet<ulong>();

        // Накопленный ввод от всех пилотов (сервер)
        private float _sumThrust, _sumYaw, _sumPitch, _sumVertical;
        private int _boostCount, _inputCount;

        // Smooth state — текущие сглаженные значения (сохраняются между кадрами)
        private float _currentYawRate;
        private float _currentPitchRate;
        private float _currentLiftForce;
        private float _currentThrust;

        // Velocity tracking для SmoothDamp (ref параметры)
        private float _yawVelocitySmooth;
        private float _pitchVelocitySmooth;
        private float _liftVelocitySmooth;
        private float _thrustVelocitySmooth;

        // Таймер отсутствия ввода (для стабилизации)
        private float _noInputTimer = 0f;

        // Сессия 2: Система коридоров высот
        private AltitudeStatus _currentAltitudeStatus = AltitudeStatus.Safe;
        private TurbulenceEffect _turbulence;
        private SystemDegradationEffect _degradation;
        private DegradationModifiers _currentDegradationModifiers;

        // Активный коридор (обновляется каждый кадр)
        private AltitudeCorridorData _activeCorridor;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rb.linearDamping = linearDrag;
                _rb.angularDamping = angularDrag;
                _rb.useGravity = true;
                _rb.constraints = RigidbodyConstraints.None;
            }

            // Сессия 2: Инициализация системы коридоров
            InitializeAltitudeSystem();

            // Применить пресет класса корабля
            ApplyShipClass();
        }

        /// <summary>
        /// Сессия 2: Инициализация системы коридоров высот.
        /// Находит AltitudeCorridorSystem на сцене или использует fallback.
        /// </summary>
        private void InitializeAltitudeSystem()
        {
            // Находим систему коридоров
            if (corridorSystem == null)
            {
                corridorSystem = FindAnyObjectByType<AltitudeCorridorSystem>();
            }

            if (corridorSystem == null && IsServer)
            {
                Debug.LogWarning("[ShipController] AltitudeCorridorSystem not found! Corridor validation disabled.");
            }

            // Создаём эффекты
            if (_rb != null)
            {
                _turbulence = new TurbulenceEffect(_rb, transform);
                _degradation = new SystemDegradationEffect(_rb, transform);
            }

            Debug.Log($"[ShipController] Altitude system initialized. CorridorSystem: {(corridorSystem != null ? "Found" : "Not Found")}");
        }

        /// <summary>
        /// Применить пресет параметров в зависимости от класса корабля.
        /// Вызывается в Awake() и при смене shipClass в Inspector.
        /// </summary>
        private void ApplyShipClass()
        {
            // Пресеты основаны на параметрах пользователя (скриншот 11.04.2026)
            // Medium = текущие значения пользователя (баланс)
            switch (shipFlightClass)
            {
                case ShipFlightClass.Light:
                    // Лёгкий: маневренный, быстрый, слабый
                    thrustForce = 500f;
                    yawForce = 3500f;
                    pitchForce = 25f;
                    verticalForce = 7000f;
                    yawSmoothTime = 0.25f;
                    pitchSmoothTime = 0.6f;
                    liftSmoothTime = 0.8f;
                    thrustSmoothTime = 0.2f;
                    yawDecayTime = 0.8f;
                    maxSpeed = 50f;
                    if (_rb != null) _rb.mass = 800f;
                    break;

                case ShipFlightClass.Medium:
                    // Средний: баланс (параметры пользователя)
                    thrustForce = 650f;
                    yawForce = 3000f;
                    pitchForce = 20f;
                    verticalForce = 8000f;
                    yawSmoothTime = 0.3f;
                    pitchSmoothTime = 0.7f;
                    liftSmoothTime = 1.0f;
                    thrustSmoothTime = 0.3f;
                    yawDecayTime = 1.0f;
                    maxSpeed = 40f;
                    if (_rb != null) _rb.mass = 1000f;
                    break;

                case ShipFlightClass.Heavy:
                    // Тяжёлый: медленный, устойчивый, мощный
                    thrustForce = 800f;
                    yawForce = 2000f;
                    pitchForce = 15f;
                    verticalForce = 6000f;
                    yawSmoothTime = 0.5f;
                    pitchSmoothTime = 0.9f;
                    liftSmoothTime = 1.2f;
                    thrustSmoothTime = 0.4f;
                    yawDecayTime = 1.5f;
                    maxSpeed = 25f;
                    if (_rb != null) _rb.mass = 1500f;
                    break;

                case ShipFlightClass.HeavyII:
                    // Тяжёлый II: очень медленный, очень устойчивый
                    thrustForce = 900f;
                    yawForce = 1500f;
                    pitchForce = 12f;
                    verticalForce = 5000f;
                    yawSmoothTime = 0.7f;
                    pitchSmoothTime = 1.1f;
                    liftSmoothTime = 1.5f;
                    thrustSmoothTime = 0.5f;
                    yawDecayTime = 2.0f;
                    maxSpeed = 18f;
                    if (_rb != null) _rb.mass = 2000f;
                    break;
            }

            // Обновить Rigidbody если есть
            if (_rb != null)
            {
                _rb.linearDamping = linearDrag;
                _rb.angularDamping = angularDrag;
            }

            Debug.Log($"[ShipController] Applied class: {shipFlightClass}");
        }

#if UNITY_EDITOR
        /// <summary>
        /// Вызывается при изменении shipClass в Inspector (Editor only).
        /// </summary>
        private void OnValidate()
        {
            // Применить пресет только если корабль уже инициализирован
            if (_rb != null)
            {
                ApplyShipClass();
            }
        }
#endif

        private void FixedUpdate()
        {
            if (_rb == null || !IsServer) return;
            if (_pilots.Count == 0) return;

            float dt = Time.fixedDeltaTime;

            // 1. Усредняем ввод от всех пилотов
            int n = Mathf.Max(1, _inputCount);
            float avgThrust = _sumThrust / n;
            float avgYaw = _sumYaw / n;
            float avgPitch = _sumPitch / n;
            float avgVertical = _sumVertical / n;
            bool anyBoost = _boostCount > 0;

            // 2. Smooth thrust ramp-up (0.3s)
            float targetThrust = avgThrust * thrustForce * (anyBoost ? 2f : 1f);
            _currentThrust = Mathf.SmoothDamp(_currentThrust, targetThrust, ref _thrustVelocitySmooth, thrustSmoothTime);

            // 3. Smooth yaw с затуханием (0.6s smooth, 1.0s decay)
            float targetYawRate = avgYaw * yawForce;
            bool hasYawInput = Mathf.Abs(avgYaw) > 0.01f;
            _currentYawRate = hasYawInput
                ? Mathf.SmoothDamp(_currentYawRate, targetYawRate, ref _yawVelocitySmooth, yawSmoothTime)
                : Mathf.SmoothDamp(_currentYawRate, 0f, ref _yawVelocitySmooth, yawDecayTime);

            // 4. Smooth pitch с затуханием (0.7s smooth, 0.8s decay)
            float targetPitchRate = avgPitch * pitchForce;
            bool hasPitchInput = Mathf.Abs(avgPitch) > 0.01f;
            _currentPitchRate = hasPitchInput
                ? Mathf.SmoothDamp(_currentPitchRate, targetPitchRate, ref _pitchVelocitySmooth, pitchSmoothTime)
                : Mathf.SmoothDamp(_currentPitchRate, 0f, ref _pitchVelocitySmooth, pitchDecayTime);

            // 5. Smooth lift (очень медленно, 1.0s)
            float targetLift = avgVertical * verticalForce;
            _currentLiftForce = Mathf.SmoothDamp(_currentLiftForce, targetLift, ref _liftVelocitySmooth, liftSmoothTime);
            // Clamp к максимальной скорости лифта (Сессия 2: используем активный коридор)
            float maxLiftSpeed = (_activeCorridor != null && _activeCorridor.corridorId == "global") ? 2.5f : 2.0f;
            float maxLiftForce = maxLiftSpeed * _rb.mass * Mathf.Abs(Physics.gravity.y);
            _currentLiftForce = Mathf.Clamp(_currentLiftForce, -maxLiftForce, maxLiftForce);

            // 6. Обновляем таймер отсутствия ввода
            bool hasAnyInput = Mathf.Abs(avgThrust) > 0.01f || hasYawInput || hasPitchInput || Mathf.Abs(avgVertical) > 0.01f;
            if (hasAnyInput)
            {
                _noInputTimer = 0f;
            }
            else
            {
                _noInputTimer += dt;
            }

            // 7. Сессия 2: Валидация высоты и применение эффектов
            ValidateAndApplyAltitudeEffects(dt);

            // 8. Применяем силы (с учётом деградации если есть)
            ApplyThrustForce(_currentThrust);
            ApplyAntiGravity();
            ApplyLiftForce(_currentLiftForce);
            ApplyRotation(_currentYawRate, _currentPitchRate);

            // 9. Стабилизация (если нет ввода 0.5s+)
            if (autoStabilize && _noInputTimer > 0.5f)
                ApplyStabilization();

            // 10. Ограничение скорости
            ClampVelocity();

            // 11. Ограничение угла тангажа
            ClampPitchAngle();

            // 12. Сброс буфера ввода
            _sumThrust = 0; _sumYaw = 0; _sumPitch = 0; _sumVertical = 0;
            _boostCount = 0; _inputCount = 0;
        }

        /// <summary>
        /// Пилот шлёт ввод на сервер
        /// </summary>
        [Rpc(SendTo.Server)]
        private void SubmitShipInputRpc(float thrust, float yaw, float pitch, float vertical, bool boost, RpcParams rpcParams = default)
        {
            if (!_pilots.Contains(rpcParams.Receive.SenderClientId)) return;

            _sumThrust += thrust;
            _sumYaw += yaw;
            _sumPitch += pitch;
            _sumVertical += vertical;
            if (boost) _boostCount++;
            _inputCount++;
        }

        public void SendShipInput(float thrust, float yaw, float pitch, float vertical, bool boost)
        {
            SubmitShipInputRpc(thrust, yaw, pitch, vertical, boost);
        }

        private void ApplyThrustForce(float currentThrust)
        {
            if (Mathf.Abs(currentThrust) < 0.01f) return;

            // Применяем штраф скорости от груза (Сессия 2)
            float cargoPenalty = 1.0f;
            if (cargoSystem != null)
            {
                cargoPenalty = cargoSystem.GetSpeedPenalty();
            }

            _rb.AddForce(transform.forward * currentThrust * cargoPenalty, ForceMode.Force);
        }

        private void ApplyAntiGravity()
        {
            if (antiGravity <= 0f) return;
            float gravityCompensation = _rb.mass * Mathf.Abs(Physics.gravity.y) * antiGravity;
            _rb.AddForce(Vector3.up * gravityCompensation, ForceMode.Force);
        }

        private void ApplyLiftForce(float currentLiftForce)
        {
            if (Mathf.Abs(currentLiftForce) < 0.01f) return;
            _rb.AddForce(Vector3.up * currentLiftForce, ForceMode.Force);
        }

        private void ApplyRotation(float yawRate, float pitchRate)
        {
            if (Mathf.Abs(yawRate) > 0.01f)
                _rb.AddTorque(Vector3.up * yawRate, ForceMode.Force);

            if (Mathf.Abs(pitchRate) > 0.01f)
                _rb.AddTorque(transform.right * -pitchRate, ForceMode.Force);
        }

        private void ApplyStabilization()
        {
            // Получаем текущие углы в диапазоне [-180, 180]
            float currentPitch = GetNormalizedPitch();
            float currentRoll = GetNormalizedRoll();

            // Ошибка (отклонение от горизонта)
            float pitchError = currentPitch;  // desired = 0
            float rollError = currentRoll;    // desired = 0

            // PI-подобный контроллер: возврат к горизонту за ~3с
            Vector3 stabilizationTorque = new Vector3(
                -pitchError * pitchStabForce,   // pitch к 0
                0f,                              // yaw НЕ стабилизируется
                -rollError * rollStabForce      // roll к 0
            );

            _rb.AddTorque(stabilizationTorque, ForceMode.Force);
        }

        /// <summary>
        /// Получить угол тангажа в диапазоне [-180, 180]
        /// </summary>
        private float GetNormalizedPitch()
        {
            float angle = transform.eulerAngles.x;
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        /// <summary>
        /// Получить угол крена в диапазоне [-180, 180]
        /// </summary>
        private float GetNormalizedRoll()
        {
            float angle = transform.eulerAngles.z;
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        private bool HasNoInput(float t, float y, float p, float v) =>
            Mathf.Abs(t) < 0.01f && Mathf.Abs(y) < 0.01f && Mathf.Abs(p) < 0.01f && Mathf.Abs(v) < 0.01f;

        private void ClampVelocity()
        {
            if (_rb.linearVelocity.magnitude > maxSpeed)
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
        }

        /// <summary>
        /// Ограничить угол тангажа ±maxPitchAngle
        /// </summary>
        private void ClampPitchAngle()
        {
            float currentPitch = GetNormalizedPitch();

            if (currentPitch > maxPitchAngle)
            {
                // "Отталкиваем" нос вниз
                float correction = (currentPitch - maxPitchAngle) * 10f;
                _rb.AddTorque(transform.right * correction, ForceMode.Force);
            }
            else if (currentPitch < -maxPitchAngle)
            {
                float correction = (currentPitch + maxPitchAngle) * 10f;
                _rb.AddTorque(transform.right * correction, ForceMode.Force);
            }
        }

        /// <summary>
        /// Сессия 2: Валидация высоты и применение эффектов коридоров.
        /// Определяет активный коридор, проверяет статус и применяет эффекты.
        /// </summary>
        private void ValidateAndApplyAltitudeEffects(float dt)
        {
            if (corridorSystem == null) return;

            float currentAlt = transform.position.y;

            // 1. Определяем активный коридор
            _activeCorridor = corridorSystem.GetActiveCorridor(transform.position);

            // 2. Получаем статус высоты
            _currentAltitudeStatus = _activeCorridor.GetStatus(currentAlt);

            // 3. Применяем эффекты в зависимости от статуса
            switch (_currentAltitudeStatus)
            {
                case AltitudeStatus.Safe:
                    // Всё OK — сбрасываем эффекты
                    if (_turbulence != null)
                        _turbulence.turbulenceIntensity = 0f;
                    _currentDegradationModifiers = new DegradationModifiers { thrust = 1f, yaw = 1f, pitch = 1f, vertical = 1f, extraDrag = 0f };
                    break;

                case AltitudeStatus.WarningLower:
                case AltitudeStatus.WarningUpper:
                    // Warning — показываем предупреждение (будет в UI)
                    // Эффекты пока не применяем
                    Debug.LogWarning($"[ShipController] Altitude Warning: {_currentAltitudeStatus} at {currentAlt:F0}m");
                    break;

                case AltitudeStatus.DangerLower:
                    // Ниже минимума — турбулентность от Завесы!
                    ApplyTurbulence(dt, currentAlt);
                    break;

                case AltitudeStatus.DangerUpper:
                    // Выше критического порога — деградация систем
                    ApplySystemDegradation(dt, currentAlt);
                    break;
            }

            // 4. Логируем для отладки (можно убрать в продакшене)
#if UNITY_EDITOR
            Debug.Log($"[ShipController] Alt: {currentAlt:F0}m | Corridor: {_activeCorridor.displayName} | Status: {_currentAltitudeStatus}");
#endif
        }

        /// <summary>
        /// Применить турбулентность при полёте ниже минимальной границы коридора.
        /// </summary>
        private void ApplyTurbulence(float dt, float currentAlt)
        {
            if (_turbulence == null || _activeCorridor == null) return;

            // Рассчитываем severity: 0 на границе, 1 на 200м ниже
            float severity = TurbulenceEffect.CalculateSeverity(currentAlt, _activeCorridor.minAltitude, 200f);

            // Применяем турбулентность
            _turbulence.Update(severity, dt);

            Debug.LogWarning($"[ShipController] TURBULENCE! Alt: {currentAlt:F0}m, Severity: {severity:F2}");
        }

        /// <summary>
        /// Применить деградацию систем при полёте выше критической высоты.
        /// </summary>
        private void ApplySystemDegradation(float dt, float currentAlt)
        {
            if (_degradation == null || _activeCorridor == null) return;

            // Критическая высота = maxAlt + criticalUpperMargin
            float criticalAlt = _activeCorridor.maxAltitude + _activeCorridor.criticalUpperMargin;

            // Рассчитываем severity: 0 на критической высоте, 1 на 500м выше
            float severity = SystemDegradationEffect.CalculateSeverity(currentAlt, criticalAlt, 500f);

            // Получаем модификаторы
            _currentDegradationModifiers = _degradation.GetModifiers(severity);

            // Применяем дополнительное сопротивление
            _degradation.ApplyExtraDrag(severity);

            Debug.LogWarning($"[ShipController] DEGRADATION! Alt: {currentAlt:F0}m, Severity: {severity:F2}, Thrust: {_currentDegradationModifiers.thrust:F2}");
        }

        public float CurrentSpeed => _rb != null ? _rb.linearVelocity.magnitude : 0f;
        public bool IsGrounded => Physics.Raycast(transform.position, Vector3.down, out _, 1.5f);
        public Vector3 GetExitPosition() => transform.position + Vector3.up * 1.5f;
        public int PilotCount => _pilots.Count;

        /// <summary>
        /// Добавить пилота (кооп — несколько могут одновременно)
        /// </summary>
        public void AddPilot(NetworkPlayer pilot)
        {
            AddPilotRpc(pilot.OwnerClientId);
        }

        [Rpc(SendTo.Everyone)]
        private void AddPilotRpc(ulong clientId, RpcParams rpcParams = default)
        {
            _pilots.Add(clientId);
            enabled = true;
        }

        /// <summary>
        /// Снять пилота
        /// </summary>
        public void RemovePilot(ulong clientId)
        {
            RemovePilotRpc(clientId);
        }

        [Rpc(SendTo.Everyone)]
        private void RemovePilotRpc(ulong clientId, RpcParams rpcParams = default)
        {
            _pilots.Remove(clientId);
            if (_pilots.Count == 0) enabled = false;
        }
    }
}
