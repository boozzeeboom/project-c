# Iteration 3.4 — Session Prompt: Race Conditions Fix

**Дата:** 18.04.2026  
**Статус:** ⏳ ГОТОВ К РЕАЛИЗАЦИИ  
**Предыдущие итерации:** I3.1 ✅ I3.2 ✅ I3.3 ✅

---

## 📊 Контекст: Что Было Сделано

### I3.1: FindObjectsByType Ghost Fix
Устранён выбор ghost объекта при поиске NetworkPlayer.

### I3.2: FloatingOriginMP.GetWorldPosition()
Использование кэшированной позиции вместо oscilling transform.position.

### I3.3: Race Condition (Дублирующий Update)
Удалён дублирующий Update() цикл из PlayerChunkTracker.

---

## 🔴 Проблемы для Решения (I3.4)

### Проблема 1: "Чанк X в процессе fade-out, отмена выгрузки"
```
[Client] Loading chunk Chunk(5, 19) by server command
[ChunkLoader] Чанк Chunk(5, 19) в процессе fade-out, отмена выгрузки.
```
**Root Cause:** RPC reorder — Unload выполняется ПОСЛЕ Load.

### Проблема 2: "Чанк X не загружен, невозможно выгрузить"
```
[Client] Unloading chunk Chunk(2, -1) by server command
[ChunkLoader] Чанк Chunk(2, -1) не загружен, невозможно выгрузить.
```
**Root Cause:** Server отправляет Unload для чанков которые НИКОГДА не были загружены.

---

## 🎯 Subagent Analysis Results

### Subagent 1: Network Programmer
**Ключевая проблема:** Оптимистичное обновление state ДО подтверждения RPC.

| Метод | Строка | Порядок |
|-------|--------|---------|
| `_clientLoadedChunks.Add()` | 229 | ПЕРЕД RPC |
| `_clientLoadedChunks.Remove()` | 261 | ПЕРЕД RPC |

**Рекомендация:** Delta Tracking — вычислять реальную разницу old vs new.

### Subagent 2: Gameplay Programmer
**Ключевая проблема:** `Debug.LogWarning` создаёт "спам ошибок".

**Рекомендация:** Graceful Degradation — игнорировать Unload для незагруженных.

### Subagent 3: Unity Specialist
**Рекомендуемое решение:** **Hybrid B + D**
- Server: Delta Tracking (отправлять только ~20 RPC вместо 162)
- Client: Validation (игнорировать Unload для незагруженных)

---

## 📋 План Реализации

### Phase 1: Quick Fix (Без изменения архитектуры)

#### 1.1 ChunkLoader.cs — Строка 148
```csharp
// БЫЛО:
Debug.LogWarning($"[ChunkLoader] Чанк {chunkId} не загружен...");

// СТАЛО:
Debug.Log($"[ChunkLoader] Chunk {chunkId} not loaded, skipping.");
```

#### 1.2 ChunkLoader.cs — UnloadChunk()
```csharp
public void UnloadChunk(ChunkId chunkId)
{
    // Graceful degradation — игнорировать если не загружен
    if (!loadedChunks.ContainsKey(chunkId))
    {
        Debug.Log($"[ChunkLoader] Chunk {chunkId} not loaded, skipping.");
        return;
    }
    // ... существующий код
}
```

**Impact:** Убирает спам ошибок в консоли.

---

### Phase 2: Delta Tracking (Server-side)

#### 2.1 PlayerChunkTracker.cs — LoadChunksForClient()
Текущий код (строки 217-235):
```csharp
private void LoadChunksForClient(ulong clientId, Vector3 position)
{
    List<ChunkId> chunksInRange = _chunkManager.GetChunksInRadius(position, loadRadius);
    
    foreach (var chunkId in chunksInRange)
    {
        if (!_clientLoadedChunks[clientId].Contains(chunkId))
        {
            _clientLoadedChunks[clientId].Add(chunkId);
            LoadChunkClientRpc(clientId, chunkId);
        }
    }
}
```

**Комментарий:** Уже содержит проверку `Contains()` — это правильно!

#### 2.2 PlayerChunkTracker.cs — UnloadChunksForClient()
Текущий код (строки 240-266):
```csharp
private void UnloadChunksForClient(ulong clientId, Vector3 position)
{
    List<ChunkId> chunksInUnloadRadius = _chunkManager.GetChunksInRadius(position, unloadRadius);
    var chunksInRadiusSet = new HashSet<ChunkId>(chunksInUnloadRadius);
    
    var chunksToUnload = new List<ChunkId>();
    
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

**Комментарий:** Код ПРАВИЛЬНЫЙ — выгружает только то что реально загружено.

---

## 🔍 Детальный Анализ Проблемы 2

### Почему возникает "Чанк X не загружен"?

При запуске игры:
```
1. Player connected → OnClientConnected()
2. FindPlayerTransformCoroutine() → ждёт 0.5 сек
3. LoadChunksForClient() → загружает начальные чанки
```

**Проблема:** Если игрок двигается БЫСТРО до завершения `FindPlayerTransformCoroutine()`:
```
T=0.0: Player connected
T=0.1: Player moves to distant position
T=0.5: FindPlayerTransformCoroutine() завершается
T=0.5: LoadChunksForClient() — но player уже в другом месте!
```

**Решение:** НЕ вызывать Unload пока Load не завершён.

---

## ✅ Метрики Успеха

После Phase 1:
- [ ] Ноль `Debug.LogWarning` в консоли
- [ ] "Chunk not loaded" — INFO level, не WARNING

После Phase 2:
- [ ] Ноль ошибок "не загружен, невозможно выгрузить"
- [ ] Ноль ошибок "в процессе fade-out"
- [ ] ~20 RPC на движение вместо 162

---

## 📁 Файлы для Изменения

| Файл | Изменение | Priority |
|------|-----------|----------|
| `ChunkLoader.cs` | Debug.LogWarning → Debug.Log | HIGH |
| `ChunkLoader.cs` | Graceful degradation в UnloadChunk | HIGH |

**ВНИМАНИЕ:** Код PlayerChunkTracker.cs выглядит ПРАВИЛЬНЫМ — не требует изменений для delta tracking. Проблема в том что:
1. Unload вызывается для чанков которые НИКОГДА не были загружены
2. Это происходит когда начальная загрузка НЕ завершена а игрок уже двигается

---

## ⚠️ User Instructions

**Требования:**
1. ❌ НЕ писать код в этой сессии
2. ❌ НЕ модифицировать файлы
3. ✅ Только анализ и план
4. ✅ Подготовить prompt для следующей сессии реализации

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 18:00  
**Документы:**
- `ANALYSIS_I3_DEEP.md` — полный анализ
- `SESSION_END_I33_COMPLETE.md` — итоги I3.1-I3.3
