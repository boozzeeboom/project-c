# Баг: Топливо = 0 не блокирует вращение и лифт

**Сессия:** 5 | **Дата:** 12 апреля 2026 | **Приоритет:** P1
**Статус:** ✅ Исправлен (порог увеличен в сессии 5_2)

## Описание
При `currentFuel = 0` двигатель глохнет (thrust = 0) — это работает корректно.
НО: вращение (yaw A/D) и лифт (Q/E) продолжают работать, хотя не должны.
Также топливо перестаёт восстанавливаться (regen не работает при fuel = 0).

## Исправление (сессия 5)
1. В `FixedUpdate()` при `engineStalled`: avgYaw=0, avgPitch=0, avgVertical=0
2. В `ShipFuelSystem.RegenFuel()` убрана проверка `if (IsEmpty) return;`

## Исправление (сессия 5_2)
Порог `controlThreshold` увеличен с **5** до **10** fuel.
При threshold=5 пользователь видел что корабль управляется уже при ~5 fuel,
что давало ощущение "неполной блокировки".
При threshold=10 (≈33 секунды regen для Medium класса) блокировка ощутима.

## Затронутые файлы
- `Assets/_Project/Scripts/Player/ShipController.cs` — engineStalled обнуляет все avg-значения
- `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs` — RegenFuel работает при fuel=0
- `Assets/_Project/Scripts/Player/ShipController.cs` — controlThreshold = 10 (сессия 5_2)
