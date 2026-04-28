# Testing Guide: Phase 2 World Streaming

**Дата:** 16 апреля 2026 г.
**Проект:** ProjectC_client
**Компоненты:** PlayerChunkTracker, ChunkNetworkSpawner, FloatingOriginMP, WorldStreamingManager

---

## Подготовка сцены

### Шаг 1: Добавить компоненты в сцену

1. Откройте сцену с игроком (например, `Assets/_Project/Scenes/`)
2. Создайте пустой GameObject → назовите `WorldStreamingManager`
3. Добавьте компоненты:
   - `WorldChunkManager`
   - `ChunkLoader`
   - `ProceduralChunkGenerator`
   - `PlayerChunkTracker`
   - `ChunkNetworkSpawner`
   - `WorldStreamingManager`

### Шаг 2: Проверить ссылки

В `WorldStreamingManager`:
- `WorldData` — назначьте `WorldData.asset`
- `WorldChunkManager` — должен auto-find
- `ChunkLoader` — должен auto-find
- `ProceduralChunkGenerator` — должен auto-find

В `PlayerChunkTracker`:
- `showDebugLogs` = true

В `ChunkNetworkSpawner`:
- `chestPrefab` — назначьте NetworkObject префаб сундука
- `showDebugLogs` = true

### Шаг 3: Настроить FloatingOriginMP

1. Найдите камеру игрока
2. Добавьте компонент `FloatingOriginMP`
3. Настройте режим:
   - **Singleplayer:** `mode = Local`
   - **Multiplayer Host:** `mode = ServerAuthority`
   - **Multiplayer Client:** `mode = ServerSynced`

4. Убедитесь что в сцене есть `WorldRoot` объект:
   - Создайте пустой `GameObject` → назовите `WorldRoot`
   - FloatingOriginMP должен найти его в `Awake()`

---

## Тест 1: Singleplayer — Загрузка чанков

### Цель
Проверить что чанки загружаются при движении игрока.

### Шаги
1. Запустите игру в Play Mode
2. Откройте Console
3. Перемещайтесь по миру (WASD)
4. Наблюдайте за логами

### Ожидаемые логи
```
[WorldStreamingManager] Initializing streaming system...
[WorldStreamingManager] Loading chunk Chunk(0, 0)
[ChunkLoader] Начало загрузки Chunk(0, 0)
[ChunkLoader] Чанк Chunk(0, 0) полностью загружен
```

### Критерий успеха
- ✅ Чанки загружаются вокруг игрока
- ✅ Логи появляются в Console
- ✅ Debug HUD показывает количество загруженных чанков

---

## Тест 2: Preload System

### Цель
Проверить что соседние чанки загружаются заранее.

### Шаги
1. В `WorldStreamingManager` настройте:
   - `preloadLayers` = 1
   - `preloadDelay` = 1.0
   - `preloadChunkInterval` = 0.3
2. Запустите игру
3. Двигайтесь в одном направлении
4. Наблюдайте за логами

### Ожидаемые логи
```
[WorldStreamingManager] Preload queue built: 12 chunks
[WorldStreamingManager] Preloading chunk Chunk(1, 0)
[WorldStreamingManager] Preloading chunk Chunk(1, 1)
[WorldStreamingManager] Preloading chunk Chunk(2, 0)
```

### Критерий успеха
- ✅ Чанки загружаются постепенно (не все сразу)
- ✅ Нет hitching при движении

---

## Тест 3: Multiplayer — Host

### Цель
Проверить что сервер отслеживает игроков.

### Шаги
1. Откройте Build Settings
2. Добавьте сцену в Build
3. Постройте Dedicated Server или запустите в редакторе как Host
4. Запустите игру

### Ожидаемые логи
```
[PlayerChunkTracker] PlayerChunkTracker initialized on server.
[PlayerChunkTracker] Client 1 connected at chunk Chunk(0, 0)
[PlayerChunkTracker] Loading chunk Chunk(0, 0) for client 1
```

### Критерий успеха
- ✅ PlayerChunkTracker работает на сервере
- ✅ Клиент получает команду загрузки чанка

---

## Тест 4: Multiplayer — 2 игрока в разных чанках

### Цель
Проверить server-authoritative streaming для двух клиентов.

### Шаги
1. Запустите Host (Окно 1)
2. Запустите Client (Окно 2) — подключитесь
3. Host телепортируйтесь в позицию (0, 0, 0) — чанк (0, 0)
4. Client телепортируйтесь в позицию (3000, 0, 0) — чанк (1, 0)

### Ожидаемые логи (Host)
```
[PlayerChunkTracker] Player 0 entered chunk (0, 0)
[PlayerChunkTracker] Player 1 entered chunk (1, 0)
```

### Ожидаемые логи (Client 0)
```
[WorldStreamingManager] Loading chunk Chunk(0, 0) by server command
```

### Ожидаемые логи (Client 1)
```
[WorldStreamingManager] Loading chunk Chunk(1, 0) by server command
```

### Критерий успеха
- ✅ Host видит обоих игроков в разных чанках
- ✅ Каждый клиент получает только свой контент
- ✅ Server-authoritative: клиент НЕ может загрузить чанк сам

---

## Тест 5: FloatingOriginMP — Server Authority

### Цель
Проверить синхронизацию сдвига мира между клиентами.

### Шаги
1. Host: `FloatingOriginMP.mode = ServerAuthority`
2. Client: `FloatingOriginMP.mode = ServerSynced`
3. Host: Телепортируйтесь в позицию (120000, 0, 0)
4. Наблюдайте за логами

### Ожидаемые логи (Host)
```
[FloatingOriginMP] Shifted world by offset=(100000, 0, 0)
[FloatingOriginMP] BroadcastWorldShiftRpc sent to clients
```

### Ожидаемые логи (Client)
```
[FloatingOriginMP] Received world shift from server: offset=(100000, 0, 0)
```

### Критерий успеха
- ✅ Host вычисляет сдвиг и рассылает всем
- ✅ Client принимает сдвиг (не вычисляет сам)
- ✅ Оба клиента в одной позиции мира

---

## Тест 6: ChunkNetworkSpawner — Спавн сундуков

### Цель
Проверить что сундуки спавнятся при загрузке чанка.

### Шаги
1. Назначьте `chestPrefab` в `ChunkNetworkSpawner`
2. Убедитесь что префаб — NetworkObject
3. Запустите игру
4. Наблюдайте за логами

### Ожидаемые логи
```
[ChunkNetworkSpawner] Spawning network objects for chunk Chunk(0, 0)
[ChunkNetworkSpawner] Spawned 2 network objects for chunk Chunk(0, 0)
```

### Критерий успеха
- ✅ Сундуки спавнятся на сервере
- ✅ Видны всем клиентам
- ✅ Сундуки деспавнятся при выгрузке чанка

---

## Тест 7: Memory Budget

### Цель
Проверить ограничение загруженных чанков.

### Шаги
1. В `WorldStreamingManager` настройте:
   - `maxLoadedChunks` = 10
2. Запустите игру
3. Двигайтесь по миру
4. Наблюдайте за логами

### Ожидаемые логи
```
[WorldStreamingManager] Memory budget reached: 10/10
```

### Критерий успеха
- ✅ Количество чанков не превышает лимит
- ✅ Старые чанки выгружаются при достижении лимита

---

## Debug HUD

Включите `showDebugHUD = true` в компонентах для визуального мониторинга:

### WorldStreamingManager HUD
```
┌─────────────────────────────┐
│ World Streaming Manager      │
│ Loaded Chunks: 9            │
│ Center Chunk: [0, 0]         │
│ Preload Queue: 4             │
└─────────────────────────────┘
```

### FloatingOriginMP HUD
```
┌─────────────────────────────┐
│ FloatingOriginMP             │
│ Offset: 100,000             │
│ Shifts: 1                   │
│ Cam: (50000, 100, 0)        │
└─────────────────────────────┘
```

---

## Чеклист перед тестом

- [ ] Unity компилируется без ошибок
- [ ] Все компоненты добавлены в сцену
- [ ] Ссылки настроены (WorldData, Prefabs)
- [ ] Debug logs включены
- [ ] Console открыта

---

## Troubleshooting

### Чанки не загружаются
1. Проверьте что `WorldData` назначена
2. Проверьте что `WorldChunkManager.BuildChunkRegistry()` вызывается
3. Откройте Console — есть ошибки?

### PlayerChunkTracker не работает
1. Убедитесь что игра запущена как Host/Server
2. Проверьте `IsServer` — должен быть `true`
3. Проверьте `NetworkManager.Singleton.IsServer`

### FloatingOriginMP не сдвигает мир
1. Убедитесь что `WorldRoot` существует в сцене
2. Проверьте режим (`mode` настроен правильно?)
3. Камера достаточно далеко от origin (>100,000)?

### ChunkId не сериализуется
1. Убедитесь что `ChunkId` реализует `INetworkSerializable`
2. Проверьте что `[Serializable]` атрибут добавлен

---

**Удачного тестирования!**