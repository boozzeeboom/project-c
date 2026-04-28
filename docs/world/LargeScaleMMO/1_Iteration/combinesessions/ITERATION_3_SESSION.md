# Iteration 3 Session Prompt: Fix PlayerChunkTracker Integration

**Цель:** Создать надёжную связь между NetworkPlayer и PlayerChunkTracker.

**Длительность:** 1-2 сессии

**Критерий приёмки:** 
> PlayerChunkTracker получает обновления позиции от NetworkPlayer.
> Сервер отправляет LoadChunk RPC при смене чанка.

---

## 📋 Задачи

### 3.1 Добавить UpdatePlayerPosition() в PlayerChunkTracker
**Файл:** `PlayerChunkTracker.cs`

```csharp
/// <summary>
/// Обновить позицию игрока для трекинга.
/// Вызывается из NetworkPlayer.
/// </summary>
public void UpdatePlayerPosition(ulong clientId, Vector3 worldPosition) {
    if (!IsServer) return;
    
    // Определить текущий чанк
    var chunkId = GetChunkAtPosition(worldPosition);
    
    // Проверить смену чанка
    if (_playerChunks.TryGetValue(clientId, out var currentChunk)) {
        if (currentChunk != chunkId) {
            // Смена чанка — отправить RPC
            UnloadChunkClientRpc(clientId, currentChunk);
            LoadChunkClientRpc(clientId, chunkId);
        }
    } else {
        // Первый чанк
        LoadChunkClientRpc(clientId, chunkId);
    }
    
    _playerChunks[clientId] = chunkId;
}

private ChunkId GetChunkAtPosition(Vector3 position) {
    // Использовать WorldChunkManager
    if (chunkManager != null) {
        return chunkManager.GetChunkAtPosition(position);
    }
    // Fallback: вычислить вручную
    int gridX = Mathf.FloorToInt(position.x / CHUNK_SIZE);
    int gridZ = Mathf.FloorToInt(position.z / CHUNK_SIZE);
    return new ChunkId(gridX, gridZ);
}
```

### 3.2 Добавить вызов из NetworkPlayer
**Файл:** `NetworkPlayer.cs`

```csharp
private PlayerChunkTracker _playerChunkTracker;

private void Start() {
    // Найти PlayerChunkTracker
    _playerChunkTracker = FindFirstObjectByType<PlayerChunkTracker>();
}

private void FixedUpdate() {
    if (IsOwner && _playerChunkTracker != null) {
        _playerChunkTracker.UpdatePlayerPosition(OwnerClientId, transform.position);
    }
}
```

---

## 🔍 Перед началом

Прочитать:
- `docs/world/LargeScaleMMO/CURRENT_STATE.md` — секция "PlayerChunkTracker слабая связь с NetworkPlayer"
- `docs/world/LargeScaleMMO/ITERATION_PLAN.md` — Iteration 3

---

## 📝 Шаги выполнения

1. Открыть `PlayerChunkTracker.cs`
2. Добавить метод `UpdatePlayerPosition(ulong clientId, Vector3 worldPosition)`
3. Добавить метод `GetChunkAtPosition(Vector3 position)`
4. Открыть `NetworkPlayer.cs`
5. Добавить поле `_playerChunkTracker`
6. Добавить поиск в `Start()`
7. Добавить вызов в `FixedUpdate()`
8. Протестировать в Host режиме

---

## ✅ Тестирование

1. Запустить Play Mode как Host
2. Перемещаться по миру
3. Проверить Console:
   - `PlayerChunkTracker: Chunk changed from X,Y to Z,W`
   - `LoadChunkClientRpc sent to client`

---

## 📊 Ожидаемые результаты

| Метрика | До | После |
|---------|-----|-------|
| PlayerChunkTracker updates | Слабая связь | Strong update cycle |
| RPC при смене чанка | Нет | Да |
| Серверный трекинг | Частичный | Полный |

---

**Автор:** Claude Code  
**Дата:** 18.04.2026  
**Статус:** Нужно выполнить (после Iteration 2)