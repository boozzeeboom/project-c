# Итерации разработки

## Итерация от 2025-07-17

**Задача:** Исправить баг: визуалы двигателя (EngineThrusterVisual, ShipPartShake) реагируют на WASD после выхода из корабля (F) и перехода в пеший режим.

**Коммит:** `1812fea` — T-ENG02: фикс визуалов двигателя — реакция на WASD после выхода из корабля

**Изменения:**
- `Assets/_Project/Scripts/Ship/Engine/EngineThrusterVisual.cs` — добавлена проверка `!_shipController.enabled` в Update()
- `Assets/_Project/Scripts/Ship/ShipPartShake.cs` — добавлена проверка `!_shipController.enabled` в Update()
- `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` — Disembark() отключает ShipInputReader, ApplyFlying() включает его

## Итерация от 2025-07-17 (v2)

**Задача:** Та же — первая итерация фиксила не тот код-путь. Реальный disembark идёт через NetworkPlayer, не PlayerStateMachine.

**Коммит:** `abfa9ff` — T-ENG02: фикс визуалов двигателя v2 — правильный путь disembark в NetworkPlayer

**Изменения:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — Disembark: отключает ShipInputReader; Board: включает ShipInputReader
- `Assets/_Project/Scripts/Player/ShipInputReader.cs` — OnDisable(): сброс _currentThrust/_currentYaw в ноль
- Защитные проверки `!_shipController.enabled` из v1 в EngineThrusterVisual, ShipPartShake сохранены
- Фикс в PlayerStateMachine из v1 сохранён (для офлайн/тестового режима)
