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
#pragma warning disable 0414
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
#pragma warning restore 0414
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

        [Header("Ветер и Окружающая Среда (Сессия 3)")]
        [Tooltip("Влияние ветра на корабль (1.0 = полный снос, 0.0 = игнор)")]
        [SerializeField] private float windInfluence = 0.5f;
        [Tooltip("Экспозиция к ветру (зависит от класса: Light=1.2, Medium=1.0, Heavy=0.7, HeavyII=0.5)")]
        [SerializeField] private float windExposure = 1.0f;
        [Tooltip("Время затухания ветра при выходе из зоны")]
        [SerializeField] private float windDecayTime = 1.5f;

        [Header("Cargo (Сессия 2)")]
        [Tooltip("Система груза корабля (влияет на скорость)")]
        [SerializeField] private ProjectC.Player.CargoSystem cargoSystem;

        [Header("Модули (Сессия 4)")]
        [Tooltip("Менеджер модулей корабля")]
        [SerializeField] private ShipModuleManager moduleManager;

        [Header("Мезиевая Тяга (Сессия 5)")]
        [Tooltip("Активатор мезиевых модулей")]
        [SerializeField] private MeziyModuleActivator meziyActivator;

        [Tooltip("Система топлива корабля")]
        [SerializeField] private ShipFuelSystem fuelSystem;

        [Tooltip("Визуал сопел при мезиевой тяге (опционально)")]
        [SerializeField] private MeziyThrusterVisual meziyVisual;

        // Модификаторы от модулей (применяются в FixedUpdate)
        private float _moduleThrustMult = 1f;
        private float _moduleYawMult = 1f;
        private float _modulePitchMult = 1f;
        private float _moduleRollMult = 1f;
        private float _moduleLiftMult = 1f;
        private float _moduleMaxSpeedMod = 0f;
        private float _moduleWindExposureMod = 0f;

        // Состояние мезиевой тяги
        private bool _rollUnlocked = false;  // Разблокировка крена через MODULE_ROLL
        private Vector3 _activeMeziyTorque;  // Применяемый момент от мезиевой тяги
        private bool _meziyActive = false;    // Флаг активной мезиевой тяги

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
        private float _currentRollRate;  // Сессия 5: roll для MODULE_ROLL

        // Velocity tracking для SmoothDamp (ref параметры)
        private float _yawVelocitySmooth;
        private float _pitchVelocitySmooth;
        private float _liftVelocitySmooth;
        private float _thrustVelocitySmooth;
        private float _rollVelocitySmooth;  // Сессия 5: roll smooth

        // Таймер отсутствия ввода (для стабилизации)
        private float _noInputTimer = 0f;

        // Сессия 2: Система коридоров высот
        private AltitudeStatus _currentAltitudeStatus = AltitudeStatus.Safe;
        private TurbulenceEffect _turbulence;
        private SystemDegradationEffect _degradation;
        private DegradationModifiers _currentDegradationModifiers;

        // Активный коридор (обновляется каждый кадр)
        private AltitudeCorridorData _activeCorridor;

        // Wind state — зарегистрированные зоны
        private List<ProjectC.Ship.WindZone> _activeWindZones = new();
        private Vector3 _currentWindForce;

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

            // Инициализация модулей (Сессия 4)
            InitializeModules();

            // Инициализация топлива (Сессия 5)
            InitializeFuelSystem();

            // Инициализация между (Сессия 5_2)
            InitializeMeziySystem();

            // Авто-назначение Debug HUD (Сессия 5_2)
            InitializeDebugHUD();

        // Инициализация ветра
            _currentWindForce = Vector3.zero;

            // REFACTORED: Register with InteractableManager for trigger-based ship detection
            Core.InteractableManager.RegisterShip(this);
        }

        private new void OnDestroy()
        {
            // Cleanup: unregister from InteractableManager
            Core.InteractableManager.UnregisterShip(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Also register with InteractableManager when player enters trigger
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                Core.InteractableManager.RegisterShip(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Unregister when player exits trigger
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                Core.InteractableManager.UnregisterShip(this);
            }
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
                _turbulence.SetShipClassMultiplier(shipFlightClass);
                _degradation = new SystemDegradationEffect(_rb, transform);
            }

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
                    windExposure = 1.2f;
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
                    windExposure = 1.0f;
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
                    windExposure = 0.7f;
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
                    windExposure = 0.5f;
                    if (_rb != null) _rb.mass = 2000f;
                    break;
            }

            // Обновить Rigidbody если есть
            if (_rb != null)
            {
                _rb.linearDamping = linearDrag;
                _rb.angularDamping = angularDrag;
            }

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

            // 1.5. Применяем модификаторы модулей (Сессия 4)
            ApplyModuleModifiers();

            // 1.6. Обновить между состояния (Сессия 5_2 -- continuous mode)
            if (meziyActivator != null)
                meziyActivator.Tick(dt);

            // 1.7. Проверить топливо — если пусто или ниже порога, отключить управление (Сессия 5)
            // Порог: корабль заблокирован пока fuel < 10 (≈33 секунды regen для Medium класса)
            const float controlThreshold = 10f;
            bool engineStalled = false;
            if (fuelSystem != null && fuelSystem.CurrentFuel < controlThreshold)
            {
                engineStalled = true;
                avgThrust = 0f;
                avgYaw = 0f;
                avgPitch = 0f;
                avgVertical = 0f;
                anyBoost = false;
            }

            // 1.8. Атмосферная дозаправка (клавиша L, Сессия 5)
            // РАБОТАЕТ ТОЛЬКО когда корабль неподвижен (velocity ~ 0, thrust ~ 0)
            bool isRefueling = false;
            if (fuelSystem != null && !engineStalled)
            {
                bool isStationary = _rb.linearVelocity.magnitude < 1f && Mathf.Abs(_currentThrust) < 1f;

                if (isStationary)
                {
                    if (IsKeyDown(KeyCode.L))
                    {
                        fuelSystem.RefuelAtmospheric(dt);
                        isRefueling = true;
                    }
                    else
                    {
                        fuelSystem.StopRefueling();
                    }
                }
                else
                {
                    fuelSystem.StopRefueling();
                }
            }

            // 1.85. Между passive/active mode (Сессия 5_3 — обновлённый маппинг)
            // Принцип: модуль установлен = пассивный эффект (бесплатно, без частиц)
            // Клавиша направления зажата = активный выхлоп (расход топлива, частицы, torque)
            // Перегрев после 10 сек непрерывного активного использования → кулдаун 15 сек
            //
            // Управление (зажатие):
            //   MODULE_MEZIY_PITCH:  C (нос вверх, dir=-1), V (нос вниз, dir=+1)
            //   MODULE_MEZIY_ROLL:   Z (крен влево, dir=-1), X (крен вправо, dir=+1)
            //   MODULE_MEZIY_YAW:    Shift+A (влево), Shift+D (вправо)
            //   MODULE_MEZIY_THRUST: Shift+W (ускорение), Shift+S (торможение) — если будет добавлен
            if (meziyActivator != null && !engineStalled && fuelSystem != null && fuelSystem.CurrentFuel >= 5f)
            {
                bool shiftHeld = IsKeyDown(KeyCode.LeftShift) || IsKeyDown(KeyCode.RightShift);

                // MODULE_MEZIY_PITCH: C/V
                if (meziyActivator.IsModuleInstalled("MODULE_MEZIY_PITCH"))
                {
                    if (IsKeyDown(KeyCode.C))
                        meziyActivator.TryActivate("MODULE_MEZIY_PITCH", -1f);
                    else if (IsKeyDown(KeyCode.V))
                        meziyActivator.TryActivate("MODULE_MEZIY_PITCH", +1f);
                    else
                        meziyActivator.Deactivate("MODULE_MEZIY_PITCH");
                }

                // MODULE_MEZIY_ROLL: Z/X
                if (meziyActivator.IsModuleInstalled("MODULE_MEZIY_ROLL"))
                {
                    if (IsKeyDown(KeyCode.Z))
                        meziyActivator.TryActivate("MODULE_MEZIY_ROLL", -1f);
                    else if (IsKeyDown(KeyCode.X))
                        meziyActivator.TryActivate("MODULE_MEZIY_ROLL", +1f);
                    else
                        meziyActivator.Deactivate("MODULE_MEZIY_ROLL");
                }

                // MODULE_MEZIY_YAW: Shift+A / Shift+D
                if (meziyActivator.IsModuleInstalled("MODULE_MEZIY_YAW"))
                {
                    if (shiftHeld && IsKeyDown(KeyCode.A))
                        meziyActivator.TryActivate("MODULE_MEZIY_YAW", -1f);
                    else if (shiftHeld && IsKeyDown(KeyCode.D))
                        meziyActivator.TryActivate("MODULE_MEZIY_YAW", +1f);
                    else
                        meziyActivator.Deactivate("MODULE_MEZIY_YAW");
                }

                // MODULE_MEZIY_THRUST: Shift+W (ускорение) / Shift+S (торможение)
                if (meziyActivator.IsModuleInstalled("MODULE_MEZIY_THRUST"))
                {
                    if (shiftHeld && IsKeyDown(KeyCode.W))
                        meziyActivator.TryActivate("MODULE_MEZIY_THRUST", +1f);
                    else if (shiftHeld && IsKeyDown(KeyCode.S))
                        meziyActivator.TryActivate("MODULE_MEZIY_THRUST", -1f);
                    else
                        meziyActivator.Deactivate("MODULE_MEZIY_THRUST");
                }
            }
            else if (meziyActivator != null)
            {
                // Нет топлива или engine stalled -- деактивируем всё
                meziyActivator.Deactivate("MODULE_MEZIY_PITCH");
                meziyActivator.Deactivate("MODULE_MEZIY_ROLL");
                meziyActivator.Deactivate("MODULE_MEZIY_YAW");
                meziyActivator.Deactivate("MODULE_MEZIY_THRUST");
            }

            // 1.9. Определяем hasInput переменные заранее (для fuel check и таймера)
            bool hasYawInputCheck = Mathf.Abs(avgYaw) > 0.01f;
            bool hasPitchInputCheck = Mathf.Abs(avgPitch) > 0.01f;
            bool hasRollInputCheck = false;
            if (_rollUnlocked && !engineStalled)
            {
                float rollInput = GetCurrentRollInput();
                hasRollInputCheck = Mathf.Abs(rollInput) > 0.01f;
            }

            // Расход/регенерация топлива (Сессия 5)
            // Топливо тратится от ВСЕХ действий: thrust, yaw, pitch, lift, roll
            if (fuelSystem != null)
            {
                bool hasAnyAction = Mathf.Abs(avgThrust) > 0.01f
                    || hasYawInputCheck || hasPitchInputCheck
                    || Mathf.Abs(avgVertical) > 0.01f
                    || hasRollInputCheck;

                if (engineStalled || !hasAnyAction)
                {
                    // Нет ввода — регенерация
                    fuelSystem.RegenFuel(dt);
                }
                else
                {
                    // Есть ввод — расход (сумма всех действий)
                    float totalActivity = Mathf.Abs(avgThrust)
                        + Mathf.Abs(avgYaw) + Mathf.Abs(avgPitch)
                        + Mathf.Abs(avgVertical);
                    // Нормализуем: 4 канала → множитель ~0.25 чтобы базовый расход при full thrust
                    float activityFactor = Mathf.Clamp01(totalActivity * 0.25f);
                    fuelSystem.ConsumeFuelPerSecond(dt, activityFactor);
                }
            }

            // 2. Smooth thrust ramp-up (0.3s до полной тяги)
            // thrustPenalty применяется при дозаправке отдельно в ClampVelocity
            float targetThrust = avgThrust * thrustForce * _moduleThrustMult;
            _currentThrust = Mathf.SmoothDamp(_currentThrust, targetThrust, ref _thrustVelocitySmooth, thrustSmoothTime);

            // 3. Smooth yaw с затуханием (0.6s smooth, 1.0s decay)
            float targetYawRate = avgYaw * yawForce * _moduleYawMult;
            bool hasYawInput = Mathf.Abs(avgYaw) > 0.01f;
            _currentYawRate = hasYawInput
                ? Mathf.SmoothDamp(_currentYawRate, targetYawRate, ref _yawVelocitySmooth, yawSmoothTime)
                : Mathf.SmoothDamp(_currentYawRate, 0f, ref _yawVelocitySmooth, yawDecayTime);

            // 4. Smooth pitch с затуханием (0.7s smooth, 0.8s decay)
            float targetPitchRate = avgPitch * pitchForce * _modulePitchMult;
            bool hasPitchInput = Mathf.Abs(avgPitch) > 0.01f;
            _currentPitchRate = hasPitchInput
                ? Mathf.SmoothDamp(_currentPitchRate, targetPitchRate, ref _pitchVelocitySmooth, pitchSmoothTime)
                : Mathf.SmoothDamp(_currentPitchRate, 0f, ref _pitchVelocitySmooth, pitchDecayTime);

            // 5. Smooth lift (очень медленно, 1.0s)
            float targetLift = avgVertical * verticalForce * _moduleLiftMult;
            _currentLiftForce = Mathf.SmoothDamp(_currentLiftForce, targetLift, ref _liftVelocitySmooth, liftSmoothTime);
            // Clamp к максимальной скорости лифта (Сессия 2: используем активный коридор)
            float maxLiftSpeed = (_activeCorridor != null && _activeCorridor.corridorId == "global") ? 2.5f : 2.0f;
            float maxLiftForce = maxLiftSpeed * _rb.mass * Mathf.Abs(Physics.gravity.y);
            _currentLiftForce = Mathf.Clamp(_currentLiftForce, -maxLiftForce, maxLiftForce);

            // 5.5. Roll (Z/C клавиши) -- непрерывный крен
            float rollForce = _rb.mass * 0.2f * _moduleRollMult;  // 200 для Medium * roll modifier
            bool hasRollInput = false;
            if (!engineStalled)
            {
                float rollInput = GetCurrentRollInput();
                hasRollInput = Mathf.Abs(rollInput) > 0.01f;
                float targetRollRate = rollInput * rollForce;
                _currentRollRate = hasRollInput
                    ? Mathf.SmoothDamp(_currentRollRate, targetRollRate, ref _rollVelocitySmooth, 0.4f)
                    : Mathf.SmoothDamp(_currentRollRate, 0f, ref _rollVelocitySmooth, 0.8f);
            }
            else
            {
                // Roll заблокирован -- затухаем к 0
                _currentRollRate = Mathf.SmoothDamp(_currentRollRate, 0f, ref _rollVelocitySmooth, 0.5f);
            }

            // 6. Обновляем таймер отсутствия ввода
            bool hasAnyInput = Mathf.Abs(avgThrust) > 0.01f || hasYawInput || hasPitchInput || Mathf.Abs(avgVertical) > 0.01f || hasRollInput;
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
            ApplyRotation(_currentYawRate, _currentPitchRate, _currentRollRate);

            // 9. Стабилизация (если нет ввода 0.5s+)
            if (autoStabilize && _noInputTimer > 0.5f)
                ApplyStabilization();

            // 9.5. Применить мезиевые эффекты (Сессия 5)
            ApplyMeziyEffects(dt);

            // 9.6. Ветер (Сессия 3)
            ApplyWind(dt);

            // 10. Ограничение скорости (с учётом штрафа дозаправки)
            ClampVelocity(isRefueling);

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

        private void ApplyRotation(float yawRate, float pitchRate, float rollRate)
        {
            if (Mathf.Abs(yawRate) > 0.01f)
                _rb.AddTorque(Vector3.up * yawRate, ForceMode.Force);

            if (Mathf.Abs(pitchRate) > 0.01f)
                _rb.AddTorque(transform.right * -pitchRate, ForceMode.Force);

            // Roll: применяем по локальной оси forward
            if (Mathf.Abs(rollRate) > 0.01f)
                _rb.AddTorque(transform.forward * rollRate, ForceMode.Force);
        }

        private void ApplyStabilization()
        {
            // Получаем текущие углы в диапазоне [-180, 180]
            float currentPitch = GetNormalizedPitch();
            float currentRoll = GetNormalizedRoll();

            // Ошибка (отклонение от горизонта)
            float pitchError = currentPitch;  // desired = 0
            float rollError = currentRoll;    // desired = 0

            // MODULE_ROLL: если крен разблокирован, уменьшить стабилизацию крена
            float effectiveRollStabForce = _rollUnlocked ? rollStabForce * 0.3f : rollStabForce;

            // PI-подобный контроллер: возврат к горизонту за ~3с
            Vector3 stabilizationTorque = new Vector3(
                -pitchError * pitchStabForce,   // pitch к 0
                0f,                              // yaw НЕ стабилизируется
                -rollError * effectiveRollStabForce      // roll к 0 (мягче если разблокирован)
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

        /// <summary>
        /// Сессия 4: Инициализация системы модулей.
        /// Вызывается из Awake().
        /// </summary>
        private void InitializeModules()
        {
            if (moduleManager != null)
            {
                moduleManager.Initialize(shipFlightClass);
            }
        }

        /// <summary>
        /// Сессия 4: Применить модификаторы от установленных модулей.
        /// Вызывается каждый FixedUpdate после AverageInputs.
        /// Сессия 5_3: добавлены пассивные мезиевые эффекты.
        /// </summary>
        private void ApplyModuleModifiers()
        {
            // Сбросить roll unlock каждый кадр (пересчитывается из слотов)
            _rollUnlocked = false;

            if (moduleManager != null)
            {
                _moduleThrustMult = moduleManager.GetThrustMultiplier();
                _moduleYawMult = moduleManager.GetYawMultiplier();
                _modulePitchMult = moduleManager.GetPitchMultiplier();
                _moduleRollMult = moduleManager.GetRollMultiplier();
                _moduleLiftMult = moduleManager.GetLiftMultiplier();
                _moduleMaxSpeedMod = moduleManager.GetMaxSpeedModifier();
                _moduleWindExposureMod = moduleManager.GetWindExposureModifier();

                // Сессия 5_3: пассивные мезиевые эффекты (умножаются поверх обычных модульных множителей)
                // Сессия 5_4: добавлен Thrust
                if (meziyActivator != null)
                {
                    _modulePitchMult *= meziyActivator.GetPassiveModifier(MeziyAxis.Pitch);
                    _moduleRollMult *= meziyActivator.GetPassiveModifier(MeziyAxis.Roll);
                    _moduleYawMult *= meziyActivator.GetPassiveModifier(MeziyAxis.Yaw);
                    _moduleThrustMult *= meziyActivator.GetPassiveModifier(MeziyAxis.Thrust);
                }

                // Проверить MODULE_ROLL для разблокировки крена
                CheckRollUnlock();
            }
            else
            {
                // Если менеджера нет — базовые значения
                _moduleThrustMult = 1f;
                _moduleYawMult = 1f;
                _modulePitchMult = 1f;
                _moduleRollMult = 1f;
                _moduleLiftMult = 1f;
                _moduleMaxSpeedMod = 0f;
                _moduleWindExposureMod = 0f;
            }
        }

        /// <summary>
        /// Сессия 5: Проверить установлен ли MODULE_ROLL.
        /// Если да — разблокировать крен.
        /// </summary>
        private void CheckRollUnlock()
        {
            if (moduleManager == null) return;

            foreach (var slot in moduleManager.slots)
            {
                if (slot != null && slot.isOccupied && slot.installedModule.moduleId == "MODULE_ROLL")
                {
                    _rollUnlocked = true;
                    return;
                }
            }
        }

        private void ClampVelocity(bool isRefueling = false)
        {
            // Базовая maxSpeed + модификатор от модулей
            float effectiveMaxSpeed = maxSpeed + _moduleMaxSpeedMod;

            // Штраф к скорости при атмосферной дозаправке
            if (isRefueling && fuelSystem != null)
                effectiveMaxSpeed *= fuelSystem.speedPenaltyMult;

            if (_rb.linearVelocity.magnitude > effectiveMaxSpeed)
                _rb.linearVelocity = _rb.linearVelocity.normalized * effectiveMaxSpeed;
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

        /// <summary>
        /// Сессия 5_3: Применить passive/active между эффекты к кораблю.
        /// Вызывается из FixedUpdate после стабилизации.
        /// - Пассивный эффект: бесплатный множитель управления (через ApplyModuleModifiers)
        /// - Активный выхлоп: torque + расход топлива + частицы
        /// - Перегрев: частицы ВЫКЛ, torque ВЫКЛ, кулдаун
        /// </summary>
        private void ApplyMeziyEffects(float dt)
        {
            if (meziyActivator == null) return;

            // Расход топлива для активных модулей (пассивные НЕ расходуют)
            meziyActivator.ConsumeFuelForActiveModules(dt);

            _meziyActive = false;
            _activeMeziyTorque = Vector3.zero;
            float meziyThrustForce = 0f;

            var allStates = meziyActivator.GetActiveStates();

            foreach (var kvp in allStates)
            {
                string moduleId = kvp.Key;
                var state = kvp.Value;

                // Пропускаем неактивные и перегретые
                if (!state.isActive || state.isOnCooldown) continue;

                _meziyActive = true;
                ShipModule module = state.module;

                // Применить torque по направлению из состояния (без повторного IsKeyDown)
                float dir = state.activeDirection;
                if (Mathf.Abs(dir) < 0.01f) continue;

                switch (moduleId)
                {
                    case "MODULE_MEZIY_PITCH":
                        _activeMeziyTorque += transform.right * module.meziyForce * dir;
                        break;

                    case "MODULE_MEZIY_ROLL":
                        _activeMeziyTorque += transform.forward * module.meziyForce * dir;
                        break;

                    case "MODULE_MEZIY_YAW":
                        _activeMeziyTorque += Vector3.up * module.meziyForce * dir;
                        break;

                    case "MODULE_MEZIY_THRUST":
                        // Thrust — не torque, а сила вдоль оси transform.forward
                        // dir = +1 (вперёд, Shift+W) или -1 (назад/торможение, Shift+S)
                        meziyThrustForce += module.meziyForce * dir;
                        _meziyActive = true;
                        break;
                }
            }

            // Применить torque к Rigidbody
            if (_activeMeziyTorque.sqrMagnitude > 0.01f)
            {
                _rb.AddTorque(_activeMeziyTorque, ForceMode.Force);
            }

            // Применить meziy thrust boost (MODULE_MEZIY_THRUST)
            if (Mathf.Abs(meziyThrustForce) > 0.01f)
            {
                _rb.AddForce(transform.forward * meziyThrustForce * dt, ForceMode.Force);
            }

            // Обновить визуал (только при активной тяге, НЕ при перегреве)
            if (meziyVisual != null)
            {
                if (_meziyActive)
                    meziyVisual.Activate();
                else
                    meziyVisual.Deactivate();
            }
        }

        /// <summary>
        /// Безопасная проверка клавиш (Input Manager vs Input System).
        /// Возвращает false если Input недоступен.
        /// </summary>
        private bool IsKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            // Input System пакет
            try
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard == null) return false;
                var keyControl = KeyCodeToKey(key);
                return keyboard[keyControl].isPressed;
            }
            catch
            {
                return false;
            }
#else
            // Old Input Manager
            try
            {
                return Input.GetKey(key);
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// Конвертировать KeyCode в Key (Input System).
        /// </summary>
        private UnityEngine.InputSystem.Key KeyCodeToKey(KeyCode code)
        {
            // Маппинг для используемых клавиш
            switch (code)
            {
                case KeyCode.A: return UnityEngine.InputSystem.Key.A;
                case KeyCode.B: return UnityEngine.InputSystem.Key.B;
                case KeyCode.C: return UnityEngine.InputSystem.Key.C;
                case KeyCode.D: return UnityEngine.InputSystem.Key.D;
                case KeyCode.E: return UnityEngine.InputSystem.Key.E;
                case KeyCode.F: return UnityEngine.InputSystem.Key.F;
                case KeyCode.G: return UnityEngine.InputSystem.Key.G;
                case KeyCode.H: return UnityEngine.InputSystem.Key.H;
                case KeyCode.I: return UnityEngine.InputSystem.Key.I;
                case KeyCode.J: return UnityEngine.InputSystem.Key.J;
                case KeyCode.K: return UnityEngine.InputSystem.Key.K;
                case KeyCode.L: return UnityEngine.InputSystem.Key.L;
                case KeyCode.M: return UnityEngine.InputSystem.Key.M;
                case KeyCode.N: return UnityEngine.InputSystem.Key.N;
                case KeyCode.O: return UnityEngine.InputSystem.Key.O;
                case KeyCode.P: return UnityEngine.InputSystem.Key.P;
                case KeyCode.Q: return UnityEngine.InputSystem.Key.Q;
                case KeyCode.R: return UnityEngine.InputSystem.Key.R;
                case KeyCode.S: return UnityEngine.InputSystem.Key.S;
                case KeyCode.T: return UnityEngine.InputSystem.Key.T;
                case KeyCode.U: return UnityEngine.InputSystem.Key.U;
                case KeyCode.V: return UnityEngine.InputSystem.Key.V;
                case KeyCode.W: return UnityEngine.InputSystem.Key.W;
                case KeyCode.X: return UnityEngine.InputSystem.Key.X;
                case KeyCode.Y: return UnityEngine.InputSystem.Key.Y;
                case KeyCode.Z: return UnityEngine.InputSystem.Key.Z;
                case KeyCode.Alpha1: return UnityEngine.InputSystem.Key.Digit1;
                case KeyCode.Alpha2: return UnityEngine.InputSystem.Key.Digit2;
                case KeyCode.Alpha3: return UnityEngine.InputSystem.Key.Digit3;
                case KeyCode.LeftShift: return UnityEngine.InputSystem.Key.LeftShift;
                case KeyCode.RightShift: return UnityEngine.InputSystem.Key.RightShift;
                case KeyCode.Space: return UnityEngine.InputSystem.Key.Space;
                default: return UnityEngine.InputSystem.Key.None;
            }
        }
#endif

        /// <summary>
        /// Получить текущий ввод крена (Z = влево, C = вправо).
        /// Возвращает -1 (влево), 0 (нет), 1 (вправо).
        /// </summary>
        private float GetCurrentRollInput()
        {
            float z = IsKeyDown(KeyCode.Z) ? -1f : 0f;
            float c = IsKeyDown(KeyCode.C) ? 1f : 0f;
            return z + c;
        }

        /// <summary>
        /// Получить текущий ввод тангажа для мезиевого модуля.
        /// </summary>
        private float GetCurrentPitchInput()
        {
            float w = IsKeyDown(KeyCode.W) ? -1f : 0f;  // W = нос вверх
            float s = IsKeyDown(KeyCode.S) ? 1f : 0f;   // S = нос вниз
            return w + s;
        }

        /// <summary>
        /// Получить текущий ввод рыскания для мезиевого модуля.
        /// </summary>
        private float GetCurrentYawInput()
        {
            float a = IsKeyDown(KeyCode.A) ? -1f : 0f;
            float d = IsKeyDown(KeyCode.D) ? 1f : 0f;
            return a + d;
        }

        /// <summary>
        /// Сессия 5: Инициализация системы топлива.
        /// Вызывается из Awake().
        /// </summary>
        private void InitializeFuelSystem()
        {
            if (fuelSystem != null)
            {
                fuelSystem.Initialize(shipFlightClass);
                Debug.Log($"[ShipController] Fuel system initialized. Fuel: {fuelSystem.CurrentFuel}/{fuelSystem.MaxFuel}");
            }
            else
            {
            }
        }

        /// <summary>
        /// Сессия 5_2: Авто-назначение Debug HUD.
        /// Добавляет ShipDebugHUD если его нет на объекте.
        /// </summary>
        private void InitializeDebugHUD()
        {
            var hud = GetComponent<ShipDebugHUD>();
            if (hud == null)
            {
                hud = gameObject.AddComponent<ShipDebugHUD>();
            }

            // Сессия 5_4: авто-добавление MeziyStatusHUD
            var meziyHUD = GetComponent<MeziyStatusHUD>();
            if (meziyHUD == null)
            {
                meziyHUD = gameObject.AddComponent<MeziyStatusHUD>();
            }
        }

        /// <summary>
        /// Сессия 5_3: Инициализация системы между (passive/active режим).
        /// Вызывается из Awake().
        /// </summary>
        private void InitializeMeziySystem()
        {
            if (meziyActivator != null)
            {
                meziyActivator.Initialize();

                // Diagnostic: which meziy modules found
                int pitchFound = meziyActivator.IsModuleInstalled("MODULE_MEZIY_PITCH") ? 1 : 0;
                int rollFound = meziyActivator.IsModuleInstalled("MODULE_MEZIY_ROLL") ? 1 : 0;
                int yawFound = meziyActivator.IsModuleInstalled("MODULE_MEZIY_YAW") ? 1 : 0;
                int thrustFound = meziyActivator.IsModuleInstalled("MODULE_MEZIY_THRUST") ? 1 : 0;

                if (rollFound == 0 || yawFound == 0)
                {
                    Debug.LogWarning("[ShipController] Some meziy modules NOT found!");
                }
            }
        }

        /// <summary>
        /// Применить внешнюю силу (например, ветер из WindZone).
        /// Вызывается только на сервере.
        /// </summary>
        public void ApplyExternalForce(Vector3 force)
        {
            if (!IsServer || _rb == null) return;
            _rb.AddForce(force, ForceMode.Force);
        }

        /// <summary>
        /// Зарегистрировать зону ветра (вызывается из WindZone.OnTriggerEnter)
        /// </summary>
        public void RegisterWindZone(ProjectC.Ship.WindZone zone)
        {
            if (!_activeWindZones.Contains(zone))
            {
                _activeWindZones.Add(zone);
            }
        }

        /// <summary>
        /// Отрегистрировать зону ветра (вызывается из WindZone.OnTriggerExit)
        /// </summary>
        public void UnregisterWindZone(ProjectC.Ship.WindZone zone)
        {
            _activeWindZones.Remove(zone);
        }

        /// <summary>
        /// Получить количество активных зон ветра (для дебага)
        /// </summary>
        public int GetActiveWindZoneCount() => _activeWindZones.Count;

        /// <summary>
        /// Применить силу ветра от всех активных зон.
        /// Ветер суммируется векторно от всех зон, плавный lerp при входе/выходе.
        /// </summary>
        private void ApplyWind(float dt)
        {
            if (_activeWindZones.Count == 0)
            {
                // Затухание к 0 при выходе из всех зон
                _currentWindForce = Vector3.Lerp(_currentWindForce, Vector3.zero, dt / windDecayTime);
            }
            else
            {
                // Суммировать ветер от всех активных зон
                Vector3 totalWind = Vector3.zero;
                foreach (var zone in _activeWindZones)
                {
                    if (zone != null && zone.windData != null)
                    {
                        totalWind += zone.GetWindForceAtPosition(transform.position);
                    }
                }
                // Lerp к целевой силе (плавный переход между зонами)
                _currentWindForce = Vector3.Lerp(_currentWindForce, totalWind, dt / windDecayTime);
            }

            // Применить с учётом влияния и экспозиции (базовая + модификатор модулей)
            float effectiveWindExposure = windExposure + _moduleWindExposureMod;
            Vector3 windEffect = _currentWindForce * windInfluence * effectiveWindExposure;
            if (windEffect.sqrMagnitude > 0.01f)
            {
                _rb.AddForce(windEffect, ForceMode.Force);
            }
        }
    }
}
