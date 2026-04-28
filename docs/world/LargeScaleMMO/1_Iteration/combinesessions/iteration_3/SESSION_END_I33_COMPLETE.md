# Iteration 3 — Session End: Complete Summary

**Дата:** 18.04.2026, 17:50  
**Статус:** ✅ OSCILLATION ИСПРАВЛЕНО  
**Следующий шаг:** Итерация 3.4 — Race Conditions в Server→Client Chunk Sync

---

## ✅ Итерация 3 — Что Было Сделано

### I3.1: FindObjectsByType Ghost Object Fix
**Проблема:** `FindObjectsByType` выбирал ghost объект вместо реального NetworkPlayer.

**Решение:** Используем `OwnerClientId` для точного поиска игрока.

### I3.2: FloatingOriginMP.GetWorldPosition()
**Проблема:** `transform.position` oscills между чанками на больших координатах.

**Решение:** Используем `FloatingOriginMP.GetWorldPosition()` который возвращает стабильные координаты.

### I3.3: Race Condition Fix (Дублирующий Update)
**Проблема:** ДВА источника обновления чанков:
- `PlayerChunkTracker.Update()` → корутина
- `NetworkPlayer.FixedUpdate()` → `UpdatePlayerChunkTracker()`

**Решение:** Удалён дублирующий `Update()` из PlayerChunkTracker. Теперь только NetworkPlayer вызывает `ForceUpdatePlayerChunk()`.

---

## 🔴 ОСТАВШИЕСЯ ПРОБЛЕМЫ (Требуют Итерации 3.4)

### Проблема 1: "Чанк X в процессе fade-out, отмена выгрузки"

**Симптомы в консоли:**
```
[Client] Loading chunk Chunk(5, 19) by server command
[ChunkLoader] Чанк Chunk(5, 19) в процессе fade-out, отмена выгрузки.
```

**Описание:**
Когда игрок перемещается, сервер отправляет Load команды для новых чанков. Но команда Unload для старого чанка приходит ПОСЛЕ того как Load нового чанка уже запустил fade-out.

**Последовательность событий:**
```
1. Server: UnloadChunkClientRpc(Chunk(5, 19)) отправлен
2. Server: LoadChunkClientRpc(Chunk(4, 18)) отправлен  
3. Client: UnloadChunkClientRpc(Chunk(5, 19)) выполняется — начинается fade-out
4. Client: LoadChunkClientRpc(Chunk(4, 18)) выполняется — отменяет fade-out!
5. ERROR: Chunk(5, 19) в процессе fade-out, но его пытаются загрузить снова
```

**Root Cause:**
- Server отправляет Load И Unload команды **в одном кадре**
- Client выполняет их **в другом порядке** (RPC reorder)
- `_clientLoadedChunks[clientId]` на сервере рассинхронизирован с реальным состоянием клиента

---

### Проблема 2: "Чанк X не загружен, невозможно выгрузить"

**Симптомы в консоли:**
```
[Client] Unloading chunk Chunk(2, -1) by server command
[ChunkLoader] Чанк Chunk(2, -1) не загружен, невозможно выгрузить.
```

**Описание:**
Сервер отправляет Unload команду для чанка, который НИКОГДА не был загружен клиентом.

**Последовательность событий:**
```
1. Player at Chunk(4, 18) → 9x9 grid loaded: Chunk(0..8, 14..22)
2. Player moves to Chunk(5, 19) → need 9x9 grid: Chunk(1..9, 15..23)
3. Server: UnloadChunkClientRpc(Chunk(2, -1)) отправлен
4. ERROR: Chunk(2, -1) НЕ был в предыдущем 9x9 радиусе!
```

**Root Cause:**
- `GetChunksInRadius()` возвращает **ВСЕ** чанки в радиусе, а не **DELTA** от текущего состояния
- При движении некоторые чанки на "углу" старого радиуса никогда не загружались
- Но Unload для них всё равно отправляется

---

## 📊 Анализ Root Cause

### Проблема: Server State ≠ Client State

```
┌─────────────────────┐        RPCs        ┌─────────────────────┐
│   SERVER STATE      │ ─────────────────→│   CLIENT STATE      │
│                     │                    │                     │
│ _clientLoadedChunks │   Load/Unload      │ _loadedChunks       │
│ [HashSet<ChunkId>]  │ ─────────────────→│ [HashSet<ChunkId>]  │
│                     │                    │                     │
│ ⚠️ МОЖЕТ ОТЛИЧАТЬСЯ │   (reorder, loss) │ ⚠️ МОЖЕТ ОТЛИЧАТЬСЯ  │
└─────────────────────┘                    └─────────────────────┘
         │                                            │
         │  GetChunksInRadius() возвращает            │
         │  НЕ delta, а ПОЛНЫЙ список                 │
         │                                            │
         ▼                                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  Player moves from Chunk(4,18) to Chunk(5,19):                  │
│                                                                 │
│  OLD radius (4,18): Chunk(0..8, 14..22) = 81 chunks            │
│  NEW radius (5,19): Chunk(1..9, 15..23) = 81 chunks            │
│                                                                 │
│  ACTUAL DELTA: 20 chunks (edges changed)                        │
│  Server sends: 162 Load/Unload commands                        │
│                                                                 │
│  Problem: Server's _clientLoadedChunks becomes corrupted       │
│  because it's not properly tracking what was actually loaded   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📋 Документация Ошибок

### Error Log Analysis

```
[PlayerChunkTracker] Player 0 moved from Chunk(0, 0) to Chunk(4, 18)
[Client] Loading chunk Chunk(5, 19) by server command
[Client] Loading chunk Chunk(6, 19) by server command
[Client] Loading chunk Chunk(7, 19) by server command
...
[Client] Unloading chunk Chunk(-1, 17) by server command
[Client] Unloading chunk Chunk(-1, 18) by server command
...
[ChunkLoader] Чанк Chunk(-1, 18) не загружен, невозможно выгрузить.
[ChunkLoader] Чанк Chunk(-1, 19) не загружен, невозможно выгруз

```

**Issues:**
1. Server отправляет Load/Unload для **ВСЕХ** 81 чанков, а не только изменившихся (20)
2. Некоторые Unload команды для чанков которые НИКОГДА не были загружены
3. Client получает команды в непредсказуемом порядке

---

## 🛠️ Возможные Решения

### Solution 1: Server-side Delta Tracking (Рекомендуется)

**Принцип:** Сервер должен отслеживать **что клиент ДЕЙСТВИТЕЛЬНО загрузил**.

```csharp
// В PlayerChunkTracker: вместо GetChunksInRadius()
// использовать разницу между текущим и новым состоянием

private void LoadChunksForClient(ulong clientId, Vector3 position)
{
    List<ChunkId> expectedChunks = _chunkManager.GetChunksInRadius(position, loadRadius);
    HashSet<ChunkId> expectedSet = new HashSet<ChunkId>(expectedChunks);
    
    foreach (var chunkId in expectedSet)
    {
        // Загружаем ТОЛЬКО если ещё не загружен
        if (!_clientLoadedChunks[clientId].Contains(chunkId))
        {
            _clientLoadedChunks[clientId].Add(chunkId);
            LoadChunkClientRpc(clientId, chunkId);
        }
    }
}

private void UnloadChunksForClient(ulong clientId, Vector3 position)
{
    List<ChunkId> expectedChunks = _chunkManager.GetChunksInRadius(position, unloadRadius);
    HashSet<ChunkId> expectedSet = new HashSet<ChunkId>(expectedChunks);
    
    var chunksToUnload = new List<ChunkId>();
    
    foreach (var loadedChunk in _clientLoadedChunks[clientId])
    {
        // Выгружаем ТОЛЬКО если вне радиуса И уже загружен
        if (!expectedSet.Contains(loadedChunk))
        {
            chunksToUnload.Add(loadedChunk);
        }
    }
    
    foreach (var chunkId in chunksToUnload)
    {
        _clientLoadedChunks[clientId].Remove(chunkId);
        UnloadChunkClientRpc(clientId, chunkId);
    }
}
```

**Проблема:** Код ВЫГЛЯДИТ правильно, но `_clientLoadedChunks` рассинхронизирован.

---

### Solution 2: Client Acknowledgment Protocol

**Принцип:** Клиент подтверждает загрузку/выгрузку, сервер обновляет состояние.

```
1. Server: SendLoadChunkRpc(chunkId) → Client
2. Client: Load chunk, send LoadConfirmedRpc(chunkId) → Server
3. Server: Add to _clientLoadedChunks[clientId]
```

**Минус:** Увеличивает сетевой трафик и добавляет задержку.

---

### Solution 3: Client-side Validation

**Принцип:** Клиент игнорирует Unload для незагруженных чанков.

```csharp
// В WorldStreamingManager.UnloadChunkByServerCommand():
public void UnloadChunkByServerCommand(ChunkId chunkId)
{
    if (!_loadedChunks.Contains(chunkId))
    {
        Debug.Log($"[WorldStreamingManager] Chunk {chunkId} not loaded, skipping.");
        return; // БЕЗ ОШИБКИ - просто игнорируем
    }
    // ... нормальная выгрузка
}
```

**Минус:** Маскирует проблему синхронизации.

---

## 📁 Файлы для Анализа (Итерация 3.4)

### Обязательно прочитать:

1. **`Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs`**
   - Строки 180-230: `LoadChunksForClient()` — как отслеживает загруженные чанки
   - Строки 230-270: `UnloadChunksForClient()` — как отслеживает выгруженные чанки
   - Строки 290-320: `_clientLoadedChunks` — словарь состояния

2. **`Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs`**
   - Строки 160-200: `LoadChunkByServerCommand()` — что делает при получении команды
   - Строки 200-240: `UnloadChunkByServerCommand()` — что делает при получении команды

3. **`Assets/_Project/Scripts/World/Streaming/ChunkLoader.cs`**
   - Строки 65-90: `LoadChunk()` — проверки перед загрузкой
   - Строки 90-120: `UnloadChunk()` — проверки перед выгрузкой

---

## 🎯 Задачи для Subagents (Итерация 3.4)

### Subagent 1: Network Programmer

**Задача:** Проанализировать синхронизацию Server ↔ Client state

```
Прочитай PlayerChunkTracker.cs и ответь:
1. Как _clientLoadedChunks[clientId] обновляется при Load?
2. Как _clientLoadedChunks[clientId] обновляется при Unload?
3. Почему _clientLoadedChunks может рассинхронизироваться с реальным состоянием клиента?
4. Предложи решение для синхронизации состояния
```

### Subagent 2: Gameplay Programmer

**Задача:** Проанализировать команды от сервера

```
Прочитай WorldStreamingManager.cs и ответь:
1. Что происходит при получении LoadChunkByServerCommand()?
2. Что происходит при получении UnloadChunkByServerCommand()?
3. Почему клиент получает Unload для незагруженных чанков?
4. Предложи validation логику для предотвращения ошибок
```

### Subagent 3: Unity Specialist

**Задача:** Предложить архитектурное решение

```
Проанализируй архитектуру:
1. Почему Server отправляет Unload для чанков которые не загружал?
2. Должен ли сервер знать о состоянии fade-out на клиенте?
3. Нужна ли очередь команд на клиенте?
4. Предложи архитектурное решение для надежной синхронизации
```

---

## ✅ Метрики Успеха (Итерация 3.4)

После исправления:

1. **Ноль ошибок "Чанк X не загружен, невозможно выгрузить"**
2. **Ноль ошибок "Чанк X в процессе fade-out, отмена выгрузки"**
3. **Server отправляет ТОЛЬКО необходимые Load/Unload команды**
4. **Console чистая от спама**

---

## 📋 Файлы Документации

| Файл | Описание |
|------|----------|
| `SESSION_END_I33.md` | Iteration 3.3 — Oscillation fix |
| `SESSION_END_I33_COMPLETE.md` | Этот файл — полная документация Iteration 3 |
| `NEXT_SESSION_PROMPT_I34.md` | Prompt для следующей сессии |

---

**Автор:** Claude Code  
**Завершение:** 18.04.2026, 17:50  
**Следующий шаг:** Итерация 3.4 — Race Conditions
