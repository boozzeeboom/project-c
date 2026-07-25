# Итерации — Система Ветров (Winds)

## Итерация от 2026-07-15

**Задача:** SplineWindZone — сплайновые ветровые коридоры параллельно с WindZone
**Коммит:** `bbc30a7997f188b9a22030a76e461a74e0f44453` — T-WIND02: SplineWindZone — сплайновые ветровые коридоры параллельно с WindZone
**Изменения:**
- `Assets/_Project/Scripts/Ship/SplineWindZone.cs` — новый компонент
- `docs/world/Winds/SplineWindZone.md` — документация

---

## Итерация от 2026-07-15 (T-WIND02 — фаза 2: перф)

**Задача:** Оптимизация SplineWindZone — один GetNearestPoint на корабль + троттлинг детекции
**Коммит:** `e58deee` — T-WIND02: fix perf — один GetNearestPoint на корабль + троттлинг детекции
**Изменения:**
- `SplineWindZone.cs` — объединённый цикл: один вызов `SplineUtility.GetNearestPoint` на корабль за цикл детекции
- Добавлен `_detectionStep` — детекция каждый N-й FixedUpdate (не каждый кадр)
- Кэш `_shipEntries` используется между циклами для применения силы

---

## Итерация от 2026-07-15 (T-WIND02 — фаза 3: reverseDirection)

**Задача:** reverseDirection toggle — разворот потока на 180° вдоль сплайна
**Коммит:** `65897f6` — T-WIND02: reverseDirection toggle — разворот потока на 180° вдоль сплайна
**Изменения:**
- `SplineWindZone.cs` — поле `reverseDirection`, применяется к направлению после вычисления (AlongSpline или Custom)

---

## Итерация от 2026-07-15 (T-WIND02 — фаза 4: centeringStrength)

**Задача:** Центрирующая сила удержания в трубе
**Коммит:** `7c2ef26` — T-WIND02: centeringStrength — центрирующая сила удержания в трубе
**Изменения:**
- `SplineWindZone.cs` — поле `centeringStrength` (0 = без центрирования, 3 = дефолт, 10 = жёсткая труба)
- Квадратичная кривая: сила ∝ (distance/radius)² — мягко в центре, агрессивно у края
- Сила `toCenter * (strength * windForce)` добавляется аддитивно к основному вектору ветра

---

## Итерация от 2026-07-15 (T-WIND02 — фаза 5: HUD)

**Задача:** HUD — отображение displayName сплайнового коридора
**Коммит:** `94353a2` — T-WIND02: HUD K4 — отображение displayName сплайнового коридора
**Изменения:**
- `SplineWindZone.cs` — статический реестр `ShipZoneNames` (ShipController → displayName)
- `ShipHudController.cs` — строка «Wind Corridor» с именем зоны из `SplineWindZone.GetZoneDisplayName()`

---

## Итерация от 2026-07-15 (T-PERF-opt)

**Задача:** Stagger detection + static ship registry (A+B+C fix)
**Коммит:** `ac589bb` — T-PERF-opt: SplineWindZone — stagger detection + static ship registry (A+B+C fix)
**Изменения:**
- `SplineWindZone.cs`:
  - Статический `HashSet<ShipController> AllShips` — корабли регистрируются при спавне (вместо FindObjectsByType)
  - `_frameCounter` со случайным сдвигом — зоны детектят в разных кадрах (stagger)
  - Статический `List<SplineWindZone> AllZones` — задел под батч-обработку
- `ShipController.cs`:
  - `OnNetworkSpawn`: `SplineWindZone.AllShips.Add(this)`
  - `OnNetworkDespawn`: `SplineWindZone.AllShips.Remove(this)`

---

## Текущий статус (август 2026)

**Все компоненты системы ветров реализованы и оптимизированы:**

| Компонент | Файл | Статус |
|-----------|------|--------|
| WindManager | `Core/WindManager.cs` | ✅ Готов |
| WindZone (триггерный) | `Ship/WindZone.cs` | ✅ Готов |
| SplineWindZone (сплайновый) | `Ship/SplineWindZone.cs` | ✅ Готов |
| WindZoneData (SO) | `Ship/WindZoneData.cs` | ✅ Готов |
| Интеграция с ShipController | `Player/ShipController.cs` | ✅ Готов |
| HUD (имя зоны) | `Ship/UI/ShipHudController.cs` | ✅ Готов |
| Влияние на персонажей | `Player/NetworkPlayer.cs` | ✅ Готов |

**Документация:**
- `GlobalWind_Ships.md` — глобальный ветер → корабли и персонажи
- `SplineWindZone.md` — сплайновые ветровые коридоры
- `ITERATIONS.md` — этот файл

**Что дальше (кандидаты):**
- Ручная расстановка SplineWindZone в игровых сценах
- Создание WindZoneData-ассетов для конкретных зон (джет-стримы, ущелья)
- Визуальные эффекты ветра (частицы листвы/пыли в зонах)
- Интеграция с погодной системой (шторм генерирует временные WindZone)

---

## Итерация от 2026-08 — T-WIND03: Рефакторинг архитектуры SplineWindZone

**Задача:** Убрать FixedUpdate из SplineWindZone — централизованный round-robin процессинг в WindManager.
**Коммит:** `0016d74` — T-WIND03: рефакторинг SplineWindZone

**Проблема:**
 2 зоны × 50 кораблей × GetNearestPoint каждый FixedUpdate = **40ms на кадр** (профайлер `ProjectC_client_2026-07-25_23-26-47.data`). Каждая зона бежала свой FixedUpdate независимо → O(зоны × корабли).

**Решение:**
- `SplineWindZone` → пассивный дескриптор (данные + Gizmos), без FixedUpdate
- `WindManager.FixedUpdate` → центральный дирижёр:
  - **ApplyAllCachedForces** каждый кадр — дёшево (<0.5ms)
  - **Round-robin:** 1 зона за FixedUpdate
  - **Per-zone throttling:** `_splineDetectionStep=5` — детекция раз в 5 вызовов
  - **Снапшот кораблей** только когда зона реально детектится


**Архитектура (было → стало):**
```
Было:  SplineWindZone_1.FixedUpdate → GetNearestPoint × N → 21ms
       SplineWindZone_2.FixedUpdate → GetNearestPoint × N → 16ms
       Σ ≈ 40ms/кадр

Стало: WindManager.FixedUpdate:
         Снапшот кораблей (lock+copy)        <0.1ms
         Zone_A детекция (раз в N кадров)    ~2-3ms
         ApplyAllCached (A+B из кэша)        <0.5ms
         Σ ≈ 3ms/кадр
```

**Изменения:**
- `SplineWindZone.cs` — вырезан FixedUpdate, RefreshShipCache, DetectShipsAndCacheSplineData, ApplyWindToShipsCached. Поля `_detectionStep`/`_shipCacheRefreshInterval` удалены. Добавлены статические `SetZoneDisplayName`/`ClearZoneDisplayName`, публичный `SplineContainer`, `ComputeForceMagnitude`.
- `WindManager.cs` — добавлен `FixedUpdate` с `ProcessSplineWindZones`, round-robin `_nextZoneIndex`, `_zoneStates`, `_shipSnapshot`, `_splineZonesPerFrame`, `_splineDetectionStep`.


**Ожидаемый эффект:** -90% CPU на сплайновый ветер (с 40ms до ~3ms на 2 зоны).

