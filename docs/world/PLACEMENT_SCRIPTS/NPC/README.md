# NPC World Inspector — единый инспектор NPC по всем WorldScene

## Назначение
EditorWindow для просмотра ВСЕХ NPC-сущностей во всех WorldScene_*_*.unity сценах. Показывает связи: квесты, фракция, корабль, спавнер, market, расписание.

## Как открыть
`Tools → Project C → NPC World Inspector`

## Возможности

### Таб «Scene NPCs»
- Сканирует все 24 WorldScene_* файла (additive load → FindObjectsByType → close)
- Находит 4 типа NPC:
  - **🎯 Quest NPC** — `NpcController` → `NpcDefinition` SO (npcId, faction, questOffers, questTurnIns, dialogTree, services, greeting, attitudeLinks)
  - **⚔ Combat NPC** — `NpcBrain` + `NpcAttacker` + `NpcTarget` + `MarketZone` → `MarketConfig` (behaviorType, aggroRange, socialEnabled, combatData, skillSet)
  - **🔄 Spawner** — `NpcSpawner` → `NpcSpawnerConfig` SO (prefab, spawnMode, faction, socialEnabled, lootTable, patrolWaypoints)
  - **🚢 Ship** — `NpcShipController` → `NpcShipSchedule` SO (routes, cargo, scheduleType, flight class)
- Grouped by scene, раскрывающиеся детали со всеми связями
- Фильтр по типу и тексту (имя, faction, npcId, prefab)
- Ping-кнопки: открыть SO-ассет в инспекторе, найти GameObject в сцене

### Таб «Quest DB Cross-Ref»
- Показывает все NpcDefinition из `QuestDatabase.asset` (106+ NPC)
- Для каждого: ✅ размещён в сцене / ⚠ не размещён
- В какой сцене, summary quests (offers/turnIns), faction, services

## Архитектура
- `NpcWorldInspectorData.cs` — структуры данных (NpcEntryType, NpcEntry, SceneNpcScanResult)
- `NpcWorldInspectorWindow.cs` — EditorWindow с двумя табами и сканером
- Паттерн сканирования: additive scene load + FindObjectsByType + serialize to strings + close scene
  (данные кэшируются как строки/пути, т.к. ссылки на объекты невалидны после закрытия сцены)

## Связи с другими тулами
- **Независим** от `NpcShipScheduleOverviewWindow` (тот только для кораблей, этот — все NPC)
- Использует `QuestDatabase` из `QuestDatabaseAutoDiscover` для кросс-референса
- Читает `NpcSpawnerConfig`, `NpcDefinition`, `NpcShipSchedule` через SerializedObject/AssetDatabase

## История
- **v1.0** (2026-07): Создание. Полный анализ NPC-подсистем, 4 типа NPC, 2 таба, cross-ref с QuestDatabase.
