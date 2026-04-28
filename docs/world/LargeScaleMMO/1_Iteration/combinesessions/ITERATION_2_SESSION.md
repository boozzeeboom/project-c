# Iteration 2 Session Prompt: Fix WorldStreamingManager Integration

**Цель:** Подключить обратную связь от ChunkLoader к WorldStreamingManager.

**Длительность:** 1 сессия

**Критерий приёмки:** 
> WorldStreamingManager получает события OnChunkLoaded/OnChunkUnloaded.
> Console показывает "Chunk loaded: X,Y" при загрузке.

---

## 📋 Задачи

### 2.1 Подписаться на ChunkLoader events
**Файл:** `WorldStreamingManager.cs`, метод `Awake()`

```csharp
private void Awake() {
    if (_instance == null) {
        _instance = this;
        DontDestroyOnLoad(gameObject);
    } else {
        Destroy(gameObject);
        return;
    }
    
    // Подписка на ChunkLoader events
    if (chunkLoader != null) {
        chunkLoader.OnChunkLoaded += OnChunkLoadedHandler;
        chunkLoader.OnChunkUnloaded += OnChunkUnloadedHandler;
    }
}

private void OnChunkLoadedHandler(ChunkId chunkId) {
    Debug.Log($"[WorldStreamingManager] Chunk loaded: {chunkId.GridX},{chunkId.GridZ}");
}

private void OnChunkUnloadedHandler(ChunkId chunkId) {
    Debug.Log($"[WorldStreamingManager] Chunk unloaded: {chunkId.GridX},{chunkId.GridZ}");
}
```

### 2.2 Добавить логирование в ChunkLoader
**Файл:** `ChunkLoader.cs`

```csharp
public System.Action<ChunkId> OnChunkLoaded;
public System.Action<ChunkId> OnChunkUnloaded;

// В конце LoadChunkCoroutine():
OnChunkLoaded?.Invoke(chunkId);

// В конце UnloadChunk():
OnChunkUnloaded?.Invoke(chunkId);
```

---

## 🔍 Перед началом

Прочитать:
- `docs/world/LargeScaleMMO/CURRENT_STATE.md` — секция "WorldStreamingManager не получает события"
- `docs/world/LargeScaleMMO/ITERATION_PLAN.md` — Iteration 2

---

## 📝 Шаги выполнения

1. Открыть `ChunkLoader.cs`
2. Добавить события `OnChunkLoaded` и `OnChunkUnloaded`
3. Добавить вызов событий в `LoadChunkCoroutine()` и `UnloadChunk()`
4. Открыть `WorldStreamingManager.cs`
5. Добавить подписку в `Awake()`
6. Добавить обработчики `OnChunkLoadedHandler` и `OnChunkUnloadedHandler`
7. Протестировать

---

## ✅ Тестирование

1. Запустить Play Mode
2. Нажать F7 → загрузка чанков вокруг позиции
3. Проверить Console:
   - `[WorldStreamingManager] Chunk loaded: X,Y`
   - `[WorldStreamingManager] Chunk unloaded: X,Y`

---

## 📊 Ожидаемые результаты

| Метрика | До | После |
|---------|-----|-------|
| WorldStreamingManager events | None | Subscribed |
| Console logs | Нет логов | "Chunk loaded: X,Y" |
| Preload система | Не работает | Работает |

---

**Автор:** Claude Code  
**Дата:** 18.04.2026  
**Статус:** Нужно выполнить (после Iteration 1)