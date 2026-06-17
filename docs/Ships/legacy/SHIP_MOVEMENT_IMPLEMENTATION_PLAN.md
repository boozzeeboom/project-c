# Ship Movement Implementation Plan — Project C

**Дата:** Апрель 2026 | **Приоритет:** P0 | **Спринт:** Текущий

---

## Цель
Переписать систему управления кораблём с «резкой и неживой» на «плавную, текучую, баржеподобную». Корабль должен ощущаться как воздушная баржа — медленные разгоны, инерция, стабилизация, отсутствие резких движений.

---

## 1. Текущее Состояние (Проблемы)

| Проблема | Описание | Где |
|----------|----------|-----|
| **Резкий yaw** | A/D поворачивают мгновенно, нет инерции | ShipController.cs:130 |
| **Резкий pitch** | Мышь Y — мгновенный наклон носа | ShipController.cs:135 |
| **Резкий lift** | Q/E — телепортация по вертикали | ShipController.cs:120 |
| **Нет стабилизации** | Корабль не возвращается к горизонту | ShipController.cs:141 (слабая) |
| **Нет коридоров** | Нет ограничения/валидации высоты | Отсутствует |
| **Нет ветра** | Нет влияния окружающей среды | Отсутствует |
| **Нет модулей** | Нет системы модульности | Отсутствует |

---

## 2. План Реализации по Сессиям

### Сессия 1: Core Smooth Movement (Текущая)

**Задачи:**
1. ✅ Переписать `ShipController.cs` — добавить smooth-переменные
2. ✅ Добавить Lerp для yaw, pitch, lift, thrust
3. ✅ Увеличить angularDrag ×2-3
4. ✅ Уменьшить yawForce ×0.3-0.4
5. ✅ Уменьшить pitchForce ×0.4-0.5
6. ✅ Уменьшить verticalForce ×0.3-0.4
7. ✅ Добавить auto-stabilization при отсутствии ввода
8. ✅ Ограничить pitch ±20°
9. ✅ Заблокировать roll (baseMaxRoll = 0)

**Тесты:**
```
Test 1: Нажать W → корабль разгоняется плавно (0.3s ramp-up), не рывком
Test 2: Нажать A/D → корабль поворачивает медленно, как баржа
Test 3: Отпустить A/D → корабль НЕ останавливается мгновенно, а продолжает 
        поворачиваться по инерции и затухает за ~1 сек
Test 4: Нажать Q/E → корабль поднимается/опускается со скоростью ~2 м/с
Test 5: Отпустить все клавиши → корабль плавно выравнивается к горизонту
Test 6: Столкновение с пиком → отскок по физике, стабилизация возвращает к горизонту
```

**Unity Test Script:** `Assets/_Project/Tests/ShipMovementTests.cs`

---

### Сессия 2: Altitude Corridor System

**Задачи:**
1. Создать `AltitudeCorridorData` (ScriptableObject) — данные коридоров
2. Создать `AltitudeCorridorSystem.cs` — runtime менеджер
3. Настроить 5 городских коридоров + глобальный
4. Server-side validation (каждые 0.5с проверка высоты)
5. Warning UI при приближении к границам
6. Turbulence при выходе за нижнюю границу

**Тесты:**
```
Test 1: Лететь вниз к 1200м → предупреждение на 1300м, турбулентность на 1200м
Test 2: Лететь выше 4450м → предупреждение на 4350м, деградация на 4650м
Test 3: Подойти к Примуму → сервер обновляет коридор на 4100-4450м
Test 4: Зарегистрированный корабль в городе → корректный локальный коридор
```

---

### Сессия 3: Wind & Environmental Forces ✅ ЗАВЕРШЕНА

**Задачи:**
1. ✅ `WindZone.cs` — объёмные триггеры зон ветра
2. ✅ Применение силы ветра к кораблю
3. ✅ Turbulence при приближении к Завесе (улучшена — Cinemachine Impulse, класс корабля)
4. ⚠️ Cinemachine Impulse для тряски камеры (неработает, нужна отладка и правка, отложено)
5. ⏸ Wind lanes между пиками (визуальные воздушные коридоры) — отложено

**Тесты:**
```
Test 1: Войти в зону ветра → корабль начинает сносить ✅
Test 2: Тяжёлый корабль сносит меньше чем лёгкий (windExposure) ✅
Test 3: Приближение к Завесе → нарастающая тряска ✅
```

**Созданные файлы:**
- `Assets/_Project/Scripts/Ship/WindZoneData.cs` — ScriptableObject данных зоны
- `Assets/_Project/Scripts/Ship/WindZone.cs` — MonoBehaviour объёмных триггерных зон
- `Assets/_Project/Editor/CreateWindZoneTestScene.cs` — Editor утилита

**Изменённые файлы:**
- `Assets/_Project/Scripts/Player/ShipController.cs` v2.1 → v2.2 (интеграция ветра)
- `Assets/_Project/Scripts/Ship/TurbulenceEffect.cs` (Cinemachine + класс корабля)

**Документация:**
- `docs/Ships/SESSION_3_COMPLETE.md` — полная инструкция по тестированию

**Известные ограничения:**
- Визуальная проверка ветра затруднена (зоны не видны в Game view) — отложено до будущих инструментов отладки
- Cinemachine Impulse работает только при установленном пакете Cinemachine
- 5 Unity тестов не созданы — отложено

**Исправленные баги (после тестирования):**
- WindZone.cs не вызывал `ship.RegisterWindZone(this)` — `_activeWindZones` всегда пустой
- `GetComponentInParent` искал только на родителе, ShipController на том же объекте → заменён на `GetComponent` → `GetComponentInParent` → `GetComponentInChildren`
- Добавлен Debug лог при входе/выходе из зоны
- Добавлен `GetActiveWindZoneCount()` публичный метод
```

---

### Сессия 4: Module System Foundation

**Задачи:**
1. `ShipModule.cs` (ScriptableObject) — базовый класс модуля
2. `ShipDefinition.cs` (ScriptableObject) — определение корабля
3. ModuleSlot на кораблях
4. MODULE_YAW_ENH, PITCH_ENH, LIFT_ENH (тир 1)
5. Runtime применение эффектов модулей
6. ShipRegistry.md обновление

**Тесты:**
```
Test 1: Установить MODULE_YAW_ENH → yawSpeed ×1.4
Test 2: Установить несовместимый модуль → ошибка валидации
Test 3: Снять модуль → характеристики возвращаются к базовым
```

---

### Сессия 5: Meziy Thrust & Advanced Modules

**Задачи:**
1. MODULE_MEZIY_ROLL — бросок крена
2. MODULE_MEZIY_PITCH — бросок тангажа
3. MODULE_MEZIY_YAW — резкий поворот
4. Визуальный эффект: сопло, пламя
5. Система топлива для мезиевой тяги
6. Cooldown система

**Тесты:**
```
Test 1: Активировать MEZIY_PITCH → резкий наклон носа на 1.5с
Test 2: После активации → cooldown 8с, нельзя повторить
Test 3: Топливо уменьшается на 5 за активацию
Test 4: Визуальный эффект сопла при активации
```

---

### Сессия 6+: Co-Op, KeyRod, Docking (Будущие)

- KeyRod система
- Adaptive multi-pilot
- DockingDispatcher
- CommPanel UI
- SOL zones

---

## 3. Код: ShipController.cs v2 (Новая Версия)

### Новые Поля
```csharp
// Smooth Movement
[SerializeField] private float yawSmoothTime = 0.6f;
[SerializeField] private float pitchSmoothTime = 0.7f;
[SerializeField] private float liftSmoothTime = 1.0f;
[SerializeField] private float thrustSmoothTime = 0.3f;
[SerializeField] private float yawDecayTime = 1.0f;
[SerializeField] private float pitchDecayTime = 0.8f;

// Altitude Corridor
[SerializeField] private float minAltitude = 1200f;
[SerializeField] private float maxAltitude = 4450f;
[SerializeField] private float maxLiftSpeed = 2.5f; // м/с

// Stabilization
[SerializeField] private float pitchStabForce = 2.5f;
[SerializeField] private float rollStabForce = 4.0f;
[SerializeField] private float maxPitchAngle = 20f;
[SerializeField] private float maxRollAngle = 0f; // заблокирован

// Wind
[SerializeField] private float windInfluence = 0.5f;
[SerializeField] private float turbulenceThreshold = 50f; // м до мин. высоты
```

### Smooth State (текущие значения с Lerp)
```csharp
private float _currentYawRate;
private float _currentPitchRate;
private float _currentLiftForce;
private float _currentThrust;
private float _currentRoll;
private float _turbulenceIntensity;
```

### Новый FixedUpdate (основная логика)
```csharp
private void FixedUpdate() {
    if (!IsServer || _pilots.Count == 0) return;

    // 1. Усредняем ввод
    AverageInputs();

    // 2. Smooth thrust ramp-up
    _currentThrust = Mathf.Lerp(_currentThrust, _avgThrust * baseThrust * boostMultiplier, 
                                thrustSmoothTime / Time.fixedDeltaTime);
    
    // 3. Smooth yaw с затуханием
    float targetYaw = _avgYaw * yawSpeed;
    _currentYawRate = HasYawInput() 
        ? Mathf.Lerp(_currentYawRate, targetYaw, yawSmoothTime / Time.fixedDeltaTime)
        : Mathf.Lerp(_currentYawRate, 0f, yawDecayTime / Time.fixedDeltaTime);
    
    // 4. Smooth pitch с затуханием и ограничением
    float targetPitch = _avgPitch * pitchSpeed;
    _currentPitchRate = HasPitchInput()
        ? Mathf.Lerp(_currentPitchRate, targetPitch, pitchSmoothTime / Time.fixedDeltaTime)
        : Mathf.Lerp(_currentPitchRate, 0f, pitchDecayTime / Time.fixedDeltaTime);
    
    // 5. Smooth lift (очень медленно)
    float targetLift = (_avgLiftUp - _avgLiftDown) * liftSpeed;
    _currentLiftForce = Mathf.Lerp(_currentLiftForce, targetLift, 
                                   liftSmoothTime / Time.fixedDeltaTime);
    _currentLiftForce = Mathf.Clamp(_currentLiftForce, -maxLiftSpeed * mass, maxLiftSpeed * mass);

    // 6. Smooth roll (заблокирован если maxRollAngle = 0)
    if (maxRollAngle > 0) {
        float targetRoll = _avgRoll * maxRollAngle;
        _currentRoll = Mathf.Lerp(_currentRoll, targetRoll, rollSmoothTime / Time.fixedDeltaTime);
    } else {
        _currentRoll = Mathf.Lerp(_currentRoll, 0f, 3.0f / Time.fixedDeltaTime);
    }

    // 7. Применяем силы
    ApplyThrust(_currentThrust);
    ApplyLift(_currentLiftForce);
    ApplyRotation(_currentYawRate, _currentPitchRate, _currentRoll);
    
    // 8. Стабилизация (если нет ввода)
    if (HasNoInput()) {
        ApplyStabilization();
    }
    
    // 9. Ветер
    ApplyWind();
    
    // 10. Турбулентность (близко к Завесе)
    ApplyTurbulence();
    
    // 11. Ограничение скорости
    ClampVelocity();
    
    // 12. Ограничение pitch angle
    ClampPitchAngle();

    // 13. Серверная валидация высоты
    ValidateAltitude();
}
```

---

## 4. Unity Тесты

### ShipMovementTests.cs
```csharp
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

namespace ProjectC.Tests
{
    public class ShipMovementTests
    {
        private ShipController _ship;
        private Rigidbody _rb;

        [SetUp]
        public void Setup()
        {
            // Создаём тестовый корабль
            var go = new GameObject("TestShip");
            _rb = go.AddComponent<Rigidbody>();
            _rb.mass = 1.0f;
            _rb.useGravity = true;
            _ship = go.AddComponent<ShipController>();
            
            // Настраиваем NetworkObject mock (для тестов без NGO)
            // ...
        }

        [UnityTest]
        public IEnumerator SmoothThrust_RampsUpOverTime()
        {
            // Arrange
            _ship.SendShipInput(1f, 0f, 0f, 0f, false); // full thrust
            
            // Act — ждём 0.3s (thrustSmoothTime)
            yield return new WaitForSeconds(0.3f);
            
            // Assert — скорость должна быть плавной, не мгновенной
            Assert.Less(_ship.CurrentSpeed, _ship.MaxSpeed);
        }

        [UnityTest]
        public IEnumerator SmoothYaw_SlowTurnWithInertia()
        {
            // Arrange
            float initialYaw = _ship.transform.eulerAngles.y;
            _ship.SendShipInput(0f, 1f, 0f, 0f, false); // full yaw right
            
            // Act — ждём 1 секунду
            yield return new WaitForSeconds(1f);
            
            // Assert — поворот должен быть медленным (< 40°/s)
            float deltaYaw = Mathf.DeltaAngle(initialYaw, _ship.transform.eulerAngles.y);
            Assert.Less(Mathf.Abs(deltaYaw), 40f);
        }

        [UnityTest]
        public IEnumerator YawDecay_ContinuesAfterInputReleased()
        {
            // Arrange
            _ship.SendShipInput(0f, 1f, 0f, 0f, false);
            yield return new WaitForSeconds(0.5f);
            
            // Act — отпускаем
            _ship.SendShipInput(0f, 0f, 0f, 0f, false);
            float yawRateBefore = _ship.CurrentYawRate;
            
            yield return new WaitForSeconds(1f);
            
            // Assert — yaw должен затухнуть
            Assert.Less(Mathf.Abs(_ship.CurrentYawRate), Mathf.Abs(yawRateBefore));
        }

        [UnityTest]
        public IEnumerator Stabilization_ReturnsToLevel()
        {
            // Arrange — наклоняем корабль
            _ship.transform.rotation = Quaternion.Euler(15f, 0f, 0f); // 15° pitch down
            
            // Act — нет ввода, ждём
            _ship.SendShipInput(0f, 0f, 0f, 0f, false);
            yield return new WaitForSeconds(2f);
            
            // Assert — должен вернуться близко к горизонту
            Assert.Less(Mathf.Abs(_ship.transform.eulerAngles.x), 5f);
        }

        [UnityTest]
        public IEnumerator SlowLift_VeryGentleAltitudeChange()
        {
            // Arrange
            float startAlt = _ship.transform.position.y;
            _ship.SendShipInput(0f, 0f, 0f, 1f, false); // full lift up
            
            // Act — ждём 1 секунду
            yield return new WaitForSeconds(1f);
            
            // Assert — изменение высоты < 3м (maxLiftSpeed = 2.5 м/с)
            float deltaAlt = _ship.transform.position.y - startAlt;
            Assert.Less(Mathf.Abs(deltaAlt), 3f);
        }

        [UnityTest]
        public IEnumerator AltitudeValidation_BelowMin_Turbulence()
        {
            // Arrange — телепортируем ниже минимума
            _ship.transform.position = new Vector3(0, 1100f, 0); // ниже 1200м
            
            // Act
            _ship.SendShipInput(0f, 0f, 0f, 0f, false);
            yield return new WaitForSeconds(1f);
            
            // Assert — turbulence intensity > 0
            Assert.Greater(_ship.TurbulenceIntensity, 0f);
        }

        [UnityTest]
        public IEnumerator AutoHover_NoPilots_ShipHovers()
        {
            // Arrange
            _ship.AddPilotRpc(1);
            _ship.SendShipInput(0.5f, 0f, 0f, 0f, false);
            yield return new WaitForSeconds(1f);
            float speedBefore = _ship.CurrentSpeed;
            
            // Act — все пилоты выходят
            _ship.RemovePilotRpc(1);
            yield return new WaitForSeconds(3f);
            
            // Assert — корабль должен замедлиться и зависнуть
            Assert.Less(_ship.CurrentSpeed, speedBefore * 0.1f);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_ship.gameObject);
        }
    }
}
```

---

## 5. Debug & Observability

### Debug UI (временно, для отладки)
```
[DEBUG SHIP]
Speed: 12.3 m/s (max: 40.0)
Altitude: 3245m
Corridor: [1200m - 4450m]
Yaw Rate: 15.2°/s | Target: 20.0°/s
Pitch Rate: 5.1°/s | Target: 8.0°/s
Lift: 1.2 m/s | Max: 2.5 m/s
Roll: 0.0° | Max: 0.0° (locked)
Stabilizing: YES
Wind Force: (2.1, 0.0, -0.5)
Turbulence: 0.0 (none)
Pilots: 1
```

**Как включить:** `F3` в режиме корабля (только development builds)

---

## 6. Критерии Приёмки

| Критерий | Метрика | Pass/Fail |
|----------|---------|-----------|
| Yaw плавный | < 40°/s, Lerp 0.6s | ☐ |
| Pitch плавный | < 30°/s, Lerp 0.7s | ☐ |
| Lift медленный | < 2.5 м/с, Lerp 1.0s | ☐ |
| Thrust ramp-up | 0.3s до полной тяги | ☐ |
| Stabilization | Возврат к 0° за < 3с | ☐ |
| Yaw decay | Затухание за ~1с | ☐ |
| Angular drag | Вращение гасится | ☐ |
| Нет резких стопов | Инерция сохраняется | ☐ |
| AutoHover | Корабль зависает без пилотов | ☐ |
| Altitude warnings | Предупреждения на границах | ☐ |
| Турбулентность | Тряска под Завесой | ☐ |

---

*Документ создан: Апрель 2026 | @engine-programmer, @gameplay-programmer, @unity-specialist*
