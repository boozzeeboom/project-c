# Large-Scale MMO World Streaming Architecture

**Дата:** 14 апреля 2026  
**Проект:** ProjectC_client  
**Unity версия:** Unity 6 (6000.x LTS), URP  
**Ветка:** qwen-gamestudio-agent-dev

---

## 1. Резюме проблемы

### 1.1. Текущее состояние
- **Мир:** радиус ~350,000 units, 5 горных массивов, 29 пиков
- **Архитектура:** единая сцена, весь мир загружен постоянно
- **Floating Origin:** реализован, но сдвигает только "Mountains" hierarchy
- **Мультиплеер:** Unity NGO (Netcode for GameObjects), authoritative server
- **Контент:** процедурные горы + 890+ облачных мешей + фермы + объекты

### 1.2. Основные проблемы
1. **Editor навигация:** Scene View не может перемещаться к координатам >100,000 units (floating-point precision)
2. **Performance:** весь мир всегда загружен — высокий draw call count, память
3. **Floating Origin bug:** облака, фермы, network objects вне "Mountains" не сдвигаются
4. **Масштабируемость:** невозможно добавлять детальный контент — framerate упадёт

### 1.3. Три рассмотренных решения

| # | Решение | Вердикт |
|---|---------|---------|
| A | World Streamer 2 (Asset Store) | ❌ Не подходит для MMO |
| B | Кастомная система стриминга | ✅ **Выбранный подход** |
| C | Отдельные сцены с seamless переходами | ⚠️ Дополнение к B (subscenes для дизайна) |

---

## 2. Сравнительный анализ решений

### 2.1. Вариант A: World Streamer 2 (Asset Store)

**Цена:** $60 | **Рейтинг:** 168 отзывов | **Unity 6 совместимость:** Да (до 6000.3.x)

#### Плюсы:
- Готовое решение с UI-настройкой
- Встроенная floating point correction система
- Асинхронная загрузка/выгрузка террейнов и объектов
- Интеграция с Addressables
- Быстрый старт (настройка вместо разработки)

#### Минусы для MMO:
- **НЕ поддерживает мультиплеер** — концепция "камера = точка стриминга" ломается когда у каждого клиента своя камера
- **Сервер не управляет стримингом** — в авторитарной архитектуре сервер должен знать какие чанки загружены у каждого клиента
- **Процедурная генерация облаков не совместима** — работает с pre-placed объектами, не с runtime-генерацией из Seed
- **Нет синхронизации Floating Origin** — при стриминге чанков worldRoot сдвигается, нужно координировать на всех клиентах
- **Vendor lock-in** — зависимость от стороннего ассета

#### Вывод: НЕ подходит для MMO проекта

---

### 2.2. Вариант C: Отдельные сцены с seamless переходами

#### Плюсы:
- Нативная поддержка Unity (SceneManager.LoadSceneAsync)
- Визуальное разделение в Editor (каждый массив — отдельная сцена)
- Командная работа (разные дизайнеры в разных сценах)
- NGO поддерживает scene synchronization через NetworkSceneManager

#### Минусы для MMO:
- **NGO NetworkObjects привязаны к сцене** — при выгрузке сцены все NetworkObjects уничтожаются
- **Синхронизация загрузки** — сервер должен управлять загрузкой сцен для каждого клиента
- **Границы сцен = точки рассинхронизации** — два клиента могут быть на разных сторонах границы
- **Процедурные облака не привязаны к сценам** — 890+ мешей распределены по всему миру
- **Циклический мир усложняет** — seam handling на границе цикла требует кастомной логики

#### Вывод: Хорошее дополнение к Варианту B для дизайна, но не основной подход

---

### 2.3. Вариант B: Кастомная система стриминга ✅ ВЫБРАНО

#### Плюсы:
- **Полный контроль над мультиплеером** — сервер решает какие чанки активны для каждого клиента
- **Нативная интеграция с процедурной генерацией** — облака и горы генерируются runtime из Seed
- **Синхронизация Floating Origin** — кастомная система знает когда и как сдвигать мир
- **Бесшовные переходы** — полный контроль над preloading, fade-in, boundary handling
- **Нет vendor lock-in** — полный контроль над кодом

#### Минусы:
- Больше начальных затрат времени (2-4 спринта на MVP)
- Нужно реализовать: chunk manager, preloading, LOD, unload queues, memory budgeting

#### Вывод: НАИБОЛЕЕ подходящий вариант для MMO проекта

---

## 3. Рекомендуемая архитектура

### 3.1. Общая схема

```
                        ┌──────────────────────────────────┐
                        │     SERVER (Authoritative)        │
                        │                                   │
                        │  WorldChunkManager                │
                        │  ┌─────────────────────────┐     │
                        │  │ Chunk Registry          │     │
                        │  │ (все чанки мира, grid)  │     │
                        │  └─────────────────────────┘     │
                        │  ┌─────────────────────────┐     │
                        │  │ PlayerChunkTracker      │     │
                        │  │ (кто в каком чанке)     │     │
                        │  └─────────────────────────┘     │
                        │  ┌─────────────────────────┐     │
                        │  │ Spawn/Despawn Queue     │     │
                        │  └─────────────────────────┘     │
                        └───────────────┬───────────────────┘
                                        │ RPC: LoadChunk/UnloadChunk
                      ┌─────────────────┼─────────────────┐
                      ▼                 ▼                 ▼
              ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
              │  Клиент 0    │  │  Клиент 1    │  │  Клиент N    │
              │ (Host игрок) │  │              │  │              │
              │              │  │              │  │              │
              │ ChunkLoader  │  │ ChunkLoader  │  │ ChunkLoader  │
              │ FloatingOrg  │  │ FloatingOrg  │  │ FloatingOrg  │
              └──────────────┘  └──────────────┘  └──────────────┘
```

### 3.2. Ключевые компоненты

#### 3.2.1. WorldChunkManager (Server-Authoritative)

**Путь:** `Assets/_Project/Scripts/World/Streaming/WorldChunkManager.cs`

**Ответственность:**
- Реестр всех чанков мира (grid-based, 2000x2000 units)
- Отслеживание позиции каждого игрока → определение активных чанков
- Рассылка RPC клиентам о загрузке/выгрузке чанков
- Управление preloading (загрузка соседних чанков ДО входа)

**Структура данных:**
```csharp
public struct ChunkId {
    public int GridX;  // координата чанка в сетке
    public int GridZ;
    
    public override bool Equals(object obj) => ...
    public override int GetHashCode() => HashCode.Combine(GridX, GridZ);
}

public enum ChunkState { Unloaded, Loading, Loaded, Unloading }

public class WorldChunk {
    public ChunkId Id;
    public Bounds WorldBounds;          // мировые границы чанка
    public ChunkState State;            // текущее состояние
    public List<PeakData> Peaks;        // пики в этом чанке
    public List<FarmData> Farms;        // фермы в этом чанке
    public int CloudSeed;               // seed для генерации облаков
    public List<ulong> ActiveObjectIds; // NetworkObject IDs в чанке
}
```

**Параметры:**
- Размер чанка: **2000x2000 units** — баланс между granularity и overhead
- Радиус стриминга: **3 чанка** от игрока (6000 units)
- Preload радиус: **4 чанка** (preloading ДО входа)
- При мире ~10000x10000 units → ~25 чанков

---

#### 3.2.2. ChunkLoader (Client-Side)

**Путь:** `Assets/_Project/Scripts/World/Streaming/ChunkLoader.cs`

**Ответственность:**
- Получает RPC от сервера: `LoadChunkRpc(ChunkId)`, `UnloadChunkRpc(ChunkId)`
- Асинхронно загружает чанк: генерирует горы, облака, фермы
- Управляет fade-in/fade-out (для облаков — alpha fade)
- Сообщает серверу о статусе: `ChunkLoadedRpc(ChunkId, bool success)`

**Coroutine загрузка:**
```csharp
IEnumerator LoadChunkCoroutine(WorldChunk chunk) {
    // 1. Генерация гор (Job System off-main-thread)
    yield return GenerateMountainsAsync(chunk);
    
    // 2. Генерация облаков (детерминированно из CloudSeed)
    yield return GenerateCloudsAsync(chunk);
    
    // 3. Инстанцирование ферм
    InstantiateFarms(chunk);
    
    // 4. Report серверу
    ReportChunkLoaded(chunk.Id);
}
```

---

#### 3.2.3. ProceduralChunkGenerator

**Путь:** `Assets/_Project/Scripts/World/Streaming/ProceduralChunkGenerator.cs`

**Ответственность:**
- По `ChunkId` вычисляет WorldBounds
- Из `WorldData` находит пики/фермы в пределах чанка
- Генерирует облака из `CloudSeed` чанка (детерминированно)
- Вызывает `MountainMeshGenerator` для каждого пика в чанке
- Инстанцирует префабы ферм

**Детерминизм генерации:**
```csharp
// Seed = hash(ChunkId.GridX, ChunkId.GridZ, globalSeed)
// Гарантирует что server и client генерируют идентичные облака
public int GenerateChunkSeed(ChunkId chunkId, int globalSeed) {
    unchecked {
        int hash = 17;
        hash = hash * 31 + chunkId.GridX;
        hash = hash * 31 + chunkId.GridZ;
        hash = hash * 31 + globalSeed;
        return hash;
    }
}
```

---

#### 3.2.4. FloatingOriginMP (Multiplayer-Synced)

**Путь:** `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`

**Проблема текущей системы:**
- FloatingOrigin сдвигает мир локально
- В мультиплеере если host сдвинул мир, клиенты не знают → рассинхронизация NetworkTransform позиций

**Решение:**
```csharp
// Server инициирует сдвиг:
// 1. Server проверяет позицию host-камеры
// 2. Если > threshold → сдвигает мир
// 3. Рассылает всем клиентам: WorldShiftRpc(Vector3 offset)
// 4. Все клиенты применяют тот же сдвиг к worldRoot
// 5. FloatingOrigin.ResetOrigin() вызывается синхронно

// На сервере:
[ServerRpc]
void RequestWorldShiftRpc(Vector3 cameraPos, ServerRpcParams rpcParams = default) {
    if (Vector3.Magnitude(cameraPos) > threshold) {
        Vector3 offset = Vector3.Round(cameraPos / 10000f) * 10000f;
        worldRoot.position -= offset;
        // Рассылаем всем клиентам
        WorldShiftRpc(offset, NetworkManager.ServerTime.Tick);
    }
}

// На всех клиентах (включая host):
[ClientRpc]
void WorldShiftRpc(Vector3 offset, long serverTick, ClientRpcParams rpcParams = default) {
    // Применяем сдвиг к worldRoot
    worldRoot.position -= offset;
    // Все NetworkObject корректируют свои позиции
    // FloatingOrigin синхронно сбрасывается
    FloatingOrigin.ResetOrigin();
}
```

**Важно:** сдвиг происходит в `LateUpdate` ДО того как NetworkTransform снимает позицию

---

### 3.3. Интеграция с NGO (Netcode for GameObjects)

#### 3.3.1. Спавн/деспавн объектов в чанках

**Принцип разделения:**
- **Горы и облака** — НЕ NetworkObjects (локальные визуальные объекты, детерминированы)
- **Фермы с NPC, сундуки, предметы** — NetworkObjects (спавнятся сервером, деспавнятся при выгрузке)
- **Корабли** — всегда NetworkObjects (уже реализовано)

```csharp
// При загрузке чанка на сервере:
void OnChunkLoaded(WorldChunk chunk) {
    foreach (var farmData in chunk.Farms) {
        var farmInstance = Instantiate(farmPrefab);
        farmInstance.GetComponent<NetworkObject>().Spawn();
        chunk.ActiveObjectIds.Add(farmInstance.GetComponent<NetworkObject>().NetworkObjectId);
    }
}

// При выгрузке чанка:
void OnChunkUnloading(WorldChunk chunk) {
    foreach (var objId in chunk.ActiveObjectIds) {
        if (NetworkManager.Singleton.SpawnManager.IsSpawned(objId)) {
            var obj = NetworkManager.Singleton.SpawnManager.GetNetworkObject(objId);
            obj.Despawn();
        }
    }
    chunk.ActiveObjectIds.Clear();
}
```

#### 3.3.2. Использование NetworkSceneManager

**Ключевое правило:** Используйте `NetworkManager.Singleton.SceneManager.LoadScene()`, а НЕ `UnityEngine.SceneManagement`.

```csharp
// Правильно (сервер):
var status = NetworkManager.SceneManager.LoadScene("Chunk_5_3", LoadSceneMode.Additive);

// Подписка на события:
NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;

private void OnSceneEvent(SceneEvent sceneEvent) {
    if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted) {
        // Безопасно вызывать RPC и обновлять NetworkVariable
    }
}
```

**Late Joining Clients:** Новые клиенты автоматически получают все ранее загруженные сервером сцены.

---

### 3.4. Интеграция с существующими системами

| Существующая система | Изменение |
|---------------------|-----------|
| **FloatingOrigin.cs** | Заменяется на `FloatingOriginMP.cs` — сдвиг инициируется сервером, рассылается всем клиентам |
| **WorldGenerator.cs** | Заменяется на `WorldChunkManager` + `ProceduralChunkGenerator`. Удаляется |
| **MountainMeshGenerator.cs** | Остаётся без изменений — вызывается из ProceduralChunkGenerator |
| **MountainMeshBuilder.cs** | Остаётся без изменений |
| **MountainMeshBuilderV2.cs** | Остаётся без изменений |
| **CloudSystem (CumulonimbusManager, VeilSystem)** | VeilSystem — глобальный (не чанковый). Cumulonimbus — генерируется per-chunk из CloudSeed |
| **NetworkManagerController.cs** | Добавляется callback `OnPlayerPositionChanged` для трекинга чанков |
| **NetworkPlayer.cs** | Добавляется отчёт позиции серверу (уже есть через NetworkTransform) |
| **ShipController.cs** | Без изменений — NetworkTransform уже реплицирует позицию корабля |

---

## 4. Риски и стратегии смягчения

| Риск | Вероятность | Влияние | Стратегия |
|------|------------|---------|-----------|
| **Рассинхронизация Floating Origin** | Высокая | Критическое | Сервер — единственный источник сдвигов. Клиенты НЕ сдвигают мир самостоятельно. Таймстемп сдвига для корректного ordering. |
| **Hitching при генерации чанка** | Высокая | Среднее | Асинхронная генерация на coroutine. Preloading соседних чанков заранее. Job System для генерации мешей off-main-thread. |
| **Memory leak при unload** | Средняя | Критическое | Строгий lifecycle: Load → Track → Unload → Destroy → GC.Collect (периодически). Профилирование через Perf Profile. |
| **NGO NetworkObject orphan при unload** | Средняя | Среднее | Перед выгрузкой чанка — Despawn() всех NetworkObjects. Проверка через SpawnManager.IsSpawned(). |
| **Bandwidth при рассылке chunk events** | Низкая | Среднее | RPC батчинг: один `UpdateChunksRpc` вместо множества мелких. Delta compression: шлём только изменения. |
| **Determinism drift (cloud generation)** | Низкая | Высокое | Фиксированный RNG (Unity.Mathematics.Random). Все вычисления в float. Периодическая верификация hash от сгенерированных мешей. |

---

## 5. Фазы реализации

### Фаза 1: Foundation (2 спринта, ~4 недели)

**Цель:** Базовая инфраструктура стриминга, без мультиплеера

| ID | Задача | Описание | Критерий приёмки |
|----|--------|----------|-----------------|
| F1.1 | WorldChunkManager | Создать с реестром чанков | Реестр содержит все чанки мира, grid-based lookup работает |
| F1.2 | ProceduralChunkGenerator | Генерация гор + облаков для одного чанка | Генерация по Seed даёт идентичный результат |
| F1.3 | ChunkLoader | Client-side загрузка/выгрузка | Загрузка/выгрузка чанка по команде, fade-in |
| F1.4 | FloatingOrigin интеграция | FloatingOrigin работает при загрузке чанков | Сдвиг мира корректен при teleport между чанками |
| F1.5 | Chunk Visualizer | Editor tool | Отображение сетки чанков в Scene View |

---

### Фаза 2: Multiplayer Integration (2 спринта, ~4 недели)

**Цель:** Стриминг работает в Host-режиме с 2+ клиентами

| ID | Задача | Описание | Критерий приёмки |
|----|--------|----------|-----------------|
| F2.1 | PlayerChunkTracker | Server-side трекинг чанков | Сервер знает в каком чанке каждый игрок |
| F2.2 | RPC LoadChunk/UnloadChunk | Сервер → клиент команды | Клиенты получают команды загрузки/выгрузки |
| F2.3 | NetworkObject Spawn/Despawn | Per-chunk спавн/деспавн | Фермы, сундуки спавнятся/деспавнятся с чанками |
| F2.4 | FloatingOriginMP | Синхронизированный сдвиг | Сдвиг мира синхронен на server + всех clients |
| F2.5 | Тест: 2 клиента | Перемещение между чанками | Оба клиента видят одинаковый мир, без hitching |

---

### Фаза 3: Polish & Optimization (1 спринт, ~2 недели)

**Цель:** Производительность, preloading, edge cases

| ID | Задача | Описание | Критерий приёмки |
|----|--------|----------|-----------------|
| F3.1 | Preloading | Соседние чанки заранее | Чанки загружаются ДО входа игрока, без hitching |
| F3.2 | Job System | Генерация мешей off-main-thread | Генерация чанка < 100ms на main thread |
| F3.3 | Memory budget | Мониторинг памяти | Память чанков не превышает бюджет |
| F3.4 | Edge case: граница чанков | Корректная загрузка обоих чанков | Без "провалов" при нахождении на границе |
| F3.5 | Unload queue с delay | Выгрузка через 30 сек | Избегание reload loops |

---

### Фаза 4: Cyclic World Support (1 спринт, опционально)

**Цель:** Поддержка циклического мира (если требуется)

| ID | Задача | Описание | Критерий приёмки |
|----|--------|----------|-----------------|
| F4.1 | Wrap-around grid | Чанк (-1, 0) = чанк (N-1, 0) | Координаты корректно wrap-around |
| F4.2 | Seam handling | Бесшовный переход на границе цикла | Preloading работает, нет визуальных артефактов |
| F4.3 | Server authority | Валидация позиций | Сервер предотвращает "выход за мир" |

---

## 6. Editor Navigation Solutions

### 6.1. Проблема
В Unity Editor Scene View невозможно переместиться к удалённым объектам (координаты >100,000 units).

### 6.2. Решения

#### Решение A: Editor Tool для телепортации (рекомендуется)

```csharp
// Assets/_Project/Scripts/Editor/SceneViewNavigator.cs
using UnityEditor;
using UnityEngine;

public class SceneViewNavigator : EditorWindow {
    [MenuItem("Tools/Project C/SceneView Navigator")]
    public static void ShowWindow() => GetWindow<SceneViewNavigator>("Scene Navigator");
    
    void OnGUI() {
        if (GUILayout.Button("Go to Everest (0,0)")) GoToPeak(0, 0);
        if (GUILayout.Button("Go to Aconcagua (-208800,-105500)")) GoToPeak(-208800, -105500);
        // ... остальные пики
    }
    
    void GoToPeak(float x, float z) {
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) {
            sv.LookAtDirect(new Vector3(x, 1000, z), Quaternion.identity, 500f);
            sv.Repaint();
        }
    }
}
```

#### Решение B: Relative Coordinates Workflow
- При редактировании пика — временно сдвигать его к origin
- После редактирования — возвращать на место
- Сложнее в реализации, но решает floating point в Editor

#### Решение C: Subscenes для каждого массива
- Разбить 5 массивов на 5 subscenes
- Редактировать каждый массив в отдельной сцене
- Runtime стриминг всё равно кастомный

---

## 7. Технические ограничения и Best Practices

### 7.1. Floating Point Precision

- **Unity использует float (32-bit)** для трансформов, физики и editor
- **Максимальный рекомендуемый размер мира:** ~20-50 км от начала координат
- **За пределами ~10,000 единиц** начинаются артефакты физики и визуальные искажения
- **Решение:** Floating Origin с порогом 100,000 units + округление сдвига

### 7.2. ECS vs NGO

**Критическая архитектурная развилка:**
- **NGO (Netcode for GameObjects)** — работает с обычными сценами, MonoBehaviour, проще. МЕНЕЕ производителен
- **Netcode for Entities** — работает с SubScene/ECS, высокая производительность для 100+ игроков. ТРЕБУЕТ полного перехода на DOTS
- **НЕЛЬЗЯ смешивать** NGO и Netcode for Entities в одном проекте

**Project C использует NGO** → остаёмся на NGO, не переходим на ECS для стриминга

### 7.3. Scene Loading Best Practices

- Использовать `SceneManager.LoadSceneAsync`, НЕ синхронный `LoadScene`
- Для NGO использовать `NetworkManager.Singleton.SceneManager.LoadScene()`
- Новые клиенты автоматически получают все загруженные сервером сцены (Late Joining)
- Сервер должен валидировать загрузку сцен через `VerifySceneBeforeLoading`

### 7.4. Addressables для Dynamic Content

- Использовать Addressables для загрузки/выгрузки сцен во время выполнения
- Создавать bootstrap-сцену (не Addressable), которая загружает первую игровую сцену
- Разделять сцены на логические группы для независимого управления памятью
- НЕ размещать Addressable-ассеты напрямую в не-Addressable сценах (создаёт дубликаты)

---

## 8. Рекомендации по реализации

### 8.1. Что делать СРАЗУ (Приоритет 1)

1. **Создать `WorldChunkManager`** — реестр чанков, grid-based lookup
2. **Создать `FloatingOriginMP`** — мультиплеер-синхронизированный сдвиг
3. **Создать Editor Tool** для навигации в Scene View
4. **Исправить Floating Origin bug** — сдвигать ВСЕ объекты, не только Mountains

### 8.2. Что делать во вторую очередь (Приоритет 2)

5. **Создать `ProceduralChunkGenerator`** — генерация гор + облаков per chunk
6. **Создать `ChunkLoader`** — client-side загрузка/выгрузка
7. **Интегрировать с NGO** — NetworkObject spawn/despawn per chunk

### 8.3. Что делать в последнюю очередь (Приоритет 3)

8. **Preloading система** — загрузка соседних чанков заранее
9. **Job System оптимизация** — генерация мешей off-main-thread
10. **Memory budgeting** — мониторинг и контроль памяти
11. **Cyclic world support** — если потребуется

---

## 9. Источники и ссылки

### Официальная документация Unity 6
- [Scene Management](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.LoadSceneMode.Additive.html)
- [Addressables 2.5 - Scene Loading](https://docs.unity3d.com/Packages/com.unity.addressables@2.5/manual/LoadingSceneAsync.html)
- [ECS SubScene 6.5](https://docs.unity3d.com/Packages/com.unity.entities@6.5/manual/conversion-subscenes.html)
- [NGO Scene Management](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/basics/scenemanagement/using-networkscenemanager.html)
- [Dedicated Server Build](https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server.html)

### Сообщество и обсуждения
- [Floating Point Problem - Unity Discussions](https://discussions.unity.com/t/floating-point-problem-on-large-maps/945102)
- [Code Monkey: Solution for HUGE WORLDS](https://unitycodemonkey.com/text.php?v=r5WtbelFC-E)
- [MMO Architecture: Area-Based Sharding](https://prdeving.wordpress.com/2025/05/12/mmo-architecture-area-based-sharding-shared-state-and-the-art-of-herding-digital-cats/)

### Asset Store
- [World Streamer 2](https://assetstore.unity.com/packages/tools/terrain/world-streamer-2-176482)
- [SECTR Complete](https://assetstore.unity.com/publishers/1468)
- [MapMagic 2](https://assetstore.unity.com/packages/tools/terrain/mapmagic-2-infinite-lands-163616)
- [Gaia Pro 2023](https://assetstore.unity.com/packages/tools/terrain/gaia-pro-2023-terrain-scene-generator-202320)

### Референсные проекты
- [Megacity Metro (Unity Demo)](https://unity.com/demos/megacity-competitive-action-sample) — DOTS + Netcode for Entities, 100+ игроков

---

## 10. Выводы

**Выбранная архитектура: Кастомная система стриминга (Вариант B).**

### Обоснование:

1. **Процедурный мир = процедурный стриминг.** Облака и горы генерируются runtime из данных. Кастомная система позволяет генерировать чанки "на лету" из Seed, без pre-baked сцен.

2. **MMO требует серверного контроля.** Только кастомная система даёт серверу авторитет над тем, что загружено у каждого клиента. Критично для spawn/despawn NetworkObjects, античита, синхронизации Floating Origin.

3. **Существующий код легко адаптируется.** `MountainMeshGenerator`, `MountainMeshBuilder`, `WorldData` — все уже работают в runtime. Их нужно обернуть в chunk-based API, а не переписывать.

4. **World Streamer 2 не решает задачу.** Не поддерживает мультиплеерный стриминг и процедурную генерацию.

5. **Отдельные сцены — хороший complementary подход.** На этапе дизайна мира можно разбить массивы на subscenes для удобства работы дизайнеров. Но runtime стриминг всё равно будет кастомным.

### Следующий шаг:

**Начать Фазу 1** — создать `WorldChunkManager` с реестром чанков и `ProceduralChunkGenerator` для одиночного режима. После валидации — перейти к Фазе 2 (мультиплеер).
