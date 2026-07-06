# GDD_10: Ship System v4.1

**Версия:** 4.3 | **Дата:** 6 июля 2026 г. | **Статус:** 🟢 В разработке — §9 (Damage), §8 (Engine states), §13 (Obsolete cleanup) актуализированы по коду
**Ветка:** `qwen-gamestudio-agent-dev` (дизайн), `feature/npc-quest-v2` (merged) (реализация)

---

## 1. Обзор и Философия

### 1.1 Ключевая Идея
Корабли Project C — это **воздушные баржи/платформы**, а не истребители. Управление должно ощущаться как **контроль над ветром и стабилизацией**, а не прямое маневрирование. Игрок не «крутит» корабль — он **направляет** его, а антигравитационная рамка самостоятельно стабилизирует положение.

### 1.2 Дизайн-Пиллары
| Пиллар | Описание |
|--------|----------|
| **Плавность** | Все движения — медленные, текучие, без резких остановок |
| **Стабилизация** | Без ввода корабль плавно возвращается к горизонту |
| **Коридор высот** | Базовые корабли летают в диапазоне 1200м–4450м |
| **Адаптивность** | Модули изменяют поведение; система масштабируется |
| **Co-Op-first** | Несколько пилотов = распределённое управление, не конфликт |

### 1.3 Ощущения от Полёта (Target Feel)
```
┌──────────────────────────────────────────────────────────┐
│  Представьте: вы стоите на палубе тяжёлой баржи.        │
│  Корабль не «поворачивает» — он «плывёт» в новом        │
│  направлении. Ветер давит на борт. Рамка антигравия     │
│  мягко гасит крен. Когда вы отпускаете управление —     │
│  корабль не замирает — он продолжает скользить,         │
│  постепенно выравниваясь.                               │
│                                                          │
│  Курсовой поворот = как руль корабля в воде:            │
│  медленно, с инерцией, без резких стопов.               │
│  Лифт = как лифт в здании:平稳, без рывков.             │
└──────────────────────────────────────────────────────────┘
```

---

## 2. Система Коридоров Высот

### 2.1 Глобальный Коридор
| Параметр | Значение | Описание |
|----------|----------|----------|
| **MinAltitude (глоб.)** | 1 200 м | Ниже — Завеса, турбулентность |
| **MaxAltitude (глоб.)** | 4 450 м | Выше — разреженный воздух, холод |
| **Базовая скорость лифта** | 1.5–2.5 м/с | Очень медленно, плавно |

### 2.2 Локальные Коридоры Городов
Города регистрируют коридор для авторизованных судов (по ключ-стержню):

| Город | Высота города | Min | Max | Коридор |
|-------|--------------|-----|-----|---------|
| **Примум** | 4 348 м | 4 100 м | 4 450 м | 350 м |
| **Тертиус** | 2 462 м | 2 300 м | 2 600 м | 300 м |
| **Квартус** | 1 690 м | 1 500 м | 1 850 м | 350 м |
| **Килиманджаро** | 1 395 м | 1 200 м | 1 550 м | 350 м |
| **Секунд** | 1 142 м | 1 000 м | 1 250 м | 250 м |

**Механика:** При приближении к городу сервер проверяет регистрацию корабля. Если корабль зарегистрирован — ему разрешается коридор города. Если нет — глобальный.

### 2.3 Серверная Валидация Высоты

```
Каждые 0.5 сек (сервер):
  currentAlt = ship.transform.position.y
  
  if currentAlt < corridor.min + 100m:
    → Warning: "Приближение к нижней границе коридора"
  if currentAlt < corridor.min:
    → Alert: "ВНИМАНИЕ: Зона Завесы! Турбулентность!"
    → Apply turbulence
    → SOL notification (если в зоне СОЛ)
  
  if currentAlt > corridor.max - 100m:
    → Warning: "Приближение к верхней границе коридора"
  if currentAlt > corridor.max + 200m:
    → Alert: "ВНИМАНИЕ: Критическая высота!"
    → Apply system degradation
```

> **Реализация (2026-07-05):** ⚠️ Не server-тик 0.5с, а per-frame (ShipController._evaluateAltitude + AltitudeCorridorSystem.GetActiveCorridor()). TurbulenceEffect и SystemDegradationEffect применяются в каждом FixedUpdate.

### 2.4 Зоны Вне Коридора
| Зона | Эффект | Реализация |
|------|--------|------------|
| **100м ниже минимума** | Лёгкая турбулентность, предупреждение | Фаза 3 |
| **Ниже минимума** | Сильная тряска, видимость ~0, SOL | Фаза 3 |
| **Под Завесой** | Тряска + урон + отключение систем | Фаза 5 |
| **100м выше максимума** | Лёгкое падение тяги | Фаза 3 |
| **Выше максимума + 200м** | Системы начинают отказывать | Фаза 5 |
| **> 6000м (космос)** | Замерзание систем, падение | Фаза 5 (поздно) |

---

## 3. Физика Движения

### 3.1 Базовые Формулы

#### Антигравитация
```
F_gravity = Vector3.up * rb.mass * Mathf.Abs(Physics.gravity.y) * antiGravityFactor
// antiGravityFactor = 1.0 (полная компенсация)
```

#### Тяга (Forward/Backward)
```
// SMOOTH: не мгновенная, а с ramp-up
targetThrust = inputZ * baseThrust * boostMultiplier
currentThrust = Mathf.Lerp(currentThrust, targetThrust, thrustSmoothTime * dt)
F_thrust = transform.forward * currentThrust

// thrustSmoothTime = 0.3s (плавный разгон)
// baseThrust зависит от класса корабля
```

#### Рыскание (Yaw — курсовой поворот)
```
// SMOOTH: медленный, текучий, без резких стопов
targetYawRate = inputX * yawSpeed  // yawSpeed = 25-40°/s (зависит от класса)
currentYawRate = Mathf.Lerp(currentYawRate, targetYawRate, yawSmoothTime * dt)
torqueY = currentYawRate * yawTorqueMultiplier

// yawSmoothTime = 0.5-0.8s (ОЧЕНЬ плавный)
// Без ввода: затухание до 0 за 1.0s
```

#### Тангаж (Pitch — нос вверх/вниз)
```
targetPitchRate = mouseInputY * pitchSpeed  // pitchSpeed = 20-30°/s
currentPitchRate = Mathf.Lerp(currentPitchRate, targetPitchRate, pitchSmoothTime * dt)
torqueX = currentPitchRate * pitchTorqueMultiplier

// pitchSmoothTime = 0.6-0.9s (еще плавнее чем yaw)
// Limited range: ±20° от горизонта
```

#### Лифт (Vertical — Q/E)
```
// VERY SMOOTH: как лифт в здании
targetLiftForce = (inputQ - inputE) * liftSpeed  // liftSpeed = 80-150
currentLiftForce = Mathf.Lerp(currentLiftForce, targetLiftForce, liftSmoothTime * dt)
F_lift = Vector3.up * currentLiftForce

// liftSmoothTime = 0.8-1.2s (ОЧЕНЬ медленно)
// Максимальная скорость лифта: 2-3 м/с
```

#### Крен (Roll — ограничен!)
```
// БАЗОВЫЕ корабли: roll = 0 (заблокирован)
// С MODULE_ROLL: ±15° макс
// С MODULE_MEZIY_THRUST_ROLL: кратковременный бросок до ±25°

targetRoll = rollInput * maxRollAngle
currentRoll = Mathf.Lerp(currentRoll, targetRoll, rollSmoothTime * dt)
torqueZ = currentRoll * rollTorqueMultiplier
```

#### Стабилизация (Auto-Level)
```
// Когда нет ввода — корабль плавно возвращается к горизонту
if (HasNoInput()):
    // Roll к 0
    desiredRoll = 0
    // Pitch к 0 (горизонт)
    desiredPitch = 0
    // Yaw НЕ стабилизируется (корабль сохраняет курс)
    
    rollError = currentRoll - desiredRoll
    pitchError = currentPitch - desiredPitch
    
    stabilizationTorque = new Vector3(
        -pitchError * pitchStabForce,
        0,  // yaw не стабилизируется
        -rollError * rollStabForce
    )
    
    rb.AddTorque(stabilizationTorque, ForceMode.Force)

// pitchStabForce = 2.0-3.0
// rollStabForce = 3.0-5.0
```

#### Сопротивление
```
F_drag = -rb.velocity * linearDrag        // линейное сопротивление
F_angularDrag = -rb.angularVelocity * angularDrag  // угловое

// linearDrag:  0.3-0.8 (зависит от класса)
// angularDrag: 3.0-5.0 (ВЫСОКОЕ — гасит вращение)
```

### 3.2 Параметры по Классам Кораблей

| Параметр | Лёгкий | Средний | Тяжёлый | Тяжёлый II |
|----------|--------|---------|---------|-----------|
| **baseThrust** | 300-400 | 250-350 | 180-250 | 150-200 |
| **yawSpeed** | 35°/s | 25°/s | 18°/s | 15°/s |
| **yawSmoothTime** | 0.5s | 0.6s | 0.7s | 0.8s |
| **pitchSpeed** | 25°/s | 20°/s | 15°/s | 12°/s |
| **pitchSmoothTime** | 0.6s | 0.7s | 0.8s | 0.9s |
| **maxRollAngle** | 0° (15° с мод.) | 0° (10° с мод.) | 0° (5° с мод.) | 0° |
| **liftSpeed** | 120 | 100 | 80 | 60 |
| **liftSmoothTime** | 0.8s | 0.9s | 1.0s | 1.2s |
| **maxSpeed** | 35-45 м/с | 25-35 м/с | 15-22 м/с | 10-18 м/с |
| **mass (до множителя ×10 → rb.mass)** | 800 → 80 | 1000 → 100 | 1500 → 150 | 2000 → 200 |
| **linearDrag** | 0.4 (все классы — единое значение) | — | — | — |
| **angularDrag** | 8.0 (все классы — единое значение) | — | — | — |
| **stabilizationForce (pitch)** | 15.0 (все классы — единое значение) | — | — | — |
| **stabilizationForce (roll)** | 20.0 (все классы — единое значение) | — | — | — |
| **thrustForce** | 650 (все классы — единое значение) | — | — | — |

> **Примечание (2026-07-05):** В текущем коде большая часть параметров — единые для всех классов. Класс-зависимы только масса (через massLight/massMedium/massHeavy/massHeavyII с massMultiplier=10) и windExposure. Остальные GDD-спеки (класс-зависимые yawSpeed, pitchSpeed, angularDrag и т.д.) — целевые для будущих итераций. Числовые значения в строках выше — из ShipController.cs на момент 2026-07-05.

### 3.3 Влияние Ветра и Облаков

#### Ветровые Течения
```
// Ветер — постоянная сила в определённых зонах мира
F_wind = windDirection * windStrength * windExposureCoefficient

// windExposureCoefficient зависит от размера корабля
// Лёгкий: 0.7 (сильнее сносит)
// Тяжёлый: 0.3 (менее чувствителен)

// Зоны ветра между пиками — создаёт «воздушные коридоры»
// Попутный ветер: +5-15% к скорости
// Встречный ветер: -5-15% к скорости
// Боковой ветер: снос курса, требует компенсации
```

#### Турбулентность Облаков
```
// При приближении к Завесе (нижняя граница коридора + 50м)
turbulenceIntensity = map(currentAlt, minAlt, minAlt - 200, 0, 1)

F_turbulence = Random.onUnitSphere * turbulenceIntensity * turbulenceStrength
rb.AddForce(F_turbulence, ForceMode.Force)
rb.AddTorque(Random.onUnitSphere * turbulenceIntensity * 2, ForceMode.Force)

// Визуальный эффект: тряска камеры (Cinemachine Impulse)
```

---

## 4. Система Модулей Кораблей

### 4.1 Архитектура
```
ShipDefinition (ScriptableObject)
├── shipId: string (уникальный ID)
├── className: ShipClass (enum)
├── baseStats: ShipStats
├── moduleSlots: ModuleSlot[]
│   ├── slotId: string
│   ├── slotType: ModuleSlotType (Utility | Propulsion | Special | Autopilot)
│   └── installedModule: ShipModule (может быть null)
└── compatibilityRules: ModuleCompatibility

ShipModule (ScriptableObject)
├── moduleId: string
├── moduleName: string
├── slotType: ModuleSlotType
├── compatibleShipClasses: ShipClass[]
├── incompatibleShips: string[] (конкретные shipId)
├── effects: ModuleEffect[]
├── powerRequirement: float                     // ⚠️ ТОЛЬКО В GDD — в ShipModule.cs поле отсутствует. Система не реализована. См. summary_05.07.2026.md §4.
├── unlockTier: int (1 = базовый, 4 = очень редкий)
└── description: string
```

### 4.2 Каталог Модулей

#### Пропульсия (Propulsion)

| Модуль | ID | Эффект | Совместимость | Тир |
|--------|-----|--------|--------------|-----|
| **Мезиевая Тяга (Крен)** | `MODULE_MEZIY_ROLL` | Кратковременный бросок крена ±25° (2с, CD 10с) | LIGHT+, не LG_01 | 2 |
| **Мезиевая Тяга (Тангаж)** | `MODULE_MEZIY_PITCH` | Бросок тангажа ±10° (1.5с, CD 8с) | LIGHT+, не LG_01 | 2 |
| **Мезиевая Тяга (Рыскание)** | `MODULE_MEZIY_YAW` | Резкий поворот на 30° (0.5с, CD 12с) | LIGHT+, не LG_01 | 2 |
| **Улучшенное Рыскание** | `MODULE_YAW_ENH` | +40% к yawSpeed | ВСЕ | 1 |
| **Улучшенный Тангаж** | `MODULE_PITCH_ENH` | +30% к pitchSpeed | ВСЕ | 1 |
| **Улучшенный Лифт** | `MODULE_LIFT_ENH` | +50% к liftSpeed, ×1.5 ускорение лифта | ВСЕ | 1 |
| **Модуль Крена** | `MODULE_ROLL` | Разблокирует крен ±15° (без мезии) | LG_02+, MD, HV | 2 |

#### Специальные (Special)

| Модуль | ID | Эффект | Совместимость | Тир |
|--------|-----|--------|--------------|-----|
| **Под-Завесный Спуск** | `MODULE_VEIL` | Спуск ниже коридора. Тряска, видимость~0 | LG_02+, MD, HV, SV | 3 |
| **Высотная Изоляция** | `MODULE_SPACE` | Защита от замерзания >4450м. Отказ >8000м | MD_03+, SS | 4 |
| **Маскировка СОЛ** | `MODULE_STEALTH` | -60% радиус обнаружения СОЛ | LG_02, MD, SV | 3 |

#### Автопилот (Autopilot)

| Модуль | ID | Эффект | Совместимость | Тир |
|--------|-----|--------|--------------|-----|
| **Автопилот: Стыковка** | `MODULE_AUTO_DOCK` | Автоподход + посадка по инструкции диспетчера | MD, HV, H2, SV, SS | 2 |
| **Автопилот: Навигация** | `MODULE_AUTO_NAV` | Следование по вейпоинтам | MD, HV, H2, SS | 3 |

### 4.3 Как Модули Влияют на Управление
```
// Пример: MODULE_YAW_ENH установлен на SHIP_MD_01
finalYawSpeed = baseYawSpeed * (1 + moduleYawBonus)
// baseYawSpeed = 25°/s
// moduleYawBonus = 0.4
// finalYawSpeed = 35°/s

// Пример: MODULE_MEZIY_PITCH активирован
if (meziyPitchActive) {
    pitchTorqueMultiplier = 3.0; // ×3 на 1.5 секунды
    ApplyTorque(transform.right * -pitchInput * pitchTorqueMultiplier);
    // Визуал: выброс пламени из сопла (particle system)
}
```

Полный каталог кораблей и модулей с таблицами совместимости: **см. `../Ships/ShipRegistry.md`**

---

## 5. Ключ-Стержень Система

### 5.1 Механика
- **Ключ-стержень** (KeyRod) — физический предмет, вставляется в пульт корабля
- Каждый корабль имеет **registeredOwnerId** — владелец
- Ключ-стержень можно **передать**, **украсть**, **скопировать** (нелегально)

### 5.2 Поток
```
1. Игрок подходит к кораблю
2. Проверяет: есть ли KeyRod в инвентаре?
   → Да: проверяет соответствие shipId на KeyRod и корабля
     → Совпадает: Engine On, AddPilot
     → Не совпадает: "Это не ваш корабль"
   → Нет: "Требуется ключ-стержень"
3. Если чужой KeyRod — можно завладеть кораблём (угон)
```

### 5.3 Угон
- Угон = иметь чужой KeyRod и запустить корабль
- Владелец получает уведомление: "Судно [ID] запущено без вашего разрешения"
- В зоне СОЛ: угнанный корабль помечается → СОЛ перехват

### 5.4 Данные KeyRod
```csharp
public class KeyRodData : ScriptableObject {
    public string keyRodId;
    public string registeredShipId;
    public string ownerPlayerId;
    public bool isDuplicate; // true = нелегальная копия
    public KeyRodAccessLevel accessLevel; // Full, Limited, OneTime — ⚠️ В коде отсутствует. Текущий реализован только Full через KeyRodState (Active/Stolen/Consumed/Destroyed).
}
```

---

## 6. Co-Op Пилотирование

### 6.1 Single Player
```
Один пилот → полный контроль всех систем
```

### 6.2 Multi-Pilot (Адаптивная Система)
```
2+ пилота → адаптивное распределение:

Малый корабль (LIGHT):
  Пилот 1: полное управление (тяга, курс, тангаж, лифт)
  Пилот 2: может взять «наблюдение» (камера, навигация)
           или предложить ввод (усредняется с ×0.5 весом)

Средний корабль (MEDIUM):
  Пилот 1 (Капитан): курс, тангаж, тяга
  Пилот 2 (Штурман): лифт, навигация, связь с диспетчером

Тяжёлый корабль (HEAVY/HEAVY_II):
  Пилот 1 (Капитан): курс, тангаж, тяга
  Пилот 2 (Штурман): лифт, автопилот, навигация
  Пилот 3 (Инженер): модули (мезиевая тяга, СОЛ-маскировка)
  
  Вес ввода: Капитан ×1.5, остальные ×1.0
  Итог: serverAverage = (captainInput × 1.5 + other1 + other2) / 3.5
```

### 6.3 Сетевая Синхронизация
```
Клиент → SubmitShipInputRpc(inputX, inputZ, pitchInput, liftInput, boost, moduleInputs)
                                    ↓
Сервер → Валидация (авторизован ли пилот?)
       → Усреднение с учётом ролей
       → Применение физики (FixedUpdate)
       → NetworkTransform репликация (ServerAuthority)
                                    ↓
Клиент ← Интерполяция позиции/ротации
       ← Prediction для отзывчивости
```

---

## 7. Стыковка и Диспетчер

> **Статус (2026-07-05):** ✅ **MVP реализован — цикл Docked→Loading→Undocking→Departing.** Подробности в `docs/Docking_stations/` и `docs/NPC_others_peacfull/`. ⚠️ Визуальные маркеры падов (`DockPadVisualMarker`) требуют переработки.

### 7.1 Поток Стыковки (как реализовано)

```
1. Игрок входит в OuterCommZone (radius 1000m для Примум)
2. T в корабле (Q10: только если пилотирует) → CommPanel открывается
3. Кнопка "Запросить посадку" → RequestDockingRpc на DockingServer
4. Сервер (DockingWorld.AssignPad):
   - Проверка физической занятости пада (Physics.OverlapBox)
   - Проверка совместимости классов (compatibleShipClasses)
   - Назначение pad'а из свободных
   - Регистрация как pending (Q7: ждёт подтверждения игрока 30 сек)
5. Клиент получает DockingAssignmentDto → UI: "Назначаю pad #5, подход..."
   Кнопки [Хорошо] / [Отбой]
6. Игрок жмёт "Хорошо" → RequestConfirmAssignmentRpc(true)
   Сервер: _occupiedPads[padKey] = clientId, статус Assigned
   Клиент: окно 5 минут (timer), кнопка [Отменить запрос]
7. Игрок летит к pad'у (без автопилота — MVP)
8. Касание DockingPadTriggerBox → NotifyTouchedDownRpc
9. Сервер (DockingWorld.ConfirmTouchdown):
   - Проверка ship assigned этому client'у + правильный padId
   - Статус Docked
   - ShipController.EnterDocked(): _netIsDocked=true, rb.isKinematic=true
10. Клиент: "Стыковка зафиксирована. Двигатели заблокированы."
    HUD: Dispatch column зелёная, кнопка "T — связаться"
    Кнопки CommPanel: [Отстыковка] / [Закрыть]
11. W/A/S/D в Docked → ничего не происходит (SendShipInput guard)
12. T → CommPanel → [Отстыковка] → RequestTakeoffRpc
13. Сервер: ReleaseAssignment + ExitDocked (rb.isKinematic=false)
    Клиент: окно CommPanel автоматически закрывается, двигатели разблокированы
```

### 7.2 Диспетчер — DTO (как реализовано)

```csharp
public struct DockingAssignmentDto : INetworkSerializable {
    public string stationId;          // "STN-PRM-001"
    public string padId;              // "PAD-001".."PAD-005"
    public Vector3 approachPoint;
    public float approachAltitude;
    public float approachHeading;
    public float landingWindowSeconds; // 300 (5 минут)
    public string voiceLine;          // фраза из DispatcherVoiceLines SO
    public ulong shipNetworkObjectId;
    public bool success;
    public string failReason;
}
```

### 7.3 Подсистемы (Phase 2 / Phase 1.5)

#### 7.3.1 Departure — вылет из зоны

Реализован как часть NPC Ship FSM и Player Docking:
- **NPC:** NpcShipWorld FSM — Docked → Loading → Undocking → **Departing** (anti-gravity boost, вертикальный набор 5с, переход в Lifting → Cruising)
- **Player:** DockingServer.RequestTakeoffRpc → ExitDocked → корабль свободен для маневра
- Anti-gravity boost (10-сек) через ShipController.AntiGravity setter

> Отдельного T-DEPART-* нет. Функционал покрыт NpcShipController.cs + DockingServer.cs.

#### 7.3.2 Автопилот стыковки (`MODULE_AUTO_DOCK`)

Автоподход по инструкции диспетчера (approachPoint, approachAltitude, approachHeading). Модуль C/B/A/S-tier. Тир 2. **Phase 2** (после MVP).

#### 7.3.3 NPC-корабли на падах

Сервер-авторитативный SOT (`_occupiedPads: Dictionary<padKey, ulong>`) уже поддерживает. На старте `ScanExistingOccupants()` регистрирует корабли, стоящие на падах.

**Реализовано (Phase 3, M3.2.15):** Полный цикл NPC-кораблей — 4 NPC курсируют между Примум и TestZone. FSM: Docked → Lifting (прямой взлёт на 5м, detectCollisions=false) → Yawing (MoveRotation 45°/с к цели) → Cruising (12 м/с + altitude hold) → Berthing (CommZone → AssignPad → полёт к паду) → Docked. Schedule advance (reverse route) после каждой стыковки. Round-trip бесконечен. **Прямой Rigidbody control** (MoveRotation + linearVelocity) — ShipController.AddTorque не используется (ForceMode.Force даёт 0.017°/с² для mass=2000).

**Архитектура:** `NpcShipController.NavTick` (5-mode FSM) вызывается из `NpcShipWorld.FixedUpdate`. ShipController.FixedUpdate пропускает всю физику если `_hasNpcPilot && _pilots.Count==0`. Парковка через `DockingWorld.AssignPadForNpc` + `ReleaseNpcAssignment`. Документация: `docs/NPC_others_peacfull/pc_ship/`.

### 7.3 Зоны СОЛ и Заброшенные Корабли
```
Зона стыковки = город или платформа + radius 300м

Если корабль оставлен в зоне СОЛ:
  0-2 мин: предупреждение в CommPanel
  2-5 мин: второе предупреждение, штраф репутации
  5+ мин: СОЛ блокирует корабль (Engine Lock)
  
  С MODULE_STEALTH: таймеры ×3 (игнор СОЛ)
  Без СОЛ модулей: полная блокировка
```

---

## 8. Машина Состояний Корабля

> **Статус (2026-06-20):** ✅ **Реализовано** через `ShipController._netIsDocked: NetworkVariable<bool>` (server-authoritative).
> Игрок под управлением (`_pilots`) + `DockingWorld` серверный singleton. `SendShipInput` имеет guard на `IsDocked`.

| Состояние | Описание | Триггер Входа | Триггер Выхода | Реализация |
|-----------|----------|---------------|----------------|------------|
| **EngineOff** | Все системы неактивны, антиграв выключен, корабль падает | Enter (пилот в кресле) или fuel=0 | Enter (если топливо ≥10%) | ✅ (2026-07-05: `_netEngineRunning` NetworkVariable, ToggleEngineServerRpc) |
| **Idle** | Антиграв активен, зависание, idle-расход 0.05 fuel/s | Engine ON, пилотов нет | Пилот сел (ввод) | ✅ (2026-07-05: IDLE с fuel-логикой) |
| **Flying** | Под управлением пилота | Ввод от пилота | Все пилоты вышли | ✅ |
| **Docking** | Следует инструкциям диспетчера | Docking accepted | Landed / Cancelled | Phase 2 (автопилот) |
| **Docked** | Заблокирован на платформе | `DockingWorld.ConfirmTouchdown` → `ShipController.EnterDocked()` | `RequestTakeoffRpc` → `ExitDocked()` | ✅ (`_netIsDocked=true`, `rb.isKinematic=true`, `SendShipInput` blocked) |
| **AutoHover** | Зависание (все пилоты вышли) = Engine ON Idle с расходом топлива | PilotCount = 0 | PilotCount > 0 | ✅ (2026-07-05: IDLE с fuel-логикой, выход F на любой скорости) |
| **⚠️ VeilTurbulence** | Ниже коридора | Alt < minAlt | Alt >= minAlt + 50 | ✅ |
| **⚠️ SystemDegrade** | Выше коридора | Alt > maxAlt + 100 | Alt <= maxAlt | ✅ |
| **⚠️ SOLLock** | СОЛ блокирует | SOL violation timeout | Оплата штрафа / модуль Stealth | Phase 4 |

---

## 9. Повреждения и Ремонт ✅ Реализовано (MVP, 2026-07-05)

> **Статус:** ✅ MVP. См. `docs/Ships/damage_subsystem/00_DESIGN.md`.

**Архитектура:** `ShipHull` (NetworkBehaviour, `IDamageTarget`) + `ShipDamageConfig` SO.

**Два источника урона:**
- Столкновения: `ShipController.OnCollisionEnter` → `ShipHull.ApplyCollisionDamage(energy)`. Формула: `min(floor((energy−8)×0.5), 50)`. Три защиты от ложных ударов при стыковке (minRelativeSpeed 3 м/с + postUndockGrace 3 сек + IsDocked guard).
- Боевое оружие: `CombatServer.ResolveAttack` → `ShipHull.ApplyDamage(DamageResult)`.

**HP по классам:** Light=100, Medium=200, Heavy=400, HeavyII=600. armorHull=5.

**0 HP = «сломан»:** скорости ×0.1, груз обнулён, `IsAlive()=true` (корабль не деспаунится). Ремонт в доке за 300 кр.

**Что НЕ входит в MVP (post-MVP):**
- Визуальные эффекты (дым, искры, деформация)
- Щиты как отдельный ресурс
- Урон по отдельным модулям/слотам
- Градации деградации (сейчас только 0/1)
- Саморемонт в полёте

---

## 10. План Реализации

### Фаза 1: Core Movement (✅ Done)
| # | Задача | Приоритет | Статус (2026-07-05) |
|---|--------|-----------|--------------------|
| 1.1 | ShipController.cs переписан с SmoothDamp-физикой | P0 | ✅ Done |
| 1.2 | Smooth yaw — yawSmoothTime=0.6 + yawDecayTime=1.0 | P0 | ✅ Done |
| 1.3 | Smooth pitch — maxPitchAngle=20°, pitchSmoothTime=0.7 | P0 | ✅ Done |
| 1.4 | Smooth lift — liftSmoothTime=1.0, maxLiftSpeed=2.5 м/с | P0 | ✅ Done |
| 1.5 | Auto-stabilization — pitchStabForce=15, rollStabForce=20 | P0 | ✅ Done |
| 1.6 | Angular drag=8.0 (выше спеки 3-5) | P0 | ✅ Done |
| 1.7 | Unity тесты: проверить плавность | P1 | ⏳ TODO |

### Фаза 2: Altitude Corridor System
| # | Задача | Приоритет | Ответственный | Статус (2026-07-05) |
|---|--------|-----------|--------------|--------------------|
| 2.1 | AltitudeCorridorSystem.cs (ScriptableObject + runtime) | P0 | engine-programmer | ✅ Done |
| 2.2 | City corridor data: 5 городов | P0 | game-designer | ✅ Done (9 SO assets) |
| 2.3 | Server altitude validation (per-frame) | P0 | engine-programmer | ✅ Done (ShipController._evaluateAltitude + AltitudeCorridorSystem, не server-тик) |
| 2.4 | Warning UI: предупреждения высоты | P1 | ui-programmer | ⏳ TODO |
| 2.5 | Unity тесты: corridor boundaries | P1 | unity-specialist | ⏳ TODO |

### Фаза 3: Wind & Turbulence (✅ Done)
| # | Задача | Приоритет | Статус (2026-07-05) |
|---|--------|-----------|--------------------|
| 3.1 | WindZone + WindManager (глобальные и локальные зоны) | P1 | ✅ Done |
| 3.2 | Wind force application на корабль | P1 | ✅ Done (аддитивно с локальными зонами) |
| 3.3 | Turbulence near Veil (TurbulenceEffect) | P1 | ✅ Done |
| 3.4 | Cinemachine Impulse для камеры | P2 | ⏳ TODO |

### Фаза 4: Module System Foundation (✅ Done)
| # | Задача | Приоритет | Статус (2026-07-05) |
|---|--------|-----------|--------------------|
| 4.1 | ShipModule ScriptableObject architecture | P0 | ✅ Done |
| 4.2 | ModuleSlot на кораблях | P0 | ✅ Done (через ShipModuleManager) |
| 4.3 | MODULE_YAW_ENH, PITCH_ENH, LIFT_ENH, ROLL (тир 1) | P1 | ✅ Done (8 ShipModule + 8 ShopEntry) |
| 4.4 | MODULE_MEZIY_* (burst maneuvers) | P1 | ✅ Done (MeziyModuleActivator + visual) |
| 4.5 | ShipRegistry.md наполнение | P1 | ⏳ Partially

### Фаза 5: Co-Op & Docking (✅ Phase 1 Done)
| # | Задача | Приоритет | Статус (2026-07-05) |
|---|--------|-----------|--------------------|
| 5.1 | KeyRod system (Phase 1 — scene-placed + persistence + ownership) | P1 | ✅ Done (KeyRodInstanceWorld + JSON persistence) |
| 5.2 | Adaptive multi-pilot input | P1 | ✅ Done (_pilots HashSet + input summing/averaging) |
| 5.3 | DockingDispatcher + DockingServer | P2 | ✅ Done (pad assignment + NPC + player cycles) |
| 5.4 | CommPanel UI (Elite Dangerous style) | P2 | ⏳ TODO |
| 5.5 | SOL zone warnings | P2 | ⏳ TODO |

> **Key Phase 2** (TBD): Crafting copies, NPC key sales, trade UI, access levels (Limited/OneTime), quest integration — см. §§ 13.3 TODO.

### Фаза 6: Advanced (Будущее — ⏳ P3+)
| # | Задача | Приоритет | Статус (2026-07-05) |
|---|--------|-----------|--------------------|
| 6.1 | Veil penetration mechanics | P3 | ⏳ TODO |
| 6.2 | Space freeze mechanics | P3 | ⏳ TODO |
| 6.3 | MODULE_AUTO_DOCK (автопилот) | P3 | ⏳ TODO |
| 6.4 | Damage/crash system | P4 | ✅ MVP (ShipHull + collision damage + repair, 2026-07-05). Full crash/visual = P4. |
| 6.5 | MODULE_STEALTH (counter-SOL) | P3 | ⏳ TODO |

---

## 11. Технические Спецификации

### 11.1 Текущие vs Целевые Параметры ShipController.cs (на момент 2026-07-05)

| Параметр | Code (ShipController.cs) | Цель (LIGHT) | Цель (HEAVY) | Статус |
|----------|--------------------------|-------------|-------------|--------|
| thrustForce | 650 | 350 | 200 | Выше — требует ребаланса |
| maxSpeed | 40 | 40 | 18 | Light ✅; Heavy не класс-зависим |
| yawForce | 25 | ×0.4 (10) | ×0.25 (6.25) | ⚠️ Не CLASS-зависим |
| pitchForce | 20 | ×0.5 (10) | ×0.35 (7) | ⚠️ Не CLASS-зависим |
| verticalForce | 120 | 120 | 80 | Light ✅; Heavy ⚠️ не класс-зависим |
| linearDrag | 0.4 | 0.4 | 0.6 | Единое значение для всех классов |
| angularDrag | 8.0 | 3.5 | 4.5 | ×2.3 от спецификации, единое значение |
| pitchStabForce | 15.0 | 2.5 | — | ×6 от спецификации |
| rollStabForce | 20.0 | 4.0 | — | ×5 от спецификации |
| mass (rb.mass) | 80/100/150/200 | "0.8-1.2" | "4.0-6.0" | Scale different — massMultiplier=10 |

### 11.2 Новые Параметры для Добавления

```csharp
[Header("Smooth Movement")]
[SerializeField] private float yawSmoothTime = 0.6f;       // Lerp time для yaw
[SerializeField] private float pitchSmoothTime = 0.7f;     // Lerp time для pitch
[SerializeField] private float liftSmoothTime = 1.0f;      // Lerp time для lift
[SerializeField] private float thrustSmoothTime = 0.3f;    // Lerp time для thrust
[SerializeField] private float yawDecayTime = 1.0f;        // Затухание без ввода
[SerializeField] private float pitchDecayTime = 0.8f;      // Затухание без ввода

[Header("Altitude Corridor")]
[SerializeField] private float minAltitude = 1200f;        // Глобальный минимум
[SerializeField] private float maxAltitude = 4450f;        // Глобальный максимум
[SerializeField] private float maxLiftSpeed = 2.5f;        // м/с — макс. скорость лифта

[Header("Stabilization")]
[SerializeField] private float pitchStabForce = 2.5f;      // Сила стабилизации pitch
[SerializeField] private float rollStabForce = 4.0f;       // Сила стабилизации roll
[SerializeField] private float maxPitchAngle = 20f;        // ° — ограничение тангажа
[SerializeField] private float maxRollAngle = 0f;          // ° (0 = заблокирован)

[Header("Wind & Environment")]
[SerializeField] private float windInfluence = 0.5f;       // Влияние ветра
[SerializeField] private float turbulenceThreshold = 50f;  // м до мин. высоты
```

---

## 12. Связанные Документы

| Документ | Путь | Описание |
|----------|------|----------|
| **Ship Registry** | `../Ships/ShipRegistry.md` | Полный каталог кораблей, модулей, совместимость |
| **World & Environment** | `gdd/GDD_02_World_Environment.md` | Мир, города, Завеса |
| **Network** | `gdd/GDD_12_Network_Multiplayer.md` | Сетевая архитектура |
| **Faction & SOL** | `gdd/GDD_23_Faction_Reputation.md` | СОЛ, фракции |
| **Core Gameplay** | `gdd/GDD_01_Core_Gameplay.md` | Core Loop, управление |
| **Implementation Plan** | `../Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` | План тестов и код |
| **Agent Summary** | `../Ships/AGENTS_SHIP_SYSTEM_SUMMARY.md` | Оркестрация и roadmap |
| **Lore Book** | `WORLD_LORE_BOOK.md` | Лор мира из книги |
| **Ship Lore** | `SHIP_LORE_AND_MECHANICS.md` | Лор кораблей |
| **Ship Key Subsystem** | `../Ships/Key-subsystem/00_OVERVIEW.md` | Физический ключ-предмет для запуска (R2-SHIP-KEY-001, 2026-06-06) |
| **MetaRequirement** | `../MetaRequirement/00_OVERVIEW.md` | Универсальная система требований (R2-META-REQ-001, 2026-06-06) |
| **NPC + Quests v2** | `../NPC_quests/08_ROADMAP.md` | Квесты используют MetaRequirement pattern (post-MVP, T-Q??) |

---

## 13. Реализация в коде (2026-06-06)

> **Секция добавлена Mavis 2026-06-10.** Дизайн-контент (физика, мезий, модули, коридоры высот) остаётся в зоне game-designer'а. Здесь — **только статус реализации** физического ключа и lock-key подсистемы.

### 13.1 Ship Key Subsystem (R2-SHIP-KEY-001, 2026-06-06) ✅

**MVP:** физический ключ-предмет для запуска корабля. F-посадка блокируется, если в инвентаре пилота нет нужного ключа.

**Архитектура:**
```
SERVER (host):
[ShipKeyServer] : NetworkBehaviour (BootstrapScene, DontDestroyOnLoad)
    ├── ShipKeyBinding registry: Dictionary<netId, ShipKeyBinding>
    ├── CanPlayerBoard(clientId, netId) → bool + reason
    ├── RequestCanBoardRpc(netId) → TargetRpc (CanBoard response)
    └── Defense-in-depth guard в SubmitSwitchModeRpc

CLIENT:
[ShipKeyClientState] (singleton, RuntimeInitializeOnLoadMethod)
    ├── OnBoardDenied event
    ├── OnBindingsPushed event
    └── ReceiveShipKeyCanBoardResponseTargetRpc / ReceiveShipKeyBindingsTargetRpc

UI:
[ShipKeyToast] (UIDocument, UI Toolkit) — fade-out 3 сек
    └── "Нужен ключ X для корабля Y" + визуальная индикация
```

**Wiring:**
- ✅ `Assets/_Project/Scripts/Ship/Key/ShipKeyBinding.cs` (MonoBehaviour, ship ↔ key ItemData)
- ✅ `Assets/_Project/Scripts/Ship/Key/ShipKeyServer.cs` (NetworkBehaviour hub)
- ✅ `Assets/_Project/Scripts/Ship/Key/ShipKeyClientState.cs` (singleton projection)
- ✅ `Assets/_Project/Scripts/Ship/Key/ShipKeyToast.cs` (UIDocument)
- ✅ `NetworkPlayer.cs` — F-key разделён на выход/посадку, pre-F RPC `RequestCanBoard` (1.5 сек timeout)
- ✅ `NetworkManagerController.cs` — `CreateShipKeyClientState()` (auto-spawn)
- ✅ `InventoryWorld.cs` — `+HasItem(clientId, itemId)` extension

**Ассеты:**
- ✅ 3 SO `ItemData`: `Item_Key_ShipLight/Medium/Heavy.asset`
- ✅ 1 PanelSettings: `ShipKeyPanelSettings.asset`
- ✅ `WorldScene_0_0.unity` — 3 KeyRod PickupItem + ShipKeyBinding на 3 ShipController

**Статус:** ✅ **DONE** (MVP). **Deprecated** — superseded by MetaRequirement (см. §13.2).

**Документация:** `docs/Ships/Key-subsystem/00_OVERVIEW.md` + `KNOWN_ISSUES.md` + `SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md`.

### 13.2 MetaRequirement v1 (R2-META-REQ-001, 2026-06-06) ✅

**Универсализация:** обобщить Ship Key Subsystem (1 предмет на 1 корабль) в систему требований для **любых** Interactable-объектов с массивом требуемых предметов (от 1 до N) и логикой ALL / ANY / AT_LEAST_N.

**Архитектура (generic):**
```
SERVER (host):
[MetaRequirementRegistry] : NetworkBehaviour (scene-placed в BootstrapScene)
    ├── RegisterMetaRequirement(netId, MetaRequirement)
    ├── CanPlayerUse(clientId, netId) → bool + reason
    └── RequestCanUseRpc(netId) → TargetRpc

MetaRequirement на любом GameObject (generic):
    ├── _requiredItems : ItemData[]  ← массив
    ├── _logic : RequirementLogic { All, Any, AtLeastN }
    ├── _requiredCount : int (для AtLeastN)
    ├── _interactableDisplayName : string
    ├── OnInventoryChanged event
    └── CanPlayerUse(ulong clientId, out string reason)

CLIENT:
[MetaRequirementClientState] (singleton)
    ├── OnCanUseResponse
    ├── OnBindingsPushed
    └── OnInteractableFound

UI:
[MetaRequirementToast] (UIDocument) — generic: "X/N собрано" + список недостающих
```

**Wiring:**
- ✅ `Assets/_Project/Scripts/MetaRequirement/RequirementLogic.cs` (enum)
- ✅ `MetaRequirement.cs`, `MetaRequirementRegistry.cs`, `MetaRequirementClientState.cs`, `MetaRequirementToast.cs`, `LockBox.cs` (7 файлов, ~50 KB)
- ✅ `InventoryWorld.cs` — 4 extensions: `HasAllItems`, `HasAnyItem`, `CountOf`, `GetMissingItems`
- ✅ `NetworkManagerController.CreateMetaRequirementClientState()` (auto-spawn)
- ✅ `NetworkPlayer.TryInteractNearestMetaRequirement()` (E-key entry point для НЕ-кораблей)

**Алиасы (backward compat):**
- ✅ **Удалены в P1 рефакторинге (2026-07-05):** `ShipKeyBinding.cs`, `ShipKeyServer.cs`, `ShipKeyClientState.cs`, `ShipKeyToast.cs`. `NetworkManagerController` больше не создаёт `ShipKeyClientState`. `NetworkPlayer` — убраны `ReceiveShipKey*TargetRpc`. См. `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md`.

**Тестовые ассеты (R2-META-REQ-001 verification):**
- ✅ 3 SO `ItemData`: `Item_Key_Blue/Red/Green.asset`
- ✅ 6 URP/Lit материалов: `Key_{Blue,Red,Green}.mat` + `LockBox_{Blue,Red,Green}.mat`
- ✅ `MetaRequirementPanelSettings.asset`
- ✅ `WorldScene_0_0.unity`: `[MetaRequirement_Test]` parent + 3 Pickup + 3 LockBox
- ✅ `BootstrapScene.unity`: `[MetaRequirementRegistry]` + `[MetaRequirementToast]`

**Compile (2026-06-06):** 0 errors, warnings только pre-existing + by-design obsolete-usage.

**Stats:**
- +7 C# файлов (~50 KB)
- +3 SO ItemData
- +6 материалов
- +9 GameObject'ов в сценах
- +9 документов в `docs/MetaRequirement/`

**TODO (Этап 2+):**
- ⏳ `_consumeOnUse` логика + reservation pattern
- ⏳ `ProgressInfo` UI в `MetaRequirementToast` (multi-item tooltip "3/5 ключей собрано")
- ⏳ Disconnect → reconnect race fix
- ⏳ Multi-MetaRequirement в одной зоне (сейчас 1→1)
- ⏳ Использование `MetaRequirement` для квестов (T-Q?? когда потребуется)

**Документация:** `docs/MetaRequirement/00_OVERVIEW.md` (517 строк) + `10_IMPLEMENTATION_GUIDE.md` (22 KB) + `20_INSPECTOR_REFERENCE.md` + `30_RUNTIME_FLOW.md` + `40_TESTING_GUIDE.md` + `50_KNOWN_ISSUES.md` + `99_CHANGELOG.md` + `RECIPES.md` (10 рецептов).



### 13.3 Ship Key Subsystem v2 (R2-SHIP-KEY-003, 2026-06-19) ✅

**MVP:** уникальные экземпляры ключей для каждого корабля. Каждый из 3 кораблей (Light/Medium/Heavy) получил свой уникальный KeyRodInstance — серверный реестр различает физические копии ключей.

**Ключевая идея:** ключ — это пара `(itemId, instanceId)`. ItemId определяет тип ключа (Light/Medium/Heavy), instanceId — конкретный физический стержень в мире. Сервер гарантирует 1:1 привязку: один instanceId ↔ один корабль ↔ один владелец.

**Игровой цикл:**
1. Игрок видит [KeyRod_ShipLight] в мире (физический объект с `PickupItem` + `KeyRodInstanceBinding`).
2. **E (interact)**: `PickupItem.Collect` → `RequestPickupRpc(itemId=2010, instanceId=N)`. Сервер проверяет что instanceId не дубликат, `TransferInstance(NONE→playerId)`, добавляет в `InventoryData._keySlots[]` с правильным instanceId.
3. **P → КОРАБЛЬ**: в CharacterWindow показан dropdown с кораблями игрока (только те, instanceId которых у него в инвентаре). Можно выбрать корабль и просмотреть телеметрию (fuel, cargo, modules, position).
4. **F (board ship)**: `MetaRequirementRegistry.CanPlayerUse` → `ShipOwnershipRequirement.IsOwnerOfShip` → `KeyRodInstanceWorld.IsOwnerOfInstance(clientId, instanceId) == true` → F разрешает, `SubmitSwitchModeRpc` → посадка.
5. **TAB → БРОСИТЬ ключ**: `TransferInstance(playerId→NONE)` + `UpdateState(Lost)`. Корабль больше не доступен через F.
6. **E на дропнутый ключ**: сервер **реактивирует** Lost instance (а не создаёт новый): `UpdateState(Lost→Active)` + `TransferInstance(NONE→playerId)`. Слот получает тот же instanceId — корабль снова доступен.

**Архитектура:**
```
SERVER (host):
[InventoryServer] : NetworkBehaviour (BootstrapScene)
    ├── HandleClientConnectedServer → InventoryWorld.GetOrCreate(clientId)
    └── RequestPickupRpc(itemId, type, instanceId, pos) → InventoryWorld.TryPickup(clientId, itemId, type, pos, instanceId)
                                                ↓
[InventoryWorld] : MonoBehaviour (DontDestroyOnLoad) — глобальный singleton
    ├── _playerInventories : Dictionary<ulong, InventoryData>
    ├── TryPickup: для Key → FindLostInstance (reactivate) → FindActiveKeyInstance (return existing) → CreateInstance (last fallback)
    └── AddKeyItem(itemId, instanceId) — создаёт слот С instanceId (НЕ AddItem!)
                                                ↓
[KeyRodInstanceWorld] : static — server-only single source of truth для всех KeyRodInstance
    ├── _instancesById : Dictionary<int, KeyRodInstance>
    ├── _primaryInstanceByShipId : Dictionary<ulong, int> (1:1 ship → instance)
    ├── _instancesByPlayer : Dictionary<ulong, List<int>> (владение)
    ├── CreateInstance(itemId, shipId, ownerId)
    ├── TransferInstance(id, oldOwner, newOwner)
    ├── UpdateState(id, Active/Lost/Destroyed)
    └── FindActiveKeyInstance(clientId, itemId) — поиск existing instance для pickup drop-нутого ключа
                                                ↓
[ShipController] : NetworkBehaviour
    ├── ShipOwnershipRequirement — авто-attach в Awake
    └── ShipTelemetryState : NetworkVariable<struct> — fuel/cargo/position для UI
                                                ↓
[JsonKeyRodInstanceRepository] : IPlayerDataRepository — persistence
    └── KeyRodInstances.json — {instances: [{instanceId, itemId, registeredShipId, ownerPlayerId, state, ...}]}
                                                ↓
[ScenePlacedObjectSpawner] : NetworkBehaviour
    └── Auto-spawn [KeyRod_*] PickupItem + KeyRodInstanceBinding при загрузке сцены

CLIENT:
[InventoryClientState] : MonoBehaviour singleton
    └── OnSnapshotReceived → InventoryTab + MyShipsTab обновляются

[MyShipsTab] : UI Toolkit tab в CharacterWindow
    ├── Subscribe: InventoryClientState.OnSnapshotUpdated
    └── Render: dropdown кораблей игрока + telemetry
```

**Ключевые компоненты (новые/обновлённые):**
- ✅ `KeyRodInstance` — POCO: instanceId, itemId, registeredShipId (shipNetId), ownerPlayerId, originalOwnerId, state (Active/Lost/Destroyed)
- ✅ `KeyRodInstanceWorld` — server-only static facade с persistence (единственный источник правды, 0 reflection)
- ✅ `KeyRodInstanceBinding` — ~~scene-placed MonoBehaviour (auto-register с retry 1.0s × 15)~~ **Удалён в P1 (2026-07-05).** ShipController создаёт KeyRodInstance в OnNetworkSpawn.
- ✅ `ShipOwnershipRequirement` — auto-attach на ShipController.Awake
- ✅ `ShipOwnershipRegistry` — **Удалён в P1 (2026-07-05).** ShipTelemetryClientState читает ownerClientId из telemetry напрямую.
- ✅ `MyShipsTab` — UI вкладка в CharacterWindow с dropdown + telemetry
- ✅ `JsonKeyRodInstanceRepository` — JSON persistence (`KeyRodInstances.json`)

**Backward compatibility:**
- ✅ `ShipKeyBinding`/`ShipKeyServer`/`ShipKeyClientState`/`ShipKeyToast` — **Удалены в P1 (2026-07-05).** 0 ссылок в коде и сценах.
- `MetaRequirement` — продолжает работать как generic требование (двери, контейнеры)
- ShipKey теперь построен поверх MetaRequirement для boarding check

**Документация:** `docs/Ships/Key-subsystem/99_CHANGELOG.md` (v1–v20) + `28_KEY_ARCHITECTURE_REVIEW.md` (глубокий обзор 11 проблем) + `29_KEY_REFACTOR_PLAN.md` (план полного рефакторинга, Phase 2).

**Что изменилось для игрока:**
- 🔑 Каждый ключ теперь **уникален** (нельзя скопировать без специального крафта — Phase 2).
- 🚪 Дроп ключа = потеря доступа к кораблю (нельзя сесть).
- 🔄 Re-pickup дропнутого ключа = реактивация того же instance (не дубль).
- 👥 Передача ключа другому игроку = передача владения кораблём (Phase 2: trade UI).
- 📋 TAB → ВЛАДЕНИЕ (новое имя сектора): Equipment + Key предметы вместе.

**TODO (Phase 2):**
- ⏳ Квесты на ключи (`DialogueAction.GiveItem` для quest reward)
- ⏳ Крафт копий ключей на верфи (нелегальный `isDuplicate` путь)
- ⏳ NPC-продажа ключей (вторичный рынок)
- ⏳ Trade UI: передача ключа между игроками
- ⏳ HUD telemetry widget (position, fuel, cargo на экране)
- ⏳ Key access level: Limited / OneTime (текущий Full)


### 13.3 Что НЕ реализовано (out of scope)

- ⏳ **Полноценная inventory-based boarding UI** — сейчас `ShipKeyToast` показывает только текст. UI с прогресс-баром "X/N ключей собрано" — TODO.
- ⏳ **Пер-slot key requirement** — разные ключи для разных слотов (Light/Medium/Heavy vs custom binding).
- ⏳ **Multiple keys per ship** — расширение `MetaRequirement._requiredItems[]` уже поддерживает, но не используется в production.
- ⏳ **Key как квестовый reward** — `DialogueAction.GiveItem` может выдать ключ (T-Q15, T-Q27), но **реальный** квест "принеси ключ" ещё не создан (только тестовые).
- ⏳ **NPC trade ключами** — сейчас ключ — только pickup с пола. Возможность купить у NPC — Future.
- ✅ **Crafting ключа** — `docs/Crafting_system/` реализована (T-C01–T-C07c, 2026-06-11). Станция Shipyard в WorldScene_0_0 варит ключ ShipLight (1 слиток + 1 кристальная пыль → 1 ключ, 30с).

### 13.4 Где смотреть актуальный статус

- **`docs/Ships/Key-subsystem/00_OVERVIEW.md`** — обзор Ship Key Subsystem
- **`docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md`** — миграция
- **`docs/MetaRequirement/00_OVERVIEW.md`** — дизайн MetaRequirement
- **`docs/MetaRequirement/99_CHANGELOG.md`** — changelog
- **`docs/MMO_Development_Plan.md`** §1.4, §1.5, §1.9 — общий план

---

## 14. Composite Ship Architecture (Phase 0–1, 2026-06-17)

> **Секция добавлена 2026-06-17.** Реализация составного корабля — фундамент для всех будущих ship-систем.

### 14.1 Концепция

Корабль больше не 1 куб. Теперь это **иерархия GameObjects** с единым корневым Rigidbody:

```
Ship_Root (Rigidbody + NetworkObject + ShipController)
├── PilotSeat (PilotSeatController + ShipRootReference + BoxCollider trigger)
├── Door (DoorController + ShipRootReference + BoxCollider trigger)
├── Engine_Left (ModuleSlot + ShipRootReference)
└── (любые другие части)
```

### 14.2 Ключевые решения

| Решение | Обоснование |
|---------|-------------|
| **ShipController на корне** | `GetComponent<Rigidbody>()` работает; WindZone находит через GetComponentInParent |
| **Один Rigidbody на корне** | Физика цельной конструкции, никаких вложенных Rigidbody |
| **Дочерние объекты — без NetworkObject** | Для MVP — только корневой NetworkObject. Всё движется как единое целое |
| **Парентинг игрока при посадке** | Без парентинга коллайдер игрока внутри корабля → физика дергается |

### 14.3 Новые компоненты

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `ShipRootReference` | `Scripts/Ship/ShipRootReference.cs` | Маркер на любой части корабля. В Awake кеширует ShipController/Rigidbody/NetworkObject с корня |
| `ShipComponentLocator` | `Scripts/Ship/ShipComponentLocator.cs` | Static helper: FindShipController(GameObject) от любой части корабля |
| `PilotSeatController` | `Scripts/Ship/PilotSeatController.cs` | Триггер места пилота. `_controller.enabled = false`, renderer остаётся видимым |
| `DoorController` | `Scripts/Ship/DoorController.cs` | Slide-анимация (Lerp). Локальная, без сети. E-key toggle |

### 14.4 Изменения в существующих скриптах

| Скрипт | Изменение | Связанные тикеты |
|--------|-----------|-----------------|
| `ShipController.cs` | + `public Transform ShipRoot => transform.root` | Phase 0 |
| `NetworkPlayer.cs` | + `transform.SetParent(корень)` при посадке / `SetParent(null)` при выходе | Phase 1 |
| `NetworkPlayer.cs` | Игрок НЕ скрывается (renderer остаётся включён) | Phase 1, дизайн |
| `ThirdPersonCamera.cs` | + `SetTargetMode(target, isShip)` | Phase 1 |
| `InteractableManager.cs` | `FindNearestShip` — приоритет PilotSeat коллайдера | Phase 1 |

### 14.5 Совместимость с подсистемами

| Подсистема | Статус | Как работает |
|-----------|--------|-------------|
| **ShipModuleManager** | ✅ Готов | GetComponentsInChildren\<ModuleSlot\> — ищет в детях |
| **ModuleSlot** | ✅ Готов | Отдельный MonoBehaviour на дочерних объектах |
| **WindZone** | ✅ Готов | GetComponentInParent\<ShipController\> — находит корень |
| **MetaRequirement** | ✅ Готов | На любом дочернем GameObject. Ships пропущены через фильтр `ShipController` |
| **MeziyModuleActivator** | ⏳ Phase 4 | Сейчас serialized ссылка; нужно `GetComponentsInChildren\<MeziyNozzle\>()` |

### 14.6 Документация

- `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` — обзор архитектуры
- `docs/Ships/analysis-composite-ship.md` — полный анализ (29 KB, 12 разделов)
- `docs/Ships/roadmap-integration.md` — план реализации

---

*Документ создан: Апрель 2026 | Агенты: @technical-director, @game-designer, @lead-programmer, @engine-programmer, @gameplay-programmer, @unity-specialist | Дополнено Mavis 2026-06-10 (раздел реализации Key + MetaRequirement), 2026-06-17 (Composite Ship Architecture), 2026-06-19 (R2-SHIP-KEY-003 §13.3 — уникальные экземпляры ключей), 2026-07-05 (коррекция по коду: §3.2, §4.1, §5.4, §7.3.1, §11.1, план реализации) *
