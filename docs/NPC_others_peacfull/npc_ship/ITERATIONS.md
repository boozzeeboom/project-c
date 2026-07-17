# ITERATIONS — Peaceful NPC Ships (runtime fixes)

## Итерация от 2026-07-17

**Задача:** NPC не спавнятся на палубе, игрок проваливается сквозь платформу при включённом NpcShipController.
**Коммит:** `65c3293` — T-NS11: fix detectCollisions=false ломал коллайдер платформы NPC-корабля
**Изменения:**
- `NpcShipController.cs` — убран `detectCollisions=false` в SetMode(Lifting), гарантия `true` в OnNetworkSpawn
- `NpcSpawner.cs` — отладочные логи в TickSpawn/TryFindSpawnPoint
- `NpcSpawner_ship_deck.asset` — новый конфиг спавнера для палубы
- `NpcSpawner_neutral.asset` — новый конфиг
- `Ship_Medium.prefab` — префаб корабля с платформой, NpcShipController, NpcSpawner
- `Npc_Goblin 2.prefab` — префаб NPC для тестов
- `10_COLLIDER_BUG_detectCollisions_false.md` — документ с разбором бага
- `CHANGELOG.md` — запись в логе
