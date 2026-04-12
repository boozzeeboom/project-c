# Баг: Корабль не летит вперёд (P0)

**Сессия:** 5 → 5_2 | **Дата:** 12 апреля 2026 | **Приоритет:** P0
**Статус:** ✅ Исправлен в сессии 5_2

## Описание
При нажатии W топливо тратится, но корабль не двигается вперёд.
Тяга `ApplyThrustForce(_currentThrust)` не работает — `_currentThrust` всегда 0.

## Причина
В `FixedUpdate()` **отсутствовала строка обновления `_currentThrust`** через `Mathf.SmoothDamp`.
Переменная была объявлена и использовалась в `ApplyThrustForce()`, но никогда не вычислялась.

В плане (`SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md`) была секция "2. Smooth thrust ramp-up",
но она не была реализована в сессии 5.

## Исправление
Добавлена секция 2 в `FixedUpdate()` после fuel check:
```csharp
// 2. Smooth thrust ramp-up (0.3s до полной тяги)
float targetThrust = avgThrust * thrustForce * _moduleThrustMult;
_currentThrust = Mathf.SmoothDamp(_currentThrust, targetThrust, ref _thrustVelocitySmooth, thrustSmoothTime);
```

## Затронутые файлы
- `Assets/_Project/Scripts/Player/ShipController.cs` — добавлена секция 2 (Smooth thrust)

## Тест
```
Test: Нажать W → корабль разгоняется плавно за ~0.3s
Expected: thrust применяется, скорость растёт, топливо уходит
```
