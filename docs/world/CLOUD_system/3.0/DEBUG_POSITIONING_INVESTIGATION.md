# Storm System 3.0 — Debug Positioning Investigation

**Date:** 2026-08-04  
**Status:** ✅ Завершено — позиционирование и дебаг-визуализация работают

---

## Problem

Штормовые ячейки не были видны. Корневая причина: `Camera.main` возвращал Bootstrap-камеру на `(240000, 3000, 160000)` вместо `ThirdPersonCamera` у игрока. Ячейки спавнились в 200 км от корабля.

## Root Cause Chain

```
Camera.main → Bootstrap камера (240000, 3000, 160000) — тег MainCamera, всегда enabled
    ↓
SpawnTestCellsAroundCamera() использовала Camera.main.transform.position
    ↓
Ячейки спавнились в 200 км от игрока
    ↓
ThirdPersonCamera рендерит сцену вокруг игрока → ячеек нет
```

Обе камеры имели тег `MainCamera`. `Camera.main.FindGameObjectWithTag("MainCamera")` возвращает первую найденную — Bootstrap-камеру. `ThirdPersonCamera` (префаб) создаётся позже через `NetworkPlayer.SpawnCamera()`.

**Подтверждение из кодовой базы:** `HorizonVeilRenderer.cs` (строка 16) уже использует `FindGameObjectWithTag("Player")` с комментарием: *«Camera.main broken with FloatingOrigin»*.

## Fixes Applied

| Commit | Ticket | Что сделано |
|---|---|---|
| `ea7b07f` | T-CLOUD28 | `Camera.main` → `GameObject.FindGameObjectWithTag("Player")`. Задержка спавна 2→15 сек. |
| `243d8a8` | T-CLOUD29 | Маркеры-столбы (200×4200×200) вместо кубов 200×200×200 |
| `a46513b` | T-CLOUD30 | Все параметры маркеров в инспектор: `MarkerWidth`, `MarkerHeight`, `MarkerSizeVariation`, `MarkerColor`, `WindSpeedMultiplier`, `TestSpawnDelay` |
| `00892c7` | T-CLOUD31 | `CellRadius` до 50000, `MarkerWidth=0` → авто `CellRadius×2` |
| `e139e49` | T-CLOUD32 | Scale маркеров обновляется каждый кадр (live-update из инспектора) |
| `b28e40c` | T-CLOUD32b | Сброс сериализованного `MarkerWidth=200` → `0` в сцене |
| `8a38ea7` | T-CLOUD33 | Убрана авто-привязка `MarkerWidth` к `CellRadius` (40км куб глотал игрока). `MarkerWidth=500` фиксированный. |

## Current Debug Tools

### В инспекторе StormDirector:

| Секция | Параметр | Значение | Назначение |
|---|---|---|---|
| Cells | `CellRadius` | 5000–50000 | Логический радиус ячейки (молнии, влияние) |
| Cells | `CellBottomY` / `CellTopY` | 800 / 5000 | Высота столба |
| Wind | `WindSpeedMultiplier` | 0.1 | Множитель скорости движения по ветру |
| Test | `SpawnTestCells` | true | Спавнить тестовые ячейки |
| Test | `TestCellCount` | 2 | Количество |
| Test | `TestSpawnDistance` | 1500 | Дистанция от игрока |
| Test | `TestSpawnDelay` | 15 | Задержка перед спавном (сек) |
| Debug Visuals | `ShowDebugMarkers` | true | Розовые столбы (Game View) |
| Debug Visuals | `ShowDebugColumns` | true | Цилиндры Debug.DrawLine (Scene View) |
| Debug Visuals | `ShowDebugGizmos` | true | Gizmos (Scene View) |
| Debug Markers | `MarkerWidth` | 500 | Ширина столба XZ (м) |
| Debug Markers | `MarkerHeight` | 0 | Высота (0 = авто CellTopY−CellBottomY) |
| Debug Markers | `MarkerSizeVariation` | 0.1 | ±10% вариативность |
| Debug Markers | `MarkerColor` | Magenta | Цвет |

### Что видно:

- **Game View:** розовые столбы (Unlit), позиция — центр ячейки по XZ, midY по вертикали
- **Scene View:** цилиндры (кольца каждые 500м + 8 вертикальных рёбер), сферы, кресты, подписи с Handles.Label
- **Console:** Awake, spawn position, lightning triggers

## Next: Real Storm Cloud Visuals

Сейчас если выключить `ShowDebugMarkers` — штормов НЕТ ВООБЩЕ. Никакого облачного покрова, затемнения, volumetric эффекта.

**Направление:** визуализация грозовых облаков — тёмные кучево-дождевые массы, volumetric или mesh-based, с внутренними вспышками молний и затенением земли под ячейкой.
