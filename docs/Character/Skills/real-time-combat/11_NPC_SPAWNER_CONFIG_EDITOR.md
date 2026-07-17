# NpcSpawnerConfig Custom Editor

> Добавлен в итерации 2026-07-20. См. также `docs/Character/Skills/real-time-combat/ITERATIONS.md`.

## Назначение

Кастомный редактор для `NpcSpawnerConfig` (`Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs`). Заменяет плоский default inspector на 10 сворачиваемых foldout-групп, упрощая конфигурацию NPC-спавнеров.

## Foldout-группы

| # | Группа | Поля |
|---|--------|------|
| 1 | **Prefab & Debug** | `npcPrefab`, `showDebugLogs` |
| 2 | **Spawn Rules** | `spawnRadiusMin`, `spawnRadiusMax`, `activationRadius`, `maxAliveCount`, `spawnCheckInterval`, `spawnChance`, `maxSpawnsPerPlayerPerMinute`, `spawnMode`, `totalSpawnLimit` |
| 3 | **Ground & Chunk** | `groundMask`, `groundRaycastDistance`, `minDistanceFromOtherNpc`, `autoPopulateChunks`, `chunkSpawnRadius`, `maxAlivePerChunk` |
| 4 | **Difficulty Scaling** | `difficultyByDistance` (AnimationCurve) |
| 5 | **Behavior** | `behaviorType`, `passiveAggroHpThreshold`, `passiveMaxHitsPerMinute` |
| 6 | **Visual & Skills** | `visualConfig`, `npcSkillSet` |
| 7 | **Social: General** | `socialEnabled`, `personalityConfig`, `defaultIdleActivity`, `patrolPattern`, `patrolWaypoints`, `idleAtWaypointSec`, `wanderRadius` |
| 8 | **Social: Combat** | `canFlee`, `fleeHpThreshold`, `fleeAllySeekRadius`, `alarmRadius`, `allyDeathRadius`, `isGuard`, `threatEvaluationRange`, `coverSeekRadius`, `coverHpThreshold`, `canSurrender`, `surrenderHpThreshold`, `enablePostCombat`, `woundedDuration`, `healHpThreshold` |
| 9 | **Social: Group & Memory** | `assignGroupOnSpawn`, `groupSpawnRadius`, `enableGrudgeMemory`, `grudgeDurationSec`, `enableVengeanceMemory` |
| 10 | **Faction, Role & Loot** | `socialRole`, `faction`, `lootPrefab`, `lootTable` |

## UX-фичи редактора

- **Условная видимость**: `totalSpawnLimit` показывается только для `Finite`/`FiniteCycle`; chunk-поля — только при `autoPopulateChunks = true`; flee/cover/surrender/post-combat поля — только при включённых флагах.
- **Gray-out**: passive-пороги в группе Behavior засерены (read-only) когда `behaviorType != Passive`.
- `patrolWaypoints` — разворачиваемый массив Vector3 через `PropertyField(..., true)`.

## Перенесённые поля (из MonoBehaviour → SO)

Поля, которые раньше были только на `NpcSpawner` (сцена), теперь доступны в `NpcSpawnerConfig` (пресет):

| Поле | Назначение | Было на MB | Теперь в SO |
|------|-----------|-----------|-------------|
| `autoPopulateChunks` | Включить chunk-based спавн | `NpcSpawner._autoPopulateChunks` | `NpcSpawnerConfig.autoPopulateChunks` |
| `chunkSpawnRadius` | Радиус спавна вокруг центра чанка | `NpcSpawner._chunkSpawnRadius` | `NpcSpawnerConfig.chunkSpawnRadius` |
| `maxAlivePerChunk` | Лимит NPC на чанк | `NpcSpawner._maxAlivePerChunk` | `NpcSpawnerConfig.maxAlivePerChunk` |
| `showDebugLogs` | Включить debug-логи | `NpcSpawner._showDebugLogs` | `NpcSpawnerConfig.showDebugLogs` |

**Backward compatibility**: MB-поля остались как fallback-дефолты. `ApplyConfig()` переопределяет их из SO когда `_config != null`. Существующие сцены не сломаны.

## Файлы

| Файл | Роль |
|------|------|
| `Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs` | Добавлены chunk + debug поля |
| `Assets/_Project/Scripts/AI/NpcSpawner.cs` | `ApplyConfig()` читает новые поля из SO |
| `Assets/_Project/Scripts/AI/Editor/NpcSpawnerConfigEditor.cs` | Кастомный редактор (10 foldout-групп) |
