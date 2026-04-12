# Баг: Топливо = 0 не блокирует вращение и лифт

**Сессия:** 5 | **Дата:** 12 апреля 2026 | **Приоритет:** P1
**Статус:** 🐛 Открыт

## Описание
При `currentFuel = 0` двигатель глохнет (thrust = 0) — это работает корректно.
НО: вращение (yaw A/D) и лифт (Q/E) продолжают работать, хотя не должны.
Также топливо перестаёт восстанавливаться (regen не работает при fuel = 0).

## Ожидаемое поведение
При `fuel <= 0`:
- thrust = 0 ✅ (работает)
- yaw = 0 ❌ (не блокируется — баг)
- pitch = 0 ❌ (не блокируется — баг)
- lift = 0 ❌ (не блокируется — баг)
- fuel regen должен работать даже при fuel = 0 ❌ (не работает — баг)

## Текущее поведение
- thrust = 0 ✅
- yaw работает ❌
- pitch работает ❌
- lift работает ❌
- fuel regen остановился при fuel = 0 ❌

## Причина
В `FixedUpdate()` проверка `engineStalled` влияет только на `targetThrust`.
Yaw, pitch, lift вычисляются независимо от топлива.

В `ShipFuelSystem.RegenFuel()` есть проверка `if (IsEmpty) return;` — блокирует регенерацию при fuel = 0.

## Затронутые файлы
- `Assets/_Project/Scripts/Player/ShipController.cs`
- `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs`

## Предлагаемое решение
1. В `ShipFuelSystem.RegenFuel()` убрать проверку `if (IsEmpty) return;`
2. В `FixedUpdate()` при `engineStalled`: yaw=0, pitch=0, lift=0
