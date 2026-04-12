# Баг: Топливо = 0 не блокирует вращение и лифт

**Сессия:** 5 | **Дата:** 12 апреля 2026 | **Приоритет:** P1
**Статус:** ✅ Исправлен

## Описание
При `currentFuel = 0` двигатель глохнет (thrust = 0) — это работает корректно.
НО: вращение (yaw A/D) и лифт (Q/E) продолжают работать, хотя не должны.
Также топливо перестаёт восстанавливаться (regen не работает при fuel = 0).

## Исправление
1. В `FixedUpdate()` при `engineStalled`: avgYaw=0, avgPitch=0, avgVertical=0
2. В `ShipFuelSystem.RegenFuel()` убрана проверка `if (IsEmpty) return;`

## Затронутые файлы
- `Assets/_Project/Scripts/Player/ShipController.cs` — engineStalled обнуляет все avg-значения
- `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs` — RegenFuel работает при fuel=0
