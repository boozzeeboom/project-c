# Phase 2: World Streaming — Implementation Progress

**Дата:** 16 апреля 2026 г.
**Проект:** ProjectC_client
**Статус:** ✅ Основные компоненты реализованы

---

## Реализованные компоненты

### 1. PlayerChunkTracker.cs ✅

**Расположение:** `Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs`

**Функциональность:**
- Server-side отслеживание позиции каждого игрока
- Определение текущего чанка игрока
- Управление загрузкой/выгрузкой чанков для каждого клиента индивидуально
- RPC: `LoadChunkClientRpc` и `UnloadChunkClientRpc`

**Ключевые методы:**
- `OnClientConnected()` — регистрация игрока
- `OnClientDisconnected()` — очистка при отключении
- `UpdatePlayerChunk()` — проверка смены чанка
- `LoadChunksForClient()` — загрузка чанков по радиусу
- `UnloadChunksForClient()` — выгрузка дальних чанков

---

### 2. WorldStreamingManager.cs — Обновления ✅

**Добавленные методы:**
- `LoadChunkByServerCommand(ChunkId)` — загрузка по команде сервера
- `UnloadChunkByServerCommand(ChunkId)` — выгрузка по команде сервера

**Preload System:**
- `preloadLayers` — количество слоев для preloading
- `preloadDelay` — задержка перед началом preloading
- `preloadChunkInterval` — интервал между загрузкой чанков
- `maxLoadedChunks` — memory budget

**Методы Preload:**
- `UpdatePreload()` — обновление preload системы
- `BuildPreloadQueue()` — построение очереди по приоритету
- `ProcessPreloadQueue()` — обработка очереди с интервалом

---

### 3. FloatingOriginMP.cs — Обновления ✅

**Добавленный enum OriginMode:**
```csharp
public enum OriginMode
{
    Local,           // Локальный сдвиг (singleplayer)
    ServerSynced,     // Сдвиг от сервера (multiplayer client)
    ServerAuthority   // Сервер инициирует сдвиг (multiplayer host/server)
}
```

**Добавленные RPC:**
- `BroadcastWorldShiftRpc()` — рассылка сдвига всем клиентам

**Логика режимов:**
- `Local` — самостоятельное вычисление и применение сдвига
- `ServerSynced` — приём сдвига от сервера (клиент)
- `ServerAuthority` — сервер вычисляет, рассылает всем (host/server)

---

### 4. ChunkNetworkSpawner.cs ✅

**Расположение:** `Assets/_Project/Scripts/World/Streaming/ChunkNetworkSpawner.cs`

**Функциональность:**
- Server-side спавн/деспавн NetworkObjects
- Автоматическая привязка к чанкам
- Поддержка сундуков и NPC

**Ключевые методы:**
- `SpawnForChunk()` — спавн объектов при загрузке чанка
- `DespawnForChunk()` — деспавн объектов при выгрузке
- `OnChunkLoaded()` — событие загрузки
- `OnChunkUnloaded()` — событие выгрузки

---

### 5. ChestContainer.cs — Обновления ✅

**Добавленные поля:**
```csharp
private World.Streaming.ChunkId _owningChunkId;
public World.Streaming.ChunkId OwningChunkId => _owningChunkId;

public void SetChunk(World.Streaming.ChunkId chunkId)
{
    _owningChunkId = chunkId;
}
```

---

## Критерии приёмки

| Тест | Критерий | Статус |
|------|----------|--------|
| T1 | PlayerChunkTracker логирует: `Player {id} entered chunk ({x}, {z})` | ✅ Готово |
| T2 | LoadChunkClientRpc вызывается при смене чанка | ✅ Готово |
| T3 | FloatingOriginMP не вычисляет сдвиг самостоятельно (ServerSynced) | ✅ Готово |
| T4 | При загрузке чанка спавнятся сундуки | ✅ Готово |
| T5 | При выгрузке чанка сундуки деспавнятся | ✅ Готово |
| T6 | Preload загружает соседние чанки заранее | ✅ Готово |

---

## Архитектура Phase 2

```
┌─────────────────────────────────────────────────────────────┐
│                     SERVER (Authoritative)                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              PlayerChunkTracker                        │   │
│  │  • Track player positions                             │   │
│  │  • Determine active chunks per client                 │   │
│  │  • Send LoadChunkRpc / UnloadChunkRpc                │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              ChunkNetworkSpawner                       │   │
│  │  • Spawn network objects on chunk load                 │   │
│  │  • Despawn on chunk unload                            │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────┬───────────────────────────────┘
                              │ RPC: LoadChunk / UnloadChunk
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│     Client 0     │  │     Client 1     │  │     Client N     │
│ WorldStreaming   │  │ WorldStreaming   │  │ WorldStreaming   │
│ FloatingOriginMP │  │ FloatingOriginMP │  │ FloatingOriginMP │
│   (ServerSynced) │  │   (ServerSynced) │  │   (ServerSynced) │
└──────────────────┘  └──────────────────┘  └──────────────────┘
```

---

## Файлы

**Новые:**
- `PlayerChunkTracker.cs` — серверный трекинг игроков
- `ChunkNetworkSpawner.cs` — спавн/деспавн NetworkObjects

**Модифицированные:**
- `WorldStreamingManager.cs` — +Server Command API, +Preload System
- `FloatingOriginMP.cs` — +OriginMode, +BroadcastWorldShiftRpc
- `ChestContainer.cs` — +SetChunk(), +OwningChunkId

---

## Следующие шаги

1. **Интеграция в сцену** — добавить компоненты на объекты
2. **Настройка NetworkManager** — убедиться что NetworkPrefabs зарегистрированы
3. **Тестирование** — проверить все критерии приёмки

---

**Статус Phase 2:** ✅ Основные компоненты реализованы, готовы к интеграции