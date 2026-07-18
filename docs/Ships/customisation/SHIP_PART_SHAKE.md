# ShipPartShake — Дребезг частей корабля при тяге

> **Дата:** 2026-07-21
> **Последнее изменение:** 2026-07-21 (fix 4 — ShipInputReader.Awake gate)
> **Тикет:** T-SHIP-SHAKE
> **Файл:** `Assets/_Project/Scripts/Ship/ShipPartShake.cs`

---

## Назначение

`ShipPartShake` — MonoBehaviour, который вешается на любой визуальный элемент корабля (двигатель, антенна, декоративный блок) и заставляет его вибрировать при активации тяги (W/S).

Эффект чисто визуальный — меняются только `localPosition` и `localRotation`, физика не затрагивается.

---

## Как работает

```
ShipPartShake (на визуале)
  └─ ShipRootReference (GetComponentInParent)
       └─ ShipController.IsEngineRunning  → двигатель включен?
       └─ ShipInputReader.CurrentThrust   → текущая тяга (-1..+1)
```

1. В `Update()` читает `Mathf.Abs(CurrentThrust)` — работает и на W (вперёд), и на S (назад).
2. Если тяга ниже `_thrustThreshold` — не трясёт.
3. Фаза `_phase` накапливается с частотой `_frequency` (Гц), прогоняется через `AnimationCurve` (0..1 → -1..1).
4. Результат кривой умножается на `thrustNorm` и соответствующие амплитуды.
5. Смещение применяется к `localPosition` и `localRotation` относительно сохранённых базовых значений.

Базовые `localPosition`/`localRotation` кешируются в `Start()`. При `OnDisable()` трансформ сбрасывается к базе.

---

## Поля инспектора

| Поле | Тип | По умолчанию | Описание |
|---|---|---|---|
| `_shakeCurve` | AnimationCurve | Синусоида (5 keyframes) | Форма колебаний: X=фаза (0-1), Y=амплитуда (-1..1) |
| `_frequency` | float | 15 | Частота вибрации в герцах |
| `_positionAmplitude` | Vector3 | (0.01, 0.01, 0.02) | Амплитуда позиции по XYZ в локальном пространстве |
| `_rotationAmplitude` | Vector3 | (0.5, 0.3, 0.5) | Амплитуда вращения (градусы) вокруг локальных XYZ |
| `_thrustThreshold` | float (0-1) | 0.05 | Минимальная тяга для активации дрожи |
| `_maxReferenceSpeed` | float | 10 | Скорость (м/с) = 100% thrust для NPC-режима |
| `_rootRef` | ShipRootReference | auto-find | Ссылка на корень корабля |

---

## Примеры настройки

### Двигатель (крупная деталь, низкая частота)

```
_frequency = 8
_positionAmplitude = (0.02, 0.015, 0.03)
_rotationAmplitude = (0.8, 0.5, 0.6)
_thrustThreshold = 0.03
```

### Антенна/мачта (лёгкая деталь, высокая частота)

```
_frequency = 25
_positionAmplitude = (0.005, 0.02, 0.005)
_rotationAmplitude = (1.5, 0.2, 0.3)
_thrustThreshold = 0.1
```

---

## Настройка кривой

Дефолтная кривая — классическая синусоида:

| Фаза | Значение | Касательная |
|---|---|---|
| 0.00 | 0.0 | +1.57 (cos(0)=1) |
| 0.25 | +1.0 | 0 (пик) |
| 0.50 | 0.0 | -1.57 (cos(π)=-1) |
| 0.75 | -1.0 | 0 (дно) |
| 1.00 | 0.0 | +1.57 (cos(2π)=1) |

Для более резкой дрожи — заменить на пилообразную (2 keyframes: 0→-1, 1→+1).
Для мягкой — уменьшить амплитуду кривой до ±0.5.

---

## NPC Fallback (fix 5 — 2026-07-21)

**Проблема:** NPC-корабли управляются через `NpcShipController.NavTick()` — прямые `rb.linearVelocity` / `rb.MoveRotation`, минуя и `ShipInputReader`, и силовой конвейер `ShipController.FixedUpdate`. Визуалы (`ShipPartShake`, `EngineThrusterVisual`) видели `_inputReader.CurrentThrust = 0` (компонент отключён) → анимации не работали.

**Решение:** когда `ShipInputReader` отключён (`!isActiveAndEnabled`), визуалы выводят thrust/yaw из фактического движения `Rigidbody`:

```
_player_ship:  _inputReader.isActiveAndEnabled → клавиатурный ввод (мгновенный)
_npc_ship:    fallback → Rigidbody.velocity / angularVelocity
```

- **Thrust:** `Clamp01(linearVelocity.magnitude / _maxReferenceSpeed)`, по умолчанию `_maxReferenceSpeed = 10 м/с`
- **Yaw:** `Clamp(angularVelocity.y / _maxRefYawRate, -1, 1)`, по умолчанию `_maxRefYawRate = 45°/с`

Оба параметра настраиваются в инспекторе (секция «NPC Fallback»).

## Input Gating (fix 4 — 2026-07-21)

**Проблема:** `ShipInputReader.Update()` читает `Keyboard.current` напрямую, без проверки наличия пилота. Если `ShipInputReader.enabled = true` в префабе — ВСЕ корабли на сцене читают W/S игрока с первого кадра. Сценарий:

1. Игрок в пешем режиме, не садился в корабль
2. W/S зажимаются для ходьбы
3. `ShipInputReader` (на каждом корабле) выставляет `_currentThrust ≠ 0`
4. `ShipPartShake`/`EngineThrusterVisual` проверяют `_shipController.enabled` (true) и `IsEngineRunning` — если двигатель запущен (NPC-корабли через `NpcShipController.SetEngineRunning(true)`) → визуалы трясутся

**Решение:** `ShipInputReader.Awake()` → `enabled = false`. Компонент стартует выключенным **всегда**. `NetworkPlayer.SubmitSwitchModeRpc` (стр. 1128) и `PlayerStateMachine.ApplyFlying` (стр. 148) включают его при посадке пилота.

**Уровни защиты (defence in depth):**

| Уровень | Где | Что |
|---|---|---|
| 1 (root cause) | `ShipInputReader.Awake()` | `enabled = false` — не читает клавиатуру без пилота |
| 2 (disembark) | `NetworkPlayer.SubmitSwitchModeRpc` | `inputReader.enabled = false` при выходе |
| 3 (stale state) | `ShipInputReader.OnDisable()` | Сброс `_currentThrust`/`_currentYaw`/etc в 0 |
| 4 (defence) | `ShipPartShake.Update()` / `EngineThrusterVisual.Update()` | Проверка `!_shipController.enabled` и `!IsEngineRunning` |

## Зависимости

- `ShipRootReference` — должен быть на этом же объекте или родителе
- `ShipController` — на корне корабля (через ShipRootReference)
- `ShipInputReader` — на том же объекте что и ShipController

Паттерн разрешения зависимостей идентичен `EngineThrusterVisual`.
