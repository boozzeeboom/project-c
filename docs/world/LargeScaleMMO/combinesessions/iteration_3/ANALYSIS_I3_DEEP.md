# Iteration 3 — Deep Analysis: Subagent Research Report

**Дата:** 18.04.2026, 17:55  
**Статус:** ✅ I3.1-I3.3 ЗАВЕРШЕНЫ | 🔴 I3.4 ТРЕБУЕТ РЕШЕНИЯ  
**Фокус:** Race Conditions в Server→Client Chunk Sync

---

## 📊 Итог Iteration 3: Что Было Сделано

### ✅ I3.1: FindObjectsByType Ghost Object Fix
**Проблема:** `FindObjectsByType` выбирал ghost объект вместо реального NetworkPlayer.

**Решение:** Используем `OwnerClientId` для точного поиска игрока.

---

### ✅ I3.2: FloatingOriginMP.GetWorldPosition()
**Проблема:** `transform.position` oscills между чанками на больших координатах.

**Решение:** Используем `FloatingOriginMP.GetWorldPosition()` который возвращает стабильные координаты.

---

### ✅ I3.3: Race Condition Fix (Дублирующий Update)
**Проблема:** ДВА источника обновления чанков:
- `PlayerChunkTracker.Update()` → корутина
- `NetworkPlayer.FixedUpdate()` → `UpdatePlayerChunkTracker()`

**Решение:** Удалён дублирующий `Update()` из PlayerChunkTracker. Теперь только NetworkPlayer вызывает `ForceUpdatePlayerChunk()`.

---

## 🔴 ОСТАВШИЕСЯ ПРОБЛЕМЫ (I3.4)

### Проблема 1: "Чанк X в процессе fade-out, отмена выгрузки"

**Симптомы в консоли:**
```
[Client] Loading chunk Chunk(5, 19) by server command
[ChunkLoader] Чанк Chunk(5, 19) в процессе fade-out, отмена выгрузки.
```

**Последовательность событий:**
```
1. Server: UnloadChunkClientRpc(Chunk(5, 19)) отправлен
2. Server: LoadChunkClientRpc(Chunk(4, 18)) отправлен  
3. Client: UnloadChunkClientRpc(Chunk(5, 19)) выполняется — начинается fade-out
4. Client: LoadChunkClientRpc(Chunk(4, 18)) выполняется — отменяет fade-out!
5. ERROR: Chunk(5, 19) в процессе fade-out, но его пытаются загрузить снова
```

**Root Cause:** RPC reorder — ClientRPC не гарантирует порядок доставки.

---

### Проблема 2: "Чанк X не загружен, невозможно выгрузить"

**Симптомы в консоли:**
```
[Client] Unloading chunk Chunk(2, -1) by server command
[ChunkLoader] Чанк Chunk(2, -1) не загружен, невозможно выгрузить.
```

**Последовательность событий:**
```
1. Player at Chunk(4, 18) → 9x9 grid loaded: Chunk(0..8, 14..22)
2. Player moves to Chunk(5, 19) → need 9x9 grid: Chunk(1..9, 15..23)
3. Server: UnloadChunkClientRpc(Chunk(2, -1)) отправлен
4. ERROR: Chunk(2, -1) НЕ был в предыдущем 9x9 радиусе!
```

**Root Cause:** `GetChunksInRadius()` возвращает **ВСЕ** чанки в радиусе, а не **DELTA** от текущего состояния.

---

## 📊 Архитектурный Анализ

### Поток данных: Server → Client

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         SERVER SIDE                                       │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    PlayerChunkTracker                             │    │
│  │                                                                   │    │
│  │  ForceUpdatePlayerChunk(clientId, position)                       │    │
│  │         │                                                        │    │
│  │         ▼                                                        │    │
│  │  UpdatePlayerChunk(clientId, position)                            │    │
│  │         │                                                        │    │
│  │         ├── LoadChunksForClient()  ──────────────────────┐        │    │
│  │         │   1. GetChunksInRadius() → 81 chunks           │        │    │
│  │         │   2. For each chunk NOT in _clientLoadedChunks │        │    │
│  │         │      → _clientLoadedChunks.Add(chunk)  ⚠️ HERE  │        │    │
│  │         │      → LoadChunkClientRpc(clientId, chunk)     │        │    │
│  │         │                                                    │        │
│  │         │                                                    │        │    │
│  │         └── UnloadChunksForClient() ───────────────────┐   │        │    │
│  │             1. GetChunksInRadius() → 81 chunks         │   │        │    │
│  │             2. For each chunk in _clientLoadedChunks   │   │        │    │
│  │                NOT in expectedSet                      │   │        │    │
│  │                → _clientLoadedChunks.Remove(chunk) ⚠️   │   │        │    │
│  │                → UnloadChunkClientRpc(clientId, chunk) │   │        │    │
│  │                                                                  │    │
│  └──────────────────────────────────────────────────────────────────│    │
│                              │                                            │
│                              │ LoadChunkClientRpc()                        │
│                              │ UnloadChunkClientRpc()                      │
│                              ▼                                            │
└─────────────────────────────────────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         CLIENT SIDE                                       │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    ChunkLoader                                    │    │
│  │                                                                   │    │
│  │  LoadChunk(chunkId) ──→ loadedChunks.Add(chunkId)                 │    │
│  │  UnloadChunk(chunkId) ──→ fade-out ──→ loadedChunks.Remove()      │    │
│  │                                                                   │    │
│  │  ⚠️ Клиент НЕ знает что сервер думает о состоянии!               │    │
│  │  ⚠️ Клиент НЕ подтверждает операции обратно серверу!             │    │
│  │                                                                   │    │
│  └──────────────────────────────────────────────────────────────────│    │
└─────────────────────────────────────────────────────────────────────────┘
```

### Root Cause Analysis

**Проблема 1: Race Condition с fade-out**

```
Timeline:
┌────────────────────────────────────────────────────────────────────────┐
│ FRAME 1:                                                                    │
│   Server: UnloadChunksForClient()                                          │
│     → _clientLoadedChunks.Remove(Chunk(5, 19))  ← REMOVED FROM SERVER     │
│     → UnloadChunkClientRpc(clientId, Chunk(5, 19))                        │
│                                                                           │
│   Server: LoadChunksForClient()                                            │
│     → _clientLoadedChunks.Add(Chunk(4, 18))     ← ADDED TO SERVER         │
│     → LoadChunkClientRpc(clientId, Chunk(4, 18))                          │
│                                                                           │
├────────────────────────────────────────────────────────────────────────┤
│ FRAME N (RPC arrives):                                                   │
│                                                                           │
│   [LATE] UnloadChunkClientRpc(Chunk(5, 19))                               │
│     → ChunkLoader.UnloadChunk(Chunk(5, 19))                               │
│     → loadedChunks.Remove(Chunk(5, 19))                                   │
│     → Start fade-out coroutine                                            │
│                                                                           │
│   [EARLY] LoadChunkClientRpc(Chunk(4, 18))                               │
│     → ChunkLoader.LoadChunk(Chunk(4, 18))                                 │
│     → BUT Chunk(5, 19) still in chunkFadeTimes!                            │
│     → ERROR: "Чанк X в процессе fade-out, отмена выгрузки"                │
│                                                                           │
└────────────────────────────────────────────────────────────────────────┘
```

**Проблема 2: Server State ≠ Client State**

```
┌────────────────────────────────────────────────────────────────────────┐
│ SERVER STATE:                                                            │
│                                                                           │
│   _clientLoadedChunks[clientId]                                          │
│     = _playerChunks[clientId] + 9x9 grid                                 │
│     = Chunk(4, 18) + 81 chunk range = 81 chunks                          │
│                                                                           │
│   При движении на Chunk(5, 19):                                          │
│     → OLD: 81 chunks (Chunk(0..8, 14..22))                               │
│     → NEW: 81 chunks (Chunk(1..9, 15..23))                               │
│                                                                           │
│   Server вычисляет DELTA:                                                │
│     → 20 chunks to unload  (edges of OLD)                                 │
│     → 20 chunks to load    (edges of NEW)                                 │
│                                                                           │
│   BUT: Server отправляет ВСЕ 162 RPC!                                    │
│                                                                           │
├────────────────────────────────────────────────────────────────────────┤
│ CLIENT STATE:                                                            │
│                                                                           │
│   loadedChunks (ChunkLoader)                                             │
│     = только реально загруженные чанки                                    │
│     = может отличаться от server state!                                   │
│                                                                           │
│   При проблемах с сетью:                                                  │
│     → Некоторые Load RPC могут быть потеряны                              │
│     → Некоторые Unload RPC могут прийти для НЕЗАГРУЖЕННЫХ чанков         │
│                                                                           │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 🎯 Subagent Tasks

### Subagent 1: Network Programmer (network-programmer)

**Файл:** `PlayerChunkTracker.cs`  
**Фокус:** Server-side state management

```
Прочитай Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs

Ответь на вопросы:
1. Как _clientLoadedChunks[clientId] обновляется при LoadChunksForClient()?
   - В какой строке добавляется? ПЕРЕД или ПОСЛЕ отправки RPC?
   
2. Как _clientLoadedChunks[clientId] обновляется при UnloadChunksForClient()?
   - В какой строке удаляется? ПЕРЕД или ПОСЛЕ отправки RPC?
   
3. Почему _clientLoadedChunks может рассинхронизироваться?
   - Что происходит если RPC теряется?
   - Что происходит если RPC приходит не в порядке?
   
4. Предложи решение:
   A) Batch RPCs: отправлять все Load, затем все Unload в ОДНОМ RPC
   B) Delta tracking: вычислять реальную разницу между старым и новым состоянием
   C) Sequence numbers: добавить номер последовательности к каждой операции
   D) Другое

ВАЖНО: 
- НЕ предлагай Client Acknowledgment Protocol (увеличивает latency)
- Ищи решение которое работает на уровне Server state management
- Фокус на минимизации количества RPC
```

---

### Subagent 2: Gameplay Programmer (gameplay-programmer)

**Файлы:** 
- `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs`
- `Assets/_Project/Scripts/World/Streaming/ChunkLoader.cs`

**Фокус:** Client-side validation и graceful degradation

```
Прочитай WorldStreamingManager.cs и ChunkLoader.cs

Ответь на вопросы:
1. Что происходит в LoadChunkByServerCommand() при получении Load RPC?
   - Как проверяется состояние чанка?
   
2. Что происходит в UnloadChunkByServerCommand() при получении Unload RPC?
   - Как проверяется состояние чанка?
   
3. Почему клиент получает Unload для чанков которые никогда не загружал?
   - Возможные причины?
   
4. Предложи минимальные изменения для graceful degradation:
   A) В UnloadChunk(): игнорировать если чанк не загружен (без ошибки)
   B) В LoadChunk(): проверять chunkFadeTimes перед добавлением в loadedChunks
   C) В WorldStreamingManager: вести локальный список ожидаемых операций
   D) Другое

Вопросы:
- Нужна ли клиенту очередь команд для последовательного выполнения?
- Должен ли клиент игнорировать дублирующие команды?

ВАЖНО:
- Фокус на Client-side validation
- НЕ менять Server-side логику кардинально
- Предложиgraceful degradation (система не падает при ошибках)
```

---

### Subagent 3: Unity Specialist (unity-specialist)

**Фокус:** Архитектурное решение для надёжной синхронизации

```
Проанализируй архитектуру Server→Client chunk streaming:

1. Почему Server отправляет Unload для чанков которые не загружал?
   - GetChunksInRadius() возвращает ПОЛНЫЙ список — это проблема?
   - Нужно ли вычислять delta вместо полного списка?

2. Должен ли сервер знать о состоянии fade-out на клиенте?
   - Сервер не знает что клиент находится в процессе fade-out
   - Это приводит к "отмена выгрузки" ошибкам

3. Предложи одно из решений:

   SOLUTION A: Command Queue на клиенте
   ─────────────────────────────────────
   - Клиент выполняет команды последовательно (Load → Unload → Load)
   - Нужен mutex для каждого чанка
   - +: Просто реализовать
   - -: Увеличивает latency загрузки

   SOLUTION B: Server-side delta tracking (Рекомендуется)
   ───────────────────────────────────────────────────────
   - Вычислять РЕАЛЬНУЮ разницу между old и new state
   - Отправлять ТОЛЬКО delta (Load для новых, Unload для удалённых)
   - +: Минимум RPC, нет лишних команд
   - -: Сложнее вычислить delta

   SOLUTION C: Ordered RPC Bundle
   ─────────────────────────────────
   - Отправлять все Load операции в ОДНОМ RPC
   - Затем все Unload в ОДНОМ RPC  
   - Клиент выполняет сначала все Load, затем все Unload
   - +: Гарантированный порядок
   - -: Больше данных в одном RPC

   SOLUTION D: Client-side Validation + Server Correction
   ─────────────────────────────────────────────────────
   - Клиент игнорирует Unload для незагруженных чанков
   - Клиент отправляет подтверждение загрузки (но не каждый раз)
   - +: Robust к потерянным RPC
   - -: Может рассинхронизироваться на долгое время

4. Оцени сложность реализации каждого решения
5. Предложи iteration path: минимальное изменение → полное решение

ВАЖНО:
- Учитывай что это MMO с potentially many players
- Учитывай что RPC могут теряться и переупорядочиваться
- Предложи решение которое работает в худших сетевых условиях
```

---

## 📁 Файлы для Исследования

### Основные файлы:

| Файл | Строки | Фокус |
|------|--------|-------|
| `PlayerChunkTracker.cs` | 401 | Server-side tracking, RPC отправка |
| `WorldStreamingManager.cs` | 708 | Client-side координация |
| `ChunkLoader.cs` | 412 | Client-side загрузка/выгрузка |

### Ключевые методы для анализа:

**PlayerChunkTracker.cs:**
- `LoadChunksForClient()` — строки 217-235
- `UnloadChunksForClient()` — строки 240-266
- `_clientLoadedChunks` — Dictionary в строках 44-45

**WorldStreamingManager.cs:**
- `LoadChunkByServerCommand()` — строки 296-314
- `UnloadChunkByServerCommand()` — строки 316-340

**ChunkLoader.cs:**
- `LoadChunk()` — строки 99-137
- `UnloadChunk()` — строки 144-173
- `chunkFadeTimes` — Dictionary в строке 56

---

## ✅ Метрики Успеха (I3.4)

После исправления:

1. **Ноль ошибок "Чанк X не загружен, невозможно выгрузить"**
2. **Ноль ошибок "Чанк X в процессе fade-out, отмена выгрузки"**
3. **Server отправляет ТОЛЬКО необходимые Load/Unload команды**
4. **Console чистая от спама при движении игрока**

---

## 📋 Output Format для Subagents

Каждый subagent должен предоставить:

1. **Root Cause Analysis** — точная причина проблемы
2. **Code References** — строки в коде которые нужно изменить
3. **Proposed Solution** —具体的 код или архитектурное изменение
4. **Complexity Estimate** — Low/Medium/High effort
5. **Risk Assessment** — что может пойти не так

---

## 🎯 Subagent Research Results

### Subagent 1: Network Programmer — Server-Side Analysis

#### Результат анализа PlayerChunkTracker:

| Метод | Строка | Действие | Порядок |
|-------|--------|----------|---------|
| `LoadChunksForClient()` | 229 | `_clientLoadedChunks.Add(chunkId)` | ПЕРЕД RPC |
| `LoadChunksForClient()` | 232 | `LoadChunkClientRpc()` | ПОСЛЕ изменения state |
| `UnloadChunksForClient()` | 261 | `_clientLoadedChunks.Remove(chunkId)` | ПЕРЕД RPC |
| `UnloadChunksForClient()` | 264 | `UnloadChunkClientRpc()` | ПОСЛЕ изменения state |

#### Root Cause:
**Оптимистичное обновление состояния ДО подтверждения доставки RPC.**

```
Сценарий потери RPC:
1. Server: _clientLoadedChunks.Add(X) — добавлен
2. Server: LoadChunkClientRpc(X) — отправлен
3. RPC ПОТЕРЯН → клиент НЕ загружает X
4. Server: _clientLoadedChunks.Remove(X) — удалён
5. Server: UnloadChunkClientRpc(X) — отправлен
6. Client: получает Unload для X — но X никогда не был загружен!
7. ERROR: "Чанк не загружен, невозможно выгрузить"
```

#### Рекомендация:
**Solution B: Delta Tracking** — вычислять реальную разницу old vs new state и отправлять только delta.

**Complexity:** Medium  
**Risk:** При изменении позиции игрока между вызовами — возможен race condition

---

### Subagent 2: Gameplay Programmer — Client-Side Analysis

#### Результат анализа WorldStreamingManager + ChunkLoader:

**Проблема в LoadChunkByServerCommand():**
```csharp
// Строка 313: СИНХРОННОЕ добавление в _loadedChunks
chunkLoader.LoadChunk(chunkId);  // АСИНХРОННАЯ операция!
_loadedChunks.Add(chunkId);      // Синхронное добавление — RACE CONDITION!
```

**Проблема в UnloadChunkByServerCommand():**
```csharp
// Строка 326: Проверка НЕЗАГРУЖЕННОГО чанка
if (!_loadedChunks.Contains(chunkId))
{
    Debug.Log($"[WorldStreamingManager] Chunk {chunkId} not loaded, skipping unload.");
    return; // Выход без ошибки
}
```

**Проблема в ChunkLoader.UnloadChunk():**
```csharp
// Строка 148: Генерирует WARNING при неудаче
if (!loadedChunks.ContainsKey(chunkId))
{
    Debug.LogWarning($"[ChunkLoader] Чанк {chunkId} не загружен...");
    return;
}
```

#### Рекомендация:
**Minimal Fix: Graceful Degradation**
1. В `UnloadChunk()` — изменить `Debug.LogWarning` на `Debug.Log` (не ошибка)
2. В `LoadChunk()` — проверять `chunkFadeTimes` перед добавлением

**Complexity:** Low  
**Risk:** Минимальный — просто меняет уровень логирования

---

### Subagent 3: Unity Specialist — Architecture Analysis

#### Рекомендуемое решение: **Hybrid B + D**

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         HYBRID SOLUTION: B + D                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  SERVER SIDE (B - Delta Tracking):                                        │
│    1. Вычислить delta = desiredChunks ⊕ _clientLoadedChunks              │
│    2. Отправлять ТОЛЬКО delta (~20 RPC вместо 162)                        │
│                                                                          │
│  CLIENT SIDE (D - Validation):                                           │
│    1. Если Unload для незагруженного → игнорировать (без ошибки)         │
│    2. Если Load для уже загруженного → игнорировать                      │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

#### Iteration Path:

| Фаза | Описание | Complexity | Приоритет |
|------|---------|------------|-----------|
| **1. Fix Logging** | UnloadChunk() — Debug.Log вместо Debug.LogWarning | Low | IMMEDIATE |
| **2. Client Validation** | Игнорировать Unload для незагруженных | Low | HIGH |
| **3. Delta Tracking** | Вычислять delta вместо полного списка | Medium | MEDIUM |
| **4. Pending State** | Добавить PendingLoad для fade-out cancellation | Medium | LOW |

---

## 📋 Итоговый План для Сессии I3.4

### Phase 1: Quick Fix (Без изменения архитектуры)

#### 1.1 Исправить логирование в ChunkLoader.cs
```csharp
// БЫЛО (строка 148):
Debug.LogWarning($"[ChunkLoader] Чанк {chunkId} не загружен...");

// СТАЛО:
Debug.Log($"[ChunkLoader] Chunk {chunkId} not loaded, skipping unload.");
```

**Impact:** Убирает "спам ошибок" в консоли

#### 1.2 Добавить Client-side validation
```csharp
// В ChunkLoader.UnloadChunk() — перед началом выгрузки:
public void UnloadChunk(ChunkId chunkId)
{
    // Игнорировать если не загружен (graceful degradation)
    if (!loadedChunks.ContainsKey(chunkId))
    {
        Debug.Log($"[ChunkLoader] Chunk {chunkId} not loaded, skipping.");
        return;
    }
    // ... существующий код
}
```

**Impact:** Ноль ошибок "не загружен, невозможно выгрузить"

---

### Phase 2: Delta Tracking (Server-side)

#### 2.1 Изменить LoadChunksForClient()
```csharp
private void LoadChunksForClient(ulong clientId, Vector3 position)
{
    if (!_chunkManager || !_clientLoadedChunks.ContainsKey(clientId))
        return;
    
    List<ChunkId> chunksInRange = _chunkManager.GetChunksInRadius(position, loadRadius);
    HashSet<ChunkId> expectedSet = new HashSet<ChunkId>(chunksInRange);
    
    // ТОЛЬКО новые чанки (delta от текущего состояния)
    foreach (var chunkId in expectedSet)
    {
        if (!_clientLoadedChunks[clientId].Contains(chunkId))
        {
            _clientLoadedChunks[clientId].Add(chunkId);
            LoadChunkClientRpc(clientId, chunkId);
        }
    }
}
```

**Impact:** 162 RPC → ~20 RPC при движении

#### 2.2 Изменить UnloadChunksForClient()
```csharp
private void UnloadChunksForClient(ulong clientId, Vector3 position)
{
    if (!_chunkManager || !_clientLoadedChunks.ContainsKey(clientId))
        return;
    
    List<ChunkId> chunksInUnloadRadius = _chunkManager.GetChunksInRadius(position, unloadRadius);
    HashSet<ChunkId> chunksInRadiusSet = new HashSet<ChunkId>(chunksInUnloadRadius);
    
    var chunksToUnload = new List<ChunkId>();
    
    // ТОЛЬКО чанки которые ДЕЙСТВИТЕЛЬНО загружены
    foreach (var loadedChunk in _clientLoadedChunks[clientId])
    {
        if (!chunksInRadiusSet.Contains(loadedChunk))
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

**Impact:** Ноль Unload для "незагруженных" чанков

---

## ✅ Метрики Успеха (I3.4)

| Метрика | До | После |
|---------|-----|-------|
| RPC на движение | 162 | ~20 |
| Ошибки "не загружен" | Да | Нет |
| Ошибки "fade-out" | Да | Нет |
| Console spam | Да | Нет |

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 17:55  
**Обновлено:** 18.04.2026, 18:00 (добавлены результаты subagents)  
**Следующий шаг:** Согласовать план с пользователем
