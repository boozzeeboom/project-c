# ADR-0002: World Streaming Architecture for Large-Scale MMO

**Статус:** ✅ Принято  
**Дата:** 14 апреля 2026  
**Контекст:** ProjectC_client — Large-scale MMO world streaming

---

## Контекст

Project C — MMO проект с миром радиусом ~350,000 units, содержащим 5 горных массивов, 29 пиков, и 890+ процедурных облачных мешей. Текущая архитектура использует единую сцену без стриминга, что создаёт проблемы:

1. Весь мир всегда загружен — высокий draw call count и потребление памяти
2. Floating Origin сдвигает только "Mountains" hierarchy, игнорируя облака, фермы, network objects
3. Editor Scene View не может навигировать к координатам >100,000 units
4. Невозможно добавлять детальный контент без падения framerate

Требуется система world streaming, которая:
- Поддерживает мультиплеер (Unity NGO)
- Работает с процедурной генерацией (горы + облака из Seed)
- Обеспечивает бесшовные переходы между зонами
- Синхронизирует Floating Origin между всеми клиентами
- Позволяет серверу контролировать что загружено у каждого клиента

---

## Рассмотpенные варианты

### Вариант A: World Streamer 2 (Asset Store, $60)

**Плюсы:**
- Готовое решение с UI-настройкой
- Floating point correction система
- Асинхронная загрузка/выгрузка
- Интеграция с Addressables
- Быстрый старт

**Минусы:**
- ❌ НЕ поддерживает мультиплеер — концепция "камера = точка стриминга" ломается при нескольких клиентах
- ❌ Сервер не управляет стримингом — невозможно контролировать что загружено у каждого клиента
- ❌ Процедурная генерация облаков не совместима — работает с pre-placed объектами
- ❌ Нет синхронизации Floating Origin между клиентами
- ❌ Vendor lock-in — зависимость от стороннего ассета

### Вариант B: Кастомная система стриминга

**Плюсы:**
- ✅ Полный контроль над мультиплеером — сервер решает какие чанки активны
- ✅ Нативная интеграция с процедурной генерацией — облака и горы генерируются runtime из Seed
- ✅ Синхронизация Floating Origin — кастомная система знает когда и как сдвигать мир
- ✅ Бесшовные переходы — полный контроль над preloading, fade-in, boundary handling
- ✅ Нет vendor lock-in — полный контроль над кодом

**Минусы:**
- ❌ Больше начальных затрат времени (2-4 спринта на MVP)
- ❌ Нужно реализовать: chunk manager, preloading, LOD, unload queues, memory budgeting

### Вариант C: Отдельные сцены с seamless переходами

**Плюсы:**
- ✅ Нативная поддержка Unity (SceneManager.LoadSceneAsync)
- ✅ Визуальное разделение в Editor (каждый массив — отдельная сцена)
- ✅ Командная работа (разные дизайнеры в разных сценах)
- ✅ NGO поддерживает scene synchronization

**Минусы:**
- ❌ NGO NetworkObjects привязаны к сцене — при выгрузке уничтожаются
- ❌ Синхронизация загрузки — сервер должен управлять для каждого клиента
- ❌ Границы сцен = точки рассинхронизации
- ❌ Процедурные облака не привязаны к сценам — 890+ мешей распределены по всему миру
- ❌ Циклический мир усложняет seam handling

---

## Решение

**Выбран: Вариант B — Кастомная система стриминга.**

**С частичным использованием Варианта C** для дизайна мира (subscenes для каждого массива в Editor, но runtime streaming кастомный).

---

## Обоснование

### 1. Процедурный мир = процедурный стриминг

Облака и горы генерируются runtime из данных (WorldData, MountainMeshGenerator). Кастомная система позволяет генерировать чанки "на лету" из Seed, без pre-baked сцен.

**Детерминизм:** Seed = hash(ChunkId.GridX, ChunkId.GridZ, globalSeed) гарантирует что server и client генерируют идентичные объекты.

### 2. MMO требует серверного контроля

В авторитарной архитектуре сервер ДОЛЖЕН знать какие чанки загружены у каждого клиента для:
- Корректного spawn/despawn NetworkObjects
- Античита (сервер валидирует что клиент в правильном чанке)
- Синхронизации Floating Origin (сервер — единственный источник сдвигов)
- Оптимизации带宽 (сервер шлёт только необходимые обновления)

Только кастомная система даёт такой контроль.

### 3. Существующий код легко адаптируется

`MountainMeshGenerator`, `MountainMeshBuilder`, `WorldData` — все уже работают в runtime. Их нужно обернуть в chunk-based API, а не переписывать.

### 4. World Streamer 2 не решает задачу

Ассет рассчитан на singleplayer. Мультиплеерный стриминг и процедурная генерация не поддерживаются. Пришлось бы переписывать значительную часть ассета, что делает его использование бессмысленным.

### 5. Отдельные сцены — хороший complementary подход

На этапе дизайна мира можно разбить массивы на subscenes для удобства работы дизайнеров (5 massifs = 5 subscenes). Но runtime стриминг всё равно будет кастомным (subscenes загружаются через SceneManager, но логика управляется `WorldChunkManager`).

---

## Последствия

### Положительные

✅ **Полный контроль** над архитектурой стриминга  
✅ **Нативная поддержка мультиплеера** через server-authoritative chunk management  
✅ **Процедурная генерация** интегрирована в стриминг  
✅ **Нет vendor lock-in** — полный контроль над кодом  
✅ **Масштабируемость** — можно добавлять любой контент без падения framerate  

### Отрицательные / Риски

⚠️ **Больше начальных затрат времени** — 2-4 спринта на MVP  
⚠️ **Нужно реализовать самостоятельно:**
  - Chunk manager с grid-based lookup
  - Client-side chunk loader с async generation
  - Server-side player chunk tracker
  - FloatingOriginMP с server-synced shifts
  - NetworkObject spawn/despawn per chunk
  - Preloading система
  - Memory budgeting

⚠️ **Риск рассинхронизации Floating Origin** — mitigated by server-authoritative shifts  
⚠️ **Риск hitching при генерации чанка** — mitigated by async generation + preloading  
⚠️ **Риск memory leak при unload** — mitigated by strict lifecycle management  

### Технический долг

Нет — это фундаментальное архитектурное решение, которое заложит основу для всей системы стриминга.

---

## Архитектура высокого уровня

```
                        ┌──────────────────────────────────┐
                        │     SERVER (Authoritative)        │
                        │  WorldChunkManager                │
                        │  - Chunk Registry (grid-based)    │
                        │  - PlayerChunkTracker             │
                        │  - Spawn/Despawn Queue            │
                        └───────────────┬───────────────────┘
                                        │ RPC
                      ┌─────────────────┼─────────────────┐
                      ▼                 ▼                 ▼
              ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
              │  Клиент 0    │  │  Клиент 1    │  │  Клиент N    │
              │ ChunkLoader  │  │ ChunkLoader  │  │ ChunkLoader  │
              │ FloatingOrg  │  │ FloatingOrg  │  │ FloatingOrg  │
              └──────────────┘  └──────────────┘  └──────────────┘
```

**Ключевые компоненты:**
- `WorldChunkManager` — серверный реестр чанков, трекинг игроков
- `ChunkLoader` — клиентская загрузка/выгрузка
- `ProceduralChunkGenerator` — детерминированная генерация из Seed
- `FloatingOriginMP` — мультиплеер-синхронизированный сдвиг мира

---

## План реализации

**Фаза 1:** Foundation (2 спринта) — базовая инфраструктура, без мультиплеера  
**Фаза 2:** Multiplayer Integration (2 спринта) — стриминг в Host-режиме с 2+ клиентами  
**Фаза 3:** Polish & Optimization (1 спринт) — производительность, preloading, edge cases  
**Фаза 4:** Cyclic World Support (1 спринт, опционально) — если требуется

**Детальный план:** см. [01_Architecture_Plan.md](./01_Architecture_Plan.md), раздел 5

---

## Источники

- [Unity 6 Scene Management](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.LoadSceneMode.Additive.html)
- [NGO Scene Management](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/basics/scenemanagement/using-networkscenemanager.html)
- [Floating Point Problem - Unity Discussions](https://discussions.unity.com/t/floating-point-problem-on-large-maps/945102)
- [World Streamer 2 Asset](https://assetstore.unity.com/packages/tools/terrain/world-streamer-2-176482)
- [Megacity Metro (Unity Demo)](https://unity.com/demos/megacity-competitive-action-sample)

---

## Связанные документы

- [01_Architecture_Plan.md](./01_Architecture_Plan.md) — Детальный план реализации
- [02_Technical_Research.md](./02_Technical_Research.md) — Техническое исследование
- [prompt_editor_navigation_large_world.md](../prompt_editor_navigation_large_world.md) — Исходный промпт
