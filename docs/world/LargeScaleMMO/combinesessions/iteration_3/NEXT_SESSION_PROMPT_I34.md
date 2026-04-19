# Iteration 3.4 — Session Start Prompt

## Цель Сессии

**Исправить Race Conditions в Server→Client Chunk Sync** — ошибки "Чанк X не загружен" и "Чанк X в процессе fade-out".

---

## Контекст: Iteration 3 Успешно Завершена

### Что Исправлено в Iteration 3:

| Итерация | Проблема | Решение |
|---------|----------|---------|
| **I3.1** | FindObjectsByType выбирал ghost объект | Используем OwnerClientId |
| **I3.2** | transform.position oscills на больших координатах | Используем GetWorldPosition() |
| **I3.3** | Два источника обновления чанков | Удалён дублирующий Update() |

**Результат:** Chunk oscillation прекратился. Игрок стоит на месте — чанки не меняются.

---

## 🔴 Оставшиеся Проблемы

### Проблема 1: "Чанк X в процессе fade-out, отмена выгрузки"

**Симптомы:**
```
[Client] Loading chunk Chunk(5, 19) by server command
[ChunkLoader] Чанк Chunk(5, 19) в процессе fade-out, отмена выгрузки.
```

**Root Cause:** RPC reorder — Unload выполняется после Load.

### Проблема 2: "Чанк X не загружен, невозможно выгрузить"

**Симптомы:**
```
[Client] Unloading chunk Chunk(2, -1) by server command
[ChunkLoader] Чанк Chunk(2, -1) не загружен, невозможно выгрузить.
```

**Root Cause:** Server отправляет Unload для чанков которые НИКОГДА не были загружены.

---

## 📊 Root Cause Analysis

### Проблема: Server State ≠ Client State

```
┌─────────────────────────────────────────────────────────────────┐
│  Player moves from Chunk(4,18) to Chunk(5,19):                  │
│                                                                 │
│  OLD radius: Chunk(0..8, 14..22) = 81 chunks                    │
│  NEW radius: Chunk(1..9, 15..23) = 81 chunks                    │
│                                                                 │
│  ACTUAL DELTA: 20 chunks (edges changed)                         │
│  Server sends: 162 Load/Unload commands (❌ INEFFICIENT)         │
│                                                                 │
│  Problem: _clientLoadedChunks[clientId] becomes corrupted       │
│  because GetChunksInRadius() returns FULL list, not delta!        │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📁 Файлы для Анализа

### 1. PlayerChunkTracker.cs

```csharp
// Строки 180-230: LoadChunksForClient()
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

// Строки 230-270: UnloadChunksForClient()
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

**Вопрос:** Почему `_clientLoadedChunks[clientId]` рассинхронизирован?

### 2. WorldStreamingManager.cs

```csharp
// Строки 160-200: LoadChunkByServerCommand()
public void LoadChunkByServerCommand(ChunkId chunkId)
{
    if (_loadedChunks.Contains(chunkId))
    {
        Debug.Log($"[WorldStreamingManager] Chunk {chunkId} already loaded, skipping.");
        return;
    }
    
    if (chunkLoader == null)
    {
        Debug.LogError("[WorldStreamingManager] ChunkLoader not initialized!");
        return;
    }
    
    Debug.Log($"[WorldStreamingManager] Loading chunk {chunkId} by server command");
    chunkLoader.LoadChunk(chunkId);
    _loadedChunks.Add(chunkId);
}

// Строки 200-240: UnloadChunkByServerCommand()
public void UnloadChunkByServerCommand(ChunkId chunkId)
{
    if (!_loadedChunks.Contains(chunkId))
    {
        Debug.Log($"[WorldStreamingManager] Chunk {chunkId} not loaded, skipping unload.");
        return;
    }
    
    if (chunkLoader == null)
    {
        Debug.LogError("[WorldStreamingManager] ChunkLoader not initialized!");
        return;
    }
    
    Debug.Log($"[WorldStreamingManager] Unloading chunk {chunkId} by server command");
    chunkLoader.UnloadChunk(chunkId);
    _loadedChunks.Remove(chunkId);
}
```

### 3. ChunkLoader.cs

```csharp
// Строки 65-90: LoadChunk()
public void LoadChunk(ChunkId chunkId)
{
    if (loadedChunks.ContainsKey(chunkId))
    {
        Debug.LogWarning($"[ChunkLoader] Чанк {chunkId} уже загружен, пропуск.");
        return;
    }
    
    // Проверка: чанк в процессе fade-out — отменяем выгрузку
    if (chunkFadeTimes.ContainsKey(chunkId))
    {
        Debug.Log($"[ChunkLoader] Чанк {chunkId} в процессе fade-out, отмена выгрузки.");
        CancelFadeOut(chunkId);
        return;
    }
    // ... нормальная загрузка
}

// Строки 90-120: UnloadChunk()
public void UnloadChunk(ChunkId chunkId)
{
    if (!loadedChunks.ContainsKey(chunkId))
    {
        Debug.LogWarning($"[ChunkLoader] Чанк {chunkId} не загружен, невозможно выгрузить.");
        return;
    }
    // ... нормальная выгрузка
}
```

---

## 🎯 Задачи для Subagents

### Subagent 1: Network Programmer (network-programmer)

**Задача:** Проанализировать синхронизацию Server ↔ Client state

```
Прочитай Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs

Ответь на вопросы:
1. Как _clientLoadedChunks[clientId] обновляется при LoadChunksForClient()?
2. Как _clientLoadedChunks[clientId] обновляется при UnloadChunksForClient()?
3. Почему _clientLoadedChunks может рассинхронизироваться с реальным состоянием клиента?
4. Что происходит если RPC теряется или приходит не в порядке?
5. Предложи решение для надёжной синхронизации

ВАЖНО: 
- НЕ предлагай Client Acknowledgment Protocol (увеличивает latency)
- Ищи решение которое работает на уровне Server state management
```

### Subagent 2: Gameplay Programmer (gameplay-programmer)

**Задача:** Проанализировать валидацию команд на клиенте

```
Прочитай Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs
Прочитай Assets/_Project/Scripts/World/Streaming/ChunkLoader.cs

Ответь на вопросы:
1. Почему клиент получает Unload для чанков которые никогда не загружал?
2. Нужна ли клиенту очередь команд для последовательного выполнения?
3. Должен ли клиент игнорировать дублирующие команды?
4. Предложи минимальные изменения для предотвращения ошибок без изменения архитектуры

ВАЖНО:
- Фокус на Client-side validation
- НЕ менять Server-side логику
- Предложиgraceful degradation
```

### Subagent 3: Unity Specialist (unity-specialist)

**Задача:** Предложить архитектурное решение

```
Проанализируй архитектуру Server→Client chunk streaming:

1. Почему Server отправляет Unload для чанков которые не загружал?
2. GetChunksInRadius() возвращает ПОЛНЫЙ список — это проблема?
3. Должен ли сервер знать о состоянии fade-out на клиенте?
4. Предложи одно из решений:
   A) Command Queue на клиенте (выполнять команды последовательно)
   B) Server-side delta tracking (отправлять только реально изменившиеся чанки)
   C) Client-side validation with Server correction (клиент подтверждает, сервер корректирует)
   D) Другое решение

ВАЖНО:
- Оцени сложность реализации каждого решения
- Предложи iteration path (минимальное изменение → полное решение)
- Учитывай что это MMO с potentially many players
```

---

## 📋 План работы

### Шаг 1: Subagents анализируют код (параллельно)
- Subagent 1 → PlayerChunkTracker.cs (Server state)
- Subagent 2 → WorldStreamingManager.cs + ChunkLoader.cs (Client validation)
- Subagent 3 → Архитектурный анализ

### Шаг 2: Координация (unity-specialist или gameplay-programmer)
- Собрать результаты анализа
- Выбрать оптимальное решение
- Согласовать с пользователем

### Шаг 3: Реализация (по согласованию)
- Код не пишем в этой сессии
- Только анализ и план

---

## ✅ Метрики Успеха

После исправления:

1. **Ноль ошибок "Чанк X не загружен, невозможно выгрузить"**
2. **Ноль ошибок "Чанк X в процессе fade-out, отмена выгрузки"**
3. **Server отправляет ТОЛЬКО необходимые Load/Unload команды**
4. **Console чистая от спама при движении игрока**

---

## ⚠️ ВАЖНО: User Instructions

**Пользователь сказал:**
> "подводим итог и подготавливаем сессию следущую для продолжения итерации 3. составим промпт для глубокого анализа сабагентами и решения проблемы"

**Требования:**
1. ❌ НЕ писать код
2. ❌ НЕ модифицировать файлы
3. ✅ Только анализ и документация
4. ✅ Подготовить prompt для следующей сессии
5. ✅ Предложить решение (без реализации)

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 17:52  
**Следующий шаг:** Запустить subagents для анализа
