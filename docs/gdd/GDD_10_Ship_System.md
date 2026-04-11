# GDD_10: Ship System v4.0

**Версия:** 4.0 | **Дата:** Апрель 2026 | **Статус:** В разработке | **Ветка:** `qwen-gamestudio-agent-dev`

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
    → Apply system degradation (future: freeze)
```

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
| **linearDrag** | 0.4 | 0.5 | 0.6 | 0.7 |
| **angularDrag** | 3.5 | 4.0 | 4.5 | 5.0 |
| **maxSpeed** | 35-45 м/с | 25-35 м/с | 15-22 м/с | 10-18 м/с |
| **mass** | 0.8-1.2 | 2.0-3.0 | 4.0-6.0 | 6.0-10.0 |

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
├── powerRequirement: float
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
    public KeyRodAccessLevel accessLevel; // Full, Limited, OneTime
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

### 7.1 Поток Стыковки
```
1. Игрок входит в зону города (radius = 500-1500м)
2. Открывает CommPanel → "Запрос стыковки"
3. Запрос на сервер → DockingDispatcher
4. Диспетчер отвечает:
   ├── Pad #5, сектор B
   ├── Подход: высота 4200, курс 270
   ├── Окно посадки: 90 секунд
   └── "Борт [ID], добро пожаловать в Примум"
5. Игрок следует инструкциям → авто-наведение (с MODULE_AUTO_DOCK)
6. Касание платформы → Docked состояние → Engine Off
```

### 7.2 Диспетчер — Сообщения
```csharp
public struct DispatcherMessage {
    public string padId;         // "PAD-PRM-005"
    public Vector3 approachPoint;
    public float approachAltitude;
    public float approachHeading;
    public float landingWindow;  // секунды
    public string voiceLine;     // "Борт 7-Альфа, Примум-Диспетчер..."
}
```

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

| Состояние | Описание | Триггер Входа | Триггер Выхода |
|-----------|----------|---------------|----------------|
| **EngineOff** | Все системы неактивны | KeyRod извлечён | KeyRod вставлен |
| **Idle** | Антиграв активен, зависание | KeyRod вставлен, нет ввода | Ввод обнаружен |
| **Flying** | Под управлением пилота | Ввод от пилота | Все пилоты вышли |
| **Docking** | Следует инструкциям диспетчера | Docking accepted | Landed / Cancelled |
| **Docked** | Заблокирован на платформе | Касание pad + скорость=0 | KeyRod извлечён / Engine On |
| **AutoHover** | Зависание (все пилоты вышли) | PilotCount = 0 | PilotCount > 0 |
| **⚠️ VeilTurbulence** | Ниже коридора | Alt < minAlt | Alt >= minAlt + 50 |
| **⚠️ SystemDegrade** | Выше коридора | Alt > maxAlt + 100 | Alt <= maxAlt |
| **⚠️ SOLLock** | СОЛ блокирует | SOL violation timeout | Оплата штрафа / модуль Stealth |

---

## 9. Повреждения и Крушения (Будущее — Низкий Приоритет)

Записать, но **НЕ реализовывать** до Этапа 5+:

- Столкновение с пиком → урон корпусу, возможное разрушение
- Критический урон → корабль теряет управление, падает
- Падение на поверхность Земли (под Завесой) → разрушение
- Ремонт на верфях (Тертиус)
- Потеря груза при крушении (частичная)

---

## 10. План Реализации

### Фаза 1: Переписывание Core Movement (Текущий спринт)
| # | Задача | Приоритет | Ответственный |
|---|--------|-----------|--------------|
| 1.1 | Переписать ShipController.cs с новой физикой | P0 | engine-programmer |
| 1.2 | Smooth yaw — убрать резкость, добавить Lerp | P0 | gameplay-programmer |
| 1.3 | Smooth pitch — ограничить ±20°, замедлить | P0 | gameplay-programmer |
| 1.4 | Smooth lift — очень медленно, 1.5-2.5 м/с | P0 | gameplay-programmer |
| 1.5 | Auto-stabilization — возврат к горизонту | P0 | engine-programmer |
| 1.6 | Angular drag ×3-5 — гасить вращение | P0 | engine-programmer |
| 1.7 | Unity тесты: проверить плавность | P1 | unity-specialist |

### Фаза 2: Altitude Corridor System
| # | Задача | Приоритет | Ответственный |
|---|--------|-----------|--------------|
| 2.1 | AltitudeCorridorSystem.cs (ScriptableObject + runtime) | P0 | engine-programmer |
| 2.2 | City corridor data: 5 городов | P0 | game-designer |
| 2.3 | Server altitude validation (каждые 0.5с) | P0 | devops-engineer |
| 2.4 | Warning UI: предупреждения высоты | P1 | ui-programmer |
| 2.5 | Unity тесты: corridor boundaries | P1 | unity-specialist |

### Фаза 3: Wind & Turbulence
| # | Задача | Приоритет | Ответственный |
|---|--------|-----------|--------------|
| 3.1 | WindZone.cs (объёмные зоны ветра) | P1 | engine-programmer |
| 3.2 | Wind force application на корабль | P1 | gameplay-programmer |
| 3.3 | Turbulence near Veil (тряска + Random force) | P1 | engine-programmer |
| 3.4 | Cinemachine Impulse для камеры | P2 | unity-specialist |

### Фаза 4: Module System Foundation
| # | Задача | Приоритет | Ответственный |
|---|--------|-----------|--------------|
| 4.1 | ShipModule ScriptableObject architecture | P0 | lead-programmer |
| 4.2 | ModuleSlot на кораблях | P0 | engine-programmer |
| 4.3 | MODULE_YAW_ENH, PITCH_ENH, LIFT_ENH (тир 1) | P1 | gameplay-programmer |
| 4.4 | MODULE_MEZIY_THRUST (burst maneuvers) | P1 | gameplay-programmer |
| 4.5 | ShipRegistry.md наполнение | P1 | game-designer |

### Фаза 5: Co-Op & Docking
| # | Задача | Приоритет | Ответственный |
|---|--------|-----------|--------------|
| 5.1 | KeyRod system (ScriptableObject + validation) | P1 | lead-programmer |
| 5.2 | Adaptive multi-pilot input | P1 | gameplay-programmer |
| 5.3 | DockingDispatcher.cs | P2 | engine-programmer |
| 5.4 | CommPanel UI (Elite Dangerous style) | P2 | ui-programmer |
| 5.5 | SOL zone warnings | P2 | game-designer |

### Фаза 6: Advanced (Будущее)
| # | Задача | Приоритет | Ответственный |
|---|--------|-----------|--------------|
| 6.1 | Veil penetration mechanics | P3 | engine-programmer |
| 6.2 | Space freeze mechanics | P3 | engine-programmer |
| 6.3 | MODULE_AUTO_DOCK (автопилот) | P3 | engine-programmer |
| 6.4 | Damage/crash system | P4 | game-designer |
| 6.5 | MODULE_STEALTH (counter-SOL) | P3 | gameplay-programmer |

---

## 11. Технические Спецификации

### 11.1 Текущие vs Целевые Параметры ShipController.cs

| Параметр | Сейчас | Цель (LIGHT) | Цель (HEAVY) | Изменение |
|----------|--------|-------------|-------------|-----------|
| thrustForce | 500 | 350 | 200 | ×0.7 / ×0.4 |
| maxSpeed | 30 м/с | 40 м/с | 18 м/с | Класс-зависимый |
| yawForce | 30 | ×0.4 (12) | ×0.25 (7.5) | РЕЗКО медленнее |
| pitchForce | 40 | ×0.5 (20) | ×0.35 (14) | Значительно медленнее |
| verticalForce | 300 | 120 | 80 | ×0.4 / ×0.27 |
| stabilizationForce | 50 | 30 | 25 | Мягче |
| linearDrag | 1.0 | 0.4 | 0.6 | Меньше (плавнее) |
| angularDrag | 2.0 | 3.5 | 4.5 | ×1.75 / ×2.25 |

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

---

*Документ создан: Апрель 2026 | Агенты: @technical-director, @game-designer, @lead-programmer, @engine-programmer, @gameplay-programmer, @unity-specialist*
