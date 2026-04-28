# Iteration 3.3 — Chunk Oscillation устранён (ФИНАЛЬНЫЙ ФИКС)

**Дата:** 18.04.2026, 17:47  
**Статус:** ✅ ИСПРАВЛЕНО — Oscillation прекратился  
**Суть:** Удалён дублирующий Update() цикл в PlayerChunkTracker

---

## Проблема (Обнаружена после Iteration 3.2)

### Два источника обновления чанков — Race Condition

```
PlayerChunkTracker
    │
    ├── Update() --> ForceUpdatePlayerChunk() --> UpdatePlayerChunk()  [1]
    │
    └── FixedUpdate (из NetworkPlayer) --> UpdatePlayerChunkTracker()
            │
            └── _playerChunkTracker.ForceUpdatePlayerChunk() --> UpdatePlayerChunk()  [2]
```

**ЛОГ консоли показывал:**
```
Player 0 moved from Chunk(2, -3) to Chunk(4, -1)     ← Update() вызвал
Player 0 moved from Chunk(4, -1) to Chunk(2, -3)     ← FixedUpdate() вызвал
[Client] Loading chunk Chunk(0, -2) by server command
[Client] Unloading chunk Chunk(0, -1) by server command
```

**Корень проблемы:** 
- `PlayerChunkTracker.Update()` обновлял чанки каждые `updateInterval` секунд
- `NetworkPlayer.FixedUpdate()` обновлял чанки в свой UpdatePlayerChunkTracker()
- Оба вызывали `ForceUpdatePlayerChunk()` с разными позициями

---

## Решение (Iteration 3.3)

### Удалён дублирующий Update() цикл

**PlayerChunkTracker.cs:**
```csharp
// БЫЛО: Update() обновлял чанки независимо
private void Update()
{
    // ... цикл обновления чанков ...
    ForceUpdatePlayerChunk(clientId, worldPosition);
}

// СТАЛО: Update() больше НЕ обновляет чанки
private void Update()
{
    // REMOVED: Update() больше не обновляет чанки!
    // Это делается из NetworkPlayer.UpdatePlayerChunkTracker()
}
```

### Единый источник обновления

```
NetworkPlayer.FixedUpdate()
    └── UpdatePlayerChunkTracker()
            └── _playerChunkTracker.ForceUpdatePlayerChunk()
                    └── UpdatePlayerChunk()
```

**Теперь только NetworkPlayer вызывает ForceUpdatePlayerChunk()** со стабильной позицией из FloatingOriginMP.GetWorldPosition().

---

## Архитектура после Iteration 3.3

```
┌─────────────────────────────────────────────────────────────────────┐
│                        NetworkPlayer (Server)                        │
├─────────────────────────────────────────────────────────────────────┤
│ FixedUpdate()                                                        │
│   └── UpdatePlayerChunkTracker()                                     │
│         ├── FloatingOriginMP.GetWorldPosition() → worldPosition     │
│         └── _playerChunkTracker.ForceUpdatePlayerChunk(worldPos)     │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        PlayerChunkTracker                            │
├─────────────────────────────────────────────────────────────────────┤
│ ForceUpdatePlayerChunk(clientId, position)                          │
│   ├── Hysteresis проверка (50 units от границы чанка)              │
│   └── UpdatePlayerChunk(clientId, position)                         │
│         ├── GetChunkIdAtPosition() → newChunk                      │
│         ├── LoadChunksForClient() — RPC на клиент                  │
│         └── UnloadChunksForClient() — RPC на клиент                │
└─────────────────────────────────────────────────────────────────────┘
```

**Больше НЕТ race condition!**

---

## Тестирование

### Метрики успеха

При стоянии на месте 10 секунд:
- ✅ Ноль логов "Player X moved from Chunk" (oscillation прекратилась)
- ✅ Chunk loading/unloading вызывается только при РЕАЛЬНОМ движении
- ✅ Консоль чистая от спама

### Проверка

1. Запустить игру в режиме Host
2. Нажать F5 для телепортации на 1M+
3. Стоять на месте 10 секунд
4. Проверить логи — не должно быть oscillation

---

## Файлы изменены

### `Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs`

- **Строки ~99-146:** `Update()` метод — УДАЛЕН цикл обновления чанков
- Теперь Update() пустой — чанки обновляются только из NetworkPlayer

---

## Итоговая архитектура итераций

| Итерация | Проблема | Решение |
|---------|----------|---------|
| **Iteration 3.1** | FindObjectsByType выбирал ghost объект | Используем OwnerClientId для точного поиска |
| **Iteration 3.2** | transform.position oscillates | Используем FloatingOriginMP.GetWorldPosition() |
| **Iteration 3.3** | Два источника обновления | Удалён дублирующий Update() цикл |

---

**Автор:** Claude Code  
**Завершение:** 18.04.2026, 17:47