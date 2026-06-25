# M2 FSM Diagnosis & M3 Plan — NPC Barges

> **Project C: The Clouds** | Unity 6000.4.1f1 | NGO 2.11.0
> **Session:** 2026-06-24 | Mavis (profile `project-c`)
> **Status:** Диагноз + План. Код M3 — начнём после approval.

---

## TL;DR

**Корневая проблема не в формулах движения, а в архитектуре.** Текущая FSM (12 состояний в `NpcShipWorld.TickNpc`) пытается одновременно управлять и логикой, и движением, и условиями переходов. Каждое состояние самостоятельно вызывает `ApplyMovementInput(...)` через `NpcShipController` — и это приводит к тому, что физика корабля (движок, anti-gravity, smoothdamp) начинает бороться с NPC-логикой.

**Ключевая аналогия:** игрок садится в кресло пилота → пилотирует сам → NPC не нужен. NPC должен быть **"диспетчером маршрута"**, а не "вторым пилотом". Диспетчер решает *куда лететь и когда останавливаться*, а корабль всё остальное делает сам — через ту же физику, что и игрок.

**Предлагаемое решение (M3):**
1. **Пилот-компонент (NPC) генерирует целевые точки и условия перехода**, а не сырые input'ы.
2. **Корабль сам решает** как лететь к цели: горизонтально thrust, набор высоты lift, разворот yaw — через те же механики что и игрок, только автоматически.
3. **Trigger-зоны станций (OuterCommZone, DockingPadTriggerBox) — единственный источник истины о прибытии и стыковке**, а не магические числа.
4. **Никакой логики движения в `NpcShipWorld`.** Только координация: какая у NPC цель, какое состояние, какие события.

---

## 1. Что фактически не работало (по всем сессиям)

### 1.1 Симптомы (по сессиям)

| # | Симптом | Где видно | Когда появился |
|---|---------|-----------|----------------|
| S1 | NPC улетают вверх на 400+м (Y=2900 вместо Y=2535) | `POSTMORTEM` §"Текущее состояние" | Все версии |
| S2 | NPC "срезают углы" — при bearing > 30° всё равно добавляют thrust | Логика диагонального полёта | M2 (текущая) |
| S3 | NPC "танцуют" — wiggle из-за thrust+yaw+vertical в одном кадре | `POSTMORTEM` §P2 | Все версии |
| S4 | При уменьшении `angularDamping` с 8 до 0.8 NPC слишком быстро крутится | `FAIL_m2_antigravity_pd.md` §"Корневая ошибка 2" | m2 antigravity PD |
| S5 | NPC "дёргается" в Approaching — пытается сесть на pad, не долетев | `POSTMORTEM` §P5 | T-NS05+ |
| S6 | Pad ID дублировались (4 пада с `padId = "PAD-001"`) | `POSTMORTEM` §P7 | T-NS05 |
| S7 | AssignPadForNpc возвращал bool без `padId` — NPC стыковался в воздухе | `POSTMORTEM` §P5 | T-NS05 |
| S8 | PD-controller для Y "не проверен в Play Mode" | `POSTMORTEM` §P5 / "Не проверено" | ba4b0ba |
| S9 | Magic numbers (30, 500, 100, 50) ломаются при изменении сил двигателя | `POSTMORTEM` §P1, P4 | Все версии |
| S10 | NPC исчезает в полёте (вне `WorldScene_0_0`) | `02_INDUSTRY_PATTERNS.md` §"Anti-patterns" | M1 |

### 1.2 Корневые проблемы (не поверхностные)

| # | Проблема | Где в коде | Почему сломано |
|---|---------|-----------|----------------|
| **R1** | **FSM не разделена с movement**: 12 состояний FSM и 3 movement-метода (`ApplyDeparting`, `ApplyTransit`, `ApplyApproach`) переплетены в одном `TickNpc` switch | `NpcShipWorld.TickNpc` (648 LOC) | Состояние FSM триггерит movement, movement триггерит переходы состояния, плюс movement сразу же вызывает `ApplyMovementInput` с magic numbers — три уровня feedback в одном методе |
| **R2** | **Magic numbers** для условий переходов: `YAW_DEAD_ZONE=15`, `BEARING_DRIFT_LIMIT=30`, `PAD_CLEAR_HEIGHT=5`, `APPROACH_THRUST_SCALE=50`, `AG_MIN=0.3`, `AG_MAX=1.5` | `NpcShipWorld.cs:35-56`, `:604-608` | При изменении `thrustForce`, `yawForce`, `mass` корабля — все условия теряют смысл, логика становится "магией" |
| **R3** | **Per-frame `ApplyMovementInput`** — каждый кадр NPC вызывает ShipController → тот передаёт в `_sumThrust/_sumYaw/_sumVertical` → SmoothDamp (0.3-0.6s) → ApplyThrust/ApplyLift/ApplyRotation | `NpcShipController.ApplyMovementInput` → `ShipController.ApplyServerInput` → `FixedUpdate:828-868` | SmoothDamp на yawForce=25 + 0.4 multiplier даёт "сглаженный вход", но SmoothDamp не останавливается мгновенно → остаточная скорость → перелёт цели → корректирующий вход → змея |
| **R4** | **NPC input конкурирует с физикой корабля**: одновременно `AntiGravity` (через `ApplyAltitudeControl` PD-controller) + `vertical` input + `linearDamping` | `NpcShipWorld.ApplyAltitudeControl` (line 614) пишет `ship.AntiGravity = newAG` каждый кадр, плюс `controller.ApplyMovementInput(vertical: ...)` | AG_Min=0.3 даёт 70% веса вниз + vertical input + drag = непредсказуемая сумма |
| **R5** | **AG_MIN/AG_MAX "магия"** — `AG_MIN=0.3` означает "оставить 70% веса" (т.е. корабль активно падает) | `NpcShipWorld.cs:607` | По задумке автора — "лёгкое проседание". По физике — падение с ускорением 6.87 м/с² |
| **R6** | **Курс проверяется раз в 5 сек** — NPC улетает от курса на 100м за 5 сек при `thrust=0.6*maxSpeed=40 → 24 м/с` | `NpcShipWorld.ApplyTransitMovement` step 2b (line 373) | Bearing 5° → 30° за 5 сек при 24 м/с = 2.1м, не критично. Но при `dist>500м` до цели и bearing>30° diagonal сразу → перелёт |
| **R7** | **Yaw мультипликативно слабее чем у игрока**: `npcYawMult=0.4` × `yawForce=5` (сцена!) = 0.4×5 = 2 → поворот 0.2°/с = 145° за 12 минут | `NpcShipController.cs:50`, `NpcShipWorld.cs:362` × `ShipController.cs:142` | `yawForce=5` в сцене уже в 5 раз слабее дефолта (25). Умножение на 0.4 даёт в 12.5× слабее игрока |
| **R8** | **angularDamping=8** в сцене блокирует ВСЕХ пилотов, не только NPC | `NpcShipController.cs:118-124` (исправление в 0.8) | Это была попытка сделать NPC "стабильным" — но ударила по всем |
| **R9** | **`scene-placed NetworkObject` с `InScenePlacedSourceGlobalObjectIdHash==0`** — не спавнится автоматически | `NpcShipController.OnNetworkSpawn` рассчитывает на `NetworkObjectId` | `ScenePlacedObjectSpawner` спавнит, но `npcInstanceId` генерируется в `OnNetworkSpawn`, что **может** не произойти до `NpcShipServer.DiscoverNpcShipsDelayed` (2 сек) |
| **R10** | **TESTZONENPC SphereCollider isTrigger=0** — `OuterCommZone.Awake` ставит `isTrigger=true`, но в сцене он 0 (override?) | `WorldScene_0_0.unity:541` | Если `isTrigger=0` — никогда не сработает `OnTriggerEnter` для Zone связи |
| **R11** | **`OuterCommZone.commRange=242.3`** на TESTZONENPC — слишком мало, NPC "проскакивает" зону | `WorldScene_0_0.unity:524` | При скорости 24 м/с и проверке раз в кадр — пролетает зону 242м за 10 сек |
| **R12** | **`commRange=421.6`** на DockStation_Primium — слеплено вручную в сцене, не через DockStationDefinition | `WorldScene_0_0.unity:15670` | Magic number в scene YAML, не в SO — изменение в DockStationDefinition.commRange не подхватывается |

---

## 2. Что было **правильно** в предыдущих попытках

Не нужно выкидывать всё. Из 7 итераций кое-что работает:

| Решение | Где | Статус | Что сохранить |
|---------|-----|--------|---------------|
| `ApplyServerInput` generic API | `ShipController.cs:110-122` | ✅ Работает | **Сохраняем**. Это чистый server-only вход, готов для v2 autopilot |
| `EnableNpcPilot` flag | `ShipController.cs:129-133` | ✅ Работает | **Сохраняем**. Gate на `_pilots` корректно открывается для NPC |
| `AntiGravity` public setter | `ShipController.cs:173-177` | ✅ Работает | **Сохраняем**. Используется для boost 5 сек после ExitDocked |
| `DockingWorld.AssignPadForNpc` | `DockingWorld.cs:477-509` | ✅ Работает | **Сохраняем**. Server-internal путь, maxConcurrentLandings check |
| `DockingPadTriggerBox.IsShipInside` | `DockingPadTriggerBox.cs:31` | ✅ Работает | **Сохраняем**. Unity OnTriggerEnter — единственный надёжный trigger-detection |
| `OuterCommZone` структура | `OuterCommZone.cs` | ⚠️ Баг в сцене | **Сохраняем код**, фиксим сцену |
| `NpcShipState` POCO | `NpcShipState.cs` | ✅ Работает | **Сохраняем**, чистим лишние поля |
| `NpcShipSchedule` SO | `NpcShipSchedule.cs` | ✅ Работает | **Сохраняем** |
| `_hasNpcPilot` flag pattern | `ShipController.cs:181` | ✅ Работает | **Сохраняем** |
| `NetworkObjectId | 0x8000...` sentinel | `NpcShipController.cs:99` | ✅ Работает | **Сохраняем** |

| Решение | Где | Статус | Что **выкидываем** |
|---------|-----|--------|---------------------|
| 12-state FSM в `NpcShipWorld.TickNpc` | `NpcShipWorld.cs:124-325` | ❌ Сложно, не верифицировано | **Заменяем** на 7-режимный cleaner |
| PD-controller на `AntiGravity` | `NpcShipWorld.cs:604-636` | ❌ Не проверен | **Заменяем** на `verticalForce` через тот же `ApplyServerInput` |
| Magic numbers: `PAD_CLEAR_HEIGHT=5`, `AG_MIN=0.3`, `APPROACH_THRUST_SCALE=50`, `YAW_DEAD_ZONE=15` и т.д. | `NpcShipWorld.cs:35-56` | ❌ Не универсально | **Удаляем**. Все константы — в `NpcShipController` через `SerializeField` или удаляем |
| `_staggerOffset` словарь | Удалён в M2 | ✅ Удалён | — |
| `cruiseAltitude` поле в `NpcShipRoute` | `NpcShipRoute.cs:50` | ❌ Не используется | **Удаляем**. Cruise высота не нужна — корабль идёт по прямой A→B |
| `flightDurationSec` поле | `NpcShipRoute.cs:41` | ❌ Не используется | **Оставляем** для v2 (analytics) |
| `npcThrustMult`, `npcYawMult` | `NpcShipController.cs:47-50` | ⚠️ Нужно пересмотреть | **Заменяем**: NPC использует `thrustForce`/`yawForce`/`verticalForce` **те же** что и игрок |
| `EnsureNpcSolidCollider` хак | `NpcShipController.cs:183-198` | ⚠️ Сломает пады | **Пересматриваем**: солид коллайдер для pad-trigger правильно — отдельный `trigger` пада + `solid` на ship. Но если у пада уже solid (как у DockStation_Primium) — может конфликтовать |

---

## 3. Предлагаемая архитектура (M3)

### 3.1 Разделение ответственности

```
┌─────────────────────────────────────────────────────────────┐
│ NpcShipController (на корне корабля)                       │
│ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ │
│  • Имеет ссылку на Schedule SO + ShipController             │
│  • Держит `currentTarget: Vector3?` (куда лететь)         │
│  • Держит `currentMode: NavMode` (Lift/Yaw/Cruise/...)     │
│  • Тикает `NavTick()` — вычисляет input раз в FixedUpdate  │
│  • Решает когда:                                           │
│      - Завершить lift (по Y)                                │
│      - Завершить yaw (по углу)                              │
│      - Завершить cruise (по dist)                           │
│      - Запросить pad (через DockingWorld)                   │
│      - Touched down (через DockingPadTriggerBox)            │
│  • ВЫЗЫВАЕТ `ship.ApplyServerInput(...)` — то же что игрок  │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│ NpcShipWorld (server-only state machine)                   │
│ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ │
│  • Хранит _npcByInstanceId + _scheduleByNpcInstanceId       │
│  • Per-frame Update():                                     │
│      1. Для каждого NPC → controller.NavTick()             │
│  • НИКАКОЙ movement-логики внутри                          │
│  • НИКАКИХ input'ов, magic numbers                         │
│  • НИКАКОГО PD-controller                                  │
│  • Только координация и события                            │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│ ShipController (без изменений)                              │
│ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ │
│  • Та же физика что и для игрока:                          │
│      - thrustForce, yawForce, verticalForce                │
│      - SmoothDamp input                                    │
│      - ApplyAntiGravity, ApplyLiftForce                    │
│  • Единственное добавление: ApplyServerInput (уже есть)    │
│  • _hasNpcPilot gate (уже есть)                            │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 NavMode — 7 чистых режимов

```csharp
public enum NavMode : byte {
    /// <summary>Стоит на паде, ждёт разрешения на вылет.</summary>
    Docked,

    /// <summary>Вертикальный набор высоты (только vertical input).</summary>
    /// <remarks>Завершение: pos.y >= startY + targetClearance (например 5м).</remarks>
    Lifting,

    /// <summary>Поворот на месте к цели (только yaw input).</summary>
    /// <remarks>Завершение: |bearing| < 5° (hysteresis).</remarks>
    Yawing,

    /// <summary>Полёт к цели: thrust + vertical по прямой A→B (диагональ).</summary>
    /// <remarks>Завершение: dist < arrivalRange (вход в зону связи порта).</remarks>
    Cruising,

    /// <summary>Hover у зоны связи, ждёт pad от диспетчера.</summary>
    /// <remarks>Завершение: AssignedPadId != null.</remarks>
    Holding,

    /// <summary>Финальный подход к паду: yaw to pad → diagonal → vertical descent.</remarks>
    /// <remarks>Завершение: DockingPadTriggerBox.IsShipInside → EnterDocked.</remarks>
    Berthing,

    /// <summary>Только anti-gravity, никаких input (отладка/пауза).</summary>
    Hover,
}
```

### 3.3 Spatial conditions — все через `Vector3.Distance` / углы / trigger

```csharp
public static class NavChecks {
    /// <summary>Стоим ли в зоне связи OuterCommZone?</summary>
    public static bool IsInCommZone(Vector3 pos, OuterCommZone zone) {
        if (zone == null) return false;
        return Vector3.Distance(pos, zone.transform.position) <= zone.CommRange;
    }

    /// <summary>Yaw выровнен? (c гистерезисом — порог входа 15°, выхода 5°)</summary>
    public static bool IsYawAligned(float currentYaw, float targetYaw, bool wasAligned) {
        float diff = Mathf.Abs(Mathf.DeltaAngle(currentYaw, targetYaw));
        return wasAligned ? diff < 5f : diff < 15f;
    }

    /// <summary>Набрали высоту?</summary>
    public static bool IsLiftedTo(float currentY, float startY, float target) {
        return currentY >= startY + target;
    }

    /// <summary>Долетели до цели?</summary>
    public static bool IsAtRange(Vector3 pos, Vector3 target, float range) {
        return Vector3.Distance(pos, target) <= range;
    }

    /// <summary>Trigger-бокс пада содержит корабль?</summary>
    public static bool IsInsidePadTrigger(ShipController ship, DockingPadTriggerBox pad) {
        return pad != null && pad.IsShipInside;
    }
}
```

### 3.4 NavTick — главный цикл внутри NpcShipController

```csharp
private void NavTick(float dt) {
    if (ship == null || ship.IsDocked) return;
    if (!NetworkingUtils.IsServerSafe()) return;

    switch (currentMode) {
        case NavMode.Lifting:
            if (NavChecks.IsLiftedTo(pos.y, startY, targetClearance)) {
                // Достигли высоты → переходим к Yaw
                currentTarget = ResolveStationCenterPos();
                currentMode = NavMode.Yawing;
                LogTransition("Lifting → Yawing");
            } else {
                ship.ApplyServerInput(thrust: 0, yaw: 0, pitch: 0, vertical: 1f);
            }
            break;

        case NavMode.Yawing:
            float bearing = Mathf.DeltaAngle(shipYaw, targetBearing);
            if (NavChecks.IsYawAligned(shipYaw, targetBearing, _wasAligned)) {
                if (_wasAligned) {
                    // Уже были выровнены, переходим к Cruise
                    currentMode = NavMode.Cruising;
                    _wasAligned = false;
                    LogTransition("Yawing → Cruising");
                } else {
                    _wasAligned = true;
                }
            } else {
                // Не выровнены → крутимся дальше
                _wasAligned = false;
                float yawInput = Mathf.Clamp(bearing * 0.02f, -1f, 1f);
                // Yaw-only: anti-gravity держит высоту
                ship.ApplyServerInput(thrust: 0, yaw: yawInput, pitch: 0, vertical: 0);
            }
            break;

        case NavMode.Cruising:
            var zone = ResolveCommZone(currentRoute.toLocationId);
            if (zone != null && NavChecks.IsInCommZone(pos, zone)) {
                // Вошли в зону связи → останавливаемся, запрашиваем pad
                currentMode = NavMode.Holding;
                LogTransition("Cruising → Holding (entered comm zone)");
            } else {
                // Периодическая проверка курса
                CheckCourseCorrection();

                // Diagonal: thrust + vertical по прямой A→B
                float dist = Vector3.Distance(pos, currentTarget);
                float thrust = Mathf.Clamp01(dist / 100f) * 0.6f;  // замедление при подходе
                float vertical = ComputeVerticalForCruise(pos, startY, endY, progress);
                ship.ApplyServerInput(thrust: thrust, yaw: 0, pitch: 0, vertical: vertical);
            }
            break;

        case NavMode.Holding:
            // Пытаемся получить pad каждые 2 сек
            if (Time.time - lastPadAttemptTime > 2f) {
                lastPadAttemptTime = Time.time;
                TryAssignPad();
            }
            if (assignedPadId != null) {
                currentTarget = ResolvePadPos(currentRoute.toLocationId, assignedPadId);
                currentMode = NavMode.Berthing;
                LogTransition("Holding → Berthing (pad assigned)");
            } else {
                // Hover: anti-gravity + нулевой input
                ship.ApplyServerInput(thrust: 0, yaw: 0, pitch: 0, vertical: 0);
            }
            break;

        case NavMode.Berthing:
            // ... см. секцию 3.5
            break;
    }
}
```

### 3.5 Berthing — финальный подход к паду (3 фазы)

```csharp
case NavMode.Berthing:
    var pad = ResolvePadTrigger(assignedPadId);
    if (pad != null && NavChecks.IsInsidePadTrigger(ship, pad)) {
        // Trigger вошёл → стыковка
        ship.EnterDocked();
        currentMode = NavMode.Docked;
        ReleasePadAssignment();
        LogTransition("Berthing → Docked (trigger)");
        return;
    }

    Vector3 padPos = ResolvePadPos(assignedPadId);
    float horizDist = HorizontalDistance(pos, padPos);
    float bearing = Mathf.DeltaAngle(shipYaw, BearingTo(pos, padPos));

    if (horizDist > 10f) {
        // Yaw к паду (на месте)
        if (Mathf.Abs(bearing) > 5f) {
            float yawInput = Mathf.Clamp(bearing * 0.02f, -1f, 1f);
            ship.ApplyServerInput(thrust: 0, yaw: yawInput, pitch: 0, vertical: 0);
        } else {
            // Diagonal: thrust + vertical к паду
            float thrust = Mathf.Clamp01(horizDist / 50f) * 0.4f;
            float vertical = ComputeVerticalForLanding(pos.y, padPos.y);
            ship.ApplyServerInput(thrust: thrust, yaw: 0, pitch: 0, vertical: vertical);
        }
    } else {
        // Vertical descent (close to pad)
        float vertical = ComputeVerticalForLanding(pos.y, padPos.y);
        ship.ApplyServerInput(thrust: 0, yaw: 0, pitch: 0, vertical: vertical);
    }
    break;
```

---

## 4. Что фиксим в сцене (T-NS-SCENE-01)

Прежде чем код, нужны 4 правки в `WorldScene_0_0.unity`:

| # | Что | Где | Зачем |
|---|-----|-----|-------|
| F1 | `TESTZONENPC` SphereCollider `m_IsTrigger: 1` | `WorldScene_0_0.unity:541` | `OuterCommZone.Awake` ставит `isTrigger=true`, но scene override ставит обратно. **Trigger — единственный способ OnTriggerEnter работает** |
| F2 | `TESTZONENPC.commRange` — 242.3 → 600 | `WorldScene_0_0.unity:524` | При скорости 24 м/с и частоте Update 60Hz — корабль проскакивает 242м за 10 сек. 600м даёт 25 сек на реакцию |
| F3 | `TESTZONENPC` — нет `DockingPadTriggerBox` (только 1 вход в `m_Children`) | `WorldScene_0_0.unity:584-588` | NPC не может сесть на TESTZONENPC — нет падов |
| F4 | `NPC_Ship_HeavyII_01..04` — `NpcShipController.schedule` не заполнен | Проверить через MCP после `refresh_unity` | Без schedule NPC не зарегистрируется в NpcShipWorld |

### 4.1 Дополнительно — починить stationId

- `TESTZONENPC.stationId = "PRIMIUM_TEST_ZONE_2"` — не соответствует `DockStation_Primium.stationId = "STN-PRM-001"`
- Это создаёт 2 разных locationId в `DockingZoneRegistry`, NPC не может найти TEST_ZONE через `GetByLocation`

**Решение:** переименовать в `TESTZONENPC.stationId = "STN-TEST-001"` (или `STN-PRM-002`), плюс в `NpcShipSchedule.routes[0].toLocationId` поставить то же.

---

## 5. Что НЕ меняем

- `ShipController` — не трогаем. Корабли работают хорошо, AG-boost через `AntiGravity` setter работает.
- `DockingWorld.AssignPadForNpc` / `ReleaseNpcAssignment` — не трогаем. Работает, корректно проверяет `maxConcurrentLandings`.
- `OuterCommZone` / `DockingPadTriggerBox` — не трогаем код. Только сцену.
- `NpcShipSchedule` SO / `NpcShipRoute` — не трогаем. Уберём только `cruiseAltitude` (не используется).
- NGO 2.11 RPC, NetworkVariable, NetworkTransform — не трогаем.
- `NpcShipServer` hub, `NpcShipTrafficManager`, `NpcShipZoneRegistry` — не трогаем. Работают.

---

## 6. Roadmap M3

### T-NS M3.1 — `NavChecks` утилиты + scene fixes (90 мин, 200 LOC)
- Создать `Assets/_Project/Scripts/PeacefulShip/Core/NavChecks.cs` — статические проверки
- Создать `Assets/_Project/Scripts/PeacefulShip/Core/NavMode.cs` — enum
- Создать `Editor tool` для фикса сцены (4 правки из §4)
- Через MCP: `refresh_unity`, исправить `m_IsTrigger` на TESTZONENPC, увеличить commRange
- Через MCP: добавить 2 DockingPadTriggerBox в TESTZONENPC
- Через MCP: переименовать stationId в TESTZONENPC + NpcShipSchedule

### T-NS M3.2 — `NpcShipController.NavTick` (180 мин, 500 LOC)
- Реализовать 7-режимный NavTick
- Убрать `ApplyMovementInput` (заменить прямыми `ship.ApplyServerInput`)
- Убрать `npcThrustMult`/`npcYawMult` (NPC использует те же `thrustForce`/`yawForce` что и игрок)
- Использовать тот же `AntiGravity=1.0` по умолчанию (1.5 только во время boost 5 сек после ExitDocked)

### T-NS M3.3 — `NpcShipWorld` — только координация (60 мин, 100 LOC)
- Убрать `TickNpc` switch, заменить на `controller.NavTick()`
- Убрать `ApplyTransitMovement`, `ApplyApproachMovement`, `ApplyDepartingMovement`
- Убрать `ApplyAltitudeControl` PD-controller (больше не нужен)
- Убрать `KillVerticalVelocity` (пусть физика работает естественно)
- Оставить только: `RegisterNpc`, `UnregisterNpc`, `GetNpc`, events

### T-NS M3.4 — `NpcShipState` cleanup (30 мин, 30 LOC)
- Удалить `FlightDirection`, `StartPathPos`, `LastCourseCheckTime`, `StartCruiseY` (всё это ушло в NpcShipController)
- Оставить: `NpcInstanceId`, `Ship`, `Status`, `CurrentRoute`, `StateEnteredAt`, `ScheduleIndex`, `AssignedPadId`

### T-NS M3.5 — Verification (60 мин, 0 LOC)
- 1 NPC, 1 маршрут PRIMIUM → TESTZONE
- Compile clean (через MCP read_console)
- Play Mode: проверить что NPC взлетает, поворачивается, летит к зоне связи, останавливается, получает pad, садится
- Console log transitions: `[NpcShipController] NavMode Docked → Lifting → Yawing → Cruising → Holding → Berthing → Docked`

### T-NS M3.6 — Round trip + 4 NPC (60 мин)
- После того как 1 NPC работает — добавить reverse direction (Round Trip)
- После round trip — расставить 4 NPC

---

## 7. Риски и митигации

| Риск | Вероятность | Митигация |
|------|-------------|-----------|
| NPC "зависает" в Cruising если CommZone не trigger | High (см. F1, F2) | Сначала фиксим сцену (T-NS M3.1) |
| NPC пролетает мимо пада при высокой скорости | Medium | Замедление через `thrust = Clamp01(dist/100) * 0.6` |
| NPC не входит в trigger-bокс пада (промахивается) | Medium | Vertical descent в Berthing когда `horizDist < 10m` |
| `ApplyServerInput` SmoothDamp даёт инерцию yaw | Low | Yaw input = `bearing * 0.02` (медленнее чем у игрока), `yawForce=25` остаётся как у игрока |
| NPC рейс-кастит в другой NPC (collision) | Low (см. F в §2 — "коллизия отключается через layer matrix") | Проверить ProjectSettings/Layer Collision Matrix |
| `NetworkTransform` лаг при резких поворотах | Low | `yawInput * 0.02` ограничивает скорость поворота |

---

## 8. Verification commands (для пользователя)

После T-NS M3.1 + M3.2 + M3.3 + M3.4:

```powershell
# 1. Compile check
mavis mcp call unityMCP refresh_unity '{"mode": "force", "compile": "request", "wait_for_ready": true}'
mavis mcp call unityMCP read_console '{"action": "get", "types": ["error", "warning"], "count": "30", "filter_text": "PeacefulShip"}'

# 2. Play Mode test (manual, в Editor)
# - Open BootstrapScene
# - Host
# - Open WorldScene_0_0
# - Wait 5 sec
# - Watch 4 NPC ships

# 3. Console expected output
mavis mcp call unityMCP read_console '{"action": "get", "types": ["log"], "count": "100", "filter_text": "NpcShipController"}'
# Expected:
# [NpcShipController:NPC_Ship_HeavyII_01] NavMode Docked → Lifting
# [NpcShipController:NPC_Ship_HeavyII_01] NavMode Lifting → Yawing
# [NpcShipController:NPC_Ship_HeavyII_01] NavMode Yawing → Cruising
# [NpcShipController:NPC_Ship_HeavyII_01] NavMode Cruising → Holding (entered comm zone)
# [NpcShipController:NPC_Ship_HeavyII_01] NavMode Holding → Berthing (pad assigned)
# [NpcShipController:NPC_Ship_HeavyII_01] NavMode Berthing → Docked (trigger)
# ... (cycle continues)
```

---

## 9. Что НЕ делаем в M3

- ❌ Не трогаем `ShipController` (работает, и наши предки хорошо его отлаживали)
- ❌ Не меняем `DockingWorld` API (работает)
- ❌ Не добавляем новых FSM состояний (7 хватает, меньше = стабильнее)
- ❌ Не пишем новый PD-controller (физика корабля сама справляется)
- ❌ Не трогаем NGO/RPC/NetworkVariable
- ❌ Не делаем ship-AI через NavMesh (открытое небо, waypoint-graph не нужен)
- ❌ Не пишем сложный FSM с 15+ состояниями
- ❌ Не делаем Cargo/Manifest (v2)

---

## 10. Открытые вопросы (после approval плана)

| # | Вопрос | Что решить |
|---|--------|-----------|
| Q1 | Можно ли оставить `npcThrustMult=0.6f` в `NpcShipController` (т.е. NPC летит медленнее игрока) или использовать 1.0? | Если тестовый маршрут слишком короткий и NPC "срезает" — да, 0.6. Иначе 1.0 |
| Q2 | `targetClearance` (высота набора при Lifting) — 5м? 10м? | Зависит от того, есть ли другие корабли над падом. 5м безопасно для уникального пада |
| Q3 | Хочется ли визуализацию пути NPC (Gizmos)? | Сейчас — нет (M3 фокус на логике), v2 добавим |

---

**Готовность к M3:** ✅ Дизайн-документ написан. Жду ответа "погнали M3" или уточнений.
