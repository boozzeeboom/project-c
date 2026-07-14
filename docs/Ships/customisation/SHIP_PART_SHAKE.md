# ShipPartShake — Дребезг частей корабля при тяге

> **Дата:** 2026-07-21
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

## Зависимости

- `ShipRootReference` — должен быть на этом же объекте или родителе
- `ShipController` — на корне корабля (через ShipRootReference)
- `ShipInputReader` — на том же объекте что и ShipController

Паттерн разрешения зависимостей идентичен `EngineThrusterVisual`.
