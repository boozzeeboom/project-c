# Project C — Large Scale MMO: Deep Analysis & Current State

**Дата:** 18 апреля 2026  
**Версия:** `v0.0.18-deep-analysis`

---

## 📊 Executive Summary

**Проблема:** Система chunk streaming состоит из 6+ компонентов, которые реализованы частично и имеют критические проблемы интеграции. FloatingOriginMP конфликтует с ChunkLoader на больших расстояниях. PlayerChunkTracker слабо связан с NetworkPlayer. WorldStreamingManager не получает обратную связь.

**Решение:** Требуется полная переинтеграция системы с чётким разделением ответственности.

---

## 🔴 CRITICAL ISSUES (P0)

### 1. FloatingOriginMP — Конфликт с ChunkLoader

**Файлы:** `FloatingOriginMP.cs` vs `ChunkLoader.cs`

**Проблема:**
```
FloatingOriginMP: threshold = 150,000 units → сдвигает мир
ChunkLoader: загружает чанки вокруг игрока

При столкновении двух систем:
- FloatingOrigin сдвигает WorldRoot
- ChunkLoader продолжает работать в абсолютных координатах
- Чанки загружаются неправильно
```

**Решение:** Определить зоны ответственности:
- **< 150,000 units:** ChunkLoader управляет
- **> 150,000 units:** FloatingOrigin управляет (ChunkLoader выключен)
- **> 1,000,000 units:** FloatingOrigin с большим rounding

---

### 2. FloatingOriginMP — Jitter после телепорта

**Метод:** `GetWorldPosition()` (строки ~500-600)

**Симптом:**
```
positionSource=(-249998, 503, -250000)
totalOffset=(-250000, 0, -250000)
truePos=(2, 503, 0)  ← НЕПРАВИЛЬНО! Должно быть -250000
```

**Причина:** После телепортации `positionSource.position` уже включает сдвиг WorldRoot, но код вычитает `totalOffset` повторно.

**Требуется:**
```csharp
Vector3 GetWorldPosition() {
    if (positionSource == null) return transform.position;
    
    // Если positionSource близко к origin — он уже локальный
    float distToOrigin = positionSource.position.magnitude;
    if (distToOrigin < threshold * 0.5f) {
        return positionSource.position;  // Не вычитать offset!
    }
    
    return positionSource.position - _totalOffset;
}
```

**Статус:** 🔴 НЕ ИСПРАВЛЕНО

---

### 3. WorldStreamingManager — Нет обратной связи от ChunkLoader

**Проблема:** `OnChunkLoaded`/`OnChunkUnloaded` события НЕ подключены обратно к WorldStreamingManager.

**Последствия:**
- WorldStreamingManager не знает когда чанк загружен
- Нет синхронизации состояния
- Preload система не работает

**Решение:** Добавить подписку в WorldStreamingManager.Awake():
```csharp
private void Awake() {
    if (chunkLoader != null) {
        chunkLoader.OnChunkLoaded += OnChunkLoadedHandler;
        chunkLoader.OnChunkUnloaded += OnChunkUnloadedHandler;
    }
}
```

---

### 4. PlayerChunkTracker — Слабая связь с NetworkPlayer

**Проблема:** 
- `PlayerChunkTracker._playerTransforms` заполняется через корутину при подключении клиента
- `NetworkPlayer` НЕ обновляет позицию в PlayerChunkTracker напрямую
- Нет гарантии что отслеживается правильный transform

**Решение:** 
1. Добавить метод `UpdatePlayerPosition(ulong clientId, Vector3 position)` в PlayerChunkTracker
2. Вызывать его из NetworkPlayer при каждой синхронизации позиции

---

## 🟡 MEDIUM ISSUES (P1)

### 5. ChunkNetworkSpawner — Prefabs не назначены

**Файлы:** `ChunkNetworkSpawner.cs`

**Проблема:** `chestPrefab` и `npcPrefab` = null в инспекторе

**Решение:** 
1. Создать prefab для сундука (с NetworkObject)
2. Создать prefab для NPC (с NetworkObject)
3. Назначить в инспекторе

---

### 6. StreamingTest — Компоненты не подключены

**Файлы:** `StreamingTest.cs`

**Проблема:** 
- `positionSource` = null
- `worldStreamingManager` = null

**Решение:** Назначить в инспекторе или добавить auto-find

---

### 7. ChunkId — Отсутствует конструктор

**Файлы:** `WorldChunkManager.cs`, `PlayerChunkTracker.cs`

**Проблема:** ChunkId используется как struct, но конструктор не определён

**Решение:** Добавить в ChunkId:
```csharp
public readonly int GridX, GridZ;

public ChunkId(int gridX, int gridZ) {
    GridX = gridX;
    GridZ = gridZ;
}
```

---

## 📋 Component Integration Map

```
┌─────────────────────────────────────────────────────────────────────┐
│                         CLIENT SIDE                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────┐     ┌─────────────────────────┐                   │
│  │ NetworkPlayer │────▶│ FloatingOriginMP        │                  │
│  │ (transform)   │     │ positionSource          │                  │
│  └──────────────┘     │ OnWorldShifted +=       │                  │
│         │              └─────────────────────────┘                  │
│         ▼                      │                                     │
│  ┌──────────────┐             │                                     │
│  │WorldStreaming │◀────────────┘                                     │
│  │  Manager      │                                                  │
│  │ (координатор) │     ┌─────────────────────────┐                  │
│  └───────┬───────┘     │ ChunkLoader              │                  │
│          │             │ OnChunkLoaded +=        │                  │
│          ▼             │ OnChunkUnloaded +=       │                  │
│  ┌──────────────┐     └─────────────────────────┘                  │
│  │WorldChunk     │                                                  │
│  │  Manager      │                                                  │
│  │ (реестр)      │     ┌─────────────────────────┐                  │
│  └──────────────┘     │ ProceduralChunkGenerator │                  │
│          │             │ (генерация контента)     │                  │
│          ▼             └─────────────────────────┘                  │
│  ┌──────────────┐                                                  │
│  │ FloatingOrigin│     ┌─────────────────────────┐                  │
│  │ MP             │     │ ChunkNetworkSpawner     │                  │
│  │ (системный)    │     │ (спавн объектов)        │                  │
│  └──────────────┘     └─────────────────────────┘                  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                         SERVER SIDE                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────┐     ┌─────────────────────────┐                  │
│  │ NetworkManager│────▶│ PlayerChunkTracker       │                  │
│  │              │     │ LoadChunkClientRpc()      │                  │
│  └──────────────┘     │ UnloadChunkClientRpc()   │                  │
│         │             └─────────────────────────┘                  │
│         ▼                      │                                     │
│  ┌──────────────┐             │                                     │
│  │ NetworkPlayer │             │                                     │
│  │ (Owned)       │             ▼                                     │
│  └──────────────┘     ┌─────────────────────────┐                  │
│          │             │ ChunkNetworkSpawner      │                  │
│          │             │ (Server-side spawn/despawn)│                │
│          ▼             └─────────────────────────┘                  │
│  ┌──────────────┐                                                  │
│  │ WorldStreaming│                                                 │
│  │  Manager      │                                                  │
│  │ (Server mode) │                                                  │
│  └──────────────┘                                                  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## ✅ What IS Implemented

### Core Components (Streaming)

| Компонент | Строк | Статус | Проблемы |
|-----------|-------|--------|----------|
| WorldChunkManager | 323 | ✅ Работает | Нет обратной связи |
| ProceduralChunkGenerator | 392 | ✅ Работает | - |
| ChunkLoader | 412 | ✅ Работает | Нет связи с WorldStreamingManager |
| PlayerChunkTracker | 383 | ✅ Работает | Слабая связь с NetworkPlayer |
| FloatingOriginMP | 1020 | ⚠️ Частично | **Jitter + конфликт с ChunkLoader** |
| WorldStreamingManager | 651 | ✅ Работает | Нет обратной связи от ChunkLoader |

### Network Components

| Компонент | Строк | Статус | Проблемы |
|-----------|-------|--------|----------|
| NetworkPlayer | 600+ | ✅ Работает | Не обновляет PlayerChunkTracker |
| NetworkManagerController | 500+ | ✅ Работает | - |
| NetworkPlayerSpawner | 347 | ✅ Работает | - |
| ChunkNetworkSpawner | 347 | ✅ Работает | Prefabs = null |

### Test Components

| Компонент | Описание |
|-----------|----------|
| StreamingTest | F5/F6/F7/F8/F9/F10 для тестирования |
| StreamingTest_AutoRun | Автоматический тест |

---

## 🗂️ Integration Issues Summary

| # | Проблема | Файлы | Серьёзность | Статус |
|---|----------|-------|-------------|--------|
| 1 | FloatingOriginMP конфликтует с ChunkLoader | FloatingOriginMP.cs, ChunkLoader.cs | 🔴 Critical | ⚠️ Частично исправлено (зоны ответственности) |
| 2 | Jitter после телепорта | FloatingOriginMP.cs:GetWorldPosition() | 🔴 Critical | ✅ Исправлено (I1-001) |
| 3 | Нет обратной связи от ChunkLoader | WorldStreamingManager.cs, ChunkLoader.cs | 🟡 Medium | ✅ Исправлено (I2-001) |
| 4 | PlayerChunkTracker не получает обновления | PlayerChunkTracker.cs, NetworkPlayer.cs | 🟡 Medium | Не исправлено |
| 5 | Prefabs не назначены | ChunkNetworkSpawner.cs | 🟡 Medium | Не исправлено |
| 6 | StreamingTest не подключен | StreamingTest.cs | 🟡 Medium | Не исправлено |

---

## 📋 Required Fixes (Priority Order)

### Immediate (до следующей сессии)

| # | Задача | Файлы | Описание |
|---|--------|-------|----------|
| 1.1 | Исправить GetWorldPosition() | FloatingOriginMP.cs | Не вычитать offset если positionSource локальный |
| 1.2 | Добавить подписку на ChunkLoader events | WorldStreamingManager.cs | Подключить OnChunkLoaded/Unloaded |
| 1.3 | Определить зоны ответственности | FloatingOriginMP.cs | ChunkLoader vs FloatingOrigin |

### Short-term (1-2 сессии)

| # | Задача | Файлы | Описание |
|---|--------|-------|----------|
| 2.1 | Подключить NetworkPlayer → PlayerChunkTracker | NetworkPlayer.cs, PlayerChunkTracker.cs | Обновлять позицию |
| 2.2 | Назначить prefabs | ChunkNetworkSpawner.cs | chestPrefab, npcPrefab |
| 2.3 | Подключить StreamingTest | StreamingTest.cs | positionSource, worldStreamingManager |

### Medium-term (2-3 сессии)

| # | Задача | Описание |
|---|--------|----------|
| 3.1 | Preload система | Загрузка чанков на 1-2 слоя ahead |
| 3.2 | Fade-in для clouds | Плавное появление облаков |
| 3.3 | Тест Host + Client | Синхронизация стриминга |

---

## 📊 Files to Modify

### 1. FloatingOriginMP.cs (1020 строк)

**Изменения:**
- Метод `GetWorldPosition()` — исправить двойной расчёт
- Метод `ShouldUseFloatingOrigin()` — определить threshold для ChunkLoader
- Добавить `OnChunkStreamingStarted`/`OnChunkStreamingEnded` события

### 2. WorldStreamingManager.cs (651 строка)

**Изменения:**
- Метод `Awake()` — подписаться на ChunkLoader events
- Метод `OnChunkLoaded()` — логирование, проверка
- Метод `OnChunkUnloaded()` — логирование, проверка

### 3. NetworkPlayer.cs (600+ строк)

**Изменения:**
- Метод `Update()` — обновлять PlayerChunkTracker с позицией
- Метод `OnNetworkSpawn()` — инициализировать связь

### 4. PlayerChunkTracker.cs (383 строки)

**Изменения:**
- Метод `UpdatePlayerPosition()` — публичный метод для обновления
- Метод `Update()` — вызывать UpdatePlayerPosition() для всех

---

**Автор:** Claude Code + Subagents  
**Обновлено:** 18.04.2026  
**Анализ:** 3 subagents провели глубокий анализ кодовой базы